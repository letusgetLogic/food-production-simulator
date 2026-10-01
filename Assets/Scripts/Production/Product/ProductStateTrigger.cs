using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Marks a collider that switches a product from one <see cref="ProductState"/> to the next on
    /// contact - e.g. the piston top of the press (PortionedDough -> FormedPizza). Further stations
    /// get their own trigger: sauce nozzle (FormedPizza -> SaucedPizza), topping dispenser
    /// (SaucedPizza -> ToppedPizza), oven (ToppedPizza -> BakedPizza).
    ///
    /// Has no logic of its own - the product (<see cref="PizzaStateVisual"/>) reacts in its
    /// OnTriggerEnter. Needs a Collider on the same GameObject or a child. The collider is a
    /// trigger on the product side, so this one can stay a normal collider.
    /// </summary>
    public class ProductStateTrigger : MonoBehaviour
    {
        [Tooltip("Product must be in this state for the switch to happen.")]
        [SerializeField] private ProductState _fromState = ProductState.PortionedDough;

        [Tooltip("State the product switches to on contact.")]
        [SerializeField] private ProductState _toState = ProductState.FormedPizza;

        public ProductState FromState => _fromState;
        public ProductState ToState => _toState;
    }
}
