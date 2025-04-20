using System.Linq;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.LinearDataAccess.Miners;

internal sealed class SplitterExecutor
{
    private int[] _splitterIndexes;

    public void GameTick(PlanetFactory planet, long time)
    {
        CargoTraffic cargoTraffic = planet.cargoTraffic;
        for (int splitterIndexIndex = 0; splitterIndexIndex < _splitterIndexes.Length; splitterIndexIndex++)
        {
            int splitterIndex = _splitterIndexes[splitterIndexIndex];
            UpdateSplitter(cargoTraffic, ref cargoTraffic.splitterPool[splitterIndex], time);
        }
    }

    public void Initialize(Graph subFactoryGraph)
    {
        _splitterIndexes = subFactoryGraph.GetAllNodes()
                                          .Where(x => x.EntityTypeIndex.EntityType == EntityType.Splitter)
                                          .Select(x => x.EntityTypeIndex.Index)
                                          .OrderBy(x => x)
                                          .ToArray();
    }

    private void UpdateSplitter(CargoTraffic cargoTraffic, ref SplitterComponent sp, long time)
    {
        sp.CheckPriorityPreset();
        if (sp.topId == 0)
        {
            if (sp.input0 == 0 || sp.output0 == 0)
            {
                return;
            }
        }
        else if (sp.input0 == 0 && sp.output0 == 0)
        {
            return;
        }
        CargoPath us_tmp_inputPath = null;
        CargoPath us_tmp_inputPath0 = null;
        CargoPath us_tmp_inputPath1 = null;
        CargoPath us_tmp_inputPath2 = null;
        CargoPath us_tmp_inputPath3 = null;
        int us_tmp_inputCargo = -1;
        int us_tmp_inputCargo0 = -1;
        int us_tmp_inputCargo1 = -1;
        int us_tmp_inputCargo2 = -1;
        int us_tmp_inputCargo3 = -1;
        int us_tmp_inputIndex0 = -1;
        int us_tmp_inputIndex1 = -1;
        int us_tmp_inputIndex2 = -1;
        int us_tmp_inputIndex3 = -1;
        CargoPath us_tmp_outputPath;
        CargoPath us_tmp_outputPath0;
        int us_tmp_outputIdx;

        if (sp.input0 != 0)
        {
            us_tmp_inputPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[sp.input0].segPathId);
            us_tmp_inputCargo = us_tmp_inputPath.GetCargoIdAtRear();
            if (us_tmp_inputCargo != -1)
            {
                us_tmp_inputCargo0 = us_tmp_inputCargo;
                us_tmp_inputPath0 = us_tmp_inputPath;
                us_tmp_inputIndex0 = 0;
            }
            if (sp.input1 != 0)
            {
                us_tmp_inputPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[sp.input1].segPathId);
                us_tmp_inputCargo = us_tmp_inputPath.GetCargoIdAtRear();
                if (us_tmp_inputCargo != -1)
                {
                    if (us_tmp_inputPath0 == null)
                    {
                        us_tmp_inputCargo0 = us_tmp_inputCargo;
                        us_tmp_inputPath0 = us_tmp_inputPath;
                        us_tmp_inputIndex0 = 1;
                    }
                    else
                    {
                        us_tmp_inputCargo1 = us_tmp_inputCargo;
                        us_tmp_inputPath1 = us_tmp_inputPath;
                        us_tmp_inputIndex1 = 1;
                    }
                }
                if (sp.input2 != 0)
                {
                    us_tmp_inputPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[sp.input2].segPathId);
                    us_tmp_inputCargo = us_tmp_inputPath.GetCargoIdAtRear();
                    if (us_tmp_inputCargo != -1)
                    {
                        if (us_tmp_inputPath0 == null)
                        {
                            us_tmp_inputCargo0 = us_tmp_inputCargo;
                            us_tmp_inputPath0 = us_tmp_inputPath;
                            us_tmp_inputIndex0 = 2;
                        }
                        else if (us_tmp_inputPath1 == null)
                        {
                            us_tmp_inputCargo1 = us_tmp_inputCargo;
                            us_tmp_inputPath1 = us_tmp_inputPath;
                            us_tmp_inputIndex1 = 2;
                        }
                        else
                        {
                            us_tmp_inputCargo2 = us_tmp_inputCargo;
                            us_tmp_inputPath2 = us_tmp_inputPath;
                            us_tmp_inputIndex2 = 2;
                        }
                    }
                    if (sp.input3 != 0)
                    {
                        us_tmp_inputPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[sp.input3].segPathId);
                        us_tmp_inputCargo = us_tmp_inputPath.GetCargoIdAtRear();
                        if (us_tmp_inputCargo != -1)
                        {
                            if (us_tmp_inputPath0 == null)
                            {
                                us_tmp_inputCargo0 = us_tmp_inputCargo;
                                us_tmp_inputPath0 = us_tmp_inputPath;
                                us_tmp_inputIndex0 = 3;
                            }
                            else if (us_tmp_inputPath1 == null)
                            {
                                us_tmp_inputCargo1 = us_tmp_inputCargo;
                                us_tmp_inputPath1 = us_tmp_inputPath;
                                us_tmp_inputIndex1 = 3;
                            }
                            else if (us_tmp_inputPath2 == null)
                            {
                                us_tmp_inputCargo2 = us_tmp_inputCargo;
                                us_tmp_inputPath2 = us_tmp_inputPath;
                                us_tmp_inputIndex2 = 3;
                            }
                            else
                            {
                                us_tmp_inputCargo3 = us_tmp_inputCargo;
                                us_tmp_inputPath3 = us_tmp_inputPath;
                                us_tmp_inputIndex3 = 3;
                            }
                        }
                    }
                }
            }
        }
        while (us_tmp_inputPath0 != null)
        {
            bool flag = true;
            if (sp.outFilter != 0)
            {
                flag = cargoTraffic.container.cargoPool[us_tmp_inputCargo0].item == sp.outFilter;
            }
            us_tmp_outputPath = null;
            us_tmp_outputPath0 = null;
            us_tmp_outputIdx = 0;
            int num = -1;
            if (!flag && sp.outFilter != 0)
            {
                goto IL_03e5;
            }
            if (sp.output0 != 0)
            {
                us_tmp_outputPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[sp.output0].segPathId);
                num = us_tmp_outputPath.TestBlankAtHead();
                if (us_tmp_outputPath.pathLength <= 10 || num < 0)
                {
                    goto IL_03e5;
                }
                us_tmp_outputPath0 = us_tmp_outputPath;
                us_tmp_outputIdx = 0;
            }
            goto IL_0514;
        IL_03e5:
            if ((!flag || sp.outFilter == 0) && sp.output1 != 0)
            {
                us_tmp_outputPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[sp.output1].segPathId);
                num = us_tmp_outputPath.TestBlankAtHead();
                if (us_tmp_outputPath.pathLength > 10 && num >= 0)
                {
                    us_tmp_outputPath0 = us_tmp_outputPath;
                    us_tmp_outputIdx = 1;
                }
                else if (sp.output2 != 0)
                {
                    us_tmp_outputPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[sp.output2].segPathId);
                    num = us_tmp_outputPath.TestBlankAtHead();
                    if (us_tmp_outputPath.pathLength > 10 && num >= 0)
                    {
                        us_tmp_outputPath0 = us_tmp_outputPath;
                        us_tmp_outputIdx = 2;
                    }
                    else if (sp.output3 != 0)
                    {
                        us_tmp_outputPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[sp.output3].segPathId);
                        num = us_tmp_outputPath.TestBlankAtHead();
                        if (us_tmp_outputPath.pathLength > 10 && num >= 0)
                        {
                            us_tmp_outputPath0 = us_tmp_outputPath;
                            us_tmp_outputIdx = 3;
                        }
                    }
                }
            }
            goto IL_0514;
        IL_0514:
            if (us_tmp_outputPath0 != null)
            {
                int num2 = us_tmp_inputPath0.TryPickCargoAtEnd();
                Assert.True(num2 >= 0);
                us_tmp_outputPath0.InsertCargoAtHeadDirect(num2, num);
                sp.InputAlternate(us_tmp_inputIndex0);
                sp.OutputAlternate(us_tmp_outputIdx);
            }
            else if (sp.topId != 0 && (flag || sp.outFilter == 0) && cargoTraffic.factory.InsertCargoIntoStorage(sp.topId, ref cargoTraffic.container.cargoPool[us_tmp_inputCargo0]))
            {
                int num3 = us_tmp_inputPath0.TryPickCargoAtEnd();
                Assert.True(num3 >= 0);
                cargoTraffic.container.RemoveCargo(num3);
                sp.InputAlternate(us_tmp_inputIndex0);
            }
            us_tmp_inputPath0 = us_tmp_inputPath1;
            us_tmp_inputCargo0 = us_tmp_inputCargo1;
            us_tmp_inputIndex0 = us_tmp_inputIndex1;
            us_tmp_inputPath1 = us_tmp_inputPath2;
            us_tmp_inputCargo1 = us_tmp_inputCargo2;
            us_tmp_inputIndex1 = us_tmp_inputIndex2;
            us_tmp_inputPath2 = us_tmp_inputPath3;
            us_tmp_inputCargo2 = us_tmp_inputCargo3;
            us_tmp_inputIndex2 = us_tmp_inputIndex3;
            us_tmp_inputPath3 = null;
            us_tmp_inputCargo3 = -1;
            us_tmp_inputIndex3 = -1;
        }
        if (sp.topId == 0)
        {
            return;
        }
        if (sp.outFilter == 0)
        {
            int num4 = 4;
            while (num4-- > 0)
            {
                us_tmp_outputPath = null;
                us_tmp_outputPath0 = null;
                us_tmp_outputIdx = 0;
                int num5 = -1;
                if (sp.output0 != 0)
                {
                    us_tmp_outputPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[sp.output0].segPathId);
                    num5 = us_tmp_outputPath.TestBlankAtHead();
                    if (us_tmp_outputPath.pathLength > 10 && num5 >= 0)
                    {
                        us_tmp_outputPath0 = us_tmp_outputPath;
                        us_tmp_outputIdx = 0;
                    }
                    else if (sp.output1 != 0)
                    {
                        us_tmp_outputPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[sp.output1].segPathId);
                        num5 = us_tmp_outputPath.TestBlankAtHead();
                        if (us_tmp_outputPath.pathLength > 10 && num5 >= 0)
                        {
                            us_tmp_outputPath0 = us_tmp_outputPath;
                            us_tmp_outputIdx = 1;
                        }
                        else if (sp.output2 != 0)
                        {
                            us_tmp_outputPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[sp.output2].segPathId);
                            num5 = us_tmp_outputPath.TestBlankAtHead();
                            if (us_tmp_outputPath.pathLength > 10 && num5 >= 0)
                            {
                                us_tmp_outputPath0 = us_tmp_outputPath;
                                us_tmp_outputIdx = 2;
                            }
                            else if (sp.output3 != 0)
                            {
                                us_tmp_outputPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[sp.output3].segPathId);
                                num5 = us_tmp_outputPath.TestBlankAtHead();
                                if (us_tmp_outputPath.pathLength > 10 && num5 >= 0)
                                {
                                    us_tmp_outputPath0 = us_tmp_outputPath;
                                    us_tmp_outputIdx = 3;
                                }
                            }
                        }
                    }
                }
                if (us_tmp_outputPath0 != null)
                {
                    int filter = ((us_tmp_outputIdx == 0) ? sp.outFilter : (-sp.outFilter));
                    int inc;
                    int num6 = cargoTraffic.factory.PickFromStorageFiltered(sp.topId, ref filter, 1, out inc);
                    if (filter > 0 && num6 > 0)
                    {
                        int cargoId = cargoTraffic.container.AddCargo((short)filter, (byte)num6, (byte)inc);
                        us_tmp_outputPath0.InsertCargoAtHeadDirect(cargoId, num5);
                        sp.OutputAlternate(us_tmp_outputIdx);
                        continue;
                    }
                    break;
                }
                break;
            }
            return;
        }
        us_tmp_outputPath = null;
        us_tmp_outputPath0 = null;
        us_tmp_outputIdx = 0;
        int num7 = -1;
        if (sp.output0 != 0)
        {
            us_tmp_outputPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[sp.output0].segPathId);
            num7 = us_tmp_outputPath.TestBlankAtHead();
            if (us_tmp_outputPath.pathLength > 10 && num7 >= 0)
            {
                us_tmp_outputPath0 = us_tmp_outputPath;
                us_tmp_outputIdx = 0;
            }
        }
        if (us_tmp_outputPath0 != null)
        {
            int filter2 = sp.outFilter;
            int inc2;
            int num8 = cargoTraffic.factory.PickFromStorageFiltered(sp.topId, ref filter2, 1, out inc2);
            if (filter2 > 0 && num8 > 0)
            {
                int cargoId2 = cargoTraffic.container.AddCargo((short)filter2, (byte)num8, (byte)inc2);
                us_tmp_outputPath0.InsertCargoAtHeadDirect(cargoId2, num7);
                sp.OutputAlternate(us_tmp_outputIdx);
            }
        }
        int num9 = 3;
        while (num9-- > 0)
        {
            us_tmp_outputPath = null;
            us_tmp_outputPath0 = null;
            us_tmp_outputIdx = 0;
            int num10 = -1;
            if (sp.output1 != 0)
            {
                us_tmp_outputPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[sp.output1].segPathId);
                num10 = us_tmp_outputPath.TestBlankAtHead();
                if (us_tmp_outputPath.pathLength > 10 && num10 >= 0)
                {
                    us_tmp_outputPath0 = us_tmp_outputPath;
                    us_tmp_outputIdx = 1;
                }
                else if (sp.output2 != 0)
                {
                    us_tmp_outputPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[sp.output2].segPathId);
                    num10 = us_tmp_outputPath.TestBlankAtHead();
                    if (us_tmp_outputPath.pathLength > 10 && num10 >= 0)
                    {
                        us_tmp_outputPath0 = us_tmp_outputPath;
                        us_tmp_outputIdx = 2;
                    }
                    else if (sp.output3 != 0)
                    {
                        us_tmp_outputPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[sp.output3].segPathId);
                        num10 = us_tmp_outputPath.TestBlankAtHead();
                        if (us_tmp_outputPath.pathLength > 10 && num10 >= 0)
                        {
                            us_tmp_outputPath0 = us_tmp_outputPath;
                            us_tmp_outputIdx = 3;
                        }
                    }
                }
            }
            if (us_tmp_outputPath0 != null)
            {
                int filter3 = -sp.outFilter;
                int inc3;
                int num11 = cargoTraffic.factory.PickFromStorageFiltered(sp.topId, ref filter3, 1, out inc3);
                if (filter3 > 0 && num11 > 0)
                {
                    int cargoId3 = cargoTraffic.container.AddCargo((short)filter3, (byte)num11, (byte)inc3);
                    us_tmp_outputPath0.InsertCargoAtHeadDirect(cargoId3, num10);
                    sp.OutputAlternate(us_tmp_outputIdx);
                    continue;
                }
                break;
            }
            break;
        }
    }
}

