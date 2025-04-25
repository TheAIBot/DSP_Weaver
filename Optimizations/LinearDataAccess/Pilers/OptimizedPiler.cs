using System.Runtime.InteropServices;
using Weaver.Optimizations.LinearDataAccess.Belts;

namespace Weaver.Optimizations.LinearDataAccess.Pilers;

[StructLayout(LayoutKind.Auto)]
internal struct OptimizedPiler
{
    private readonly OptimizedCargoPath inputBelt;
    private readonly OptimizedCargoPath outputBelt;
    private readonly int inputBeltSpeed;
    private readonly int outputBeltSpeed;
    private byte cacheCargoStack1;
    private byte cacheCargoInc1;
    private byte cacheCargoStack2;
    private byte cacheCargoInc2;
    private byte cacheCdTick;
    private short cacheItemId1;
    private short cacheItemId2;
    private PilerState pilerState;
    private int slowlyBeltSpeed;

    public OptimizedPiler(OptimizedCargoPath inputBelt,
                          OptimizedCargoPath outputBelt,
                          int inputBeltSpeed,
                          int outputBeltSpeed,
                          ref readonly PilerComponent piler)
    {
        this.inputBelt = inputBelt;
        this.outputBelt = outputBelt;
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

    public uint InternalUpdate(float power, ref int timeSpend)
    {
        if (cacheCdTick > 0)
        {
            cacheCdTick--;
        }
        bool flag = power > 0.1f;
        slowlyBeltSpeed = ((inputBeltSpeed > outputBeltSpeed) ? outputBeltSpeed : inputBeltSpeed);
        slowlyBeltSpeed = ((slowlyBeltSpeed > 2) ? 3 : slowlyBeltSpeed);
        if (timeSpend < 10000)
        {
            timeSpend += (flag ? (((pilerState == PilerState.Pile) ? inputBeltSpeed : outputBeltSpeed) * (int)(1000f * power)) : 0);
        }
        bool flag2 = flag && timeSpend >= 10000;
        if (pilerState == PilerState.Pile)
        {
            if (flag2)
            {
                if (cacheItemId2 == 0)
                {
                    int num = inputBelt.TryPickCargoAtEnd();
                    if (num >= 0)
                    {
                        OptimizedCargo cargo = inputBelt.cargoContainer.cargoPool[num];
                        if (cacheItemId1 != 0)
                        {
                            cacheItemId2 = cacheItemId1;
                            cacheCargoStack2 = cacheCargoStack1;
                            cacheCargoInc2 = cacheCargoInc1;
                        }
                        cacheItemId1 = cargo.item;
                        cacheCargoStack1 = cargo.stack;
                        cacheCargoInc1 = cargo.inc;
                        cacheCdTick = (byte)(PilerComponent.cacheCdTickArray[slowlyBeltSpeed - 1] + 1);
                        timeSpend -= 10000;
                        inputBelt.cargoContainer.RemoveCargo(num);
                    }
                }
            }
            else if (cacheItemId1 == 0)
            {
                int num2 = inputBelt.TryPickCargoAtEnd();
                if (num2 >= 0)
                {
                    OptimizedCargo cargo2 = inputBelt.cargoContainer.cargoPool[num2];
                    cacheItemId1 = cargo2.item;
                    cacheCargoStack1 = cargo2.stack;
                    cacheCargoInc1 = cargo2.inc;
                    inputBelt.cargoContainer.RemoveCargo(num2);
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
                            byte b = (byte)((float)num5 / (float)num4 * 4f + 0.5f);
                            int cargoId = outputBelt.cargoContainer.AddCargo(cacheItemId1, 4, b);
                            outputBelt.InsertCargoAtHeadDirect(cargoId, num3);
                            cacheCargoStack1 = (byte)(num4 - 4);
                            cacheCargoInc1 = (byte)(num5 - b);
                            cacheItemId2 = 0;
                            cacheCargoStack2 = 0;
                            cacheCargoInc2 = 0;
                        }
                        else
                        {
                            int cargoId2 = outputBelt.cargoContainer.AddCargo(cacheItemId1, (byte)(cacheCargoStack1 + cacheCargoStack2), (byte)(cacheCargoInc1 + cacheCargoInc2));
                            outputBelt.InsertCargoAtHeadDirect(cargoId2, num3);
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
                        int cargoId3 = outputBelt.cargoContainer.AddCargo(cacheItemId2, cacheCargoStack2, cacheCargoInc2);
                        outputBelt.InsertCargoAtHeadDirect(cargoId3, num3);
                        cacheItemId2 = 0;
                        cacheCargoStack2 = 0;
                        cacheCargoInc2 = 0;
                    }
                }
                else if (cacheCdTick == 0 && cacheItemId1 != 0)
                {
                    int cargoId4 = outputBelt.cargoContainer.AddCargo(cacheItemId1, cacheCargoStack1, cacheCargoInc1);
                    outputBelt.InsertCargoAtHeadDirect(cargoId4, num3);
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
                int num6 = inputBelt.TryPickCargoAtEnd();
                if (flag2)
                {
                    if (num6 >= 0)
                    {
                        OptimizedCargo cargo3 = inputBelt.cargoContainer.cargoPool[num6];
                        byte b2 = (byte)((float)(int)cargo3.stack / 2f + 0.5f);
                        byte b3 = (byte)((float)(int)cargo3.inc / (float)(int)cargo3.stack * (float)(int)b2 + 0.5f);
                        cacheItemId2 = cargo3.item;
                        cacheCargoStack2 = b2;
                        cacheCargoInc2 = b3;
                        if (cargo3.stack > b2)
                        {
                            cacheItemId1 = cargo3.item;
                            cacheCargoStack1 = (byte)(cargo3.stack - b2);
                            cacheCargoInc1 = (byte)(cargo3.inc - b3);
                            cacheCdTick = (byte)(PilerComponent.cacheCdTickArray[slowlyBeltSpeed - 1] * 2 + 1);
                            timeSpend -= 10000;
                        }
                        inputBelt.cargoContainer.RemoveCargo(num6);
                    }
                }
                else if (num6 >= 0)
                {
                    OptimizedCargo cargo4 = inputBelt.cargoContainer.cargoPool[num6];
                    cacheItemId2 = cargo4.item;
                    cacheCargoStack2 = cargo4.stack;
                    cacheCargoInc2 = cargo4.inc;
                    inputBelt.cargoContainer.RemoveCargo(num6);
                }
            }
            int num7 = outputBelt.TestBlankAtHead();
            if (cacheItemId2 != 0 && num7 >= 0)
            {
                int cargoId5 = outputBelt.cargoContainer.AddCargo(cacheItemId2, cacheCargoStack2, cacheCargoInc2);
                outputBelt.InsertCargoAtHeadDirect(cargoId5, num7);
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

    public void Save(ref PilerComponent piler, int timeSpend)
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
