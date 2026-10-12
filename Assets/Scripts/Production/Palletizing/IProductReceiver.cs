namespace Game.Production
{
    /// <summary>
    /// Takes finished products from the <see cref="LineEndSink"/> instead of letting them disappear - e.g. the
    /// palletizer stacks good cartons on a pallet. While <see cref="IsAccepting"/> is false the line end
    /// leaves products where they are (the last belt holds them).
    /// </summary>
    public interface IProductReceiver
    {
        /// <summary>Ready for the next product (e.g. empty space on a pallet).</summary>
        bool IsAccepting { get; }

        /// <summary>Called after the QualitySystem judged the product. False = not taken, the line end removes it (scrap).</summary>
        bool TryReceive(ProductToken token);
    }
}
