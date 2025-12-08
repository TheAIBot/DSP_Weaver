using System.Runtime.InteropServices;
using Weaver.Optimizations.Belts;
using Weaver.Optimizations.Statistics;

namespace Weaver.Optimizations.Miners;

[StructLayout(LayoutKind.Sequential, Pack=1)]
internal struct OptimizedOilMiner
{
    private readonly BeltIndex outputBeltIndex;
    private readonly int outputBeltOffset;
    public readonly int speed;
    private readonly int period;
    private readonly int insertTarget;
    private readonly int veinIndex;
    private readonly OptimizedItemId productId;
    private int time;
    public float speedDamper;
    public int productCount;
    private double costFrac;

    public OptimizedOilMiner(BeltIndex outputBeltIndex, int outputBeltOffset, OptimizedItemId productId, ref readonly MinerComponent miner)
    {
        this.outputBeltIndex = outputBeltIndex;
        this.outputBeltOffset = outputBeltOffset;
        speed = miner.speed;
        speedDamper = miner.speedDamper;
        period = miner.period;
        insertTarget = miner.insertTarget;
        veinIndex = miner.veins[0];
        time = miner.time;
        this.productId = productId;
        productCount = miner.productCount;
        costFrac = miner.costFrac;
    }

    public void InternalUpdate(PlanetFactory factory, VeinData[] veinPool, float power, float miningRate, float miningSpeed, int[] productRegister, OptimizedCargoPath[] optimizedCargoPaths)
    {
        if (power < 0.1f)
        {
            return;
        }
        ref VeinData oilVein = ref veinPool[veinIndex];
        if (time < period)
        {
            float num10 = oilVein.amount * VeinData.oilSpeedMultiplier;
            time += (int)(power * speedDamper * speed * miningSpeed * num10 + 0.5f);
        }
        if (time >= period && productCount < 50)
        {
            int num11 = time / period;
            if (miningRate > 0f && oilVein.amount > 2500)
            {
                costFrac += (double)miningRate * num11;
                int num12 = (int)costFrac;
                costFrac -= num12;
                int num13 = oilVein.amount - 2500;
                if (num12 > 0)
                {
                    if (num12 > num13)
                    {
                        num12 = num13;
                    }
                    oilVein.amount -= num12;
                    factory.veinGroups[oilVein.groupIndex].amount -= num12;
                    if (oilVein.amount <= 2500)
                    {
                        lock (veinPool)
                        {
                            factory.NotifyVeinExhausted((int)oilVein.type, oilVein.groupIndex, oilVein.pos);
                        }
                    }
                }
            }
            productCount += num11;
            productRegister[productId.OptimizedItemIndex] += num11;
            time -= period * num11;
        }

        if (productCount > 0 && insertTarget > 0 && productId.ItemIndex > 0)
        {
            double num15 = 36000000.0 / period * (double)miningSpeed;
            num15 *= (double)(veinPool[veinIndex].amount * VeinData.oilSpeedMultiplier);
            int num16 = (int)(num15 - 0.009999999776482582) / 1800 + 1;
            num16 = num16 >= 4 ? 4 : num16 < 1 ? 1 : num16;
            int num17 = productCount < num16 ? productCount : num16;
            int num18 = outputBeltIndex.GetBelt(optimizedCargoPaths).TryInsertItem(outputBeltOffset, productId.ItemIndex, (byte)num17, 0) ? (byte)num17 : 0;
            productCount -= num18;
        }
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

    public readonly void Save(ref MinerComponent miner)
    {
        miner.time = time;
        miner.speedDamper = speedDamper;
        miner.productCount = productCount;
        miner.costFrac = costFrac;
    }
}
