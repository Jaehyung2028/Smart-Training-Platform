using System;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class ContentAddressablesSetup
    {
        // 기본 프리팹과 추가 에셋을 등록할 Addressables 설정을 만듭니다. 이미 있으면 그대로 사용합니다.
        [MenuItem("Smart Training/Prepare Addressable Content")]
        public static void Prepare()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                throw new InvalidOperationException("Addressables 설정을 만들지 못했습니다.");
            }

            // 프로젝트 이름 해시(32자) 대신 짧은 접두사를 사용합니다.
            // ASP.NET 출력 폴더로 복사할 때 Visual Studio의 Windows 경로 길이 제한을 넘지 않도록 합니다.
            settings.MonoScriptBundleNaming = MonoScriptBundleNaming.Custom;
            settings.MonoScriptBundleCustomNaming = "st";
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("콘텐츠 Addressables 설정이 준비됐습니다. Groups 창에서 프리팹의 Address를 지정해주세요.");
        }
    }
}