internal sealed class BeltExecutor
{
    private int[] _beltIndexes;

    public void GameTick(PlanetFactory planet)
    {
        CargoTraffic cargoTraffic = planet.cargoTraffic;
        for (int beltIndexIndex = 0; beltIndexIndex < _beltIndexes.Length; beltIndexIndex++)
        {
            int beltIndex = _beltIndexes[beltIndexIndex];
            cargoTraffic.pathPool[beltIndex].Update();
        }
    }

    public void Initialize(Graph subFactoryGraph)
    {
        _beltIndexes = subFactoryGraph.GetAllNodes()
                                      .Where(x => x.EntityTypeIndex.EntityType == EntityType.Belt)
                                      .Select(x => x.EntityTypeIndex.Index)
                                      .OrderBy(x => x)
                                      .ToArray();
    }
}

internal sealed class TankExecutor
{
    private int[] _tankIndexes;

    public void GameTick(PlanetFactory planet)
    {
        FactoryStorage storage = planet.factoryStorage;
        for (int tankIndexIndex = 0; tankIndexIndex < _tankIndexes.Length; tankIndexIndex++)
        {
            int tankIndex = _tankIndexes[tankIndexIndex];
            storage.tankPool[tankIndex].GameTick(planet);
            storage.tankPool[tankIndex].TickOutput(planet);
            if (storage.tankPool[tankIndex].fluidCount == 0)
            {
                storage.tankPool[tankIndex].fluidId = 0;
            }
        }
    }

