using System;
using System.Threading;
using UnityEngine;

namespace Weaver.Optimizations.WorkDistributors.WorkChunks;

internal sealed class EnemyGroundCombat : IWorkChunk
{
    private readonly PlanetFactory _planet;
    private readonly int _workIndex;
    private readonly int _maxWorkCount;

    public EnemyGroundCombat(PlanetFactory planet, int workIndex, int maxWorkCount)
    {
        _planet = planet;
        _workIndex = workIndex;
        _maxWorkCount = maxWorkCount;
    }

    public void Execute(int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, Vector3 playerPosition)
    {
        EnemyDFGroundSystem enemySystem = _planet.enemySystem;

        DeepProfiler.BeginSample(DPEntry.DFGSystem, workerIndex);
        if (enemySystem.turrets.count > 0)
        {
            DeepProfiler.BeginSample(DPEntry.DFGTurret, workerIndex);
            EnemyGroundTurretGameTick(workerIndex, time);
            DeepProfiler.EndSample(DPEntry.DFGTurret, workerIndex);
        }

        if (enemySystem.units.count > 0)
        {
            DeepProfiler.BeginSample(DPEntry.DFGUnit, workerIndex);
            EnemyGroundUnitGameTick(workerIndex, time);
            DeepProfiler.EndSample(DPEntry.DFGUnit, workerIndex);
        }
        DeepProfiler.EndSample(DPEntry.DFGSystem, workerIndex);
    }

    public static int GetParallelCount(PlanetFactory planet, int maxParallelism)
    {
        int maxEntityCount = Math.Max(planet.enemySystem.turrets.cursor - 1, planet.enemySystem.units.cursor - 1);
        const int entitiesPerWorkChunk = 100;
        int workChunkCount = (maxEntityCount + (entitiesPerWorkChunk - 1)) / entitiesPerWorkChunk;
        return Math.Min(workChunkCount, maxParallelism);
    }

    private void EnemyGroundTurretGameTick(int workerIndex, long time)
    {
        EAggressiveLevel eAggressiveLevel = GameMain.logic.aggressiveLevel;
        EnemyData[] enemyPool = _planet.enemyPool;
        AnimData[] enemyAnimPool = _planet.enemyAnimPool;
        bool isLocalLoaded = _planet.enemySystem.isLocalLoaded;
        ObjectPool<DFGBaseComponent> bases = _planet.enemySystem.bases;
        DataPool<EnemyBuilderComponent> builders = _planet.enemySystem.builders;
        DataPool<DFGTurretComponent> turrets = _planet.enemySystem.turrets;
        (int startIndex, int workLength) = UnOptimizedPlanetWorkChunk.GetWorkChunkIndices(turrets.cursor, _maxWorkCount, _workIndex);
        for (int i = Math.Max(1, startIndex); i < startIndex + workLength; i++)
        {
            ref DFGTurretComponent reference2 = ref turrets.buffer[i];
            if (reference2.id != i)
            {
                continue;
            }
            int enemyId = reference2.enemyId;
            ref EnemyData reference3 = ref enemyPool[enemyId];
            PrefabDesc prefabDesc = SpaceSector.PrefabDescByModelIndex[reference3.modelIndex];
            reference2.InternalUpdate(prefabDesc);
            switch (reference2.state)
            {
                case EDFTurretState.None:
                    reference2.NoneState();
                    break;
                case EDFTurretState.Searching:
                    if (reference2.DetermineSearchTarget(time))
                    {
                        reference2.SearchTarget(_planet, bases.buffer[reference2.baseId], eAggressiveLevel, workerIndex);
                    }
                    break;
                case EDFTurretState.Aiming:
                    if (reference2.target.id == 0)
                    {
                        reference2.state = EDFTurretState.Searching;
                    }
                    reference2.Aim(_planet, bases.buffer[reference2.baseId], builders.buffer, eAggressiveLevel, workerIndex);
                    break;
            }
            if (isLocalLoaded && builders.buffer[reference2.builderId].state > 0)
            {
                enemyAnimPool[enemyId].prepare_length = reference2.localDir.x;
                enemyAnimPool[enemyId].working_length = reference2.localDir.y;
                enemyAnimPool[enemyId].power = reference2.localDir.z;
                enemyAnimPool[enemyId].state = (uint)reference2.state;
                int dfTurretAttackInterval = prefabDesc.dfTurretAttackInterval;
                float num5 = (float)(-reference2.fire) / (float)dfTurretAttackInterval;
                if (num5 < -1f)
                {
                    num5 = -1f;
                }
                float num6 = 2f;
                if (reference2.heat < 0)
                {
                    num6 = (float)reference2.heat / 3600f + 1f;
                    num6 = ((num6 > 1f) ? 1f : ((num6 < 0f) ? 0f : num6));
                }
                else if (reference2.heat > 0)
                {
                    num6 = 0f;
                }
                float num7 = 0f;
                if (reference2.state == EDFTurretState.Aiming && num5 >= 0f)
                {
                    float x = reference2.localDir.x;
                    float y = reference2.localDir.y;
                    float z = reference2.localDir.z;
                    float num8 = Mathf.Sqrt(x * x + y * y + z * z);
                    num7 = (reference2.sensorRange - num8) / (reference2.sensorRange - reference2.attackRange);
                    num7 = ((num7 < 0f) ? 0f : ((num7 >= 1f) ? 1f : num7));
                }
                if (num5 >= 0f)
                {
                    num5 += ((!(num6 < 2f)) ? num7 : ((num6 > num7) ? num7 : num6));
                }
                enemyAnimPool[enemyId].time = num5 + 10f;
            }
        }
    }

