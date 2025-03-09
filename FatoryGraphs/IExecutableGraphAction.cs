namespace Weaver.FatoryGraphs;

interface IExecutableGraphAction
{
    void Execute(long time, PlanetFactory factory, int[] indexes);
}
