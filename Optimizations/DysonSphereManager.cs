using System.Collections.Generic;

namespace Weaver.Optimizations;

internal sealed class DysonSphereManager
{
    private readonly Queue<PlanetFactory> _createDysonSpheresFor = [];

    public void AddDysonDysonSphere(PlanetFactory planet)
    {
        lock (_createDysonSpheresFor)
        {
            _createDysonSpheresFor.Enqueue(planet);
        }
    }

    public void UIThreadCreateDysonSpheres()
    {
        try
        {
            foreach (var planet in _createDysonSpheresFor)
            {
                planet.CheckOrCreateDysonSphere();
            }
        }
        finally
        {
            _createDysonSpheresFor.Clear();
        }
    }
}
