using UnityEngine;

namespace SmartTraining
{
    public static class RoomMovementView
    {
        public static Vector2 Draw(RoomNetworkClient network)
        {
            GUILayout.Label("방 안의 플레이어 — 초록색: 나 / 파란색: 다른 참가자");
            GUILayout.Label("WASD 또는 방향키로 이동하세요. 아래 이동 버튼을 누르고 있어도 이동합니다.");
            var players = network.Players;
            var map = GUILayoutUtility.GetRect(280, 280, GUILayout.ExpandWidth(false));
            GUI.Box(map, GUIContent.none);
            var oldColor = GUI.color;
            for (int i = 1; i < 4; i++)
            {
                GUI.color = new Color(1, 1, 1, 0.15f);
                GUI.DrawTexture(new Rect(map.x + map.width * i / 4, map.y, 1, map.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(map.x, map.y + map.height * i / 4, map.width, 1), Texture2D.whiteTexture);
            }
            foreach (var player in players)
            {
                var position = player.ServerPosition;
                float x = map.x + 10 + (position.x / RoomPlayer.ArenaHalfSize + 1) * (map.width - 20) / 2;
                float y = map.y + 10 + (1 - position.z / RoomPlayer.ArenaHalfSize) * (map.height - 20) / 2;
                GUI.color = player.IsOwner ? new Color(0.2f, 0.85f, 0.45f) : new Color(0.25f, 0.6f, 1);
                GUI.DrawTexture(new Rect(x - 6, y - 6, 12, 12), Texture2D.whiteTexture);
                GUI.color = oldColor;
                GUI.Label(new Rect(Mathf.Clamp(x + 8, map.x + 2, map.xMax - 115), y - 22, 115, 22),
                    player.UserName + (player.UserId == network.LocalPlayer?.RoomMasterUserId ? " [방장]" : ""));
            }
            GUI.color = oldColor;
            GUILayout.Label("접속한 플레이어: " + players.Length);
            foreach (var player in players)
                GUILayout.Label((player.IsOwner ? "나: " : "참가자: ") + player.UserName +
                    (player.UserId == network.LocalPlayer?.RoomMasterUserId ? " [방장]" : "") +
                    "  ·  X " + player.ServerPosition.x.ToString("F1") + " / Z " + player.ServerPosition.z.ToString("F1"));
            if (network.LocalPlayer == null) GUILayout.Label("플레이어 생성과 접속 확정을 기다리는 중…");
            var direction = Vector2.zero;
            GUILayout.BeginHorizontal(); GUILayout.Space(80);
            if (GUILayout.RepeatButton("앞 ↑", GUILayout.Width(80))) direction.y++;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.RepeatButton("왼쪽 ←", GUILayout.Width(80))) direction.x--;
            if (GUILayout.RepeatButton("뒤 ↓", GUILayout.Width(80))) direction.y--;
            if (GUILayout.RepeatButton("오른쪽 →", GUILayout.Width(80))) direction.x++;
            GUILayout.EndHorizontal();
            return Vector2.ClampMagnitude(direction, 1);
        }
    }
}

