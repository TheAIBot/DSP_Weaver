using System.Runtime.InteropServices;
using Weaver.Optimizations.LinearDataAccess.Belts;

namespace Weaver.Optimizations.LinearDataAccess.Miners;

[StructLayout(LayoutKind.Auto)]
internal struct OptimizedWaterMiner
{
    private readonly OptimizedCargoPath outputBelt;
    private readonly int outputBeltOffset;
    private readonly int period;
    private readonly int productId;
    public readonly int speed;
    private int time;
    public float speedDamper;
    public int productCount;

    public OptimizedWaterMiner(OptimizedCargoPath outputBelt, int outputBeltOffset, int productId, ref readonly MinerComponent miner)
    {
        this.outputBelt = outputBelt;
        this.outputBeltOffset = outputBeltOffset;
        speed = miner.speed;
        speedDamper = miner.speedDamper;
        period = miner.period;
        time = miner.time;
        this.productId = productId;
        productCount = miner.productCount;
    }

    public uint InternalUpdate(float power, float miningSpeed, int[] productRegister)
    {
        if (power < 0.1f)
        {
            return 0u;
        }
        uint result = 0u;
        if (time < period)
        {
            time += (int)(power * speedDamper * (float)speed * miningSpeed);
            result = 1u;
        }
        if (time >= period)
        {
            int num14 = time / period;
            if (productCount < 50)
            {
                productCount += num14;
                lock (productRegister)
                {
                    productRegister[productId] += num14;
                }
                time -= period * num14;
            }
        }
        if (productCount > 0)
        {
            double num15 = 36000000.0 / (double)period * (double)miningSpeed;
            int num16 = (int)(num15 - 0.009999999776482582) / 1800 + 1;
            num16 = ((num16 >= 4) ? 4 : ((num16 < 1) ? 1 : num16));
            int num17 = ((productCount < num16) ? productCount : num16);
            int num18 = outputBelt.TryInsertItem(outputBeltOffset, productId, (byte)num17, 0) ? (byte)num17 : 0;
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
