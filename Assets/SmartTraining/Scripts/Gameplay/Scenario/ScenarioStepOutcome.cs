using System;
using Unity.Collections;
using Unity.Netcode;

namespace SmartTraining
{
    // 서버가 확정한 단계 결과입니다. 재실습 직후 이전 결과가 늦게 도착해도 실행 번호로 구분합니다.
    public struct ScenarioStepOutcome : INetworkSerializable, IEquatable<ScenarioStepOutcome>
    {
        public int Index, RunRevision, ToolErrors, TargetErrors;
        public double DurationSeconds;
        public FixedString128Bytes ActorName;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref Index);
            serializer.SerializeValue(ref RunRevision);
            serializer.SerializeValue(ref ToolErrors);
            serializer.SerializeValue(ref TargetErrors);
            serializer.SerializeValue(ref DurationSeconds);
            serializer.SerializeValue(ref ActorName);
        }


        public bool Equals(ScenarioStepOutcome other)
        {
            return Index == other.Index &&
                RunRevision == other.RunRevision &&
                ToolErrors == other.ToolErrors &&
                TargetErrors == other.TargetErrors &&
                DurationSeconds == other.DurationSeconds &&
                ActorName.Equals(other.ActorName);
        }
    }
}
