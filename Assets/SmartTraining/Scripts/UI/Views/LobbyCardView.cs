using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining
{
    public sealed class LobbyCardView : MonoBehaviour
    {
        public Text title, state, metrics, selection;

        public Image accent;

        public Outline border;

        public Color ink, muted, highlight, line;

        public void Render(LobbySummary lobby, bool selected)
        {
            title.text = lobby.name;
            title.color = ink;
            bool scheduled = lobby.operatingMode == "scheduled";
            bool maintenance = lobby.isMaintenance || lobby.operatingMode == "maintenance";
            state.text = scheduled ? "종료 예정" : maintenance ? "점검 중" : "입장 가능";
            state.color = scheduled ? new Color32(146, 64, 14, 255) : maintenance ? new Color32(185, 28, 28, 255) : new Color32(21, 128, 61, 255);
            metrics.text = "사용자 " + lobby.userCount + "명   ·   방 " + lobby.roomCount + "개";
            metrics.color = muted;
            selection.text = selected ? "선택됨" : "";
            selection.color = highlight;
            accent.enabled = selected;
            accent.color = highlight;
            border.effectColor = selected ? highlight : line;
        }
    }
}
