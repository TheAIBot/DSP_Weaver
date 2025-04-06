namespace Weaver.Optimizations.LinearDataAccess.Labs;

internal struct LabPowerFields
{
    public bool replicating;
    public int extraPowerRatio;

    public LabPowerFields(ref readonly LabComponent labComponent)
    {
        replicating = labComponent.replicating;
        extraPowerRatio = labComponent.extraPowerRatio;
    }
}
