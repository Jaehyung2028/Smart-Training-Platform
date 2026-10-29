using System;
using Unity.Collections;
using Unity.Netcode;

namespace SmartTraining
{
    // 참가자별 선택을 표시용으로 공유합니다. 공구 판정은 서버 ScenarioProgress가 별도로 수행합니다.
    public struct ScenarioToolSelection : INetworkSerializable, IEquatable<ScenarioToolSelection>
    {
        public ulong ClientId;
        public FixedString64Bytes ToolId;
        public int RunRevision;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref ToolId);
            serializer.SerializeValue(ref RunRevision);
        }


        public bool Equals(ScenarioToolSelection other)
        {
            return ClientId == other.ClientId && ToolId.Equals(other.ToolId) && RunRevision == other.RunRevision;
        }
    }
}
