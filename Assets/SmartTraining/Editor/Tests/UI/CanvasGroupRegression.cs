using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace SmartTraining.Editor
{
    public static class CanvasGroupRegression
    {
        public static void Run()
        {
            LobbyUguiBuilder.EnsureAssets();
            LobbyInteriorUguiBuilder.EnsureAssets();
            RoomUguiBuilder.EnsureAssets();
            var owner = new GameObject("Canvas regression controller");
            owner.SetActive(false);
            try
            {
                var controller = owner.AddComponent<ApiLobbyScreen>();
                Check<LobbyUguiView>(LobbyUguiBuilder.AssetPath, controller);
                Check<LobbyInteriorUguiView>(LobbyInteriorUguiBuilder.InteriorPath, controller);
                Check<RoomUguiView>(RoomUguiBuilder.AssetPath, controller);
                Debug.Log("CANVAS GROUP REGRESSION PASSED: all three prefabs, repeated binding, destroyed cache recovery, hidden input blocking");
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }
        private static void Check<T>(string path, ApiLobbyScreen controller) where T : MonoBehaviour
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset.GetComponent<CanvasGroup>() == null) throw new Exception(path + " missing serialized root CanvasGroup");
            var instance = UnityEngine.Object.Instantiate(asset);
            var stale = new GameObject("Destroyed component cache", typeof(CanvasGroup));
            try
            {
                var view = instance.GetComponent<T>();
                var bind = typeof(T).GetMethod("Bind");
                var refresh = typeof(T).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic);
                var cache = typeof(T).GetField("interaction", BindingFlags.Instance | BindingFlags.NonPublic);
                bind.Invoke(view, new object[] {controller});
                cache.SetValue(view, stale.GetComponent<CanvasGroup>());
                UnityEngine.Object.DestroyImmediate(stale);
                refresh.Invoke(view, null);
                cache.SetValue(view, null);
                bind.Invoke(view, new object[] {controller});
                refresh.Invoke(view, null);
                var group = instance.GetComponent<CanvasGroup>();
                if (instance.GetComponents<CanvasGroup>().Length != 1 || group.interactable || group.blocksRaycasts)
                    throw new Exception(path + " invalid hidden input state or duplicate CanvasGroup");
            }
            finally
            {
                if (stale != null) UnityEngine.Object.DestroyImmediate(stale);
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}
