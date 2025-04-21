using System.Linq;
using Weaver.FatoryGraphs;

namespace Weaver.Optimizations.LinearDataAccess.Splitters;

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
                    int filter = us_tmp_outputIdx == 0 ? sp.outFilter : -sp.outFilter;
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
