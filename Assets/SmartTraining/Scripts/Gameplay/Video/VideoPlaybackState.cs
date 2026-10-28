using System;
using Unity.Netcode;

namespace SmartTraining
{
    // 영상 파일 대신 재생 시계만 공유합니다. 영상은 각자의 Addressables 프리팹에서 로드합니다.
    public struct VideoPlaybackState : INetworkSerializable, IEquatable<VideoPlaybackState>
    {
        public double Position;
        public double StartedAt;
        public double Duration;
        public bool Playing;

        public VideoPlaybackState(double position, double startedAt, double duration, bool playing)
        {
            Position = position;
            StartedAt = startedAt;
            Duration = duration;
            Playing = playing;
        }

        public double Evaluate(double serverTime)
        {
            double elapsed = Playing ? Math.Max(0, serverTime - StartedAt) : 0;
            return Math.Clamp(Position + elapsed, 0, Math.Max(0, Duration));
        }

        public bool IsPlaying(double serverTime)
        {
            return Playing && Duration > 0 && Evaluate(serverTime) < Duration;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref StartedAt);
            serializer.SerializeValue(ref Duration);
            serializer.SerializeValue(ref Playing);
        }

        public bool Equals(VideoPlaybackState other)
        {
            return Position.Equals(other.Position) && StartedAt.Equals(other.StartedAt) &&
                Duration.Equals(other.Duration) && Playing == other.Playing;
        }
    }
}
