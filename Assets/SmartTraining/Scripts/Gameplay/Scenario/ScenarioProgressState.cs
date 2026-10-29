using System;
using Unity.Collections;
using Unity.Netcode;

namespace SmartTraining
{
    // 공동 목표 진행과 초기화 번호만 전송합니다. 개인 공구는 이 공개 상태에 넣지 않습니다.
    public struct ScenarioProgressState : INetworkSerializable, IEquatable<ScenarioProgressState>
    {
        public int CompletedSteps;
        public int Revision;
        public int RunRevision;
        public FixedString128Bytes LastActor;
        public double WorkStartedAt;
        public float WorkDuration;
        public ulong WorkClientId;
        public double RunStartedAt, RunEndedAt;
        public int ToolErrors, TargetErrors;

        public bool IsWorking => WorkDuration > 0;


        public float GetWorkProgress(double serverTime)
        {
            return IsWorking ? UnityEngine.Mathf.Clamp01((float)((serverTime - WorkStartedAt) / WorkDuration)) : 0;
        }


        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref CompletedSteps);
            serializer.SerializeValue(ref Revision);
            serializer.SerializeValue(ref RunRevision);
            serializer.SerializeValue(ref LastActor);
            serializer.SerializeValue(ref WorkStartedAt);
            serializer.SerializeValue(ref WorkDuration);
            serializer.SerializeValue(ref WorkClientId);
            serializer.SerializeValue(ref RunStartedAt);
            serializer.SerializeValue(ref RunEndedAt);
            serializer.SerializeValue(ref ToolErrors);
            serializer.SerializeValue(ref TargetErrors);
        }


        public bool Equals(ScenarioProgressState other)
        {
            return CompletedSteps == other.CompletedSteps &&
                Revision == other.Revision &&
                RunRevision == other.RunRevision &&
                LastActor.Equals(other.LastActor) &&
                WorkStartedAt == other.WorkStartedAt &&
                WorkDuration == other.WorkDuration &&
                WorkClientId == other.WorkClientId &&
                RunStartedAt == other.RunStartedAt &&
                RunEndedAt == other.RunEndedAt &&
                ToolErrors == other.ToolErrors &&
                TargetErrors == other.TargetErrors;
        }
    }
}
