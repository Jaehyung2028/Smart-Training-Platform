using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining
{
    public sealed class UguiSelectionHighlight : MonoBehaviour
    {
        public Outline border;

        public Color normalColor, selectedColor;

        public void SetSelected(bool selected)
        {
            border.effectColor = selected ? selectedColor : normalColor;
            border.effectDistance = selected ? new Vector2(2, -2) : new Vector2(1, -1);
        }
    }
}
