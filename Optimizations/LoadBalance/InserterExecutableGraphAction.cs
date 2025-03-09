using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.LoadBalance;

internal struct InserterExecutableGraphAction : IExecutableGraphAction
{
    public void Execute(long time, PlanetFactory factory, int[] indexes)
    {
        bool isActive = factory.planet == GameMain.localPlanet;

        InserterComponent[] inserterPool = factory.factorySystem.inserterPool;
        CargoTraffic traffic = factory.factorySystem.traffic;
        PowerSystem powerSystem = factory.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        AnimData[] entityAnimPool = factory.entityAnimPool;
        int[][] entityNeeds = factory.entityNeeds;
        PowerConsumerComponent[] consumerPool = powerSystem.consumerPool;
        EntityData[] entityPool = factory.entityPool;
        BeltComponent[] beltPool = factory.cargoTraffic.beltPool;
        byte b = (byte)GameMain.history.inserterStackCountObsolete;
        byte b2 = (byte)GameMain.history.inserterStackInput;
        byte stackOutput = (byte)GameMain.history.inserterStackOutput;
        bool inserterBidirectional = GameMain.history.inserterBidirectional;
        int delay = ((b > 1) ? 110000 : 0);
        int delay2 = ((b2 > 1) ? 40000 : 0);
        bool flag = time % 60 == 0;
        if (isActive)
        {
            foreach (int poolIndex in indexes)
            {
                ref InserterComponent reference = ref inserterPool[poolIndex];
                if (flag)
                {
                    reference.InternalOffsetCorrection(entityPool, traffic, beltPool);
                    if (reference.grade == 3)
                    {
                        reference.delay = delay;
                        reference.stackInput = b;
                        reference.stackOutput = 1;
                        reference.bidirectional = false;
                    }
                    else if (reference.grade == 4)
                    {
                        reference.delay = delay2;
                        reference.stackInput = b2;
                        reference.stackOutput = stackOutput;
                        reference.bidirectional = inserterBidirectional;
                    }
                    else
                    {
                        reference.delay = 0;
                        reference.stackInput = 1;
                        reference.stackOutput = 1;
                        reference.bidirectional = false;
                    }
                }
                float power = networkServes[consumerPool[reference.pcId].networkId];
                if (reference.bidirectional)
                {
                    reference.InternalUpdate_Bidirectional(factory, entityNeeds, entityAnimPool, power, isActive);
                }
                else
                {
                    reference.InternalUpdate(factory, entityNeeds, entityAnimPool, power);
                }
            }
            return;
        }
        foreach (int poolIndex in indexes)
        {
            ref InserterComponent reference2 = ref inserterPool[poolIndex];
            if (flag)
            {
                if (reference2.grade == 3)
                {
                    reference2.delay = delay;
                    reference2.stackInput = b;
                    reference2.stackOutput = 1;
                    reference2.bidirectional = false;
                }
                else if (reference2.grade == 4)
                {
                    reference2.delay = delay2;
                    reference2.stackInput = b2;
                    reference2.stackOutput = stackOutput;
                    reference2.bidirectional = inserterBidirectional;
                }
                else
                {
                    reference2.delay = 0;
                    reference2.stackInput = 1;
                    reference2.stackOutput = 1;
                    reference2.bidirectional = false;
                }
            }
            float power2 = networkServes[consumerPool[reference2.pcId].networkId];
            if (reference2.bidirectional)
            {
                reference2.InternalUpdate_Bidirectional(factory, entityNeeds, entityAnimPool, power2, isActive);
            }
            else
            {
                reference2.InternalUpdateNoAnim(factory, entityNeeds, power2);
            }
        }
    }
}
