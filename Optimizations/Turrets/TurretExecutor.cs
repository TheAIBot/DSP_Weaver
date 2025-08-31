namespace Weaver.Optimizations.Turrets;

internal sealed class TurretExecutor
{
    private readonly OptimizedTurret[] _optimizedTurrets;

    public TurretExecutor(OptimizedTurret[] optimizedTurrets)
    {
        _optimizedTurrets = optimizedTurrets;
    }

    public int GameTick(DefenseSystem defenseSystem, long tick, ref CombatUpgradeData combatUpgradeData)
    {
        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[defenseSystem.factory.index];
        int[] consumeRegister = obj.consumeRegister;
        PowerSystem powerSystem = defenseSystem.factory.powerSystem;
        SkillSystem skillSystem = defenseSystem.spaceSector.skillSystem;
        float[] networkServes = powerSystem.networkServes;
        PowerConsumerComponent[] consumerPool = powerSystem.consumerPool;
        EntityData[] entityPool = defenseSystem.factory.entityPool;
        EnemyData[] enemyPool = defenseSystem.spaceSector.enemyPool;
        ref CombatSettings combatSettings = ref defenseSystem.factory.gameData.history.combatSettings;
        int num2 = 10000;
        TurretComponent[] buffer2 = defenseSystem.turrets.buffer;
        OptimizedTurret[] optimizedTurrets = _optimizedTurrets;
        for (int i = 0; i < optimizedTurrets.Length; i++)
        {
            ref OptimizedTurret turret = ref optimizedTurrets[i];
            ref TurretComponent reference2 = ref buffer2[turret.turretIndex];

            float num4 = networkServes[consumerPool[reference2.pcId].networkId];
            PrefabDesc prefabDesc = PlanetFactory.PrefabDescByModelIndex[entityPool[reference2.entityId].modelIndex];
            turret.InternalUpdate(ref reference2, tick, num4, defenseSystem.factory, skillSystem, prefabDesc, false);
            reference2.Aim(defenseSystem.factory, enemyPool, prefabDesc, num4);
            reference2.Shoot(defenseSystem.factory, enemyPool, prefabDesc, consumeRegister, num4, tick, ref combatUpgradeData);
            if (reference2.supernovaTick < 0)
            {
                int num5 = -reference2.supernovaTick;
                if (num5 < num2)
                {
                    num2 = num5;
                }
            }
            if (reference2.isLockingTarget)
            {
                switch (reference2.type)
                {
                    case ETurretType.Gauss:
                        defenseSystem.engagingGaussCount++;
                        break;
                    case ETurretType.Laser:
                        defenseSystem.engagingLaserCount++;
                        break;
                    case ETurretType.Cannon:
                        defenseSystem.engagingCannonCount++;
                        break;
                    case ETurretType.Plasma:
                        defenseSystem.engagingPlasmaCount++;
                        break;
                    case ETurretType.Missile:
                        defenseSystem.engagingMissileCount++;
                        break;
                    case ETurretType.LocalPlasma:
                        defenseSystem.engagingLocalPlasmaCount++;
                        break;
                }
            }
            VSLayerMask num6 = reference2.vsCaps & reference2.vsSettings;
            if ((int)(num6 & VSLayerMask.OrbitAndSpace) > 0)
            {
                defenseSystem.turretEnableDefenseSpace = true;
                if (reference2.DeterminActiveEnemyUnits(isSpace: true, tick))
                {
                    reference2.ActiveEnemyUnits_Space(defenseSystem.factory, prefabDesc);
                }
            }
            if ((int)(num6 & VSLayerMask.GroundAndAir) > 0 && reference2.DeterminActiveEnemyUnits(isSpace: false, tick))
            {
                reference2.ActiveEnemyUnits_Ground(defenseSystem.factory, prefabDesc);
            }
        }

        return num2;
    }
}