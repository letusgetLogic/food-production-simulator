namespace Game.Production
{
    /// <summary>
    /// Represents the sequential state of a pizza product as it moves through the production line,
    /// from raw dough to the final packaged product. Values are ordered to reflect the intended
    /// production sequence; do not reorder without updating any code that relies on ordinal comparisons.
    ///
    /// Line order as in real frozen pizza production: the formed base is pre-baked, cooled, then sauce and
    /// topping are applied and the whole pizza is shock frozen and packaged.
    /// Serialized assets (configs, prefabs) store the numeric value - reordering requires updating them.
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

        /// <summary>The formed base has been pre-baked in the oven (no sauce or topping yet).</summary>
        BakedPizza,

        /// <summary>The pre-baked base has been cooled down so sauce and topping can be applied.</summary>
        CooledPizza,

        /// <summary>
        /// Dosing in progress: the first ingredient (tomato sauce) is on the cooled, pre-baked base, further
        /// dispensers follow. Which ingredients are on the pizza: <see cref="ProductInstance.Doses"/>.
        /// </summary>
        SaucedPizza,

        /// <summary>All ingredients of the dosing station have been applied (e.g. sauce and mozzarella).</summary>
        ToppedPizza,

        /// <summary>The topped pizza has been shock frozen.</summary>
        FrozenPizza,

        /// <summary>The frozen pizza has been packaged and is ready for shipping/storage.</summary>
        PackagedPizza
    }
}
