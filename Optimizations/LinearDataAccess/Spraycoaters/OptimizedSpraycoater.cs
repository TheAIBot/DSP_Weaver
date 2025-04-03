using System.Runtime.InteropServices;

namespace Weaver.Optimizations.LinearDataAccess.Spraycoaters;

[StructLayout(LayoutKind.Auto)]
public struct OptimizedSpraycoater
{
    public readonly int incommingBeltSegIndexPlusSegPivotOffset;
    public readonly CargoPath incommingCargoPath;
    public readonly int[] incItemIds;
    public readonly CargoPath outgoingCargoPath;
    public readonly int outgoingBeltSegIndexPlusSegPivotOffset;
    public readonly int outgoingBeltSpeed;
    public readonly PowerNetwork powerNetwork;
    public readonly int incCapacity;
    public int incItemId;
    public int incAbility;
    public int incSprayTimes;
    public int incCount;
    public int extraIncCount;
    public bool incUsed;

    public OptimizedSpraycoater(int incommingBeltSegIndexPlusSegPivotOffset,
                                CargoPath incommingCargoPath,
                                int[] incItemIds,
                                CargoPath outgoingCargoPath,
                                int outgoingBeltSegIndexPlusSegPivotOffset,
                                int outgoingBeltSpeed,
                                PowerNetwork powerNetwork,
                                ref readonly SpraycoaterComponent spraycoater)
    {
        this.incommingBeltSegIndexPlusSegPivotOffset = incommingBeltSegIndexPlusSegPivotOffset;
        this.incommingCargoPath = incommingCargoPath;
        this.incItemIds = incItemIds;
        this.outgoingCargoPath = outgoingCargoPath;
        this.outgoingBeltSegIndexPlusSegPivotOffset = outgoingBeltSegIndexPlusSegPivotOffset;
        this.outgoingBeltSpeed = outgoingBeltSpeed;
        this.powerNetwork = powerNetwork;
        incCapacity = spraycoater.incCapacity;
        incItemId = spraycoater.incItemId;
        incAbility = spraycoater.incAbility;
        incSprayTimes = spraycoater.incSprayTimes;
        incCount = spraycoater.incCount;
        extraIncCount = spraycoater.extraIncCount;
        incUsed = spraycoater.incUsed;
    }

    public void InternalUpdate(int[] consumeRegister, ref bool isSpraycoatingItem, ref int sprayTime)
    {
        if (incommingCargoPath != null && incCount + extraIncCount < incCapacity)
        {
            if (incommingCargoPath.GetCargoAtIndex(incommingBeltSegIndexPlusSegPivotOffset, out var cargo, out var _, out var _))
            {
                if (cargo.item != incItemId && incCount == 0 && incCount == 0)
                {
                    incItemId = 0;
                    incAbility = 0;
                }
                if (incItemId == 0 && cargo.item != 0)
                {
                    for (int i = 0; i < incItemIds.Length; i++)
                    {
                        if (cargo.item == incItemIds[i])
                        {
                            ItemProto itemProto = LDB.items.Select(incItemIds[i]);
                            incItemId = incItemIds[i];
                            incAbility = itemProto.Ability;
                            incSprayTimes = itemProto.HpMax;
                            break;
                        }
                    }
                }
                if (incItemId != 0 && incItemId == cargo.item && incommingCargoPath.TryPickItem(incommingBeltSegIndexPlusSegPivotOffset - 2, 5, incItemId, out var stack, out var inc) > 0)
                {
                    int num = inc;
                    for (int j = 0; j < stack; j++)
                    {
                        int num2 = stack - j;
                        int num3 = (int)(num / (float)num2 + 0.5f);
                        num3 = num3 > 10 ? 10 : num3;
                        incCount += incSprayTimes;
                        extraIncCount += (int)(incSprayTimes * (Cargo.incTable[num3] * 0.001) + 0.1);
                        if (!incUsed)
                        {
                            incUsed = extraIncCount > 0;
                        }
                        num -= num3;
                    }
                }
            }
        }
        float num4 = powerNetwork != null ? (float)powerNetwork.consumerRatio : 0f;
        bool flag = num4 > 0.1f;
        if (outgoingCargoPath != null)
        {
            if (sprayTime < 10000)
            {
                sprayTime += flag ? outgoingBeltSpeed * (int)(1000f * num4) : 0;
            }
            else
            {
                isSpraycoatingItem = false;
            }
            if (flag && outgoingCargoPath != null && outgoingCargoPath.GetCargoAtIndex(outgoingBeltSegIndexPlusSegPivotOffset, out var cargo2, out var cargoId2, out var _) && sprayTime >= 10000)
            {
                int num5 = cargo2.stack > incCount + extraIncCount ? incCount + extraIncCount : cargo2.stack;
                if (num5 * incAbility > cargo2.inc)
                {
                    sprayTime -= 10000;
                    outgoingCargoPath.cargoContainer.cargoPool[cargoId2].inc = (byte)(num5 * incAbility);
                    extraIncCount -= num5;
                    if (extraIncCount < 0)
                    {
                        int num6 = incCount;
                        incCount += extraIncCount;
                        extraIncCount = 0;
                        consumeRegister[incItemId] += num6 / incSprayTimes - incCount / incSprayTimes;
                        if (incCount <= 0)
                        {
                            incItemId = 0;
                            incAbility = 0;
                        }
                    }
                }
                isSpraycoatingItem = true;
            }
        }
    }
}
