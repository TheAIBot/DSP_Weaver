using System;
using System.Collections;
using System.Collections.Generic;
using Weaver.Optimizations.WorkDistributors.WorkChunks;

namespace Weaver.Optimizations.WorkDistributors;

internal interface IWorkNode : IDisposable
{
    bool IsLeaf { get; }
    (bool isNodeComplete, bool didAnyWork) TryDoWork(bool waitForWork, bool searchAllWork, int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition);
    IEnumerable<IWorkChunk> GetAllWorkChunks();
    void Reset();
    int GetWorkChunkCount();
    void DeepDispose();
}