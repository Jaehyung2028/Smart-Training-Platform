#if UNITY_EDITOR
using System.Runtime.CompilerServices;

// 런타임 API를 공개하지 않고 편집기 검사에서 내부 UI 모듈을 확인할 수 있게 합니다.
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
#endif