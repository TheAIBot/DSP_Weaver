using System;
using System.Runtime.InteropServices;
using Weaver.Optimizations.LinearDataAccess.Statistics;

namespace Weaver.Optimizations.LinearDataAccess.Miners;

[StructLayout(LayoutKind.Auto)]
internal struct OptimizedVeinMiner<T> : IMiner
    where T : struct, IMinerOutput<T>
{
    public readonly T output;
    public readonly int speed;
    private readonly int period;
    private readonly int[] veins;
    private readonly OptimizedItemId[] veinProducts;
    private int time;
    private int veinCount;
    private int currentVeinIndex;
    private int minimumVeinAmount;
    public OptimizedItemId productId;
    private double costFrac;

    public int ProductCount { get; set; }
    public float SpeedDamper { get; set; }

    public OptimizedVeinMiner(T output, OptimizedItemId[] veinProducts, OptimizedItemId productId, ref readonly MinerComponent miner)
    {
        this.output = output;
        this.veinProducts = veinProducts;
        speed = miner.speed;
        SpeedDamper = miner.speedDamper;
        period = miner.period;
        veins = miner.veins;
        time = miner.time;
        veinCount = miner.veinCount;
        currentVeinIndex = miner.currentVeinIndex;
        minimumVeinAmount = miner.minimumVeinAmount;
        this.productId = productId;
        ProductCount = miner.productCount;
        costFrac = miner.costFrac;
    }

    public uint InternalUpdate(PlanetFactory factory,
                               VeinData[] veinPool,
                               float power,
                               float miningRate,
                               float miningSpeed,
                               int[] productRegister,
                               ref MiningFlags miningFlags)
    {
        if (power < 0.1f)
        {
            return 0u;
        }
        uint result = 0u;
        if (veinCount > 0)
        {
            if (time <= period)
            {
                time += (int)(power * SpeedDamper * (float)speed * miningSpeed * (float)veinCount);
                if (time < -2000000000)
                {
                    time = int.MaxValue;
                }
                else if (time < 0)
                {
                    time = 0;
                }
                result = 1u;
            }
            if (time >= period)
            {
                if (veinCount > 0)
                {
                    int num = veins[currentVeinIndex];
                    OptimizedItemId veinProductId = veinProducts[currentVeinIndex];
                    Assert.Positive(num);
                    if (veinPool[num].id == 0)
                    {
                        RemoveVeinFromArray(currentVeinIndex);
                        GetMinimumVeinAmount(veinPool);
                        if (veinCount > 1)
                        {
                            currentVeinIndex %= veinCount;
                        }
                        else
                        {
                            currentVeinIndex = 0;
                        }
                        time += (int)(power * SpeedDamper * (float)speed * miningSpeed * (float)veinCount);
                        return 0u;
                    }
                    if (ProductCount < 50 && (productId.ItemIndex == 0 || productId.ItemIndex == veinPool[num].productId))
                    {
                        productId = veinProductId;
                        int num2 = time / period;
                        int num3 = 0;
                        if (veinPool[num].amount > 0)
                        {
                            if (miningRate > 0f)
                            {
                                double num4 = (double)miningRate * (double)num2;
                                costFrac += num4;
                                int num5;
                                if (costFrac < (double)veinPool[num].amount)
                                {
                                    num3 = num2;
                                    num5 = (int)costFrac;
                                    costFrac -= num5;
                                }
                                else
                                {
                                    num5 = veinPool[num].amount;
                                    double num6 = costFrac - num4;
                                    double num7 = ((double)num5 - num6) / num4;
                                    double num8 = (double)num2 * num7;
                                    num3 = (int)(Math.Ceiling(num8) + 0.01);
                                    costFrac = (double)miningRate * ((double)num3 - num8);
                                }
                                if (num5 > 0)
                                {
                                    int groupIndex = veinPool[num].groupIndex;
                                    veinPool[num].amount -= num5;
                                    if (veinPool[num].amount < minimumVeinAmount)
                                    {
                                        minimumVeinAmount = veinPool[num].amount;
                                    }
                                    factory.veinGroups[groupIndex].amount -= num5;
                                    if (veinPool[num].amount <= 0)
                                    {
                                        int veinType = (int)veinPool[num].type;
                                        UnityEngine.Vector3 pos = veinPool[num].pos;
                                        lock (veinPool)
                                        {
                                            factory.RemoveVeinWithComponents(num);
                                            factory.RecalculateVeinGroup(groupIndex);
                                            factory.NotifyVeinExhausted(veinType, pos);
                                        }
                                    }
                                    else
                                    {
                                        currentVeinIndex++;
                                    }
                                }
                            }
                            else
                            {
                                num3 = num2;
                                costFrac = 0.0;
                            }
                            ProductCount += num3;
                            productRegister[productId.OptimizedItemIndex] += num3;

                            miningFlags.AddMiningFlagUnsafe(veinPool[num].type);
                            miningFlags.AddVeinMiningFlagUnsafe(veinPool[num].type);
                        }
                        else
                        {
                            RemoveVeinFromArray(currentVeinIndex);
                            GetMinimumVeinAmount(veinPool);
                        }
                        time -= period * num3;
                        if (veinCount > 1)
                        {
                            currentVeinIndex %= veinCount;
                        }
                        else
                        {
                            currentVeinIndex = 0;
                        }
                    }
                }
            }
        }
        if (ProductCount > 0 && productId.ItemIndex > 0)
        {
            double num15 = 36000000.0 / (double)period * (double)miningSpeed;
            num15 *= (double)veinCount;
            int num16 = (int)(num15 - 0.009999999776482582) / 1800 + 1;
            num16 = ((num16 >= 4) ? 4 : ((num16 < 1) ? 1 : num16));
            int num17 = ((ProductCount < num16) ? ProductCount : num16);
            int num18 = output.InsertInto(productId.ItemIndex, (byte)num17);
            ProductCount -= num18;
            if (ProductCount == 0)
            {
                productId = default;
            }
        }
        return result;
    }

    public readonly void Save(ref MinerComponent miner)
    {
        miner.time = time;
        miner.veinCount = veinCount;
        miner.currentVeinIndex = currentVeinIndex;
        miner.minimumVeinAmount = minimumVeinAmount;
        miner.productId = productId.ItemIndex;
        miner.costFrac = costFrac;
        miner.productCount = ProductCount;
        miner.speedDamper = SpeedDamper;
    }

    public readonly EWorkState DetermineState()
    {
        if (time < period)
        {
            return EWorkState.Running;
        }
        else
        {
            return EWorkState.Full;
        }
    }

    private void RemoveVeinFromArray(int index)
    {
        if (veins != null)
        {
            veins[index] = 0;
            veinProducts[index] = default;
            veinCount--;
            if (veinCount - index > 0)
            {
                Array.Copy(veins, index + 1, veins, index, veinCount - index);
                Array.Copy(veinProducts, index + 1, veinProducts, index, veinCount - index);
            }
            if (veins.Length - veinCount > 0)
            {
                Array.Clear(veins, veinCount, veins.Length - veinCount);
                Array.Clear(veinProducts, veinCount, veinProducts.Length - veinCount);
            }
        }
    }

    private void GetMinimumVeinAmount(VeinData[] veinPool)
    {
        minimumVeinAmount = int.MaxValue;
        if (veinCount > 0)
        {
            for (int i = 0; i < veinCount; i++)
            {
                int num = veins[i];
                if (num > 0 && veinPool[num].id == num && veinPool[num].amount > 0 && veinPool[num].amount < minimumVeinAmount)
                {
                    minimumVeinAmount = veinPool[num].amount;
                }
            }
        }
        else
        {
            minimumVeinAmount = 0;
        }
    }
}
