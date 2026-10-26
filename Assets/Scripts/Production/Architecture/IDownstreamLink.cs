
namespace Game.Production
{
    /// <summary>
    /// Elements that need to know the next element of the line (belts, the portioner). Wired by
    /// ConveyorLineController in material-flow order, or set by hand in the inspector.
    /// </summary>
    public interface IDownstreamLink
    {
        void SetDownstream(MachineBase downstream);
    }
}