    public void Initialize(Graph subFactoryGraph)
    {
        _tankIndexes = subFactoryGraph.GetAllNodes()
                                      .Where(x => x.EntityTypeIndex.EntityType == EntityType.Tank)
                                      .Select(x => x.EntityTypeIndex.Index)
                                      .OrderBy(x => x)
                                      .ToArray();
    }
}

internal sealed class DispenserExecutor
{
    private int[] _dispenserIndexes;

    public void SandboxMode(PlanetFactory planet)
    {
        if (!GameMain.sandboxToolsEnabled)
        {
            return;
        }

        PlanetTransport transport = planet.transport;
        for (int dispenserIndexIndex = 0; dispenserIndexIndex < _dispenserIndexes.Length; dispenserIndexIndex++)
        {
            int dispenserIndex = _dispenserIndexes[dispenserIndexIndex];
            transport.dispenserPool[dispenserIndex].UpdateKeepMode();
        }
    }

    public void Initialize(Graph subFactoryGraph)
    {
        _dispenserIndexes = subFactoryGraph.GetAllNodes()
                                           .Where(x => x.EntityTypeIndex.EntityType == EntityType.Dispenser)
                                           .Select(x => x.EntityTypeIndex.Index)
                                           .OrderBy(x => x)
                                           .ToArray();
    }
}

