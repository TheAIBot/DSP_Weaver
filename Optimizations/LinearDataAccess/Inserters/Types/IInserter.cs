namespace Weaver.Optimizations.LinearDataAccess.Inserters.Types;

internal interface IInserter<T>
{
    public byte grade { get; }

    T Create(ref readonly InserterComponent inserter, int grade);

    void Update(PlanetFactory planet,
                OptimizedPlanet optimizedPlanet,
                float power,
                int inserterIndex,
                ref NetworkIdAndState<InserterState> inserterNetworkIdAndState,
                ref readonly InserterConnections inserterConnections,
                ref readonly int[] inserterConnectionNeeds,
                PickFromProducingPlant[] pickFromProducingPlants,
                InserterGrade inserterGrade);
}
