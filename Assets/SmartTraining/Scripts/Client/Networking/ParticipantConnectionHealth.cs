using System;

namespace SmartTraining
{
    public enum ParticipantConnectionState : byte
    {
        Measuring,
        Connected,
        Delayed,
        Checking
    }

    public static class ParticipantConnectionHealth
    {
        public const double MissingHeartbeatSeconds = 3;

        public const int DelayedMilliseconds = 500;

        public static ParticipantConnectionState Evaluate(double heartbeatAge, bool heartbeatReceived, int milliseconds)
        {
            if (double.IsNaN(heartbeatAge) ||
                double.IsInfinity(heartbeatAge) ||
                heartbeatAge >= MissingHeartbeatSeconds)
            {
                return ParticipantConnectionState.Checking;
            }

            if (!heartbeatReceived || milliseconds < 0)
            {
                return ParticipantConnectionState.Measuring;
            }

            return milliseconds >= DelayedMilliseconds ? ParticipantConnectionState.Delayed : ParticipantConnectionState.Connected;
        }

        public static string Label(ParticipantConnectionState state)
        {
            switch (state)
            {
                case ParticipantConnectionState.Connected:
                    return "정상 연결";
                case ParticipantConnectionState.Delayed:
                    return "응답 지연";
                case ParticipantConnectionState.Checking:
                    return "연결 확인 중";
                default:
                    return "연결 측정 중";
            }
        }
    }
}