internal sealed class StationExecutor
{
    private int[] _stationIndexes;

    public void InputFromBelt(PlanetFactory planet, long time)
    {
        PlanetTransport transport = planet.transport;
        CargoTraffic cargoTraffic = planet.cargoTraffic;
        SignData[] entitySignPool = planet.entitySignPool;
        bool active = (time + planet.index) % 30 == 0L;

        for (int stationIndexIndex = 0; stationIndexIndex < _stationIndexes.Length; stationIndexIndex++)
        {
            int stationIndex = _stationIndexes[stationIndexIndex];
            transport.stationPool[stationIndex].UpdateInputSlots(cargoTraffic, entitySignPool, active);
        }
    }

    public void OutputToBelt(PlanetFactory planet, long time)
    {
        PlanetTransport transport = planet.transport;
        CargoTraffic cargoTraffic = planet.cargoTraffic;
        SignData[] entitySignPool = planet.entitySignPool;
        int stationPilerLevel = GameMain.history.stationPilerLevel;
        bool active = (time + planet.index) % 30 == 0L;

        for (int stationIndexIndex = 0; stationIndexIndex < _stationIndexes.Length; stationIndexIndex++)
        {
            int stationIndex = _stationIndexes[stationIndexIndex];
            transport.stationPool[stationIndex].UpdateOutputSlots(cargoTraffic, entitySignPool, stationPilerLevel, active);
        }
    }

