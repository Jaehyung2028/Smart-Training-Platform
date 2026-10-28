using UnityEngine;

namespace SmartTraining
{
    // 개인 조작·화면·음량 설정입니다. 서버에 보내지 않고 이 PC의 PlayerPrefs에 보관합니다.
    public sealed class ClientPreferences
    {
        private const string Prefix = "SmartTraining.Content.";
        private ClientPreferences original;

        public float Sensitivity { get; set; } = 1f;

        public float FieldOfView { get; set; } = 75f;

        public float Volume { get; set; } = 1f;

        public bool InvertY { get; set; }


        // 기존 저장 키는 유지합니다. 설정을 사용하는 씬과 관계없이 이 PC의 음량에 적용합니다.
        public void ApplyAudioVolume()
        {
            AudioListener.volume = Volume;
        }


        public void Load()
        {
            original = null;
            Sensitivity = ReadFloat("Sensitivity", 1f, 0.2f, 3f);
            FieldOfView = ReadFloat("FieldOfView", 75f, 60f, 100f);
            Volume = ReadFloat("Volume", 1f, 0f, 1f);
            InvertY = PlayerPrefs.GetInt(Prefix + "InvertY", 0) == 1;
        }


        // 저장 버튼에서만 호출합니다. 저장 전 미리보기 값은 종료 시 자동 저장하지 않습니다.
        public void Save()
        {
            original = null;
            PlayerPrefs.SetFloat(Prefix + "Sensitivity", Sensitivity);
            PlayerPrefs.SetFloat(Prefix + "FieldOfView", FieldOfView);
            PlayerPrefs.SetFloat(Prefix + "Volume", Volume);
            PlayerPrefs.SetInt(Prefix + "InvertY", InvertY ? 1 : 0);
            PlayerPrefs.Save();
        }


        // 설정 창을 연 시점의 값을 보관합니다. 기본값 복원도 저장 전까지는 취소할 수 있습니다.
        public void BeginEdit()
        {
            if (original != null)
            {
                return;
            }

            original = new ClientPreferences
            {
                Sensitivity = Sensitivity,
                FieldOfView = FieldOfView,
                Volume = Volume,
                InvertY = InvertY
            };
        }


        public void CancelEdit()
        {
            if (original == null)
            {
                return;
            }

            Sensitivity = original.Sensitivity;
            FieldOfView = original.FieldOfView;
            Volume = original.Volume;
            InvertY = original.InvertY;
            original = null;
        }


        public void Reset()
        {
            Sensitivity = 1f;
            FieldOfView = 75f;
            Volume = 1f;
            InvertY = false;
        }


        private static float ReadFloat(string key, float fallback, float minimum, float maximum)
        {
            float value = PlayerPrefs.GetFloat(Prefix + key, fallback);
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, minimum, maximum);
        }
    }
}
