namespace Game.Core
{
    /// <summary>
    /// Something the player currently holds or pushes (carried item, pallet jack) that tells the crosshair what
    /// the keys do now, e.g. "(Q) Put down" or "(R) Forks up/down   (Q) Let go" (already translated).
    /// </summary>
    public interface IReleaseable
    {
        string ReleaseText { get; }
    }
}