    public void SandboxMode(PlanetFactory planet)
    {
        if (!GameMain.sandboxToolsEnabled)
        {
            return;
        }

        PlanetTransport transport = planet.transport;
        for (int stationIndexIndex = 0; stationIndexIndex < _stationIndexes.Length; stationIndexIndex++)
        {
            int stationIndex = _stationIndexes[stationIndexIndex];
            transport.stationPool[stationIndex].UpdateKeepMode();
        }
    }

    public void Initialize(Graph subFactoryGraph)
    {
        _stationIndexes = subFactoryGraph.GetAllNodes()
                                         .Where(x => x.EntityTypeIndex.EntityType == EntityType.Station)
                                         .Select(x => x.EntityTypeIndex.Index)
                                         .OrderBy(x => x)
                                         .ToArray();
    }
}

internal sealed class PilerExecutor
{
    private int[] _pilerIndexes;

    public void GameTick(PlanetFactory planet)
    {
        CargoTraffic cargoTraffic = planet.cargoTraffic;
        AnimData[] entityAnimPool = planet.entityAnimPool;

        for (int pilerIndexIndex = 0; pilerIndexIndex < _pilerIndexes.Length; pilerIndexIndex++)
        {
            int pilerIndex = _pilerIndexes[pilerIndexIndex];
            cargoTraffic.pilerPool[pilerIndex].InternalUpdate(cargoTraffic, entityAnimPool, out float _);
        }
    }

