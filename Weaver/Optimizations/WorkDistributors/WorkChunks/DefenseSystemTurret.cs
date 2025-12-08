using System;
using System.Threading;

namespace Weaver.Optimizations.WorkDistributors.WorkChunks;

internal sealed class DefenseSystemTurret : IWorkChunk
{
    private readonly PlanetFactory _planet;
    private readonly int _workIndex;
    private readonly int _maxWorkCount;

    public DefenseSystemTurret(PlanetFactory planet, int workIndex, int maxWorkCount)
    {
        _planet = planet;
        _workIndex = workIndex;
        _maxWorkCount = maxWorkCount;
    }

    public void Execute(int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        DeepProfiler.BeginSample(DPEntry.Turret, workerIndex);
        GameTickTurret(time);
        DeepProfiler.EndSample(DPEntry.Turret, workerIndex);
    }

    private void GameTickTurret(long time)
    {
        GameLogic gameLogic = GameMain.logic;
        CombatUpgradeData combatUpgradeData = default(CombatUpgradeData);
        gameLogic.history.GetCombatUpgradeData(ref combatUpgradeData);
        SkillSystem skillSystem = gameLogic.sector.skillSystem;
        EnemyData[] enemyPool = gameLogic.sector.enemyPool;
        DefenseSystem defenseSystem = _planet.defenseSystem;
        int[] consumeRegister = gameLogic.statistics.production.factoryStatPool[_planet.index].consumeRegister;
        TurretComponent[] buffer = defenseSystem.turrets.buffer;
        PowerSystem powerSystem = _planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        PowerConsumerComponent[] consumerPool = powerSystem.consumerPool;
        EntityData[] entityPool = _planet.entityPool;
        AnimData[] entityAnimPool = _planet.entityAnimPool;
        SignData[] entitySignPool = _planet.entitySignPool;
        int num2 = 10000;

        (int startIndex, int workLength) = UnOptimizedPlanetWorkChunk.GetWorkChunkIndices(defenseSystem.turrets.cursor, _maxWorkCount, _workIndex);
        for (int i = Math.Max(1, startIndex); i < startIndex + workLength; i++)
        {
            ref TurretComponent reference2 = ref buffer[i];
            if (reference2.id != i)
            {
                continue;
            }
            float num6 = networkServes[consumerPool[reference2.pcId].networkId];
            PrefabDesc prefabDesc = PlanetFactory.PrefabDescByModelIndex[entityPool[reference2.entityId].modelIndex];
            reference2.InternalUpdate(time, num6, _planet, skillSystem, prefabDesc, multithreaded: true);
            reference2.Aim(_planet, enemyPool, prefabDesc, num6);
            reference2.Shoot(_planet, enemyPool, prefabDesc, consumeRegister, num6, time, ref combatUpgradeData);
            if (reference2.supernovaTick < 0)
            {
                int num7 = -reference2.supernovaTick;
                if (num7 < num2)
                {
                    num2 = num7;
                }
            }
            if (reference2.isLockingTarget)
            {
                switch (reference2.type)
                {
                    case ETurretType.Gauss:
                        Interlocked.Increment(ref defenseSystem.engagingGaussCount);
                        break;
                    case ETurretType.Laser:
                        Interlocked.Increment(ref defenseSystem.engagingLaserCount);
                        break;
                    case ETurretType.Cannon:
                        Interlocked.Increment(ref defenseSystem.engagingCannonCount);
                        break;
                    case ETurretType.Plasma:
                        Interlocked.Increment(ref defenseSystem.engagingPlasmaCount);
                        break;
                    case ETurretType.Missile:
                        Interlocked.Increment(ref defenseSystem.engagingMissileCount);
                        break;
                    case ETurretType.LocalPlasma:
                        Interlocked.Increment(ref defenseSystem.engagingLocalPlasmaCount);
                        break;
                }
            }
            VSLayerMask num8 = reference2.vsCaps & reference2.vsSettings;
            if ((int)(num8 & VSLayerMask.OrbitAndSpace) > 0)
            {
                defenseSystem.turretEnableDefenseSpace = true;
                if (reference2.DeterminActiveEnemyUnits(isSpace: true, time))
                {
                    reference2.ActiveEnemyUnits_Space(_planet, prefabDesc);
                }
            }
            if ((int)(num8 & VSLayerMask.GroundAndAir) > 0 && reference2.DeterminActiveEnemyUnits(isSpace: false, time))
            {
                reference2.ActiveEnemyUnits_Ground(_planet, prefabDesc);
            }
            int entityId = reference2.entityId;
            if (reference2.type == ETurretType.Disturb)
            {
                entityAnimPool[entityId].state = 1u;
                if (reference2.CalculateAnimState(num6) > 1)
                {
                    float anim_working_length = prefabDesc.anim_working_length;
                    entityAnimPool[entityId].working_length = anim_working_length;
                    if (entityAnimPool[entityId].time < anim_working_length)
                    {
                        entityAnimPool[entityId].time += gameLogic.deltaTime;
                    }
                    if (entityAnimPool[entityId].time > anim_working_length)
                    {
                        entityAnimPool[entityId].time = anim_working_length - 0.01f;
                    }
                }
                else
                {
                    float anim_working_length2 = prefabDesc.anim_working_length;
                    entityAnimPool[entityId].working_length = anim_working_length2;
                    if (entityAnimPool[entityId].time > 0f)
                    {
                        entityAnimPool[entityId].time -= gameLogic.deltaTime;
                    }
                    if (entityAnimPool[entityId].time < 0f)
                    {
                        entityAnimPool[entityId].time = 0f;
                    }
                }
                entityAnimPool[entityId].power = num6;
            }
            else
            {
                bool num9 = reference2.isWorking && num6 >= 0.1f;
                float num10 = (float)(entityAnimPool[entityId].state / 100000) / 100f;
                int num11 = (int)((num10 - (float)(int)num10) * 100f + 0.5f);
                if (num9)
                {
                    num11++;
                    if (num11 > 40)
                    {
                        num11 = 40;
                    }
                }
                else
                {
                    num11--;
                    if (num11 < 0)
                    {
                        num11 = 0;
                    }
                }
                uint num12 = reference2.CalculateAnimState(num6);
                entityAnimPool[entityId].prepare_length = reference2.localDir.x;
                entityAnimPool[entityId].working_length = reference2.localDir.y;
                entityAnimPool[entityId].power = reference2.localDir.z;
                entityAnimPool[entityId].state = (uint)((int)num12 + (reference2.supernovaBursting ? ((int)(10 + (uint)(reference2.supernova_strength * 10f + 0.5f) * 100)) : (reference2.supernovaCharging ? (-reference2.supernovaTick * 100) : 0)) + num11 * 100000 + reference2.muzzleIndex * 10000000);
                entityAnimPool[entityId].time = ((prefabDesc.turretMuzzleCount == 1) ? ((1f - (float)reference2.roundFire / (float)prefabDesc.turretRoundInterval) * 10f) : ((1f - (float)reference2.muzzleFire / (float)prefabDesc.turretMuzzleInterval) * 10f));
            }
            if ((entitySignPool[entityId].signType == 0 || entitySignPool[entityId].signType > 3) && reference2.type != ETurretType.Laser)
            {
                entitySignPool[entityId].signType = ((reference2.bulletCount <= 0 && reference2.itemCount <= 0) ? 14u : 0u);
            }
        }

        defenseSystem.engagingTurretTotalCount = defenseSystem.engagingGaussCount + defenseSystem.engagingLaserCount + defenseSystem.engagingCannonCount + defenseSystem.engagingPlasmaCount + defenseSystem.engagingMissileCount + defenseSystem.engagingLocalPlasmaCount;
        lock (defenseSystem.incoming_supernova_time_lock)
        {
            if (num2 < defenseSystem.incomingSupernovaTime)
            {
                defenseSystem.incomingSupernovaTime = num2;
            }
        }
    }
}