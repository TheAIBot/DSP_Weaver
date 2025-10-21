using System.Runtime.InteropServices;
using Weaver.Optimizations.Belts;

namespace Weaver.Optimizations.Pilers;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct OptimizedPiler
{
    private readonly int inputBeltIndex;
    private readonly int outputBeltIndex;
    private readonly int inputBeltSpeed;
    private readonly int outputBeltSpeed;
    private readonly PilerState pilerState;
    private byte cacheCargoStack1;
    private byte cacheCargoInc1;
    private byte cacheCargoStack2;
    private byte cacheCargoInc2;
    private byte cacheCdTick;
    private short cacheItemId1;
    private short cacheItemId2;
    private int slowlyBeltSpeed;

    public OptimizedPiler(int inputBeltIndex,
                          int outputBeltIndex,
                          int inputBeltSpeed,
                          int outputBeltSpeed,
                          ref readonly PilerComponent piler)
    {
        this.inputBeltIndex = inputBeltIndex;
        this.outputBeltIndex = outputBeltIndex;
        this.inputBeltSpeed = inputBeltSpeed;
        this.outputBeltSpeed = outputBeltSpeed;
        cacheCargoStack1 = piler.cacheCargoStack1;
        cacheCargoInc1 = piler.cacheCargoInc1;
        cacheCargoStack2 = piler.cacheCargoStack2;
        cacheCargoInc2 = piler.cacheCargoInc2;
        cacheCdTick = piler.cacheCdTick;
        cacheItemId1 = piler.cacheItemId1;
        cacheItemId2 = piler.cacheItemId2;
        pilerState = piler.pilerState;
        slowlyBeltSpeed = piler.slowlyBeltSpeed;
    }

