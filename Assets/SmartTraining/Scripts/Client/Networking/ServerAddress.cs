using System;
using System.Linq;

namespace SmartTraining
{
    public static class ServerAddress
    {
        public const string DefaultUrl = "http://localhost:5229";
        public const int MaxLength = 2048;

        public static bool TryNormalize(string input, out string normalized, out string error)
        {
            normalized = null;
            error = "서버 주소는 http:// 또는 https://로 시작하는 주소를 입력해주세요.";
            if (string.IsNullOrWhiteSpace(input) || input.Length > MaxLength || input.Any(char.IsControl))
            {
                return false;
            }

            var text = input.Trim();
            if (text.Any(char.IsWhiteSpace) ||
                !Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" &&
                uri.Scheme != "https") ||
                string.IsNullOrEmpty(uri.Host) ||
                uri.Port <= 0)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            {
                error = "서버 주소에 사용자 정보·쿼리·# 조각을 포함하지 말아주세요.";
                return false;
            }

            var host = uri.Host.Trim('[', ']');
            if (host == "0.0.0.0" || host == "::")
            {
                error = "0.0.0.0 또는 :: 대신 실제 서버 IP나 localhost를 입력해주세요.";
                return false;
            }

            var path = uri.AbsolutePath.TrimEnd('/');
            const string swaggerIndex = "/swagger/index.html", swaggerRoot = "/swagger";
            if (path.EndsWith(swaggerIndex, StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(0, path.Length - swaggerIndex.Length);
            }
            else if (path.EndsWith(swaggerRoot, StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(0, path.Length - swaggerRoot.Length);
            }

            normalized = (uri.GetLeftPart(UriPartial.Authority) + path).TrimEnd('/');
            error = null;
            return true;
        }


        public static string Normalize(string input)
        {
            if (!TryNormalize(input, out var normalized, out var error))
            {
                throw new ArgumentException(error);
            }

            return normalized;
        }
    }
}
