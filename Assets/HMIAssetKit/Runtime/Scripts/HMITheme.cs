using UnityEngine;

namespace HMIAssetKit
{
    [CreateAssetMenu(menuName = "HMI/Theme", fileName = "HMITheme")]
    public sealed class HMITheme : ScriptableObject
    {
        [Header("Surfaces")]
        public Color background = new(0.078f, 0.094f, 0.110f, 1f);
        public Color surface = new(0.106f, 0.125f, 0.145f, 1f);
        public Color surfaceRaised = new(0.145f, 0.165f, 0.188f, 1f);
        public Color border = new(0.165f, 0.188f, 0.220f, 1f);
        public Color text = new(0.843f, 0.863f, 0.890f, 1f);
        public Color textMuted = new(0.490f, 0.529f, 0.569f, 1f);

        [Header("Status")]
        public Color running = new(0.247f, 0.749f, 0.435f, 1f);
        public Color warning = new(0.910f, 0.694f, 0.235f, 1f);
        public Color fault = new(0.851f, 0.290f, 0.267f, 1f);
        public Color idle = new(0.337f, 0.373f, 0.408f, 1f);
        public Color info = new(0.180f, 0.600f, 0.910f, 1f);
    }
}
