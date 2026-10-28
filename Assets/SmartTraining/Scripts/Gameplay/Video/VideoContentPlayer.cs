using System;
using UnityEngine;
using UnityEngine.Video;

namespace SmartTraining
{
    // 콘텐츠 프리팹의 리소스와 방의 공유 시계를 연결하는 조정자입니다. 실제 디코딩·렌더링은 공통 모듈에 맡깁니다.
    [RequireComponent(typeof(VideoPlayer), typeof(AudioSource))]
    public sealed class VideoContentPlayer : MonoBehaviour
    {
        [SerializeField]
        private VideoClip clip;
        [SerializeField]
        private Renderer display;
        [SerializeField, Tooltip("UTF-8 SRT 자막을 .srt.txt 파일로 저장하여 연결합니다.")]
        private TextAsset subtitleFile;
        private VideoSurfacePlayer surface;
        private VideoSubtitles subtitles;
        private RoomPlayer localPlayer;
        private SharedVideo shared;
        private bool subtitlesEnabled = true;
        private float nextConfigure;
        private float volume = 1;
        private VideoWatchCoverage watchCoverage = new();
        private int watchRun = -1;

        public bool IsReady => surface?.IsReady == true;

        public string Error => surface?.Error ?? "";

        public double LocalTime => surface?.LocalTime ?? 0;

        public double Duration => shared != null && shared.IsSpawned ? shared.Duration : 0;

        public double Position => shared != null && shared.IsSpawned ? shared.Position : 0;

        public bool IsPlaying => shared != null && shared.IsSpawned && shared.IsPlaying;

        public float Volume => surface?.Volume ?? volume;

        public bool HasSubtitles => subtitles?.HasCues == true;

        public bool SubtitlesEnabled => subtitlesEnabled;

        // 공통 재생 시각으로 자막을 조회하며 초기·정지 상태의 0초에서는 숨깁니다.
        public string SubtitleText => subtitlesEnabled && IsReady && Duration > 0 && Position < Duration && (IsPlaying || Position > 0) ? subtitles?.GetText(Position) ?? "" : "";

        public bool CanControl => IsReady &&
            Duration > 0 &&
            localPlayer != null &&
            localPlayer.IsSpawned &&
            localPlayer.RoomIsRunning &&
            !localPlayer.PhasePending;

        public string Status => Error.Length > 0 ? Error : !IsReady ? "영상을 준비하고 있습니다…" : Duration <= 0 ? "방장의 영상 준비를 기다리고 있습니다…" : IsPlaying ? "재생 중" : Position >= Duration ? "영상이 끝났습니다." : "일시정지";


        public bool TryInitialize(out string message)
        {
            surface?.Dispose();
            try
            {
                subtitles = new VideoSubtitles(subtitleFile != null ? subtitleFile.text : "");
            }
            catch (FormatException exception)
            {
                subtitles = null;
                Debug.LogWarning("자막 파일을 확인해주세요: " + exception.Message, this);
            }

            surface = new VideoSurfacePlayer(GetComponent<VideoPlayer>(), GetComponent<AudioSource>(), clip, display);
            surface.SetVolume(volume);
            return surface.TryInitialize(out message);
        }


        internal void Bind(RoomPlayer participant)
        {
            localPlayer = participant;
            if ((shared == null || !shared.IsSpawned) && participant != null)
            {
                shared = SharedVideo.Find(participant.NetworkManager);
            }
        }


        private void Update()
        {
            surface?.Update(Position, Duration, IsPlaying);
            var progress = localPlayer != null ? localPlayer.GetComponent<TrainingProgress>() : null;
            if (progress != null && progress.Run != watchRun)
            {
                watchRun = progress.Run;
                watchCoverage = new VideoWatchCoverage();
            }
            if (progress != null && IsReady && localPlayer.RoomIsRunning)
            {
                watchCoverage.Sample(LocalTime, Duration, IsPlaying, Time.realtimeSinceStartupAsDouble);
                progress.ReportPersonal(watchCoverage.Percent);
            }
            if (IsReady &&
                shared != null &&
                shared.IsSpawned &&
                Duration <= 0 &&
                localPlayer != null &&
                localPlayer.IsRoomMaster &&
                !localPlayer.PhasePending &&
                Time.unscaledTime >= nextConfigure)
            {
                shared.ConfigureRpc(surface.Length);
                nextConfigure = Time.unscaledTime + 1;
            }
        }


        internal void ToggleSubtitles()
        {
            subtitlesEnabled = !subtitlesEnabled;
        }


        internal void TogglePlayback()
        {
            if (CanControl)
            {
                shared.SetPlayingRpc(!IsPlaying);
            }
        }


        internal void Seek(float normalized)
        {
            if (CanControl && !float.IsNaN(normalized) && !float.IsInfinity(normalized))
            {
                shared.SeekRpc(Mathf.Clamp01(normalized) * Duration);
            }
        }


        internal void Skip(int seconds)
        {
            if (CanControl)
            {
                shared.SkipRpc(seconds);
            }
        }


        internal void StopPlayback()
        {
            if (CanControl)
            {
                shared.StopRpc();
            }
        }


        internal void SetVolume(float value)
        {
            volume = Mathf.Clamp01(value);
            surface?.SetVolume(volume);
        }


        private void OnDisable()
        {
            surface?.Dispose();
            surface = null;
        }
    }
}
