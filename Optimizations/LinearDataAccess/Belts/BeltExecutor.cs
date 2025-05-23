using System;
using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.LinearDataAccess.Belts;

internal sealed class BeltExecutor
{
    private OptimizedCargoPath[] _optimizedCargoPaths = null!;
    private Dictionary<CargoPath, OptimizedCargoPath> _cargoPathToOptimizedCargoPath = null!;
    private Dictionary<int, int> _cargoPathIdToOptimizedIndex = null!;

    public OptimizedCargoContainer OptimizedCargoContainer = null!;

    public Dictionary<CargoPath, OptimizedCargoPath> CargoPathToOptimizedCargoPath => _cargoPathToOptimizedCargoPath;

    public OptimizedCargoPath GetOptimizedCargoPath(CargoPath cargoPath)
    {
        return _cargoPathToOptimizedCargoPath[cargoPath];
    }

    public int GetOptimizedCargoPathIndex(int cargoPathIndex)
    {
        return _cargoPathIdToOptimizedIndex[cargoPathIndex];
    }

    public void GameTick(PlanetFactory planet)
    {
        OptimizedCargoPath[] optimizedCargoPaths = _optimizedCargoPaths;
        for (int i = 0; i < optimizedCargoPaths.Length; i++)
        {
            optimizedCargoPaths[i].Update();
        }
    }

    public void Save(CargoContainer cargoContainer)
    {
        foreach (KeyValuePair<CargoPath, OptimizedCargoPath> cargoPathWithOptimizedCargoPath in _cargoPathToOptimizedCargoPath)
        {
            CopyToBufferWithUpdatedCargoIndexes(cargoPathWithOptimizedCargoPath.Key.buffer, cargoPathWithOptimizedCargoPath.Value, cargoContainer);
            cargoPathWithOptimizedCargoPath.Value.Save(cargoPathWithOptimizedCargoPath.Key);
        }
    }

    public void Initialize(PlanetFactory planet, Graph subFactoryGraph)
    {
        OptimizedCargoContainer optimizedCargoContainer = new();
        List<OptimizedCargoPath> optimizedCargoPaths = [];
        Dictionary<CargoPath, OptimizedCargoPath> cargoPathToOptimizedCargoPath = [];
        Dictionary<int, int> cargoPathIdToOptimizedIndex = [];

        foreach (int cargoPathIndex in subFactoryGraph.GetAllNodes()
                                                      .Where(x => x.EntityTypeIndex.EntityType == EntityType.Belt)
                                                      .Select(x => x.EntityTypeIndex.Index)
                                                      .OrderBy(x => x))
        {
            CargoPath cargoPath = planet.cargoTraffic.pathPool[cargoPathIndex];
            if (cargoPath == null || cargoPath.id != cargoPathIndex)
            {
                continue;
            }

            byte[] updatedBuffer = GetBufferWithUpdatedCargoIndexes(cargoPath, optimizedCargoContainer);
            var optimizedCargoPath = new OptimizedCargoPath(optimizedCargoContainer, updatedBuffer, cargoPath);
            cargoPathIdToOptimizedIndex.Add(cargoPathIndex, optimizedCargoPaths.Count);
            optimizedCargoPaths.Add(optimizedCargoPath);
            cargoPathToOptimizedCargoPath.Add(cargoPath, optimizedCargoPath);
        }

        foreach (KeyValuePair<CargoPath, OptimizedCargoPath> cargoPathWithOptimizedCargoPath in cargoPathToOptimizedCargoPath)
        {
            if (cargoPathWithOptimizedCargoPath.Key.outputPath == null)
            {
                continue;
            }

            cargoPathWithOptimizedCargoPath.Value.SetOutputPath(cargoPathToOptimizedCargoPath[cargoPathWithOptimizedCargoPath.Key.outputPath]);
        }

        OptimizedCargoContainer = optimizedCargoContainer;
        _optimizedCargoPaths = optimizedCargoPaths.ToArray();
        _cargoPathToOptimizedCargoPath = cargoPathToOptimizedCargoPath;
        _cargoPathIdToOptimizedIndex = cargoPathIdToOptimizedIndex;
    }

