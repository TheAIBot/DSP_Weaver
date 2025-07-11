﻿using System.Runtime.InteropServices;
using Weaver.Optimizations.LinearDataAccess.Belts;
using Weaver.Optimizations.LinearDataAccess.Statistics;

namespace Weaver.Optimizations.LinearDataAccess.Spraycoaters;

[StructLayout(LayoutKind.Auto)]
internal struct OptimizedSpraycoater
{
    public readonly int incommingBeltSegIndexPlusSegPivotOffset;
    public readonly OptimizedCargoPath? incommingCargoPath;
    public readonly OptimizedCargoPath? outgoingCargoPath;
    public readonly int outgoingBeltSegIndexPlusSegPivotOffset;
    public readonly int outgoingBeltSpeed;
    public readonly int incCapacity;
    public readonly PowerNetwork? powerNetwork;
    public OptimizedItemId incItemId;
    public int incAbility;
    public int incSprayTimes;
    public int incCount;
    public int extraIncCount;
    public bool incUsed;

    public OptimizedSpraycoater(int incommingBeltSegIndexPlusSegPivotOffset,
                                OptimizedCargoPath? incommingCargoPath,
                                OptimizedCargoPath? outgoingCargoPath,
                                int outgoingBeltSegIndexPlusSegPivotOffset,
                                int outgoingBeltSpeed,
                                PowerNetwork? powerNetwork,
                                OptimizedItemId incItemId,
                                ref readonly SpraycoaterComponent spraycoater)
    {
        this.incommingBeltSegIndexPlusSegPivotOffset = incommingBeltSegIndexPlusSegPivotOffset;
        this.incommingCargoPath = incommingCargoPath;
        this.outgoingCargoPath = outgoingCargoPath;
        this.outgoingBeltSegIndexPlusSegPivotOffset = outgoingBeltSegIndexPlusSegPivotOffset;
        this.outgoingBeltSpeed = outgoingBeltSpeed;
        this.powerNetwork = powerNetwork;
        this.incItemId = incItemId;
        incCapacity = spraycoater.incCapacity;
        incAbility = spraycoater.incAbility;
        incSprayTimes = spraycoater.incSprayTimes;
        incCount = spraycoater.incCount;
        extraIncCount = spraycoater.extraIncCount;
        incUsed = spraycoater.incUsed;
    }

    public void InternalUpdate(OptimizedItemId[] incItemIds, int[] consumeRegister, ref bool isSpraycoatingItem, ref int sprayTime)
    {
        if (incommingCargoPath != null && incCount + extraIncCount < incCapacity)
        {
            if (incommingCargoPath.GetCargoAtIndex(incommingBeltSegIndexPlusSegPivotOffset, out OptimizedCargo cargo, out var _, out var _))
            {
                if (cargo.item != incItemId.ItemIndex && incCount == 0 && incCount == 0)
                {
                    incItemId = default;
                    incAbility = 0;
                }
                if (incItemId.ItemIndex == 0 && cargo.item != 0)
                {
                    for (int i = 0; i < incItemIds.Length; i++)
                    {
                        if (cargo.item == incItemIds[i].ItemIndex)
                        {
                            ItemProto itemProto = LDB.items.Select(incItemIds[i].ItemIndex);
                            incItemId = incItemIds[i];
                            incAbility = itemProto.Ability;
                            incSprayTimes = itemProto.HpMax;
                            break;
                        }
                    }
                }
                if (incItemId.ItemIndex != 0 && incItemId.ItemIndex == cargo.item && incommingCargoPath.TryPickItem(incommingBeltSegIndexPlusSegPivotOffset - 2, 5, incItemId.ItemIndex, out var stack, out var inc) > 0)
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
                        consumeRegister[incItemId.OptimizedItemIndex] += num6 / incSprayTimes - incCount / incSprayTimes;
                        if (incCount <= 0)
                        {
                            incItemId = default;
                            incAbility = 0;
                        }
                    }
                }
                isSpraycoatingItem = true;
            }
        }
    }

    public readonly void Save(ref SpraycoaterComponent spraycoater, bool isSpraycoatingItem, int sprayTime)
    {
        spraycoater.incItemId = incItemId.ItemIndex;
        spraycoater.incAbility = incAbility;
        spraycoater.incSprayTimes = incSprayTimes;
        spraycoater.incCount = incCount;
        spraycoater.extraIncCount = extraIncCount;
        spraycoater.incUsed = incUsed;
        spraycoater.cargoBeltItemId = isSpraycoatingItem ? 1 : 0;
        spraycoater.sprayTime = sprayTime;
    }
}