    public uint InternalUpdate(float power, ref int timeSpend, OptimizedCargoPath[] optimizedCargoPaths)
    {
        if (cacheCdTick > 0)
        {
            cacheCdTick--;
        }
        bool flag = power > 0.1f;
        slowlyBeltSpeed = inputBeltSpeed > outputBeltSpeed ? outputBeltSpeed : inputBeltSpeed;
        slowlyBeltSpeed = slowlyBeltSpeed > 2 ? 3 : slowlyBeltSpeed;
        if (timeSpend < 10000)
        {
            timeSpend += flag ? (pilerState == PilerState.Pile ? inputBeltSpeed : outputBeltSpeed) * (int)(1000f * power) : 0;
        }
        ref OptimizedCargoPath inputBelt = ref optimizedCargoPaths[inputBeltIndex];
        ref OptimizedCargoPath outputBelt = ref optimizedCargoPaths[outputBeltIndex];
        bool flag2 = flag && timeSpend >= 10000;
        if (pilerState == PilerState.Pile)
        {
            if (flag2)
            {
                if (cacheItemId2 == 0)
                {
                    if (inputBelt.TryPickCargoAtEnd(out OptimizedCargo cargo))
                    {
                        if (cacheItemId1 != 0)
                        {
                            cacheItemId2 = cacheItemId1;
                            cacheCargoStack2 = cacheCargoStack1;
                            cacheCargoInc2 = cacheCargoInc1;
                        }
                        cacheItemId1 = cargo.Item;
                        cacheCargoStack1 = cargo.Stack;
                        cacheCargoInc1 = cargo.Inc;
                        cacheCdTick = (byte)(PilerComponent.cacheCdTickArray[slowlyBeltSpeed - 1] + 1);
                        timeSpend -= 10000;
                    }
                }
            }
            else if (cacheItemId1 == 0)
            {
                if (inputBelt.TryPickCargoAtEnd(out OptimizedCargo cargo))
                {
                    cacheItemId1 = cargo.Item;
                    cacheCargoStack1 = cargo.Stack;
                    cacheCargoInc1 = cargo.Inc;
                }
            }
            int num3 = outputBelt.TestBlankAtHead();
            if (num3 >= 0)
            {
                if (cacheItemId1 != 0 && cacheItemId2 != 0)
                {
                    if (cacheItemId1 == cacheItemId2)
                    {
                        if (cacheCargoStack1 + cacheCargoStack2 > 4)
                        {
                            int num4 = cacheCargoStack1 + cacheCargoStack2;
                            int num5 = cacheCargoInc1 + cacheCargoInc2;
                            byte b = (byte)(num5 / (float)num4 * 4f + 0.5f);
                            OptimizedCargo optimizedCargo = new OptimizedCargo(cacheItemId1, 4, b);
                            outputBelt.InsertCargoAtHeadDirect(optimizedCargo, num3);
                            cacheCargoStack1 = (byte)(num4 - 4);
                            cacheCargoInc1 = (byte)(num5 - b);
                            cacheItemId2 = 0;
                            cacheCargoStack2 = 0;
                            cacheCargoInc2 = 0;
                        }
                        else
                        {
                            OptimizedCargo optimizedCargo = new OptimizedCargo(cacheItemId1, (byte)(cacheCargoStack1 + cacheCargoStack2), (byte)(cacheCargoInc1 + cacheCargoInc2));
                            outputBelt.InsertCargoAtHeadDirect(optimizedCargo, num3);
                            cacheItemId1 = 0;
                            cacheCargoStack1 = 0;
                            cacheCargoInc1 = 0;
                            cacheItemId2 = 0;
                            cacheCargoStack2 = 0;
                            cacheCargoInc2 = 0;
                        }
                    }
                    else
                    {
                        OptimizedCargo optimizedCargo = new OptimizedCargo(cacheItemId2, cacheCargoStack2, cacheCargoInc2);
                        outputBelt.InsertCargoAtHeadDirect(optimizedCargo, num3);
                        cacheItemId2 = 0;
                        cacheCargoStack2 = 0;
                        cacheCargoInc2 = 0;
                    }
                }
                else if (cacheCdTick == 0 && cacheItemId1 != 0)
                {
                    OptimizedCargo optimizedCargo = new OptimizedCargo(cacheItemId1, cacheCargoStack1, cacheCargoInc1);
                    outputBelt.InsertCargoAtHeadDirect(optimizedCargo, num3);
                    cacheItemId1 = 0;
                    cacheCargoStack1 = 0;
                    cacheCargoInc1 = 0;
                }
            }
        }
        else
        {
            if (cacheItemId1 == 0 && cacheItemId2 == 0)
            {
                bool hasCargo = inputBelt.TryPickCargoAtEnd(out OptimizedCargo cargo);
                if (flag2)
                {
                    if (hasCargo)
                    {
                        byte b2 = (byte)(cargo.Stack / 2f + 0.5f);
                        byte b3 = (byte)(cargo.Inc / (float)(int)cargo.Stack * b2 + 0.5f);
                        cacheItemId2 = cargo.Item;
                        cacheCargoStack2 = b2;
                        cacheCargoInc2 = b3;
                        if (cargo.Stack > b2)
                        {
                            cacheItemId1 = cargo.Item;
                            cacheCargoStack1 = (byte)(cargo.Stack - b2);
                            cacheCargoInc1 = (byte)(cargo.Inc - b3);
                            cacheCdTick = (byte)(PilerComponent.cacheCdTickArray[slowlyBeltSpeed - 1] * 2 + 1);
                            timeSpend -= 10000;
                        }
                    }
                }
                else if (hasCargo)
                {
                    cacheItemId2 = cargo.Item;
                    cacheCargoStack2 = cargo.Stack;
                    cacheCargoInc2 = cargo.Inc;
                }
            }
            int num7 = outputBelt.TestBlankAtHead();
            if (cacheItemId2 != 0 && num7 >= 0)
            {
                OptimizedCargo optimizedCargo = new OptimizedCargo(cacheItemId2, cacheCargoStack2, cacheCargoInc2);
                outputBelt.InsertCargoAtHeadDirect(optimizedCargo, num7);
                cacheItemId2 = cacheItemId1;
                cacheCargoStack2 = cacheCargoStack1;
                cacheCargoInc2 = cacheCargoInc1;
                cacheItemId1 = 0;
                cacheCargoStack1 = 0;
                cacheCargoInc1 = 0;
            }
        }
        if (cacheCdTick < 1)
        {
            return 0u;
        }
        return 1u;
    }

    public readonly void Save(ref PilerComponent piler, int timeSpend)
    {
        piler.cacheCargoStack1 = cacheCargoStack1;
        piler.cacheCargoInc1 = cacheCargoInc1;
        piler.cacheCargoStack2 = cacheCargoStack2;
        piler.cacheCargoInc2 = cacheCargoInc2;
        piler.cacheCdTick = cacheCdTick;
        piler.cacheItemId1 = cacheItemId1;
        piler.cacheItemId2 = cacheItemId2;
        piler.pilerState = pilerState;
        piler.timeSpend = timeSpend;
        piler.slowlyBeltSpeed = slowlyBeltSpeed;
    }
}
