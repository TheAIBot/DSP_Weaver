using Steamworks;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.Belts;

internal sealed class BeltExecutor
{
    private OptimizedCargoPath[] _optimizedCargoPaths = null!;
    private Dictionary<CargoPath, BeltIndex> _cargoPathToOptimizedCargoPathIndex = null!;

    public Dictionary<CargoPath, BeltIndex> CargoPathToOptimizedCargoPathIndex => _cargoPathToOptimizedCargoPathIndex;

    public int Count => _optimizedCargoPaths.Length;

    public OptimizedCargoPath[] OptimizedCargoPaths => _optimizedCargoPaths;

    public bool TryGetOptimizedCargoPathIndex(PlanetFactory planet, int beltId, [NotNullWhen(true)] out BeltIndex beltIndex)
    {
        if (!TryGetCargoPath(planet, beltId, out CargoPath? cargoPath))
        {
            beltIndex = BeltIndex.NoBelt;
            return false;
        }

        beltIndex = _cargoPathToOptimizedCargoPathIndex[cargoPath];
        return true;
    }

    public void GameTick()
    {
        OptimizedCargoPath[] optimizedCargoPaths = _optimizedCargoPaths;
        for (int i = 0; i < optimizedCargoPaths.Length; i++)
        {
            optimizedCargoPaths[i].Update(optimizedCargoPaths);
        }
    }

    public void Save(CargoContainer cargoContainer)
    {
        OptimizedCargoPath[] optimizedCargoPaths = _optimizedCargoPaths;
        foreach (KeyValuePair<CargoPath, BeltIndex> cargoPathWithOptimizedCargoPathIndex in _cargoPathToOptimizedCargoPathIndex)
        {
            ref OptimizedCargoPath optimizedCargoPath = ref cargoPathWithOptimizedCargoPathIndex.Value.GetBelt(optimizedCargoPaths);
            CopyToBufferWithUpdatedCargoIndexes(cargoPathWithOptimizedCargoPathIndex.Key.buffer, ref optimizedCargoPath, cargoContainer);
            optimizedCargoPath.Save(cargoPathWithOptimizedCargoPathIndex.Key);
        }
    }

    public void Initialize(PlanetFactory planet, Graph subFactoryGraph, UniverseStaticDataBuilder universeStaticDataBuilder)
    {
        List<OptimizedCargoPath> optimizedCargoPaths = [];
        Dictionary<CargoPath, BeltIndex> cargoPathToOptimizedCargoPath = [];

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

            byte[] updatedBuffer = GetBufferWithUpdatedCargoIndexes(cargoPath);
            var optimizedCargoPath = new OptimizedCargoPath(updatedBuffer, cargoPath, universeStaticDataBuilder);
            cargoPathToOptimizedCargoPath.Add(cargoPath, new BeltIndex(optimizedCargoPaths.Count));
            optimizedCargoPaths.Add(optimizedCargoPath);
        }

        _optimizedCargoPaths = optimizedCargoPaths.ToArray();

        foreach (KeyValuePair<CargoPath, BeltIndex> cargoPathWithOptimizedCargoPathIndex in cargoPathToOptimizedCargoPath)
        {
            if (cargoPathWithOptimizedCargoPathIndex.Key.outputPath == null)
            {
                continue;
            }

            ref OptimizedCargoPath belt = ref cargoPathWithOptimizedCargoPathIndex.Value.GetBelt(_optimizedCargoPaths);
            belt.SetOutputPath(cargoPathToOptimizedCargoPath[cargoPathWithOptimizedCargoPathIndex.Key.outputPath]);
        }

