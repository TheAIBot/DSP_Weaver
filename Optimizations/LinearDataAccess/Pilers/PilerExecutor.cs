using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Belts;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;

namespace Weaver.Optimizations.LinearDataAccess.Pilers;

internal sealed class PilerExecutor
{
    private int[] _networkIndices = null!;
    private OptimizedPiler[] _optimizedPilers = null!;
    private int[] _timeSpends = null!;
    private Dictionary<int, int> _pilerIdToOptimizedIndex = null!;
    private PrototypePowerConsumptionExecutor _prototypePowerConsumptionExecutor;

    public void GameTick(PlanetFactory planet,
                         int[] pilerPowerConsumerIndexes,
                         PowerConsumerType[] powerConsumerTypes,
                         long[] thisSubFactoryNetworkPowerConsumption)
    {
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        int[] networkIndices = _networkIndices;
        OptimizedPiler[] optimizedPilers = _optimizedPilers;
        int[] timeSpends = _timeSpends;

        for (int pilerIndex = 0; pilerIndex < optimizedPilers.Length; pilerIndex++)
        {
            int networkIndex = networkIndices[pilerIndex];
            float power = networkServes[networkIndex];
            ref int timeSpend = ref timeSpends[pilerIndex];
            optimizedPilers[pilerIndex].InternalUpdate(power, ref timeSpend);

            UpdatePower(pilerPowerConsumerIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, pilerIndex, networkIndex, timeSpend);
        }
    }

    public void UpdatePower(int[] pilerPowerConsumerIndexes,
                            PowerConsumerType[] powerConsumerTypes,
                            long[] thisSubFactoryNetworkPowerConsumption)
    {
        int[] networkIndices = _networkIndices;
        int[] timeSpends = _timeSpends;
        for (int pilerIndex = 0; pilerIndex < timeSpends.Length; pilerIndex++)
        {
            int networkIndex = networkIndices[pilerIndex];
            int timeSpend = timeSpends[pilerIndex];
            UpdatePower(pilerPowerConsumerIndexes, powerConsumerTypes, thisSubFactoryNetworkPowerConsumption, pilerIndex, networkIndex, timeSpend);
        }
    }

    private static void UpdatePower(int[] pilerPowerConsumerIndexes,
                                    PowerConsumerType[] powerConsumerTypes,
                                    long[] thisSubFactoryNetworkPowerConsumption,
                                    int pilerIndex,
                                    int networkIndex,
                                    int timeSpend)
    {
        int powerConsumerTypeIndex = pilerPowerConsumerIndexes[pilerIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        thisSubFactoryNetworkPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType, timeSpend);
    }

    public PrototypePowerConsumptions UpdatePowerConsumptionPerPrototype(int[] pilerPowerConsumerIndexes,
                                                                         PowerConsumerType[] powerConsumerTypes)
    {
        var prototypePowerConsumptionExecutor = _prototypePowerConsumptionExecutor;
        prototypePowerConsumptionExecutor.Clear();

        int[] timeSpends = _timeSpends;
        int[] prototypeIdIndexes = prototypePowerConsumptionExecutor.PrototypeIdIndexes;
        long[] prototypeIdPowerConsumption = prototypePowerConsumptionExecutor.PrototypeIdPowerConsumption;
        for (int pilerIndex = 0; pilerIndex < timeSpends.Length; pilerIndex++)
        {
            int timeSpend = timeSpends[pilerIndex];
            UpdatePowerConsumptionPerPrototype(pilerPowerConsumerIndexes,
                                               powerConsumerTypes,
                                               prototypeIdIndexes,
                                               prototypeIdPowerConsumption,
                                               pilerIndex,
                                               timeSpend);
        }

        return prototypePowerConsumptionExecutor.GetPowerConsumption();
    }

