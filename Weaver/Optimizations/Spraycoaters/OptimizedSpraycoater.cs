using System.Runtime.InteropServices;
using Weaver.Optimizations.Belts;
using Weaver.Optimizations.Statistics;

namespace Weaver.Optimizations.Spraycoaters;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct OptimizedSpraycoater
{
    public readonly int incommingBeltSegIndexPlusSegPivotOffset;
    public readonly BeltIndex incommingBeltIndex;
    public readonly BeltIndex outgoingBeltIndex;
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
                                BeltIndex incommingBeltIndex,
                                BeltIndex outgoingBeltIndex,
                                int outgoingBeltSegIndexPlusSegPivotOffset,
                                int outgoingBeltSpeed,
                                PowerNetwork? powerNetwork,
                                OptimizedItemId incItemId,
                                ref readonly SpraycoaterComponent spraycoater)
    {
        this.incommingBeltSegIndexPlusSegPivotOffset = incommingBeltSegIndexPlusSegPivotOffset;
        this.incommingBeltIndex = incommingBeltIndex;
        this.outgoingBeltIndex = outgoingBeltIndex;
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

    public void InternalUpdate(OptimizedItemId[] incItemIds, int[] consumeRegister, ref bool isSpraycoatingItem, ref int sprayTime, OptimizedCargoPath[] optimizedCargoPaths)
    {
        if (incommingBeltIndex.HasValue && incCount + extraIncCount < incCapacity)
        {
            ref OptimizedCargoPath incommingBelt = ref incommingBeltIndex.GetBelt(optimizedCargoPaths);
            if (incommingBelt.GetCargoAtIndex(incommingBeltSegIndexPlusSegPivotOffset, out OptimizedCargo cargo, out var _, out var _))
            {
                if (cargo.Item != incItemId.ItemIndex && incCount == 0 && incCount == 0)
                {
                    incItemId = default;
                    incAbility = 0;
                }
                if (incItemId.ItemIndex == 0 && cargo.Item != 0)
                {
                    for (int i = 0; i < incItemIds.Length; i++)
                    {
                        if (cargo.Item == incItemIds[i].ItemIndex)
                        {
                            ItemProto itemProto = LDB.items.Select(incItemIds[i].ItemIndex);
                            incItemId = incItemIds[i];
                            incAbility = itemProto.Ability;
                            incSprayTimes = itemProto.HpMax;
                            break;
                        }
                    }
                }
                if (incItemId.ItemIndex != 0 && incItemId.ItemIndex == cargo.Item)
                {
                    if (incommingBelt.TryPickItem(incommingBeltSegIndexPlusSegPivotOffset - 2, 5, incItemId.ItemIndex, out OptimizedCargo someOtherCargo))
                    {
                        int inc = someOtherCargo.Inc;
                        int stack = someOtherCargo.Stack;
                        for (int j = 0; j < stack; j++)
                        {
                            int num2 = stack - j;
                            int num3 = (int)(inc / (float)num2 + 0.5f);
                            num3 = num3 > 10 ? 10 : num3;
                            incCount += incSprayTimes;
                            extraIncCount += (int)(incSprayTimes * (Cargo.incTable[num3] * 0.001) + 0.1);
                            if (!incUsed)
                            {
                                incUsed = extraIncCount > 0;
                            }
                            inc -= num3;
                        }
                    }
                }
            }
        }
        float num4 = powerNetwork != null ? (float)powerNetwork.consumerRatio : 0f;
        bool flag = num4 > 0.1f;
        if (outgoingBeltIndex.HasValue)
        {
            if (sprayTime < 10000)
            {
                sprayTime += flag ? outgoingBeltSpeed * (int)(1000f * num4) : 0;
            }
            else
            {
                isSpraycoatingItem = false;
            }
            ref OptimizedCargoPath outgoingBelt = ref outgoingBeltIndex.GetBelt(optimizedCargoPaths);
            if (flag && outgoingBelt.GetCargoAtIndex(outgoingBeltSegIndexPlusSegPivotOffset, out var cargo2, out var cargoBufferIndex, out var _) && sprayTime >= 10000)
            {
                int num5 = cargo2.Stack > incCount + extraIncCount ? incCount + extraIncCount : cargo2.Stack;
                if (num5 * incAbility > cargo2.Inc)
                {
                    sprayTime -= 10000;
                    cargo2.Inc = (byte)(num5 * incAbility);
                    outgoingBelt.buffer.SetCargo(cargoBufferIndex, cargo2);
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
