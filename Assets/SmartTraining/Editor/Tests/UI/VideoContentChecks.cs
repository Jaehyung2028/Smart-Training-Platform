using System;
using UnityEditor;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class VideoContentChecks
    {
        [MenuItem("Smart Training/Content/Check Video Clock")]
        public static void Run()
        {
            var paused = new VideoPlaybackState(3, 10, 12, false);
            var playing = new VideoPlaybackState(3, 10, 12, true);
            Check(paused.Evaluate(100) == 3 && !paused.IsPlaying(100), "일시정지 위치는 시간이 지나도 유지됩니다.");
            Check(playing.Evaluate(12) == 5 && playing.IsPlaying(12), "서버 경과 시간으로 같은 영상 위치를 계산합니다.");
            Check(playing.Evaluate(9) == 3, "시작 시각 이전에는 재생 위치를 뒤로 보내지 않습니다.");
            Check(playing.Evaluate(30) == 12 && !playing.IsPlaying(30), "영상 끝에서 위치를 고정하고 정지합니다.");
            Check(new VideoPlaybackState().Evaluate(100) == 0, "메타데이터가 없으면 0초에서 대기합니다.");
            Check(new VideoPlaybackState(-3, 0, 12, false).Evaluate(0) == 0 &&
                new VideoPlaybackState(20, 0, 12, false).Evaluate(0) == 12, "구간 이동 범위를 영상 길이 안으로 제한합니다.");
            Debug.Log("VIDEO CLOCK CHECKS PASSED (6)");
        }

        private static void Check(bool result, string message)
        {
            if (!result)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
