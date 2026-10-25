using UnityEngine;

namespace SmartTraining
{
    [CreateAssetMenu(menuName = "Smart Training/UI Theme")]
    public sealed class UguiTheme : ScriptableObject
    {
        public Color background = new Color32(15, 23, 42, 255);

        public Color surface = new Color32(248, 250, 252, 255);

        public Color field = Color.white;

        public Color ink = new Color32(30, 41, 59, 255);

        public Color muted = new Color32(100, 116, 139, 255);

        public Color accent = new Color32(37, 99, 235, 255);

        public Color accentHover = new Color32(29, 78, 216, 255);

        public Color accentPressed = new Color32(30, 64, 175, 255);

        public Color line = new Color32(203, 213, 225, 255);

        public Color secondary = new Color32(226, 232, 240, 255);

        [Min(12)]
        public int bodySize = 16;

        [Min(36)]
        public int controlHeight = 44;
    }
}
