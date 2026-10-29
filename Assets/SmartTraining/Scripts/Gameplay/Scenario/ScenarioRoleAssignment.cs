using System;
using Unity.Netcode;

namespace SmartTraining
{
    // 서버가 승인한 협동 역할만 표시용으로 공유합니다.
    public struct ScenarioRoleAssignment : INetworkSerializable, IEquatable<ScenarioRoleAssignment>
    {
        public ulong ClientId;
        public ScenarioRole Role;
        public int RunRevision;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Role);
            serializer.SerializeValue(ref RunRevision);
        }


        public bool Equals(ScenarioRoleAssignment other)
        {
            return ClientId == other.ClientId && Role == other.Role && RunRevision == other.RunRevision;
        }
    }
}
