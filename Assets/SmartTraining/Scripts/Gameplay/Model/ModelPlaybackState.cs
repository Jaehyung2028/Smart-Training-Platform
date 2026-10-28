using System;
using Unity.Netcode;
using UnityEngine;

namespace SmartTraining
{
    // 재생 시작 시점만 공유합니다. 매 프레임 RPC나 진행도 패킷을 보내지 않고 서버 시각으로 같은 위치를 계산합니다.
    public struct ModelPlaybackState : INetworkSerializable, IEquatable<ModelPlaybackState>
    {
        public float Amount;
        public int Direction;
        public double StartedAt;
        public float Duration;

        public ModelPlaybackState(float amount, int direction, double startedAt, float duration)
        {
            Amount = amount;
            Direction = direction;
            StartedAt = startedAt;
            Duration = duration;
        }

        public float Evaluate(double serverTime)
        {
            if (Direction == 0 || Duration <= 0)
            {
                return Mathf.Clamp01(Amount);
            }
            double elapsed = Math.Max(0, serverTime - StartedAt);
            return Mathf.Clamp01(Amount + Direction * (float)(elapsed / Duration));
        }

        public int GetDirection(double serverTime)
        {
            float amount = Evaluate(serverTime);
            return (Direction > 0 && amount >= 1) || (Direction < 0 && amount <= 0) ? 0 : Direction;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Amount);
            serializer.SerializeValue(ref Direction);
            serializer.SerializeValue(ref StartedAt);
            serializer.SerializeValue(ref Duration);
        }

        public bool Equals(ModelPlaybackState other)
        {
            return Amount.Equals(other.Amount) && Direction == other.Direction &&
                StartedAt.Equals(other.StartedAt) && Duration.Equals(other.Duration);
        }
    }
}
