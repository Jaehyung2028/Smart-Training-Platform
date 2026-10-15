namespace SmartTraining
{
    public sealed partial class ApiLobbyScreen
    {
        private ServerAddressSettings serverAddressSettings;
        private string defaultServerUrl, savedServerUrl = "", serverAddressMessage = "";

        private void LoadServerAddress()
        {
            if (defaultServerUrl == null)
                defaultServerUrl = ServerAddress.TryNormalize(serverUrl, out var initial, out _) ? initial : ServerAddress.DefaultUrl;
            if (api?.IsLoggedIn == true || network?.IsConnected == true) return;
            if (serverAddressSettings == null) serverAddressSettings = new ServerAddressSettings();
            if (serverAddressSettings.TryLoad(out var address, out var error))
            { serverUrl = address; savedServerUrl = address; serverAddressMessage = "저장한 주소를 불러왔습니다."; }
            else
            {
                serverUrl = defaultServerUrl; savedServerUrl = "";
                serverAddressMessage = error == null ? "" : "저장된 주소를 사용할 수 없어 기본 주소를 불러왔습니다. 주소를 다시 저장하거나 초기화해주세요.";
            }
        }
        private bool CanEditServerAddress() => !busy && !WaitingForExitCleanup && api?.IsLoggedIn != true && network?.IsConnected != true;
        private void SaveServerAddress()
        {
            if (!CanEditServerAddress()) { status = "로그아웃하고 진행 중인 작업이 끝난 뒤 주소를 변경해주세요."; return; }
            if (serverAddressSettings == null) serverAddressSettings = new ServerAddressSettings();
            if (!serverAddressSettings.Save(serverUrl, out var address, out var error))
            { status = serverAddressMessage = error; return; }
            serverUrl = address; savedServerUrl = address;
            status = serverAddressMessage = "서버 주소를 저장했습니다. 서버 연결 확인으로 응답을 확인해주세요.";
        }
        private void ResetServerAddress()
        {
            if (!CanEditServerAddress()) { status = "로그아웃하고 진행 중인 작업이 끝난 뒤 주소를 변경해주세요."; return; }
            if (serverAddressSettings == null) serverAddressSettings = new ServerAddressSettings();
            serverAddressSettings.Reset(); serverUrl = defaultServerUrl ?? ServerAddress.DefaultUrl; savedServerUrl = "";
            status = serverAddressMessage = "저장한 주소를 삭제하고 기본 주소로 돌아왔습니다.";
        }
    }
}

