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

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref CompletedSteps);
            serializer.SerializeValue(ref Revision);
            serializer.SerializeValue(ref RunRevision);
            serializer.SerializeValue(ref LastActor);
        }


        public bool Equals(ScenarioProgressState other)
        {
            return CompletedSteps == other.CompletedSteps &&
                Revision == other.Revision &&
                RunRevision == other.RunRevision &&
                LastActor.Equals(other.LastActor);
        }
    }
}
