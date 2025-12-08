using System.Runtime.InteropServices;
using Weaver.Optimizations.Belts;
using Weaver.Optimizations.Statistics;

namespace Weaver.Optimizations.Miners;

[StructLayout(LayoutKind.Sequential, Pack=1)]
internal struct OptimizedWaterMiner
{
    private readonly BeltIndex outputBeltIndex;
    private readonly int outputBeltOffset;
    private readonly int period;
    private readonly OptimizedItemId productId;
    public readonly int speed;
    private int time;
    public float speedDamper;
    public int productCount;

    public OptimizedWaterMiner(BeltIndex outputBeltIndex, int outputBeltOffset, OptimizedItemId productId, ref readonly MinerComponent miner)
    {
        this.outputBeltIndex = outputBeltIndex;
        this.outputBeltOffset = outputBeltOffset;
        speed = miner.speed;
        speedDamper = miner.speedDamper;
        period = miner.period;
        time = miner.time;
        this.productId = productId;
        productCount = miner.productCount;
    }

    public uint InternalUpdate(float power, float miningSpeed, int[] productRegister, OptimizedCargoPath[] optimizedCargoPaths)
    {
        if (power < 0.1f)
        {
            return 0u;
        }
        uint result = 0u;
        if (time < period)
        {
            time += (int)(power * speedDamper * speed * miningSpeed);
            result = 1u;
        }
        if (time >= period)
        {
            int num14 = time / period;
            if (productCount < 50)
            {
                productCount += num14;
                productRegister[productId.OptimizedItemIndex] += num14;
                time -= period * num14;
            }
        }
        if (productCount > 0)
        {
            double num15 = 36000000.0 / period * (double)miningSpeed;
            int num16 = (int)(num15 - 0.009999999776482582) / 1800 + 1;
            num16 = num16 >= 4 ? 4 : num16 < 1 ? 1 : num16;
            int num17 = productCount < num16 ? productCount : num16;
            int num18 = outputBeltIndex.GetBelt(optimizedCargoPaths).TryInsertItem(outputBeltOffset, productId.ItemIndex, (byte)num17, 0) ? (byte)num17 : 0;
            productCount -= num18;
        }
        return result;
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
    }
}
