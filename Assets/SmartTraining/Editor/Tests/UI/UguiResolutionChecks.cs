using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining.Editor
{
    // Uses disposable preview instances; never saves the scene or changes a user's layout.
    public static class UguiResolutionChecks
    {
        public static void ApplyDefaults()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Use an isolated batch project.");
            foreach (var path in new[] { LobbyUguiBuilder.AssetPath, LobbyInteriorUguiBuilder.InteriorPath, RoomUguiBuilder.AssetPath })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    root.GetComponent<CanvasScaler>().screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                    var login = root.GetComponent<LobbyUguiView>();
                    if (login != null)
                    {
                        login.failurePanel.transform.SetParent(login.lobbyContent, false);
                        login.failurePanel.transform.SetAsFirstSibling();
                    }
                    var interior = root.GetComponent<LobbyInteriorUguiView>();
                    if (interior != null)
                    {
                        var input = interior.chatInput.GetComponent<LayoutElement>();
                        var row = interior.chatInput.transform.parent.GetComponent<LayoutElement>();
                        input.minHeight = input.preferredHeight = row.preferredHeight;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
        }
        public static void Run()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Use an isolated batch project.");
            var scene = EditorSceneManager.OpenScene(UguiSceneDesigner.ClientScene);
            var layout = UguiSceneDesigner.InScene<UguiSceneLayout>(scene).Single();
            int checks = 0, failures = 0;
            try
            {
                foreach (var size in new[] { new Vector2(800, 600), new Vector2(960, 720), new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(2560, 1080), new Vector2(720, 1280) })
                    foreach (int variant in Enumerable.Range(0, 14))
                    {
                        var mode = variant < 10 ? (UguiPreviewScreen)variant : variant == 10 ? UguiPreviewScreen.LobbyList : variant == 13 ? UguiPreviewScreen.Room : UguiPreviewScreen.LobbyInterior;
                        UguiScenePreview.Show(layout, mode);
                        layout.interiorView.notice.gameObject.SetActive(false);
                        layout.interiorView.joinPassword.gameObject.SetActive(false);
                        layout.roomView.operationNoticePanel.SetActive(false);
                        string scenario = variant < 10 ? mode.ToString() : new[] { "LobbyFailure", "ReconnectPassword", "NoticeAndReconnect", "RoomNotice" }[variant - 10];
                        if (variant == 10)
                            layout.loginView.failurePanel.SetActive(true);
                        if (variant == 11 || variant == 12)
                        {
                            layout.interiorView.reconnectPanel.SetActive(true);
                            layout.interiorView.joinPassword.gameObject.SetActive(true);
                        }
                        if (variant == 12)
                            layout.interiorView.notice.gameObject.SetActive(true);
                        if (variant == 13)
                            layout.roomView.operationNoticePanel.SetActive(true);
                        var canvas = new[] { layout.loginView.canvas, layout.interiorView.canvas, layout.roomView.canvas }.Single(x => x.enabled);
                        var scaler = canvas.GetComponent<CanvasScaler>();
                        var reference = scaler.referenceResolution;
                        float scale = scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand
                            ? Mathf.Min(size.x / reference.x, size.y / reference.y)
                            : Mathf.Pow(2, Mathf.Lerp(Mathf.Log(size.x / reference.x, 2), Mathf.Log(size.y / reference.y, 2), scaler.matchWidthOrHeight));
                        scaler.enabled = false;
                        canvas.renderMode = RenderMode.WorldSpace;
                        var root = (RectTransform)canvas.transform;
                        root.localScale = Vector3.one;
                        root.sizeDelta = size / scale;
                        Canvas.ForceUpdateCanvases();
                        foreach (var group in canvas.GetComponentsInChildren<LayoutGroup>())
                            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)group.transform);
                        Canvas.ForceUpdateCanvases();
                        var issues = new System.Collections.Generic.List<string>();
                        foreach (var group in canvas.GetComponentsInChildren<LayoutGroup>())
                        {
                            // Scrolling content may extend beyond its viewport, but ordinary layout children may not extend beyond their panel.
                            var parent = (RectTransform)group.transform;
                            if (parent.GetComponent<ContentSizeFitter>() != null)
                                continue;
                            foreach (RectTransform child in parent)
                            {
                                if (!child.gameObject.activeInHierarchy)
                                    continue;
                                var element = child.GetComponent<LayoutElement>();
                                if (element != null && element.ignoreLayout)
                                    continue;
                                var corners = new Vector3[4];
                                child.GetWorldCorners(corners);
                                foreach (var corner in corners)
                                {
                                    var point = parent.InverseTransformPoint(corner);
                                    if (point.x < parent.rect.xMin - 1 || point.x > parent.rect.xMax + 1 || point.y < parent.rect.yMin - 1 || point.y > parent.rect.yMax + 1)
                                    {
                                        issues.Add(parent.name + "/" + child.name);
                                        break;
                                    }
                                }
                            }
                        }
                        checks++;
                        foreach (var scroll in canvas.GetComponentsInChildren<ScrollRect>())
                            if (scroll.viewport.rect.width < 20 || scroll.viewport.rect.height < 20)
                                issues.Add("Collapsed scroll viewport: " + scroll.name);
                        if (issues.Count > 0)
                        {
                            failures++;
                            Debug.LogWarning("RESOLUTION FAIL: " + size + " " + scenario + " " + string.Join(", ", issues.Distinct()));
                        }
                        else
                            Debug.Log("RESOLUTION PASS: " + size + " " + scenario);
                    }
                Debug.Log("RESOLUTION CHECKS COMPLETE: " + checks + " scenarios, " + failures + " failures");
                if (failures > 0)
                    throw new InvalidOperationException("UI layout overflow detected.");
            }
            finally { UguiScenePreview.Restore(); EditorSceneManager.OpenScene(UguiSceneDesigner.ClientScene); }
        }
    }
}