        _cargoPathToOptimizedCargoPathIndex = cargoPathToOptimizedCargoPath;
    }

    /// <summary>
    /// Modified <see cref="CargoPath.PresentCargos"/>
    /// </summary>
    private static byte[] GetBufferWithUpdatedCargoIndexes(CargoPath cargoPath)
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
                    OptimizedCargo optimizedCargo = new OptimizedCargo(oldCargo.item, oldCargo.stack, oldCargo.inc);
                    SetCargoInBuffer(bufferCopy, num3 + 1, optimizedCargo);
                }
                num3 += num2;
                continue;
            }
            if (246 <= cargoPath.buffer[num3] && cargoPath.buffer[num3] < 250)
            {
                num3 += 250 - cargoPath.buffer[num3];
                int num5 = cargoPath.buffer[num3 + 1] - 1 + (cargoPath.buffer[num3 + 2] - 1) * 100 + (cargoPath.buffer[num3 + 3] - 1) * 10000 + (cargoPath.buffer[num3 + 4] - 1) * 1000000;

                Cargo oldCargo = oldCargoPool[num5];
                OptimizedCargo optimizedCargo = new OptimizedCargo(oldCargo.item, oldCargo.stack, oldCargo.inc);
                SetCargoInBuffer(bufferCopy, num3 + 1, optimizedCargo);
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
    private static void CopyToBufferWithUpdatedCargoIndexes(byte[] bufferCopy, ref OptimizedCargoPath optimizedCargoPath, CargoContainer cargoContainer)
    {
        if (bufferCopy.Length != optimizedCargoPath.buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferCopy), $"{nameof(bufferCopy)} did not have the same length as {nameof(optimizedCargoPath)}.{nameof(optimizedCargoPath.buffer)}.");
        }
        Array.Copy(optimizedCargoPath.buffer, bufferCopy, optimizedCargoPath.buffer.Length);

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
                OptimizedCargo oldCargo = GetCargo(optimizedCargoPath.buffer, num3 + 1);
                int newCargoIndex = cargoContainer.AddCargo(oldCargo.Item, oldCargo.Stack, oldCargo.Inc);
                SetCargoIndexInBufferDefaultGameWay(bufferCopy, num3 + 1, newCargoIndex);
                num3 += num2;
                continue;
            }
            if (246 <= optimizedCargoPath.buffer[num3] && optimizedCargoPath.buffer[num3] < 250)
            {
                num3 += 250 - optimizedCargoPath.buffer[num3];

                OptimizedCargo oldCargo = GetCargo(optimizedCargoPath.buffer, num3 + 1);
                int newCargoIndex = cargoContainer.AddCargo(oldCargo.Item, oldCargo.Stack, oldCargo.Inc);
                SetCargoIndexInBufferDefaultGameWay(bufferCopy, num3 + 1, newCargoIndex);
                num3 += num2;
                continue;
            }
            Assert.CannotBeReached("断言失败：buffer数据有误");
            break;
        }
    }

    internal static void SetCargoInBuffer(byte[] buffer, int bufferIndex, OptimizedCargo optimizedCargo)
    {
        buffer[bufferIndex + 0] = (byte)((optimizedCargo.Item & 0b0111_1111) + 1);
        buffer[bufferIndex + 1] = (byte)((optimizedCargo.Item >> 7) + 1);
        buffer[bufferIndex + 2] = (byte)(optimizedCargo.Stack + 1);
        buffer[bufferIndex + 3] = (byte)(optimizedCargo.Inc + 1);
    }

    internal static OptimizedCargo GetCargo(byte[] buffer, int index)
    {
        return new OptimizedCargo((short)(buffer[index] - 1 + (buffer[index + 1] - 1 << 7)),
                                  (byte)(buffer[index + 2] - 1),
                                  (byte)(buffer[index + 3] - 1));
    }

    internal static void SetCargoIndexInBufferDefaultGameWay(byte[] buffer, int bufferIndex, int cargoIndex)
    {
        buffer[bufferIndex + 0] = (byte)(cargoIndex % 100 + 1);
        cargoIndex /= 100;
        buffer[bufferIndex + 1] = (byte)(cargoIndex % 100 + 1);
        cargoIndex /= 100;
        buffer[bufferIndex + 2] = (byte)(cargoIndex % 100 + 1);
        cargoIndex /= 100;
        buffer[bufferIndex + 3] = (byte)(cargoIndex % 100 + 1);
    }

    private static bool TryGetCargoPath(PlanetFactory planet, int beltId, [NotNullWhen(true)] out CargoPath? belt)
    {
        if (beltId <= 0)
        {
            belt = null;
            return false;
        }

        belt = planet.cargoTraffic.GetCargoPath(planet.cargoTraffic.beltPool[beltId].segPathId);
        return belt != null;
    }
}