    public void UpdatePower(PlanetFactory planet)
    {
        CargoTraffic cargoTraffic = planet.cargoTraffic;
        PowerConsumerComponent[] consumerPool = planet.powerSystem.consumerPool;
        for (int pilerIndexIndex = 0; pilerIndexIndex < _pilerIndexes.Length; pilerIndexIndex++)
        {
            int pilerIndex = _pilerIndexes[pilerIndexIndex];
            cargoTraffic.pilerPool[pilerIndex].SetPCState(consumerPool);
        }
    }

    public void Initialize(PlanetFactory planet, Graph subFactoryGraph)
    {
        _pilerIndexes = subFactoryGraph.GetAllNodes()
                                       .Where(x => x.EntityTypeIndex.EntityType == EntityType.Piler)
                                       .Select(x => x.EntityTypeIndex.Index)
                                       .OrderBy(x => x)
                                       .ToArray();
    }
}

internal sealed class MonitorExecutor
{
    private int[] _monitorIndexes;

    public void GameTick(PlanetFactory planet)
    {
        AnimData[] entityAnimPool = planet.entityAnimPool;
        SpeakerComponent[] speakerPool = planet.digitalSystem.speakerPool;
        EntityData[] entityPool = planet.entityPool;
        bool sandboxToolsEnabled = GameMain.sandboxToolsEnabled;
        CargoTraffic cargoTraffic = planet.cargoTraffic;

        for (int monitorIndexIndex = 0; monitorIndexIndex < _monitorIndexes.Length; monitorIndexIndex++)
        {
            int monitorIndex = _monitorIndexes[monitorIndexIndex];
            cargoTraffic.monitorPool[monitorIndex].InternalUpdate(cargoTraffic, sandboxToolsEnabled, entityPool, speakerPool, entityAnimPool);
        }
    }

    public void UpdatePower(PlanetFactory planet)
    {
        CargoTraffic cargoTraffic = planet.cargoTraffic;
        PowerConsumerComponent[] consumerPool = planet.powerSystem.consumerPool;
        for (int monitorIndexIndex = 0; monitorIndexIndex < _monitorIndexes.Length; monitorIndexIndex++)
        {
            int monitorIndex = _monitorIndexes[monitorIndexIndex];
            cargoTraffic.monitorPool[monitorIndex].SetPCState(consumerPool);
        }
    }

    public void Initialize(PlanetFactory planet, Graph subFactoryGraph)
    {
        _monitorIndexes = subFactoryGraph.GetAllNodes()
                                         .Where(x => x.EntityTypeIndex.EntityType == EntityType.Monitor)
                                         .Select(x => x.EntityTypeIndex.Index)
                                         .OrderBy(x => x)
                                         .ToArray();
    }
}

internal sealed class SiloExecutor
{
    private int[] _siloIndexes;
    private int[] _siloNetworkIds;

    public void GameTick(PlanetFactory planet)
    {
        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[planet.index];
        int[] consumeRegister = obj.consumeRegister;
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        AnimData[] entityAnimPool = planet.entityAnimPool;
        FactorySystem factorySystem = planet.factorySystem;
        AstroData[] astroPoses = factorySystem.planet.galaxy.astrosData;

        DysonSphere dysonSphere = factorySystem.factory.dysonSphere;
        for (int siloIndexIndex = 0; siloIndexIndex < _siloIndexes.Length; siloIndexIndex++)
        {
            int siloIndex = _siloIndexes[siloIndexIndex];

            float power4 = networkServes[_siloNetworkIds[siloIndexIndex]];
            factorySystem.siloPool[siloIndex].InternalUpdate(power4, dysonSphere, entityAnimPool, consumeRegister);
        }
    }

    public void UpdatePower(PlanetFactory planet)
    {
        PowerConsumerComponent[] consumerPool = planet.powerSystem.consumerPool;

        for (int siloIndexIndex = 0; siloIndexIndex < _siloIndexes.Length; siloIndexIndex++)
        {
            int siloIndex = _siloIndexes[siloIndexIndex];
            planet.factorySystem.siloPool[siloIndex].SetPCState(consumerPool);
        }
    }

