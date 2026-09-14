namespace Game.Production
{
    /// <summary>
    /// Contract for a station that takes in a product, processes it, and produces an updated
    /// product state as the result. This is a pure contract definition - concrete processing
    /// logic (mixing, forming, baking, etc.) for individual machines follows in a later pass.
    /// </summary>
    /// <remarks>
    /// A machine implementing this interface is expected to only accept/process a product while
    /// its own MachineState (see Dev A's IMachine) is Running; that coordination happens at the
    /// machine implementation level, not within this interface itself.
    /// </remarks>
    public interface IProductProcessor
    {
        /// <summary>The ProductState this processor expects as valid input.</summary>
        ProductState ExpectedInputState { get; }

        /// <summary>The ProductState this processor produces once processing completes successfully.</summary>
        ProductState OutputState { get; }

        /// <summary>Whether this processor is currently able to accept a new product for processing.</summary>
        bool CanAcceptProduct { get; }

        /// <summary>
        /// Attempts to begin processing the given product. The processor takes ownership of the
        /// product instance for the duration of processing.
        /// </summary>
        /// <param name="product">The incoming product to process.</param>
        /// <returns>True if the product was accepted and processing started; false otherwise (e.g. wrong input state, processor busy).</returns>
        bool TryBeginProcessing(ProductInstance product);

        /// <summary>Whether processing of the currently held product has finished and is ready to be collected.</summary>
        bool IsProcessingComplete { get; }

        /// <summary>
        /// Retrieves the processed product once <see cref="IsProcessingComplete"/> is true, updating
        /// its <see cref="ProductInstance.CurrentState"/> to <see cref="OutputState"/> and releasing
        /// it from this processor.
        /// </summary>
        /// <param name="product">The processed product, or null if processing was not complete.</param>
        /// <returns>True if a completed product was retrieved; false otherwise.</returns>
        bool TryCollectProcessedProduct(out ProductInstance product);
    }
}
