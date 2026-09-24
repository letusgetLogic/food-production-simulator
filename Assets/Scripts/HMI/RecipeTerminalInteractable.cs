using Game.Core;
using UnityEngine;

namespace Game.HMI
{
    /// <summary>
    /// Terminal at a recipe pillar. Several of these stand around the hall; all
    /// open the same shared "recipe" panel showing the same active recipe - no
    /// per-terminal state, no machine binding.
    /// </summary>
    public class RecipeTerminalInteractable : InteractableBase
    {
        [SerializeField] private HmiScreenController _screen;
        [SerializeField] private string _panelId = "recipe";

        protected override void OnInteract(IInteractor interactor)
        {
            if (_screen == null)
            {
                Debug.LogError($"{nameof(RecipeTerminalInteractable)}: no screen assigned.", this);
                return;
            }

            _screen.Open(_panelId);
        }
    }
}