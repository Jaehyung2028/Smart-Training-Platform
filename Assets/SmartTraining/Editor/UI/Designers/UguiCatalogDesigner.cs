using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static SmartTraining.Editor.LobbyUguiBuilder;

namespace SmartTraining.Editor
{
    public static class UguiCatalogDesigner
    {
        [MenuItem("Smart Training/UI/Apply Lobby Catalog Theme")]
        public static void ApplyCatalogTheme()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var root=PrefabUtility.LoadPrefabContents(AssetPath);
            try { Apply(root,UguiLoginDesigner.LoadTheme());PrefabUtility.SaveAsPrefabAsset(root,AssetPath); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("CATALOG THEME APPLIED: existing lobby actions and references preserved");
        }

        public static void Apply(GameObject root,UguiTheme theme)
        {
            var ui=root.GetComponent<LobbyUguiView>();
            ui.userText.fontSize=22;ui.userText.fontStyle=FontStyle.Bold;ui.userText.color=theme.ink;
            ui.selectedDetail.fontSize=16;ui.selectedDetail.color=theme.ink;
            // Keep long maintenance reasons available without pushing the cards or entry button off screen.
            var detailScroll=ui.selectedDetail.GetComponentInParent<ScrollRect>(true);
            if(detailScroll==null)
            {
                int index=ui.selectedDetail.transform.GetSiblingIndex();
                var details=Node("Selected Lobby Details",ui.lobbyPanel.transform);details.SetSiblingIndex(index);Height(details.gameObject,80);
                var viewport=Node("Viewport",details);Stretch(viewport);viewport.gameObject.AddComponent<Image>().color=Color.clear;
                viewport.gameObject.AddComponent<RectMask2D>();
                var text=ui.selectedDetail.rectTransform;text.SetParent(viewport,false);
                Object.DestroyImmediate(text.GetComponent<LayoutElement>());
                text.anchorMin=new Vector2(0,1);text.anchorMax=Vector2.one;text.pivot=new Vector2(.5f,1);text.sizeDelta=Vector2.zero;
                text.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
                detailScroll=details.gameObject.AddComponent<ScrollRect>();detailScroll.content=text;detailScroll.viewport=viewport;
                detailScroll.horizontal=false;detailScroll.movementType=ScrollRect.MovementType.Clamped;detailScroll.scrollSensitivity=24;
            }
            foreach(var button in new[]{ui.refreshButton,ui.reconnectButton,ui.logoutButton,ui.clearPendingButton,ui.enterButton,ui.cancelButton,ui.retryButton,ui.closeFailureButton})
                StyleButton(button,theme,button==ui.enterButton||button==ui.retryButton);
            var contentLayout=ui.lobbyContent.GetComponent<VerticalLayoutGroup>();contentLayout.spacing=10;contentLayout.padding=new RectOffset(8,8,8,8);
            var scroll=ui.lobbyContent.GetComponentInParent<ScrollRect>(true);scroll.GetComponent<Image>().color=theme.surface;
            scroll.viewport.GetComponent<Image>().color=theme.surface;
            ui.emptyText.color=theme.muted;ui.emptyText.fontSize=16;
            ui.failureText.color=theme.ink;
            var buttonRoot=ui.lobbyRowTemplate;
            var row=buttonRoot.GetComponent<LobbyCardView>();
            if(row==null)
            {
                row=buttonRoot.gameObject.AddComponent<LobbyCardView>();
                row.title=buttonRoot.GetComponentInChildren<Text>(true);
                row.state=Label("입장 가능",buttonRoot.transform,14,24);
                row.metrics=Label("사용자 0명   ·   입장 대기 0명   ·   방 0개",buttonRoot.transform,15,24);
                row.selection=Label("",buttonRoot.transform,14,24);
                var bar=Node("Selected Accent",buttonRoot.transform);row.accent=bar.gameObject.AddComponent<Image>();row.accent.raycastTarget=false;
                bar.anchorMin=Vector2.zero;bar.anchorMax=new Vector2(0,1);bar.offsetMin=Vector2.zero;bar.offsetMax=new Vector2(4,0);
                row.border=buttonRoot.GetComponent<Outline>()??buttonRoot.gameObject.AddComponent<Outline>();
            }
            row.ink=theme.ink;row.muted=theme.muted;row.highlight=theme.accent;row.line=theme.line;
            row.border.effectDistance=new Vector2(1,-1);
            Place(row.title,new Vector2(0,.48f),new Vector2(.74f,1),new Vector2(18,0),new Vector2(-8,-8));
            Place(row.state,new Vector2(.74f,.5f),Vector2.one,new Vector2(0,0),new Vector2(-18,-8));
            Place(row.metrics,Vector2.zero,new Vector2(.8f,.48f),new Vector2(18,8),new Vector2(0,0));
            Place(row.selection,new Vector2(.8f,0),new Vector2(1,.48f),new Vector2(0,8),new Vector2(-18,0));
            row.title.fontSize=20;row.title.fontStyle=FontStyle.Bold;row.state.fontSize=14;row.metrics.fontSize=15;
            row.state.alignment=row.selection.alignment=TextAnchor.MiddleRight;
            row.title.text="콘텐츠 로비";row.title.color=theme.ink;row.state.color=new Color32(21,128,61,255);row.metrics.color=theme.muted;
            row.accent.enabled=false;row.border.effectColor=theme.line;
            buttonRoot.GetComponent<Image>().color=Color.white;
            var colors=buttonRoot.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color32(239,246,255,255);
            colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color32(219,234,254,255);colors.disabledColor=new Color32(226,232,240,255);buttonRoot.colors=colors;
            var size=buttonRoot.GetComponent<LayoutElement>();size.minHeight=size.preferredHeight=88;
        }

        private static void Place(Text label,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax)
        {
            var size=label.GetComponent<LayoutElement>();if(size!=null)Object.DestroyImmediate(size);
            var rect=label.rectTransform;rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=offsetMin;rect.offsetMax=offsetMax;
            label.raycastTarget=false;label.supportRichText=false;
        }
        private static void StyleButton(Button button,UguiTheme theme,bool primary)
        {
            button.GetComponent<Image>().color=Color.white;
            var colors=button.colors;colors.normalColor=primary?theme.accent:theme.secondary;
            colors.highlightedColor=primary?theme.accentHover:theme.line;colors.selectedColor=colors.highlightedColor;
            colors.pressedColor=primary?theme.accentPressed:new Color32(148,163,184,255);
            colors.disabledColor=theme.line;button.colors=colors;
            var text=button.GetComponentInChildren<Text>();text.color=primary?Color.white:theme.ink;text.fontSize=theme.bodySize;
            text.fontStyle=primary?FontStyle.Bold:FontStyle.Normal;
        }
    }
}
