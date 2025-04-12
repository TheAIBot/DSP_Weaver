using System;
using System.Collections.Generic;
using System.Linq;
using Weaver.Extensions;

namespace Weaver.Optimizations.LinearDataAccess.WorkDistributors;

internal sealed class PerformanceMonitorUpdater
{
    private readonly double[] _sumWorkerWorkTypeTimings = new double[ArrayExtensions.GetEnumValuesEnumerable<WorkType>().Max(x => (int)x) + 1];
    private readonly HashSet<ECpuWorkEntry> _factoryWorkEntryChildren;
    private readonly Dictionary<WorkType, ECpuWorkEntry[]> _workTypeToCpuWorkEntry = new()
    {
        {WorkType.BeforePower, [ECpuWorkEntry.PowerSystem] },
        {WorkType.Power, [ECpuWorkEntry.PowerSystem]},
        {WorkType.Construction, [ECpuWorkEntry.Construction]},
        {WorkType.CheckBefore, [ECpuWorkEntry.Null]},
        {WorkType.Assembler, [ECpuWorkEntry.Facility]},
        {WorkType.LabResearchMode, [ECpuWorkEntry.Facility, ECpuWorkEntry.Lab]},
        {WorkType.LabOutput2NextData, [ECpuWorkEntry.Facility, ECpuWorkEntry.Lab]},
        {WorkType.TransportData, [ECpuWorkEntry.Transport]},
        {WorkType.InputFromBelt, [ECpuWorkEntry.Storage]},
        {WorkType.InserterData, [ECpuWorkEntry.Inserter]},
        {WorkType.Storage, [ECpuWorkEntry.Storage]},
        {WorkType.CargoPathsData, [ECpuWorkEntry.Belt]},
        {WorkType.Splitter, [ECpuWorkEntry.Splitter]},
        {WorkType.Monitor, [ECpuWorkEntry.Belt]},
        {WorkType.Spraycoater, [ECpuWorkEntry.Belt]},
        {WorkType.Piler, [ECpuWorkEntry.Belt]},
        {WorkType.OutputToBelt, [ECpuWorkEntry.Storage]},
        {WorkType.SandboxMode, [ECpuWorkEntry.Storage]}, // ????? This is what the game does so sure!
        {WorkType.PresentCargoPathsData, [ECpuWorkEntry.LocalCargo]},
        {WorkType.Digital, [ECpuWorkEntry.Digital]}
    };

    private PerformanceMonitorUpdater(HashSet<ECpuWorkEntry> factoryWorkEntryChildren)
    {
        _factoryWorkEntryChildren = factoryWorkEntryChildren;
    }

    public static PerformanceMonitorUpdater Create()
    {
        HashSet<ECpuWorkEntry> factoryWorkEntryChildren = [];
        while (true)
        {
            int entryCountStart = factoryWorkEntryChildren.Count;
            for (int i = 0; i < PerformanceMonitor.cpuWorkParents.Length; i++)
            {
                ECpuWorkEntry workEntryParent = PerformanceMonitor.cpuWorkParents[i];
                if (workEntryParent == ECpuWorkEntry.Factory || factoryWorkEntryChildren.Contains(workEntryParent))
                {
                    factoryWorkEntryChildren.Add((ECpuWorkEntry)i);
                }
            }

            if (entryCountStart == factoryWorkEntryChildren.Count)
            {
                break;
            }
        }

        return new PerformanceMonitorUpdater(factoryWorkEntryChildren);
    }

    public void UpdateTimings(double totalDuration, WorkExecutor[] workExecutors)
    {
        Array.Clear(_sumWorkerWorkTypeTimings, 0, _sumWorkerWorkTypeTimings.Length);

        foreach (WorkExecutor workExecutor in workExecutors)
        {
            double[] workerWorkTypeTimings = workExecutor.GetWorkTypeTimings();
            if (_sumWorkerWorkTypeTimings.Length != workerWorkTypeTimings.Length)
            {
                throw new InvalidOperationException($"Work type timing arrays were not the same size. Sum array size: {_sumWorkerWorkTypeTimings.Length}, worker array size: {workerWorkTypeTimings.Length}");
            }

            for (int i = 0; i < _sumWorkerWorkTypeTimings.Length; i++)
            {
                _sumWorkerWorkTypeTimings[i] += workerWorkTypeTimings[i];
            }
        }

        double totalCpuTime = _sumWorkerWorkTypeTimings.Sum();
        double workTypeTimeRatio = totalDuration / totalCpuTime;

        foreach (var workTypeToCpuWorkEntries in _workTypeToCpuWorkEntry)
        {
            for (int i = 0; i < workTypeToCpuWorkEntries.Value.Length; i++)
            {
                if (workTypeToCpuWorkEntries.Value[i] == ECpuWorkEntry.Null)
                {
                    continue;
                }

                PerformanceMonitor.timeCostsFrame[(int)workTypeToCpuWorkEntries.Value[i]] += _sumWorkerWorkTypeTimings[(int)workTypeToCpuWorkEntries.Key] * workTypeTimeRatio;
            }
        }
    }
}