    public void Initialize(PlanetFactory planet, Graph subFactoryGraph)
    {
        _siloIndexes = subFactoryGraph.GetAllNodes()
                                      .Where(x => x.EntityTypeIndex.EntityType == EntityType.Silo)
                                      .Select(x => x.EntityTypeIndex.Index)
                                      .OrderBy(x => x)
                                      .ToArray();

        int[] siloNetworkIds = new int[_siloIndexes.Length];

        for (int siloIndexIndex = 0; siloIndexIndex < _siloIndexes.Length; siloIndexIndex++)
        {
            int siloIndex = _siloIndexes[siloIndexIndex];
            ref SiloComponent silo = ref planet.factorySystem.siloPool[siloIndex];

            siloNetworkIds[siloIndexIndex] = planet.powerSystem.consumerPool[silo.pcId].networkId;

            // set it here so we don't have to set it in the update loop
            silo.needs ??= new int[6];
            planet.entityNeeds[silo.entityId] = silo.needs;
        }

        _siloNetworkIds = siloNetworkIds;
    }
}

internal sealed class EjectorExecutor
{
    private int[] _ejectorIndexes;
    private int[] _ejectorNetworkIds;

    public void GameTick(PlanetFactory planet, long time)
    {
        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[planet.index];
        int[] consumeRegister = obj.consumeRegister;
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        AnimData[] entityAnimPool = planet.entityAnimPool;
        PowerConsumerComponent[] consumerPool = powerSystem.consumerPool;
        AstroData[] astroPoses = planet.factorySystem.planet.galaxy.astrosData;

        DysonSwarm swarm = null;
        if (planet.factorySystem.factory.dysonSphere != null)
        {
            swarm = planet.factorySystem.factory.dysonSphere.swarm;
        }

        int[] ejectorNetworkIds = _ejectorNetworkIds;
        for (int ejectorIndexIndex = 0; ejectorIndexIndex < _ejectorIndexes.Length; ejectorIndexIndex++)
        {
            int ejectorIndex = _ejectorIndexes[ejectorIndexIndex];

            float power3 = networkServes[ejectorNetworkIds[ejectorIndexIndex]];
            planet.factorySystem.ejectorPool[ejectorIndex].InternalUpdate(power3, time, swarm, astroPoses, entityAnimPool, consumeRegister);
        }
    }

    public void UpdatePower(PlanetFactory planet)
    {
        PowerConsumerComponent[] consumerPool = planet.powerSystem.consumerPool;

        for (int ejectorIndexIndex = 0; ejectorIndexIndex < _ejectorIndexes.Length; ejectorIndexIndex++)
        {
            int ejectorIndex = _ejectorIndexes[ejectorIndexIndex];
            if (planet.factorySystem.ejectorPool[ejectorIndex].id == ejectorIndex)
            {
                planet.factorySystem.ejectorPool[ejectorIndex].SetPCState(consumerPool);
            }
        }
    }

    public void Initialize(PlanetFactory planet, Graph subFactoryGraph)
    {
        _ejectorIndexes = subFactoryGraph.GetAllNodes()
                                         .Where(x => x.EntityTypeIndex.EntityType == EntityType.Ejector)
                                         .Select(x => x.EntityTypeIndex.Index)
                                         .OrderBy(x => x)
                                         .ToArray();

        int[] ejectorNetworkIds = new int[_ejectorIndexes.Length];

        for (int ejectorIndexIndex = 0; ejectorIndexIndex < _ejectorIndexes.Length; ejectorIndexIndex++)
        {
            int ejectorIndex = _ejectorIndexes[ejectorIndexIndex];
            ref EjectorComponent ejector = ref planet.factorySystem.ejectorPool[ejectorIndex];

            ejectorNetworkIds[ejectorIndexIndex] = planet.powerSystem.consumerPool[ejector.pcId].networkId;

            // set it here so we don't have to set it in the update loop.
            // Need to investigate when i need to update it.
            ejector.needs ??= new int[6];
            planet.entityNeeds[ejector.entityId] = ejector.needs;
        }

        _ejectorNetworkIds = ejectorNetworkIds;
    }
}

