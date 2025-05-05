using System.Runtime.InteropServices;
using Weaver.Optimizations.LinearDataAccess.Belts;

namespace Weaver.Optimizations.LinearDataAccess.Turrets;

[StructLayout(LayoutKind.Auto)]
internal readonly struct OptimizedTurret
{
    private readonly OptimizedCargoPath targetBelt;
    private readonly int targetBeltOffset;
    public readonly int turretIndex;

    public OptimizedTurret(OptimizedCargoPath targetBelt, int targetBeltOffset, int turretIndex)
    {
        this.targetBelt = targetBelt;
        this.targetBeltOffset = targetBeltOffset;
        this.turretIndex = turretIndex;
    }

    public void InternalUpdate(ref TurretComponent turret, long time, float power, PlanetFactory factory, SkillSystem skillSystem, PrefabDesc pdesc)
    {
        if (turret.type == ETurretType.Laser)
        {
            turret.bulletCount = 1;
        }
        turret.lastTotalKillCount = turret.totalKillCount;
        BeltUpdate(ref turret);
        if (turret.projectileId > 0 && (!turret.isLockingTarget || turret.supernovaCharging))
        {
            turret.StopContinuousSkill(skillSystem);
        }
        if (turret.supernovaTick > 0)
        {
            turret.supernovaTick--;
            if (turret.supernovaTick <= 600)
            {
                turret.supernovaStrength = 0f;
            }
            else
            {
                turret.supernovaStrength = turret.supernovaStrength * 0.98f + 0.06f;
            }
        }
        if (turret.supernovaTick < 0)
        {
            turret.supernovaTick++;
            if (turret.supernovaTick == 0)
            {
                turret.supernovaTick = 901;
                turret.supernovaStrength = 30f;
            }
            else
            {
                turret.supernovaStrength = 0f;
            }
        }
        if (power < 0.3f && turret.inSupernova)
        {
            turret.CancelSupernova();
        }
        if (!turret.isAiming)
        {
            turret.aimt = 0;
        }
        if (turret.target.id == 0)
        {
            turret.isAiming = false;
            turret.isLockingTarget = false;
        }
        else if (turret.target.astroId == factory.planet.astroId)
        {
            ref EnemyData reference = ref factory.enemyPool[turret.target.id];
            if (reference.id == 0 || reference.isInvincible)
            {
                turret.SetNullTarget(needSearch: true, factory, pdesc, time);
            }
        }
        else
        {
            ref EnemyData reference2 = ref factory.sector.enemyPool[turret.target.id];
            if (reference2.id == 0 || reference2.isInvincible)
            {
                turret.SetNullTarget(needSearch: true, factory, pdesc, time);
            }
        }
        if (power < 0.1f)
        {
            turret.StopContinuousSkill(skillSystem);
            turret.isAiming = false;
            turret.isLockingTarget = false;
            turret.CancelSupernova();
            int turretROF = pdesc.turretROF;
            turret.roundFire -= turretROF;
            turret.muzzleFire -= turretROF;
            if (turret.roundFire < 0)
            {
                turret.roundFire = 0;
            }
            if (turret.muzzleFire < 0)
            {
                turret.muzzleFire = 0;
            }
            return;
        }
        if (turret.bulletCount == 0 && turret.itemCount == 0)
        {
            turret.SetNullTarget(needSearch: false, null, null, 0L);
            turret.ClearItem();
            turret.CancelSupernova();
            return;
        }
        if (turret.DetermineSearchTarget(time))
        {
            turret.Search(factory, pdesc, time);
        }
        if (!turret.hatred0.notNull || turret.hatred0.value >= 60)
        {
            return;
        }
        if (turret.hatred0.isSpace)
        {
            if (turret.id % 30 == (int)(time % 30))
            {
                turret.hatred0.value++;
            }
        }
        else if (turret.id % 10 == (int)(time % 10))
        {
            turret.hatred0.value++;
        }
    }

    public void BeltUpdate(ref TurretComponent turret)
    {
        if (targetBelt == null || turret.itemCount >= 5)
        {
            return;
        }
        byte stack;
        byte inc;
        if (turret.itemId == 0)
        {
            int num = targetBelt.TryPickItem(targetBeltOffset - 2, 5, 0, ItemProto.turretNeeds[(uint)turret.ammoType], out stack, out inc);
            if (num > 0)
            {
                turret.SetNewItem(num, stack, inc);
            }
        }
        else if (turret.itemId == targetBelt.TryPickItem(targetBeltOffset - 2, 5, turret.itemId, out stack, out inc))
        {
            turret.itemCount += stack;
            turret.itemInc += inc;
        }
    }
}
