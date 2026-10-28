using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining
{
    public sealed class RoomParticipantCard : MonoBehaviour
    {
        public Text details, role, readiness, learning;

        public GameObject statusRow;

        public Outline border;

        public Color ink, muted, accent, line;

        public void RenderRoom(RoomPlayer player, bool master)
        {
            statusRow.SetActive(true);
            role.text = (player.IsOwner ? "나 · " : "") + (master ? "방장" : "참가자");
            role.color = master || player.IsOwner ? accent : muted;
            readiness.text = player.RoomIsRunning ? "진행 중" : master ? "시작 대기" : player.IsReady ? "준비 완료" : "준비 전";
            readiness.color = master ? accent : player.IsReady ? new Color32(21, 128, 61, 255) : muted;
            details.text = player.UserName + "\n" + player.ConnectionStatusLabel + " · " + player.LatencyLabel + "\n" + (player.GetComponent<TrainingProgress>()?.Label ?? "학습 미시작");
            if (learning != null)
            {
                var progress = player.GetComponent<TrainingProgress>();
                details.text = player.UserName + "\n" + player.ConnectionStatusLabel + " · " + player.LatencyLabel;
                learning.gameObject.SetActive(true);
                learning.text = progress?.Label ?? "학습 미시작";
                learning.color = progress?.IsComplete == true ? new Color32(21, 128, 91, 255) : muted;
            }
            details.color = ink;
            border.effectColor = player.IsOwner ? new Color32(191, 211, 246, 255) : line;
        }

        public void RenderLobby(string label, bool self)
        {
            statusRow.SetActive(false);
            if (learning != null) { learning.gameObject.SetActive(false); }
            details.text = label;
            details.color = ink;
            border.effectColor = self ? accent : line;
        }
    }
}
