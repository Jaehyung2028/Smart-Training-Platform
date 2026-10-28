using System;
using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining
{
    // 채팅 DTO를 텍스트와 색상으로 표시하는 UI 전용 처리입니다.
    internal static class ChatMessagePresentation
    {
        // 같은 닉네임이 아니라 서버 사용자 ID로 본인 메시지를 구분합니다. 다른 사용자는 프리팹의 기본 색상을 유지합니다.
        internal static void SetChatMessage(Text label, LobbyChatMessage message, string localUserId, Color defaultColor)
        {
            label.text = ChatLabel(message);
            label.color = !string.IsNullOrEmpty(localUserId) && message.userId == localUserId ? new Color32(21, 128, 61, 255) : defaultColor;
        }


        internal static string ChatLabel(LobbyChatMessage message)
        {
            string time = DateTimeOffset.TryParse(message.sentAt, out var sent) ? sent.ToLocalTime().ToString("HH:mm:ss") : "";
            return "[" + time + "] " + message.nickname + ": " + message.text;
        }
    }
}
