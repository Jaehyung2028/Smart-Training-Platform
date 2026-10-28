using System;
using UnityEditor;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class LearningProgressChecks
    {
        [MenuItem("Smart Training/Content/Check Learning Progress")]
        public static void Run()
        {
            var full = new VideoWatchCoverage();
            for (int index = 0; index <= 100; index++)
            {
                full.Sample(index / 10d, 10, index < 100, index / 10d);
            }

            Check(full.Percent == 100, "전체 시청 완료");
            var skip = new VideoWatchCoverage();
            skip.Sample(0, 10, true, 0);
            skip.Sample(9.9, 10, false, .1);
            Check(skip.Percent == 0, "끝으로 이동만 해서는 미완료");
            var repeat = new VideoWatchCoverage();
            for (int index = 0; index <= 50; index++)
            {
                repeat.Sample(index / 10d, 10, true, index / 10d);
            }

            double first = repeat.WatchedSeconds;
            repeat.Sample(0, 10, true, 5.1);
            for (int index = 1; index <= 50; index++)
            {
                repeat.Sample(index / 10d, 10, true, 5.1 + index / 10d);
            }

            Check(
                Math.Abs(repeat.WatchedSeconds - first) < .001 && repeat.Percent == 50,
                "반복 시청 중복 집계 방지"
            );
            var paused = new VideoWatchCoverage();
            paused.Sample(0, 10, false, 0);
            paused.Sample(5, 10, false, 1);
            Check(paused.WatchedSeconds == 0, "정지 상태 구간 이동 제외");
            var gap = new VideoWatchCoverage();
            for (int index = 0; index <= 40; index++)
            {
                gap.Sample(index / 10d, 10, true, index / 10d);
            }

            gap.Sample(6, 10, true, 4.1);
            for (int index = 1; index <= 40; index++)
            {
                gap.Sample(6 + index / 10d, 10, true, 4.1 + index / 10d);
            }

            Check(gap.Percent == 80, "누락 구간이 있으면 끝에 도착해도 미완료");
            gap.Sample(double.NaN, 10, true, 10);
            Check(gap.Percent == 80, "잘못된 재생 시각 무시");
            RoomPlayerPrefabBuilder.Create();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SmartTraining/Resources/SmartTraining/RoomPlayer.prefab");
            Check(prefab.GetComponent<TrainingProgress>() != null, "플레이어의 독립 진행 컴포넌트 연결");
            Debug.Log("LEARNING PROGRESS CHECKS PASSED (7)");
        }


        private static void Check(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
