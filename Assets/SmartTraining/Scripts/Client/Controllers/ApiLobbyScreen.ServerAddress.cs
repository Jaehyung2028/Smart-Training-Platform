namespace SmartTraining
{
    public sealed partial class ApiLobbyScreen
    {
        private string defaultServerUrl;

        // 씬에서 지정한 주소가 초기화 기준입니다. 실행 간 주소 저장·복원은 하지 않습니다.
        private void InitializeServerAddress()
        {
            if (defaultServerUrl == null)
            {
                defaultServerUrl = ServerAddress.TryNormalize(serverUrl, out var initial, out _) ? initial : ServerAddress.DefaultUrl;
                serverUrl = defaultServerUrl;
            }
        }


        private bool CanEditServerAddress() => !busy && !WaitingForExitCleanup && api?.IsLoggedIn != true && network?.IsConnected != true;

        private void ResetServerAddress()
        {
            if (!CanEditServerAddress())
            {
                status = "로그아웃하고 진행 중인 작업이 끝난 뒤 주소를 변경해주세요.";
                return;
            }

            UiAddress = defaultServerUrl ?? ServerAddress.DefaultUrl;
            status = "기본 서버 주소로 초기화했습니다.";
        }
    }
}