internal sealed class MinerExecutor
{
    private int[] _minerIndexes;
    private int[] _minerNetworkIds;

    public void GameTick(PlanetFactory planet)
    {
        GameHistoryData history = GameMain.history;
        FactoryProductionStat obj = GameMain.statistics.production.factoryStatPool[planet.index];
        int[] productRegister = obj.productRegister;
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        VeinData[] veinPool = planet.veinPool;
        FactorySystem factorySystem = planet.factorySystem;

        float num2;
        float num3 = (num2 = planet.gameData.gameDesc.resourceMultiplier);
        if (num2 < 5f / 12f)
        {
            num2 = 5f / 12f;
        }
        float num4 = history.miningCostRate;
        float miningSpeedScale = history.miningSpeedScale;
        float num5 = history.miningCostRate * 0.40111667f / num2;
        if (num3 > 99.5f)
        {
            num4 = 0f;
            num5 = 0f;
        }

        int[] minerNetworkIds = _minerNetworkIds;
        int num6 = MinerComponent.InsufficientWarningThresAmount(num3, num4);
        for (int minerIndexIndex = 0; minerIndexIndex < _minerIndexes.Length; minerIndexIndex++)
        {
            int minerIndex = _minerIndexes[minerIndexIndex];

            float num7 = networkServes[minerNetworkIds[minerIndexIndex]];
            factorySystem.minerPool[minerIndex].InternalUpdate(planet, veinPool, num7, (factorySystem.minerPool[minerIndex].type == EMinerType.Oil) ? num5 : num4, miningSpeedScale, productRegister);
        }
    }

    public void UpdatePower(PlanetFactory planet)
    {
        FactorySystem factory = planet.factorySystem;
        EntityData[] entityPool = planet.entityPool;
        StationComponent[] stationPool = planet.transport.stationPool;
        PowerConsumerComponent[] consumerPool = planet.powerSystem.consumerPool;

        for (int minerIndexIndex = 0; minerIndexIndex < _minerIndexes.Length; minerIndexIndex++)
        {
            int minerIndex = _minerIndexes[minerIndexIndex];

            int stationId = entityPool[factory.minerPool[minerIndex].entityId].stationId;
            if (stationId > 0)
            {
                StationStore[] array = stationPool[stationId].storage;
                int num = array[0].count;
                if (array[0].localOrder < -4000)
                {
                    num += array[0].localOrder + 4000;
                }
                int max = array[0].max;
                max = ((max < 3000) ? 3000 : max);
                float num2 = (float)num / (float)max;
                num2 = ((num2 > 1f) ? 1f : num2);
                float num3 = -2.45f * num2 + 2.47f;
                num3 = ((num3 > 1f) ? 1f : num3);
                factory.minerPool[minerIndex].speedDamper = num3;
            }
            else
            {
                float num4 = (float)factory.minerPool[minerIndex].productCount / 50f;
                num4 = ((num4 > 1f) ? 1f : num4);
                float num5 = -2.45f * num4 + 2.47f;
                num5 = ((num5 > 1f) ? 1f : num5);
                factory.minerPool[minerIndex].speedDamper = num5;
            }
            factory.minerPool[minerIndex].SetPCState(consumerPool);
        }
    }

    public void Initialize(PlanetFactory planet, Graph subFactoryGraph)
    {
        _minerIndexes = subFactoryGraph.GetAllNodes()
                                       .Where(x => x.EntityTypeIndex.EntityType == EntityType.Miner)
                                       .Select(x => x.EntityTypeIndex.Index)
                                       .OrderBy(x => x)
                                       .ToArray();

        int[] minerNetworkIds = new int[_minerIndexes.Length];

        for (int minerIndexIndex = 0; minerIndexIndex < _minerIndexes.Length; minerIndexIndex++)
        {
            int minerIndex = _minerIndexes[minerIndexIndex];
            ref readonly MinerComponent miner = ref planet.factorySystem.minerPool[minerIndex];

            minerNetworkIds[minerIndexIndex] = planet.powerSystem.consumerPool[miner.pcId].networkId;
        }

        _minerNetworkIds = minerNetworkIds;
    }
}
