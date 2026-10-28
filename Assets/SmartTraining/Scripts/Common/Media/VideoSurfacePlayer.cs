using System;
using UnityEngine;
using UnityEngine.Video;

namespace SmartTraining
{
    // 로컬 디코딩·화면·소리·재생 위치 보정만 담당합니다. 시계 값은 호출자가 전달하며 네트워크와 콘텐츠를 모릅니다.
    public sealed class VideoSurfacePlayer : IDisposable
    {
        private readonly VideoPlayer player;
        private readonly AudioSource audioSource;
        private readonly VideoClip clip;
        private readonly Renderer display;
        private RenderTexture texture;
        private Material material, originalMaterial;
        private bool initialized, seeking, expectedPlaying;
        private double seekTarget;
        private float preparedAt, nextCheck, seekStartedAt;
        private float volume = 1;
        private string error = "";

        public VideoSurfacePlayer(VideoPlayer player, AudioSource audioSource, VideoClip clip, Renderer display)
        {
            this.player = player;
            this.audioSource = audioSource;
            this.clip = clip;
            this.display = display;
        }


        public bool IsReady => initialized && error.Length == 0 && player != null && player.isPrepared;

        public string Error => error;

        public double LocalTime => IsReady ? player.time : 0;

        public double Length => IsReady ? player.length : 0;

        public float Volume => volume;


        public void Update(double position, double duration, bool playing)
        {
            expectedPlaying = playing;
            if (!initialized || error.Length > 0)
            {
                return;
            }

            if (!player.isPrepared)
            {
                if (Time.unscaledTime - preparedAt > 30)
                {
                    OnError(player, "영상 준비 시간이 초과됐습니다.");
                }

                return;
            }

            if (duration <= 0)
            {
                return;
            }

            if (Math.Abs(player.length - duration) > 0.25)
            {
                OnError(player, "전달받은 영상 길이와 클립이 다릅니다. 같은 콘텐츠 에셋을 사용해주세요.");
                return;
            }

            SynchronizePlayback(position, duration, playing);
        }


        public bool TryInitialize(out string message)
        {
            if (player == null || audioSource == null || clip == null || display == null || display.sharedMaterial == null)
            {
                message = "영상 프리팹에 VideoClip과 화면 Renderer를 연결해주세요.";
                return false;
            }

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0;
            audioSource.volume = volume;
            player.playOnAwake = false;
            player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            player.isLooping = false;
            // Unity 6000.4의 Windows 디코더에서 프레임 건너뛰기가 오디오 버퍼 초과를 유발할 수 있습니다.
            // 재생 위치는 외부에서 전달받은 기준 시각으로 맞추므로 디코더의 프레임 건너뛰기는 사용하지 않습니다.
            player.skipOnDrop = false;
            player.waitForFirstFrame = true;
            player.source = VideoSource.VideoClip;
            player.clip = clip;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.aspectRatio = VideoAspectRatio.FitInside;
            CreateVideoSurface();
            ConfigureAudio();
            player.errorReceived += OnError;
            player.sendFrameReadyEvents = true;
            player.frameReady += OnFrameReady;
            initialized = true;
            preparedAt = Time.unscaledTime;
            player.Prepare();
            message = null;
            return true;
        }


        private void CreateVideoSurface()
        {
            int width = Mathf.Clamp((int)clip.width, 16, 1920);
            int height = Mathf.Max(16, Mathf.RoundToInt(width * clip.height / (float)Math.Max(1u, clip.width)));
            texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "Local Video Surface"
            };
            texture.Create();
            var previousTarget = RenderTexture.active;
            Graphics.Blit(Texture2D.blackTexture, texture);
            RenderTexture.active = previousTarget;
            player.targetTexture = texture;
            originalMaterial = display.sharedMaterial;
            material = new Material(originalMaterial)
            {
                name = originalMaterial.name + " (Local Video)"
            };
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }

            display.sharedMaterial = material;
        }


        private void ConfigureAudio()
        {
            player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            if (clip.audioTrackCount > 0)
            {
                player.controlledAudioTrackCount = 1;
                player.EnableAudioTrack(0, true);
                player.SetTargetAudioSource(0, audioSource);
            }
        }


        private void SynchronizePlayback(double position, double duration, bool playing)
        {
            double target = Math.Min(position, Math.Max(0, player.length - 1 / Math.Max(1, player.frameRate)));
            bool shouldPlay = playing && position < duration - 1 / Math.Max(1, player.frameRate);
            if (!shouldPlay && !seeking && player.isPlaying)
            {
                player.Pause();
            }

            if (seeking && Time.unscaledTime - seekStartedAt > 3)
            {
                seeking = false;
                audioSource.mute = false;
            }

            if (!seeking && Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + 0.5f;
                // 영상은 프레임 단위로 멈춥니다. 시간 오차를 지나치게 작게 잡으면 같은 정지 위치를 계속 탐색합니다.
                double tolerance = shouldPlay ? 0.35 : Math.Max(0.06, 2 / Math.Max(1, player.frameRate) + 0.01);
                if (Math.Abs(player.time - target) > tolerance && player.canSetTime)
                {
                    seeking = true;
                    seekStartedAt = Time.unscaledTime;
                    seekTarget = target;
                    // 일부 네이티브 디코더는 정지 상태에서 탐색 프레임을 갱신하지 않아 재생을 잠시 진행합니다.
                    // 탐색 중 소리는 끄고 완료되면 공통 재생 상태에 맞춰 다시 정지합니다.
                    audioSource.mute = !shouldPlay;
                    if (!player.isPlaying)
                    {
                        player.Play();
                    }

                    player.time = target;
                }
            }

            if (!seeking && shouldPlay && !player.isPlaying)
            {
                player.Play();
            }
        }


        private void OnFrameReady(VideoPlayer source, long frameIndex)
        {
            if (!seeking || Math.Abs(frameIndex / Math.Max(1, source.frameRate) - seekTarget) > 0.15)
            {
                return;
            }

            seeking = false;
            if (!expectedPlaying)
            {
                source.Pause();
            }

            audioSource.mute = false;
        }


        private void OnError(VideoPlayer source, string message)
        {
            error = "영상 재생 오류: " + message;
            source.Stop();
            Debug.LogWarning(error);
        }


        public void SetVolume(float value)
        {
            volume = Mathf.Clamp01(value);
            if (audioSource != null)
            {
                audioSource.volume = volume;
            }
        }


        public void Dispose()
        {
            if (player != null)
            {
                player.errorReceived -= OnError;
                player.frameReady -= OnFrameReady;
                player.Stop();
                player.targetTexture = null;
            }

            if (display != null && display.sharedMaterial == material)
            {
                display.sharedMaterial = originalMaterial;
            }

            if (material != null)
            {
                UnityEngine.Object.Destroy(material);
            }

            if (texture != null)
            {
                if (RenderTexture.active == texture)
                {
                    RenderTexture.active = null;
                }

                texture.Release();
                UnityEngine.Object.Destroy(texture);
            }

            initialized = false;
        }
    }
}