    /// <summary>
    /// Modified <see cref="CargoPath.PresentCargos"/>
    /// </summary>
    private static byte[] GetBufferWithUpdatedCargoIndexes(CargoPath cargoPath, OptimizedCargoContainer optimizedCargoContainer)
    {
        byte[] bufferCopy = new byte[cargoPath.buffer.Length];
        Array.Copy(cargoPath.buffer, bufferCopy, cargoPath.buffer.Length);

        Cargo[] oldCargoPool = cargoPath.cargoContainer.cargoPool;
        int num = 5;
        int num2 = 10;
        int num3 = 0;
        while (num3 < cargoPath.bufferLength)
        {
            if (cargoPath.buffer[num3] == 0)
            {
                num3 += num;
                continue;
            }
            if (cargoPath.buffer[num3] == 250)
            {
                int num4 = cargoPath.buffer[num3 + 1] - 1 + (cargoPath.buffer[num3 + 2] - 1) * 100 + (cargoPath.buffer[num3 + 3] - 1) * 10000 + (cargoPath.buffer[num3 + 4] - 1) * 1000000;
                if (num4 >= oldCargoPool.Length || num4 < 0 || num3 >= cargoPath.pointPos.Length)
                {
                    Assert.CannotBeReached();
                }
                else
                {
                    Cargo oldCargo = oldCargoPool[num4];
                    int newCargoIndex = optimizedCargoContainer.AddCargo(oldCargo.item, oldCargo.stack, oldCargo.inc);
                    SetCargoIndexInBuffer(bufferCopy, num3 + 1, newCargoIndex);
                }
                num3 += num2;
                continue;
            }
            if (246 <= cargoPath.buffer[num3] && cargoPath.buffer[num3] < 250)
            {
                num3 += 250 - cargoPath.buffer[num3];
                int num5 = cargoPath.buffer[num3 + 1] - 1 + (cargoPath.buffer[num3 + 2] - 1) * 100 + (cargoPath.buffer[num3 + 3] - 1) * 10000 + (cargoPath.buffer[num3 + 4] - 1) * 1000000;

                Cargo oldCargo = oldCargoPool[num5];
                int newCargoIndex = optimizedCargoContainer.AddCargo(oldCargo.item, oldCargo.stack, oldCargo.inc);
                SetCargoIndexInBuffer(bufferCopy, num3 + 1, newCargoIndex);
                num3 += num2;
                continue;
            }
            Assert.CannotBeReached("断言失败：buffer数据有误");
            break;
        }

        return bufferCopy;
    }

    /// <summary>
    /// Modified <see cref="GetBufferWithUpdatedCargoIndexes"/>
    /// </summary>
    private static void CopyToBufferWithUpdatedCargoIndexes(byte[] bufferCopy, OptimizedCargoPath optimizedCargoPath, CargoContainer cargoContainer)
    {
        if (bufferCopy.Length != optimizedCargoPath.buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferCopy), $"{nameof(bufferCopy)} did not have the same length as {nameof(optimizedCargoPath)}.{nameof(optimizedCargoPath.buffer)}.");
        }
        Array.Copy(optimizedCargoPath.buffer, bufferCopy, optimizedCargoPath.buffer.Length);

        OptimizedCargo[] oldCargoPool = optimizedCargoPath.cargoContainer.cargoPool;
        int num = 5;
        int num2 = 10;
        int num3 = 0;
        while (num3 < optimizedCargoPath.bufferLength)
        {
            if (optimizedCargoPath.buffer[num3] == 0)
            {
                num3 += num;
                continue;
            }
            if (optimizedCargoPath.buffer[num3] == 250)
            {
                int num4 = optimizedCargoPath.buffer[num3 + 1] - 1 + (optimizedCargoPath.buffer[num3 + 2] - 1) * 100 + (optimizedCargoPath.buffer[num3 + 3] - 1) * 10000 + (optimizedCargoPath.buffer[num3 + 4] - 1) * 1000000;
                if (num4 >= oldCargoPool.Length || num4 < 0 || num3 >= optimizedCargoPath.buffer.Length)
                {
                    Assert.CannotBeReached();
                }
                else
                {
                    OptimizedCargo oldCargo = oldCargoPool[num4];
                    int newCargoIndex = cargoContainer.AddCargo(oldCargo.item, oldCargo.stack, oldCargo.inc);
                    SetCargoIndexInBuffer(bufferCopy, num3 + 1, newCargoIndex);
                }
                num3 += num2;
                continue;
            }
            if (246 <= optimizedCargoPath.buffer[num3] && optimizedCargoPath.buffer[num3] < 250)
            {
                num3 += 250 - optimizedCargoPath.buffer[num3];
                int num5 = optimizedCargoPath.buffer[num3 + 1] - 1 + (optimizedCargoPath.buffer[num3 + 2] - 1) * 100 + (optimizedCargoPath.buffer[num3 + 3] - 1) * 10000 + (optimizedCargoPath.buffer[num3 + 4] - 1) * 1000000;

                OptimizedCargo oldCargo = oldCargoPool[num5];
                int newCargoIndex = cargoContainer.AddCargo(oldCargo.item, oldCargo.stack, oldCargo.inc);
                SetCargoIndexInBuffer(bufferCopy, num3 + 1, newCargoIndex);
                num3 += num2;
                continue;
            }
            Assert.CannotBeReached("断言失败：buffer数据有误");
            break;
        }
    }

    private static void SetCargoIndexInBuffer(byte[] buffer, int bufferIndex, int cargoIndex)
    {
        buffer[bufferIndex + 0] = (byte)(cargoIndex % 100 + 1);
        cargoIndex /= 100;
        buffer[bufferIndex + 1] = (byte)(cargoIndex % 100 + 1);
        cargoIndex /= 100;
        buffer[bufferIndex + 2] = (byte)(cargoIndex % 100 + 1);
        cargoIndex /= 100;
        buffer[bufferIndex + 3] = (byte)(cargoIndex % 100 + 1);
    }
}
