using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace SmartTraining
{
    // SRT의 시각 구간과 문장을 읽습니다. 네트워크로 문장을 보내지 않고 공통 영상 시각으로 조회합니다.
    public sealed class VideoSubtitles
    {
        private readonly List<(double Start, double End, string Text)> cues = new();

        public bool HasCues => cues.Count > 0;

        public VideoSubtitles(string source)
        {
            using (var reader = new StringReader(source ?? ""))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }
                    // 번호 줄은 선택 사항입니다. 시간 줄의 형식이 잘못되면 자막 파일의 수정 위치를 알립니다.
                    if (!line.Contains("-->"))
                    {
                        if (!int.TryParse(line.Trim().TrimStart('\uFEFF'), out _))
                        {
                            throw new FormatException("SRT 자막 번호가 올바르지 않습니다.");
                        }
                        line = reader.ReadLine();
                    }
                    var times = (line ?? "").Split(new[] { "-->" }, StringSplitOptions.None);
                    if (times.Length != 2 || !TryTime(times[0], out var start) || !TryTime(times[1], out var end) || end <= start)
                    {
                        throw new FormatException("SRT 자막 시각은 00:00:00,000 --> 00:00:04,000 형식이어야 합니다.");
                    }
                    var text = new StringBuilder();
                    while ((line = reader.ReadLine()) != null && !string.IsNullOrWhiteSpace(line))
                    {
                        if (text.Length > 0)
                        {
                            text.Append('\n');
                        }
                        text.Append(line);
                    }
                    if (text.Length > 0)
                    {
                        cues.Add((start, end, text.ToString()));
                    }
                }
            }
            cues.Sort((left, right) => left.Start.CompareTo(right.Start));
        }

        public string GetText(double seconds)
        {
            // 겹치는 구간의 문장은 함께 표시하며, 종료 시각은 다음 자막과 겹치지 않도록 제외합니다.
            StringBuilder result = null;
            foreach (var cue in cues)
            {
                if (cue.Start > seconds)
                {
                    break;
                }
                if (seconds >= cue.Start && seconds < cue.End)
                {
                    result ??= new StringBuilder();
                    if (result.Length > 0)
                    {
                        result.Append('\n');
                    }
                    result.Append(cue.Text);
                }
            }
            return result?.ToString() ?? "";
        }

        private static bool TryTime(string value, out double seconds)
        {
            bool valid = TimeSpan.TryParseExact(value.Trim().Replace('.', ','), @"hh\:mm\:ss\,fff", CultureInfo.InvariantCulture, out var time);
            seconds = time.TotalSeconds;
            return valid;
        }
    }
}
