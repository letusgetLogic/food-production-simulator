using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Momentary button on the Dosierstation panel: refills one unit's reservoir completely.
    /// Crosshair text "Refill: Mozzarella".
    /// </summary>
    public class DosingRefillButton : PushButton
    {
        [Tooltip("The dispenser unit this button refills. Empty = the DosingUnit this button sits in.")]
        [SerializeField] private DosingUnit _unit;

        public DosingUnit Unit => _unit != null ? _unit : GetComponentInParent<DosingUnit>();

        public override string InteractionText
        {
            get
            {
                DosingUnit unit = Unit;
                return unit != null ? $"{base.InteractionText}: {unit.DisplayName}" : base.InteractionText;
            }
        }

        protected override void OnPressed()
        {
            DosingUnit unit = Unit;
            DosingMachine machine = unit != null ? unit.GetComponentInParent<DosingMachine>() : null;
            if (machine != null)
            {
                machine.TryRefill(unit);
            }
        }
    }
}
