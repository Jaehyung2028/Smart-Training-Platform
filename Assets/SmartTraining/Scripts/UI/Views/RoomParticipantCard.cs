using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining
{
    public sealed class RoomParticipantCard : MonoBehaviour
    {
        public Text details, role, readiness;
        public GameObject statusRow;
        public Outline border;
        public Color ink, muted, accent, line;

        public void RenderRoom(RoomPlayer player, bool master)
        {
            statusRow.SetActive(true);
            role.text=(player.IsOwner?"나 · ":"")+(master?"방장":"참가자");
            role.color=master||player.IsOwner?accent:muted;
            readiness.text=player.IsReady?"준비 완료":"준비 전";
            readiness.color=player.IsReady?new Color32(21,128,61,255):muted;
            details.text=player.UserName+"\n"+player.ConnectionStatusLabel+" · "+player.LatencyLabel+
                "\nX "+player.ServerPosition.x.ToString("F1")+" / Z "+player.ServerPosition.z.ToString("F1");
            details.color=ink;border.effectColor=player.IsOwner?accent:line;
        }

        public void RenderLobby(string label, bool self)
        {
            statusRow.SetActive(false);details.text=label;details.color=ink;border.effectColor=self?accent:line;
        }
    }
}
