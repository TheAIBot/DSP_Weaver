namespace Weaver.Optimizations.WorkDistributors.WorkChunks;

internal sealed class DefenseSystemTurret : IWorkChunk
{
    private readonly DefenseSystem _defenseSystem;

    public DefenseSystemTurret(DefenseSystem defenseSystem)
    {
        _defenseSystem = defenseSystem;
    }

    public void Execute(int workerIndex, object singleThreadedCodeLock, PlanetData localPlanet, long time, UnityEngine.Vector3 playerPosition)
    {
        _defenseSystem.GameTick_Turret(time);
    }
}