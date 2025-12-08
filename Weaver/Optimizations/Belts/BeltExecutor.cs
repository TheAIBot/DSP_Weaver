using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.StaticData;

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

    public void GameTick(long time)
    {
        OptimizedCargoPath[] optimizedCargoPaths = _optimizedCargoPaths;
        for (int i = 0; i < optimizedCargoPaths.Length; i++)
        {
            optimizedCargoPaths[i].Update(optimizedCargoPaths, time);
        }
    }

    public void Save(CargoContainer cargoContainer)
    {
        OptimizedCargoPath[] optimizedCargoPaths = _optimizedCargoPaths;
        foreach (KeyValuePair<CargoPath, BeltIndex> cargoPathWithOptimizedCargoPathIndex in _cargoPathToOptimizedCargoPathIndex)
        {
            ref OptimizedCargoPath optimizedCargoPath = ref cargoPathWithOptimizedCargoPathIndex.Value.GetBelt(optimizedCargoPaths);
            CopyToBufferWithUpdatedCargoIndexes(cargoPathWithOptimizedCargoPathIndex.Key, ref optimizedCargoPath, cargoContainer);
            optimizedCargoPath.Save(cargoPathWithOptimizedCargoPathIndex.Key);
            optimizedCargoPath.buffer.Free();
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

            if (cargoPath.chunkCount > 1)
            {
                WeaverFixes.Logger.LogMessage($"Chunk count: {cargoPath.chunkCount}");
                for (int i = 0; i < cargoPath.chunkCount; i++)
                {
                    WeaverFixes.Logger.LogMessage($"\tChunk: {i}");
                    WeaverFixes.Logger.LogMessage($"\t\tBegin: {cargoPath.chunks[i * 3 + 0]:N0}");
                    WeaverFixes.Logger.LogMessage($"\t\tLength: {cargoPath.chunks[i * 3 + 1]:N0}");
                    WeaverFixes.Logger.LogMessage($"\t\tSpeed: {cargoPath.chunks[i * 3 + 2]:N0}");
                }
            }

            BeltBuffer updatedBuffer = GetBufferWithUpdatedCargoIndexes(cargoPath);
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

    private static int GetMaxChunkSpeed(int chunkCount, int[] chunks)
    {
        int maxChunkSpeed = -1;
        for (int i = 0; i < chunkCount; i++)
        {
            int chunkSpeed = chunks[i * 3 + 2];
            if (chunkSpeed > maxChunkSpeed)
            {
                maxChunkSpeed = chunkSpeed;
            }
        }

        return maxChunkSpeed;
    }

    /// <summary>
    /// Modified <see cref="CargoPath.PresentCargos"/>
    /// </summary>
    private static BeltBuffer GetBufferWithUpdatedCargoIndexes(CargoPath cargoPath)
    {
        int maxChunkSpeed = GetMaxChunkSpeed(cargoPath.chunkCount, cargoPath.chunks);
        BeltBuffer bufferCopy = BeltBuffer.CreateFromExistingBuffer(cargoPath.buffer, cargoPath.bufferLength, maxChunkSpeed, 10);

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
                    SetCargoInBuffer(ref bufferCopy, num3 + 1, optimizedCargo);
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
                SetCargoInBuffer(ref bufferCopy, num3 + 1, optimizedCargo);
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
    private static void CopyToBufferWithUpdatedCargoIndexes(CargoPath cargoPath, ref OptimizedCargoPath optimizedCargoPath, CargoContainer cargoContainer)
    {
        //if (bufferCopy.Length != optimizedCargoPath.buffer.Length)
        //{
        //    throw new ArgumentOutOfRangeException(nameof(bufferCopy), $"{nameof(bufferCopy)} did not have the same length as {nameof(optimizedCargoPath)}.{nameof(optimizedCargoPath.buffer)}.");
        //}
        //Array.Copy(optimizedCargoPath.buffer, bufferCopy, optimizedCargoPath.buffer.Length);
        for (int i = 0; i < cargoPath.bufferLength; i++)
        {
            cargoPath.buffer[i] = optimizedCargoPath.buffer.GetBufferValue(i);
        }

        int num = 5;
        int num2 = 10;
        int num3 = 0;
        while (num3 < optimizedCargoPath.bufferLength)
        {
            if (optimizedCargoPath.buffer.GetBufferValue(num3) == 0)
            {
                num3 += num;
                continue;
            }
            if (optimizedCargoPath.buffer.GetBufferValue(num3) == 250)
            {
                GetCargo(ref optimizedCargoPath.buffer, num3 + 1, out OptimizedCargo oldCargo);
                int newCargoIndex = cargoContainer.AddCargo(oldCargo.Item, oldCargo.Stack, oldCargo.Inc);
                SetCargoIndexInBufferDefaultGameWay(cargoPath.buffer, num3 + 1, newCargoIndex);
                num3 += num2;
                continue;
            }
            if (246 <= optimizedCargoPath.buffer.GetBufferValue(num3) && optimizedCargoPath.buffer.GetBufferValue(num3) < 250)
            {
                num3 += 250 - optimizedCargoPath.buffer.GetBufferValue(num3);

                GetCargo(ref optimizedCargoPath.buffer, num3 + 1, out OptimizedCargo oldCargo);
                int newCargoIndex = cargoContainer.AddCargo(oldCargo.Item, oldCargo.Stack, oldCargo.Inc);
                SetCargoIndexInBufferDefaultGameWay(cargoPath.buffer, num3 + 1, newCargoIndex);
                num3 += num2;
                continue;
            }
            Assert.CannotBeReached("断言失败：buffer数据有误");
            break;
        }
    }

    internal static void SetCargoInBuffer(ref BeltBuffer buffer, int bufferIndex, OptimizedCargo optimizedCargo)
    {
        buffer.SetCargo(bufferIndex, optimizedCargo);
    }

    internal static void GetCargo(ref BeltBuffer buffer, int index, out OptimizedCargo optimizedCargo)
    {
        buffer.GetCargo(index, out optimizedCargo);
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
