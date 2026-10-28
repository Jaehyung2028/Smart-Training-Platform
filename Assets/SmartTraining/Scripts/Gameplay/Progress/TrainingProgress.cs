using Unity.Netcode;
using UnityEngine;

namespace SmartTraining
{
    // 방의 참가자별 실행 결과입니다. 대기 복귀에서는 유지하고 새 시작에서 초기화합니다.
    // 개인 시청·관찰은 소유 클라이언트의 보고이며, 공동 실습은 서버의 판정만 반영합니다.
    public sealed class TrainingProgress : NetworkBehaviour
    {
        private readonly NetworkVariable<int> run = new();
        private readonly NetworkVariable<int> percent = new();
        private readonly NetworkVariable<byte> kind = new();
        private int lastReported = -1;
        private int reportedRun = -1;
        private float nextReport;

        public int Run => run.Value;

        public int Percent => percent.Value;

        public bool IsComplete => kind.Value != 0 && Run > 0 && Percent == 100;

        public bool IsShared => kind.Value == 3;

        public string Label => kind.Value == 0 || Run == 0 ? "학습 미시작" : (IsComplete ? "학습 완료" : "학습 진행 " + Percent + "%") + (IsShared ? " · 공동" : " · 개인");


        // 콘텐츠 변경은 이전 결과를 지웁니다. 실행 번호는 증가시켜 늦은 이전 보고가 재사용되지 않게 합니다.
        public void ClearServer()
        {
            if (!IsServer)
            {
                return;
            }

            percent.Value = 0;
            kind.Value = 0;
            if (run.Value > 0)
            {
                run.Value++;
            }
        }


        public void BeginServer(string contentType)
        {
            if (!IsServer)
            {
                return;
            }

            kind.Value = contentType == "video" ? (byte)1 : contentType == "model" ? (byte)2 : contentType == "scenario" ? (byte)3 : (byte)0;
            percent.Value = 0;
            run.Value++;
        }


        // 프레임마다 RPC를 보내지 않고 진행 변화가 있을 때 최대 초당 한 번 보고합니다.
        public void ReportPersonal(int value)
        {
            var player = GetComponent<RoomPlayer>();
            if (!IsOwner ||
                !IsSpawned ||
                Run == 0 ||
                kind.Value is not (1 or 2) ||
                player == null ||
                !player.RoomIsRunning ||
                player.PhasePending)
            {
                return;
            }

            if (reportedRun != Run)
            {
                reportedRun = Run;
                lastReported = -1;
                nextReport = 0;
            }

            value = Mathf.Clamp(value, 0, 100);
            if (value == lastReported || (value < 100 && Time.unscaledTime < nextReport))
            {
                return;
            }

            ReportPersonalRpc(value, Run);
            lastReported = value;
            nextReport = Time.unscaledTime + 1;
        }


        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void ReportPersonalRpc(int value, int expectedRun)
        {
            var player = GetComponent<RoomPlayer>();
            // 이전 실행의 늦은 보고와 다른 참가자의 보고, 대기 중 보고를 거절합니다.
            if (expectedRun != Run ||
                kind.Value is not (1 or 2) ||
                player == null ||
                !player.RoomIsRunning ||
                player.PhasePending ||
                value < Percent ||
                value > 100)
            {
                return;
            }

            percent.Value = value;
        }


        public void SetSharedServer(int value)
        {
            if (IsServer && kind.Value == 3 && GetComponent<RoomPlayer>().RoomIsRunning)
            {
                percent.Value = Mathf.Clamp(value, 0, 100);
            }
        }
    }
}
