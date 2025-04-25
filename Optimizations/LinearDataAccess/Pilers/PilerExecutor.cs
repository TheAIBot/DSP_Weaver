using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.LinearDataAccess.Belts;
using Weaver.Optimizations.LinearDataAccess.PowerSystems;

namespace Weaver.Optimizations.LinearDataAccess.Pilers;

internal sealed class PilerExecutor
{
    private int[] _networkIndices;
    private OptimizedPiler[] _optimizedPilers;
    private int[] _timeSpends;
    private Dictionary<int, int> _pilerIdToOptimizedIndex;

    public void GameTick(PlanetFactory planet)
    {
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        int[] networkIndices = _networkIndices;
        OptimizedPiler[] optimizedPilers = _optimizedPilers;
        int[] timeSpends = _timeSpends;

        for (int i = 0; i < optimizedPilers.Length; i++)
        {
            float power = networkServes[networkIndices[i]];
            ref int timeSpend = ref timeSpends[i];
            optimizedPilers[i].InternalUpdate(power, ref timeSpend);
        }
    }

    public void UpdatePower(int[] pilerPowerConsumerIndexes,
                            PowerConsumerType[] powerConsumerTypes,
                            long[] thisSubFactoryNetworkPowerConsumption)
    {
        int[] networkIndices = _networkIndices;
        int[] timeSpends = _timeSpends;
        for (int j = 0; j < timeSpends.Length; j++)
        {
            int networkIndex = networkIndices[j];
            int powerConsumerTypeIndex = pilerPowerConsumerIndexes[j];
            PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
            thisSubFactoryNetworkPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType, timeSpends[j]);
        }
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
                           OptimizedPowerSystemBuilder optimizedPowerSystemBuilder,
                           BeltExecutor beltExecutor)
    {
        List<int> networkIndices = [];
        List<OptimizedPiler> optimizedPilers = [];
        List<int> timeSpends = [];
        Dictionary<int, int> pilerIdToOptimizedIndex = [];

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
            optimizedPowerSystemBuilder.AddPiler(in piler, networkIndex);
        }

        _networkIndices = networkIndices.ToArray();
        _optimizedPilers = optimizedPilers.ToArray();
        _timeSpends = timeSpends.ToArray();
        _pilerIdToOptimizedIndex = pilerIdToOptimizedIndex;
    }

    private long GetPowerConsumption(PowerConsumerType powerConsumerType, int timeSpend)
    {
        return powerConsumerType.GetRequiredEnergy(timeSpend < 10000);
    }
}
