using System;
using System.Collections.Generic;
using UnityEngine;
using Weaver.Optimizations.WorkDistributors.WorkChunks;

namespace Weaver.Optimizations.WorkDistributors;

internal sealed class NoWorkNode : IWorkNode
{
    public bool IsLeaf => throw new NotImplementedException();

    public int GetWorkChunkCount() => throw new NotImplementedException();

    public void Reset() => throw new NotImplementedException();

    public (bool isNodeComplete, bool didAnyWork) TryDoWork(bool waitForWork, int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, Vector3 playerPosition) => throw new NotImplementedException();
    public IEnumerable<IWorkChunk> GetAllWorkChunks() => throw new NotImplementedException();
    public void DeepDispose() { }

    public void Dispose() { }
}

internal sealed class DummyWorkDoneImmediatelyNode : IWorkNode
{
    public bool IsLeaf => true;

    public int GetWorkChunkCount() => 0;

    public void Reset(){ }

    public (bool isNodeComplete, bool didAnyWork) TryDoWork(bool waitForWork, int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, Vector3 playerPosition) => (true, true);
    public IEnumerable<IWorkChunk> GetAllWorkChunks() => [];
    public void DeepDispose() { }

    public void Dispose() { }
}