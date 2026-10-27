using System;
using System.Collections;
using UnityEngine;

namespace SmartTraining
{
    public sealed partial class ApiLobbyScreen
    {
        private CourseSummary[] courses = Array.Empty<CourseSummary>();
        private string selectedCourseId = "";
        private string courseSourceAddress = "", courseAttemptedAddress = "";
        private float nextCourseLoadAt;

        internal CourseSummary[] UiCourses => courses;
        internal string UiCourseId => selectedCourseId;
        internal bool UiCanCourseLogin => !UiProcessing &&
            ServerAddress.TryNormalize(serverUrl, out var address, out _) && courseSourceAddress == address &&
            Array.Exists(courses, course => course.id == selectedCourseId && course.canEnter);

        private void ClearCourses()
        {
            courses = Array.Empty<CourseSummary>();
            selectedCourseId = courseSourceAddress = courseAttemptedAddress = "";
            nextCourseLoadAt = Time.unscaledTime + .6f;
        }

        // 서버 주소 입력 중에는 통신하지 않고 입력이 끝나면 주소별로 한 번 목록을 불러옵니다.
        private void UpdateCoursesBeforeLogin()
        {
            if (UiLoggedIn || UiProcessing || Time.unscaledTime < nextCourseLoadAt ||
                !ServerAddress.TryNormalize(serverUrl, out var address, out _) || courseAttemptedAddress == address)
            {
                return;
            }
            if (uguiView != null && uguiView.addressInput.isFocused)
            {
                return;
            }
            RunOperation(LoadCourses(), "교육과정을 불러오고 있습니다.");
        }

        private IEnumerator LoadCourses()
        {
            if (!EnsureClient())
            {
                yield break;
            }
            var source = api;
            courseAttemptedAddress = source.BaseUrl;
            CourseSummary[] loaded = null;
            string error = null;
            yield return source.GetCourses(items => loaded = items, message => error = message);
            if (api != source)
            {
                yield break;
            }
            if (loaded == null)
            {
                courses = Array.Empty<CourseSummary>();
                selectedCourseId = courseSourceAddress = "";
                status = "교육과정 목록을 불러오지 못했습니다. 서버 연결 확인을 눌러 다시 조회해주세요. " + error;
                yield break;
            }
            courses = loaded;
            courseSourceAddress = source.BaseUrl;
            if (!Array.Exists(courses, course => course.id == selectedCourseId))
            {
                selectedCourseId = "";
            }
            status = courses.Length == 0 ? "등록된 교육과정이 없습니다." : "교육과정 " + courses.Length + "개를 불러왔습니다. 하나를 선택해주세요.";
        }

        internal void UiSelectCourse(string id)
        {
            if (UiProcessing || (!string.IsNullOrEmpty(id) && !Array.Exists(courses, course => course.id == id)))
            {
                return;
            }
            selectedCourseId = id;
            var selected = Array.Find(courses, course => course.id == id);
            status = selected == null ? "교육과정을 선택해주세요." : selected.canEnter ? selected.name + " 과정을 선택했습니다." : "입장할 수 없는 교육과정입니다. " + selected.unavailableReason;
        }

        private IEnumerator CheckHealthAndCourses()
        {
            yield return CheckHealth();
            yield return LoadCourses();
        }

        // 기존 로그인과 인증된 로비 입장을 이어 실행합니다. 과정 선택 없이 새 사용자를 만들지 않습니다.
        private IEnumerator LoginToCourse()
        {
            string courseId = selectedCourseId;
            if (string.IsNullOrEmpty(courseId) || !EnsureClient())
            {
                status = "교육과정을 먼저 선택해주세요.";
                yield break;
            }
            // 선택 후 점검으로 바뀌었거나 다른 서버로 주소가 바뀐 경우를 다시 확인합니다.
            yield return LoadCourses();
            var selected = Array.Find(courses, course => course.id == courseId);
            if (selected == null || !selected.canEnter)
            {
                status = "선택한 교육과정에 입장할 수 없습니다. " + (selected?.unavailableReason ?? "목록을 다시 확인해주세요.");
                yield break;
            }
            if (!UiLoggedIn)
            {
                yield return Login();
            }
            if (!UiLoggedIn)
            {
                yield break;
            }
            // 응답을 잃고 이미 입장한 경우 재입장 대신 현재 상태를 표시합니다.
            if (!string.IsNullOrEmpty(api.User.currentLobbyId))
            {
                yield return LoadState();
                yield break;
            }
            selectedLobbyId = courseId;
            yield return JoinLobby(courseId);
        }
    }
}
