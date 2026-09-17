using UnityEngine;
using UnityEngine.UIElements;

namespace HMIAssetKit
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class HMIThemeInstaller : MonoBehaviour
    {
        [SerializeField] private HMITheme theme;
        private UIDocument document;

        private void Awake()
        {
            document = GetComponent<UIDocument>();
            Apply();
        }

        public void Apply()
        {
            if (document == null || document.rootVisualElement == null) return;
            var root = document.rootVisualElement;
            root.style.backgroundColor = theme != null ? theme.background : new Color(0.078f, 0.094f, 0.110f);
            root.style.color = theme != null ? theme.text : Color.white;
            root.style.unityFontDefinition = StyleKeyword.None;
        }
    }
}
