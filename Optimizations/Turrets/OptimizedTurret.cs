using System.Collections.Generic;
using System.Runtime.InteropServices;
using Weaver.Optimizations.Belts;

namespace Weaver.Optimizations.Turrets;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct OptimizedTurret
{
    private readonly OptimizedIndexedCargoPath targetBelt;
    private readonly int targetBeltOffset;
    public readonly int turretIndex;

    public OptimizedTurret(OptimizedIndexedCargoPath targetBelt, int targetBeltOffset, int turretIndex)
    {
        this.targetBelt = targetBelt;
        this.targetBeltOffset = targetBeltOffset;
        this.turretIndex = turretIndex;
    }

    public static bool NeedToCheckBelt(ref TurretComponent turret)
    {
        if (turret.targetBeltId == 0 || turret.itemCount >= 5)
        {
            return false;
        }

        return true;
    }

    public void BeltUpdate(ref TurretComponent turret, Dictionary<BeltIndex, object> beltLocks)
    {
        if (!targetBelt.HasBelt || turret.itemCount >= 5)
        {
            return;
        }

        lock (beltLocks[targetBelt.BeltIndex])
        {
            if (turret.itemId == 0)
            {
                if (targetBelt.Belt.TryPickItem(targetBeltOffset - 2, 5, 0, ItemProto.turretNeeds[(uint)turret.ammoType], out OptimizedCargo optimizedCargo))
                {
                    turret.SetNewItem(optimizedCargo.Item, optimizedCargo.Stack, optimizedCargo.Inc);
                }
            }
            else if (targetBelt.Belt.TryPickItem(targetBeltOffset - 2, 5, turret.itemId, out OptimizedCargo optimizedCargo))
            {
                turret.itemCount += optimizedCargo.Stack;
                turret.itemInc += optimizedCargo.Inc;
            }
        }
    }
}