    private static void UpdatePowerConsumptionPerPrototype(int[] pilerPowerConsumerIndexes,
                                                           PowerConsumerType[] powerConsumerTypes,
                                                           int[] prototypeIdIndexes,
                                                           long[] prototypeIdPowerConsumption,
                                                           int pilerIndex,
                                                           int timeSpend)
    {
        int powerConsumerTypeIndex = pilerPowerConsumerIndexes[pilerIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        prototypeIdPowerConsumption[prototypeIdIndexes[pilerIndex]] += GetPowerConsumption(powerConsumerType, timeSpend);
    }

    public void Save(PlanetFactory planet)
    {
        PilerComponent[] pilers = planet.cargoTraffic.pilerPool;
        OptimizedPiler[] optimizedPilers = _optimizedPilers;
        int[] timeSpends = _timeSpends;
        for (int i = 1; i < planet.factorySystem.inserterCursor; i++)
        {
            if (!_pilerIdToOptimizedIndex.TryGetValue(i, out int optimizedIndex))
            {
                continue;
            }

            optimizedPilers[optimizedIndex].Save(ref pilers[i], timeSpends[optimizedIndex]);
        }
    }

    public void Initialize(PlanetFactory planet,
                           Graph subFactoryGraph,
                           SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder,
                           BeltExecutor beltExecutor)
    {
        List<int> networkIndices = [];
        List<OptimizedPiler> optimizedPilers = [];
        List<int> timeSpends = [];
        Dictionary<int, int> pilerIdToOptimizedIndex = [];
        var prototypePowerConsumptionBuilder = new PrototypePowerConsumptionBuilder();

        foreach (int pilerIndex in subFactoryGraph.GetAllNodes()
                                                  .Where(x => x.EntityTypeIndex.EntityType == EntityType.Piler)
                                                  .Select(x => x.EntityTypeIndex.Index)
                                                  .OrderBy(x => x))
        {
            ref readonly PilerComponent piler = ref planet.cargoTraffic.pilerPool[pilerIndex];
            if (piler.id != pilerIndex ||
                piler.inputBeltId == 0 ||
                piler.outputBeltId == 0 ||
                piler.pilerState == PilerState.None)
            {
                continue;
            }

            BeltComponent inputBeltComponent = planet.cargoTraffic.beltPool[piler.inputBeltId];
            CargoPath? inputCargoPath = planet.cargoTraffic.GetCargoPath(inputBeltComponent.segPathId);
            OptimizedCargoPath? inputBelt = inputCargoPath != null ? beltExecutor.GetOptimizedCargoPath(inputCargoPath) : null;
            if (inputBelt == null)
            {
                continue;
            }

            BeltComponent outputBeltComponent = planet.cargoTraffic.beltPool[piler.outputBeltId];
            CargoPath? outputCargoPath = planet.cargoTraffic.GetCargoPath(outputBeltComponent.segPathId);
            OptimizedCargoPath? outputBelt = outputCargoPath != null ? beltExecutor.GetOptimizedCargoPath(outputCargoPath) : null;
            if (outputBelt == null)
            {
                continue;
            }

            int networkIndex = planet.powerSystem.consumerPool[piler.pcId].networkId;
            networkIndices.Add(networkIndex);
            pilerIdToOptimizedIndex.Add(piler.id, optimizedPilers.Count);
            optimizedPilers.Add(new OptimizedPiler(inputBelt, outputBelt, inputBeltComponent.speed, outputBeltComponent.speed, in piler));
            timeSpends.Add(piler.timeSpend);
            subFactoryPowerSystemBuilder.AddPiler(in piler, networkIndex);
            prototypePowerConsumptionBuilder.AddPowerConsumer(in planet.entityPool[piler.entityId]);
        }

        _networkIndices = networkIndices.ToArray();
        _optimizedPilers = optimizedPilers.ToArray();
        _timeSpends = timeSpends.ToArray();
        _pilerIdToOptimizedIndex = pilerIdToOptimizedIndex;
        _prototypePowerConsumptionExecutor = prototypePowerConsumptionBuilder.Build();
    }

    private static long GetPowerConsumption(PowerConsumerType powerConsumerType, int timeSpend)
    {
        return powerConsumerType.GetRequiredEnergy(timeSpend < 10000);
    }
}
