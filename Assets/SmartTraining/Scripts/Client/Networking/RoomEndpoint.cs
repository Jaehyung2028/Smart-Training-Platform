using System;
using System.Net;

namespace SmartTraining
{
    public static class RoomEndpoint
    {
        private static IPAddress Normalize(IPAddress address) => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

        private static bool IsLoopback(string host) => host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) || (IPAddress.TryParse(host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(Normalize(address)));

        public static bool TryValidate(string host, int port, string apiBaseUrl, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(host) ||
                host != host.Trim() ||
                Uri.CheckHostName(host) == UriHostNameType.Unknown ||
                port < 1 ||
                port > 65535)
            {
                error = "방 서버 주소와 포트가 올바르지 않습니다. 서버 설정을 확인해주세요.";
                return false;
            }

            if (IPAddress.TryParse(host, out var parsed) &&
                (Normalize(parsed).Equals(IPAddress.Any) ||
                parsed.Equals(IPAddress.IPv6Any)))
            {
                error = "방 서버가 접속용 주소 대신 대기용 주소를 전달했습니다. 서버의 Room:AdvertisedAddress를 확인해주세요.";
                return false;
            }

            // 명시한 원격 IP만 확실히 구분합니다. API 호스트 이름은 로컬 주소로 해석될 수도 있어 DNS 결과를 추측하지 않습니다.
            if (IsLoopback(host) &&
                Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var api) &&
                IPAddress.TryParse(api.Host.Trim('[', ']'), out var apiIp) &&
                !IPAddress.IsLoopback(Normalize(apiIp)))
            {
                error = "다른 PC의 서버가 이 PC를 가리키는 방 주소를 전달했습니다. 서버 PC의 LAN IP를 Room:AdvertisedAddress에 지정해주세요.";
                return false;
            }

            return true;
        }
    }
}