    private void EnemyGroundUnitGameTick(int workerIndex, long time)
    {
        GameLogic gameLogic = GameMain.logic;
        SkillSystem skillSystem = gameLogic.sector.skillSystem;
        EAggressiveLevel eAggressiveLevel = gameLogic.aggressiveLevel;
        float ratio = 0.75f;
        int c = 5;
        int maxHatredGroundTmp = skillSystem.maxHatredGroundTmp;
        int maxHatredGroundBaseTmp = skillSystem.maxHatredGroundBaseTmp;
        switch (eAggressiveLevel)
        {
            case EAggressiveLevel.Rampage:
                ratio = 0.93f;
                c = 1;
                break;
            case EAggressiveLevel.Sharp:
                ratio = 0.86f;
                c = 3;
                break;
            case EAggressiveLevel.Normal:
                ratio = 0.75f;
                c = 5;
                break;
            case EAggressiveLevel.Passive:
            case EAggressiveLevel.Torpid:
                ratio = 0.6f;
                c = 6;
                break;
            case EAggressiveLevel.Dummy:
                ratio = 0f;
                c = 0;
                break;
        }
        VectorLF3 playerSkillTargetU = skillSystem.playerSkillTargetU;
        int num2 = (int)(time % 60);
        DataPool<EnemyUnitComponent> units = _planet.enemySystem.units;
        ObjectPool<DFGBaseComponent> bases = _planet.enemySystem.bases;
        EnemyData[] enemyPool = _planet.enemyPool;
        bool flag3 = _planet.enemySystem.isLocalLoaded;
        ObjectRenderer[]? array = ((flag3) ? _planet.planet.factoryModel.gpuiManager.objectRenderers : null);
        VectorLF3 vectorLF = Maths.QInvRotateLF(_planet.planet.runtimeRotation, playerSkillTargetU - _planet.planet.uPosition);
        if (array == null)
        {
            flag3 = false;
        }
        else
        {
            _planet.planet.factoryModel.enemyUnitsDirty = true;
        }
        int formTick = (int)((_planet.planet.seed + time) % 151200);
        float realRadius = _planet.planet.realRadius;
        (int startIndex, int workLength) = UnOptimizedPlanetWorkChunk.GetWorkChunkIndices(units.cursor, _maxWorkCount, _workIndex);
        for (int i = Math.Max(1, startIndex); i < startIndex + workLength; i++)
        {
            ref EnemyUnitComponent reference2 = ref units.buffer[i];
            if (reference2.id != i)
            {
                continue;
            }
            ref EnemyData reference3 = ref enemyPool[reference2.enemyId];
            DFGBaseComponent dFGBaseComponent = bases[reference3.owner];
            if (dFGBaseComponent != null)
            {
                Interlocked.Increment(ref dFGBaseComponent.currentActivatedUnitCount);
            }
            PrefabDesc pdesc = SpaceSector.PrefabDescByModelIndex[reference3.modelIndex];
            if (i % 60 == num2)
            {
                reference2.hatredLock.Enter();
                reference2.hatred.Fade(ratio, c);
                reference2.hatredLock.Exit();
                reference2.SensorLogic_Ground(ref reference3, _planet, maxHatredGroundTmp, eAggressiveLevel, workerIndex);
                reference3.hashAddress = _planet.hashSystemDynamic.UpdateObjectHashAddress(reference3.hashAddress, reference3.id, reference3.pos, EObjectType.Enemy);
                if (eAggressiveLevel > EAggressiveLevel.Dummy && reference3.willBroadcast)
                {
                    reference3.willBroadcast = false;
                    if (reference2.hatred.max.value > 80 && reference2.hatred.max.target > 0)
                    {
                        reference2.BroadcastHatred(_planet, units.buffer, ref reference3, call_near: true, maxHatredGroundTmp, maxHatredGroundBaseTmp, workerIndex);
                    }
                }
            }
            int num6 = (int)((1f - reference2.disturbValue) * 11f + 0.5f);
            if (time * 7 % 11 < num6)
            {
                reference2.UpdateFireCondition(pdesc);
            }
            switch (reference2.behavior)
            {
                case EEnemyBehavior.None:
                    reference2.RunBehavior_None(ref reference3);
                    break;
                case EEnemyBehavior.Initial:
                    reference2.RunBehavior_Initial(ref reference3);
                    break;
                case EEnemyBehavior.KeepForm:
                    reference2.ReclaimThreatCarry(_planet.enemySystem);
                    reference2.RunBehavior_KeepForm(formTick, enemyPool, bases.buffer, realRadius, ref reference3);
                    if (reference3.combatStatId == 0 && reference2.stateTick == 0 && reference2.behavior == EEnemyBehavior.KeepForm)
                    {
                        _planet.enemySystem.DeactivateUnitDeferred(i);
                    }
                    break;
                case EEnemyBehavior.SeekForm:
                    reference2.RunBehavior_SeekForm_Ground(formTick, enemyPool, bases.buffer, realRadius, ref reference3);
                    break;
                case EEnemyBehavior.SeekTarget:
                    reference2.RunBehavior_SeekTarget_Ground(_planet, ref reference3);
                    break;
                case EEnemyBehavior.Defense:
                    reference2.RunBehavior_Defense_Ground(formTick, skillSystem, enemyPool, bases.buffer, realRadius, ref reference3);
                    break;
                case EEnemyBehavior.Engage:
                    reference2.RunBehavior_Engage_Ground(_planet, ref reference3);
                    break;
            }
            if (i % 60 == num2)
            {
                reference2.UndergroundRescue(_planet.planet, ref reference3);
            }
            if ((int)reference2.behavior >= 4)
            {
                if (dFGBaseComponent != null)
                {
                    double num7 = reference3.pos.x - vectorLF.x;
                    double num8 = reference3.pos.y - vectorLF.y;
                    double num9 = reference3.pos.z - vectorLF.z;
                    double num10 = num7 * num7 + num8 * num8 + num9 * num9;
                    dFGBaseComponent.incomingUnitStatLock.Enter();
                    dFGBaseComponent.currentIncomingAttackingUnitCount++;
                    if (num10 < dFGBaseComponent.currentClosetIncomingAttackingUnitDist2 || dFGBaseComponent.currentClosetIncomingAttackingUnitDist2 == 0.0)
                    {
                        dFGBaseComponent.currentClosetIncomingAttackingUnitDist2 = num10;
                        dFGBaseComponent.currentIncomingAttackingUnitPos = reference3.pos;
                    }
                    if (reference3.isAssaultingUnit)
                    {
                        dFGBaseComponent.currentIncomingAssaultingUnitCount++;
                        if (num10 < dFGBaseComponent.currentClosetIncomingAssaultingUnitDist2 || dFGBaseComponent.currentClosetIncomingAssaultingUnitDist2 == 0.0)
                        {
                            dFGBaseComponent.currentClosetIncomingAssaultingUnitDist2 = num10;
                            dFGBaseComponent.currentIncomingAssaultingUnitPos = reference3.pos;
                        }
                    }
                    if (reference2.hatred.max.targetType == ETargetType.Player)
                    {
                        dFGBaseComponent.currentIncomingAttackingPlayerUnitCount++;
                        if (num10 < dFGBaseComponent.currentClosetIncomingAttackingPlayerUnitDist2 || dFGBaseComponent.currentClosetIncomingAttackingPlayerUnitDist2 == 0.0)
                        {
                            dFGBaseComponent.currentClosetIncomingAttackingPlayerUnitDist2 = num10;
                            dFGBaseComponent.currentIncomingAttackingPlayerUnitPos = reference3.pos;
                        }
                    }
                    dFGBaseComponent.incomingUnitStatLock.Exit();
                }
            }
            else if ((int)reference2.behavior <= 2 && dFGBaseComponent != null)
            {
                Interlocked.Increment(ref dFGBaseComponent.currentReadyUnitCount);
                if (reference3.protoId == 8128)
                {
                    Interlocked.Increment(ref dFGBaseComponent.currentReadyRaiderCount);
                }
                else if (reference3.protoId == 8129)
                {
                    Interlocked.Increment(ref dFGBaseComponent.currentReadyRangerCount);
                }
            }
            float num11 = 1.25f - reference2.disturbValue;
            if (num11 < 0.1f)
            {
                num11 = 0.1f;
            }
            reference2.disturbValue -= num11 / 120f;
            if (reference2.disturbValue < 0f)
            {
                reference2.disturbValue = 0f;
            }
            // flag3 is true if array is not null
            if (flag3 && array![reference3.modelIndex] is DynamicRenderer dynamicRenderer)
            {
                ref GPUOBJECT reference4 = ref dynamicRenderer.instPool[reference3.modelId];
                reference4.posx = (float)reference3.pos.x;
                reference4.posy = (float)reference3.pos.y;
                reference4.posz = (float)reference3.pos.z;
                reference4.rotx = reference3.rot.x;
                reference4.roty = reference3.rot.y;
                reference4.rotz = reference3.rot.z;
                reference4.rotw = reference3.rot.w;
                ref Vector4 reference5 = ref dynamicRenderer.extraPool[reference3.modelId];
                reference5.x = reference2.anim;
                reference5.y = reference2.disturbValue;
                reference5.z = reference2.steering;
                reference5.w = reference2.speed;
            }
        }
    }
}