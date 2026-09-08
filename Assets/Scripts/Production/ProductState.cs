namespace Game.Production
{
    /// <summary>
    /// Represents the sequential state of a pizza product as it moves through the production line,
    /// from raw dough to the final packaged product. Values are ordered to reflect the intended
    /// production sequence; do not reorder without updating any code that relies on ordinal comparisons.
    /// </summary>
    public enum ProductState
    {
        /// <summary>Unprocessed raw dough ingredients, not yet mixed.</summary>
        RawDough,

        /// <summary>Dough ingredients have been mixed into a uniform mass.</summary>
        MixedDough,

        /// <summary>Mixed dough has been divided into individual pizza-sized portions.</summary>
        PortionedDough,

        /// <summary>A dough portion has been shaped/pressed into a pizza base.</summary>
        FormedPizza,

        /// <summary>Sauce has been applied to the formed pizza base.</summary>
        SaucedPizza,

        /// <summary>Toppings have been applied on top of the sauce.</summary>
        ToppedPizza,

        /// <summary>The pizza has completed the baking process.</summary>
        BakedPizza,

        /// <summary>The baked pizza has been cooled to a safe handling/freezing temperature.</summary>
        CooledPizza,

        /// <summary>The cooled pizza has completed the freezing process.</summary>
        FrozenPizza,

        /// <summary>The frozen pizza has been packaged and is ready for shipping/storage.</summary>
        PackagedPizza
    }
}
