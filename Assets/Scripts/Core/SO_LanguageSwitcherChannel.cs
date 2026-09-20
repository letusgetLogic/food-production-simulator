using System;
using UnityEngine;

namespace Game.Core
{
    [CreateAssetMenu(menuName = "Core/Language Switcher Channel", fileName = "LanguageSwitcherChannel")]
    public class SO_LanguageSwitcherChannel : ScriptableObject
    {
        public event Action LanguageChanged;

        public void NotifyLanguageChanged()
        {
            LanguageChanged?.Invoke();
        }
    }
}
