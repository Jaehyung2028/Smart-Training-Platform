using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SmartTraining.Editor
{
    public static class UguiSceneChecks
    {
        private const string CopyPath="Assets/SmartTraining/Scenes/SceneUiCheckCopy.unity";
        private static int passed;
        private static void Check(bool condition,string name)
        { if(!condition)throw new InvalidOperationException(name);passed++;Debug.Log("SCENE UI PASS: "+name); }
        public static void Run()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("격리된 검사 프로젝트의 배치 모드에서 실행해주세요.");
            passed=0;
            var originalBytes=File.ReadAllBytes(UguiSceneDesigner.ClientScene);
            try
            {
                var scene=EditorSceneManager.OpenScene(UguiSceneDesigner.ClientScene);
                var layout=UguiSceneDesigner.InScene<UguiSceneLayout>(scene).Single();
                Check(UguiSceneDesigner.Validate(layout).Count==0,"saved scene has all view and input references");
                foreach(var view in new Component[]{layout.loginView,layout.interiorView,layout.roomView})
                    Check(PrefabUtility.IsPartOfPrefabInstance(view)&&PrefabUtility.GetCorrespondingObjectFromSource(view)!=null,view.name+" retains its editable prefab connection");
                var before=UguiSceneDesigner.InScene<Transform>(scene).Length;
                Check(UguiSceneDesigner.Install(scene)==layout&&UguiSceneDesigner.InScene<Transform>(scene).Length==before,"repeated placement reuses existing UI without duplication");
                var initial=new[]{layout.loginView.canvas.enabled,layout.interiorView.canvas.enabled,layout.roomView.canvas.enabled};
                var initialFont=layout.roomView.heading.font;
                foreach(UguiPreviewScreen mode in Enum.GetValues(typeof(UguiPreviewScreen)))
                {
                    UguiScenePreview.Show(layout,mode);
                    Check(new[]{layout.loginView.canvas,layout.interiorView.canvas,layout.roomView.canvas}.Count(canvas=>canvas.enabled)==1,"only one Canvas is visible for "+mode);
                }
                UguiScenePreview.Restore();
                Check(initial.SequenceEqual(new[]{layout.loginView.canvas.enabled,layout.interiorView.canvas.enabled,layout.roomView.canvas.enabled})&&layout.roomView.heading.font==initialFont,"preview restores Canvas state and original font references");
                Check(typeof(ApiLobbyScreen).GetField("api",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(layout.controller)==null,"previews do not create an API session or bind network actions");
                var originalSize=layout.roomView.heading.fontSize;
                UguiScenePreview.Show(layout,UguiPreviewScreen.RoomSettings);layout.roomView.heading.fontSize=originalSize+3;
                PrefabUtility.RecordPrefabInstancePropertyModifications(layout.roomView.heading);
                UguiScenePreview.Restore();
                Check(layout.roomView.heading.fontSize==originalSize+3,"design edits made during preview survive restoration");
                UguiScenePreview.Show(layout,UguiPreviewScreen.RoomConfirmation);
                EditorSceneManager.SaveScene(scene,CopyPath,true);
                Check(!UguiScenePreview.IsActive&&!layout.roomView.confirmationModal.activeSelf,"saving exits preview and restores modal visibility");
                var saved=File.ReadAllText(CopyPath);
                Check(!saved.Contains("방 관리 요청 확인 영역입니다."),"preview-only text is excluded from saved scene");
                var copy=EditorSceneManager.OpenScene(CopyPath);
                layout=UguiSceneDesigner.InScene<UguiSceneLayout>(copy).Single();
                Check(layout.roomView.heading.fontSize==originalSize+3&&UguiSceneDesigner.Validate(layout).Count==0,"saved scene preserves design override and valid prefab references");
                var ready=layout.roomView.ready;layout.roomView.ready=null;
                Check(UguiSceneDesigner.Validate(layout).Any(error=>error.Contains(".ready:")),"missing button reports its exact field name");
                bool blocked=false;
                try{new UguiSceneBuildCheck().OnProcessScene(copy,null);}catch(BuildFailedException){blocked=true;}
                Check(blocked,"build validation rejects missing required UI wiring");layout.roomView.ready=ready;
                var send=layout.roomView.sendChat;layout.roomView.sendChat=layout.interiorView.sendChat;
                Check(UguiSceneDesigner.Validate(layout).Any(error=>error.Contains(".sendChat:")&&error.Contains("다른 화면")),"cross-screen button wiring is detected");layout.roomView.sendChat=send;
                var move=layout.roomView.movementButtons[0];layout.roomView.movementButtons[0]=null;
                Check(UguiSceneDesigner.Validate(layout).Any(error=>error.Contains("movementButtons[0]")),"missing array element reports its exact index");layout.roomView.movementButtons[0]=move;
                var duplicate=UnityEngine.Object.Instantiate(layout.roomView,layout.transform);
                Check(UguiSceneDesigner.Validate(layout).Any(error=>error.Contains("각각 하나")),"duplicate room screens are detected");UnityEngine.Object.DestroyImmediate(duplicate.gameObject);
                UguiScenePreview.Show(layout,UguiPreviewScreen.CreateRoom);new UguiSceneBuildCheck().OnPreprocessBuild(null);
                Check(!UguiScenePreview.IsActive&&!layout.interiorView.createModal.activeSelf,"build preparation removes transient preview state");
                var window=EditorWindow.GetWindow<UguiSceneWindow>();UguiScenePreview.Show(layout,UguiPreviewScreen.Movement);window.Close();
                Check(!UguiScenePreview.IsActive&&!layout.roomView.mapModal.activeSelf,"closing preview window restores original display");
                Check(originalBytes.SequenceEqual(File.ReadAllBytes(UguiSceneDesigner.ClientScene)),"editor checks leave the actual client scene asset untouched");
                Debug.Log("SCENE UI CHECKS COMPLETE: "+passed+" checks");
            }
            finally
            {
                UguiScenePreview.Restore();EditorSceneManager.OpenScene(UguiSceneDesigner.ClientScene);
                AssetDatabase.DeleteAsset(CopyPath);
            }
        }
    }
}
