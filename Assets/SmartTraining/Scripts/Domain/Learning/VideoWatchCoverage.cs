using System;
using System.Collections.Generic;

namespace SmartTraining
{
    // 실제 재생된 구간의 합집합입니다. 되감아 같은 구간을 반복해도 누적 시간이 늘어나지 않습니다.
    public sealed class VideoWatchCoverage
    {
        private readonly List<(double Start, double End)> intervals = new();
        private double previousTime, previousClock;
        private bool previousPlaying, sampled;

        public double WatchedSeconds { get; private set; }

        public int Percent { get; private set; }


        public void Sample(double position, double duration, bool playing, double clock)
        {
            if (double.IsNaN(position) ||
                double.IsInfinity(position) ||
                duration <= 0 ||
                double.IsNaN(duration) ||
                double.IsInfinity(duration))
            {
                return;
            }

            position = Math.Max(0, Math.Min(position, duration));
            double elapsed = clock - previousClock;
            double delta = position - previousTime;
            if (sampled && previousPlaying && elapsed > 0 && elapsed < 2 && delta > 0 && delta <= elapsed * 1.5 + .1)
            {
                Add(previousTime, position);
            }

            previousTime = position;
            previousClock = clock;
            previousPlaying = playing;
            sampled = true;
            double tolerance = Math.Min(.25, duration * .02);
            bool complete = WatchedSeconds >= duration - tolerance && position >= duration - tolerance;
            Percent = complete || Percent == 100 ? 100 : Math.Max(Percent, Math.Min(99, (int)(WatchedSeconds / duration * 100)));
        }


        private void Add(double start, double end)
        {
            for (int index = intervals.Count - 1; index >= 0; index--)
            {
                var item = intervals[index];
                if (end < item.Start || start > item.End)
                {
                    continue;
                }

                start = Math.Min(start, item.Start);
                end = Math.Max(end, item.End);
                intervals.RemoveAt(index);
            }

            intervals.Add((start, end));
            WatchedSeconds = 0;
            foreach (var item in intervals)
            {
                WatchedSeconds += item.End - item.Start;
            }
        }
    }
}
