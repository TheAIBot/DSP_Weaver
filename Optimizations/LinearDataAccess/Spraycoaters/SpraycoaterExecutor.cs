using System.Collections.Generic;

namespace Weaver.Optimizations.LinearDataAccess.Spraycoaters;

internal sealed class SpraycoaterExecutor
{
    private OptimizedSpraycoater[] _optimizedSpraycoaters;

    public void SpraycoaterGameTick(PlanetFactory planet)
    {
        int[] consumeRegister = GameMain.statistics.production.factoryStatPool[planet.index].consumeRegister;
        OptimizedSpraycoater[] optimizedSpraycoaters = _optimizedSpraycoaters;
        for (int i = 0; i < optimizedSpraycoaters.Length; i++)
        {
            optimizedSpraycoaters[i].InternalUpdate(consumeRegister);
        }
    }

    public void Initialize(PlanetFactory planet)
    {
        List<OptimizedSpraycoater> optimizedSpraycoaters = [];

        for (int i = 1; i < planet.cargoTraffic.spraycoaterCursor; i++)
        {
            ref readonly SpraycoaterComponent spraycoater = ref planet.cargoTraffic.spraycoaterPool[i];
            if (spraycoater.id != i)
            {
                continue;
            }

            if (spraycoater.incBeltId == 0 && spraycoater.cargoBeltId == 0)
            {
                continue;
            }

            BeltComponent? incommingBeltComponent = default;
            CargoPath incommingCargoPath = null;
            int incommingBeltSegIndexPlusSegPivotOffset = 0;
            if (spraycoater.incBeltId > 0)
            {
                incommingBeltComponent = planet.cargoTraffic.beltPool[spraycoater.incBeltId];
                incommingCargoPath = planet.cargoTraffic.GetCargoPath(incommingBeltComponent.Value.segPathId);
                incommingBeltSegIndexPlusSegPivotOffset = incommingBeltComponent.Value.segIndex + incommingBeltComponent.Value.segPivotOffset;
            }

            BeltComponent? outgoingBeltComponent = default;
            CargoPath outgoingCargoPath = null;
            int outgoingBeltSegIndexPlusSegPivotOffset = 0;
            int outgoingBeltSpeed = 0;
            if (spraycoater.cargoBeltId > 0)
            {
                outgoingBeltComponent = planet.cargoTraffic.beltPool[spraycoater.cargoBeltId];
                outgoingCargoPath = planet.cargoTraffic.GetCargoPath(outgoingBeltComponent.Value.segPathId);
                outgoingBeltSegIndexPlusSegPivotOffset = outgoingBeltComponent.Value.segIndex + outgoingBeltComponent.Value.segPivotOffset;
                outgoingBeltSpeed = outgoingBeltComponent.Value.speed;
            }

            int[] incItemIds = LDB.models.Select(planet.cargoTraffic.factory.entityPool[spraycoater.entityId].modelIndex).prefabDesc.incItemId;
            bool isSpraycoatingItem = spraycoater.cargoBeltItemId != 0;

            int networkId = planet.powerSystem.consumerPool[spraycoater.pcId].networkId;
            PowerNetwork powerNetwork = networkId != 0 ? planet.powerSystem.netPool[networkId] : null;

            optimizedSpraycoaters.Add(new OptimizedSpraycoater(incommingBeltSegIndexPlusSegPivotOffset,
                                                               incommingCargoPath,
                                                               incItemIds,
                                                               outgoingCargoPath,
                                                               outgoingBeltSegIndexPlusSegPivotOffset,
                                                               outgoingBeltSpeed,
                                                               powerNetwork,
                                                               in spraycoater));
        }

        _optimizedSpraycoaters = optimizedSpraycoaters.ToArray();
    }
}