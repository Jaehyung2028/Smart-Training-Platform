using UnityEngine.EventSystems;
using UnityEngine;

namespace SmartTraining
{
    [DisallowMultipleComponent]
    public sealed class UguiSceneLayout : MonoBehaviour
    {
        public ApiLobbyScreen controller;
        public LobbyUguiView loginView;
        public LobbyInteriorUguiView interiorView;
        public RoomUguiView roomView;
        public EventSystem eventSystem;
    }
}
