using System;
using System.Collections;
using UnityEditor;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class RoomEndpointChecks
    {
        public static bool ExpectHttpStartFailure { get; private set; }

        public static IEnumerator RunRequestStartFailure()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SMART_TRAINING_TEST_LAN_ADDRESS")))
            {
                yield break;
            }

            var previous = PlayerSettings.insecureHttpOption;
            var api = new TrainingApiClient(Environment.GetEnvironmentVariable("SMART_TRAINING_TEST_URL"));
            try
            {
                PlayerSettings.insecureHttpOption = InsecureHttpOption.NotAllowed;
                string message = null;
                int failures = 0;
                ExpectHttpStartFailure = true;
                yield return api.CheckHealth(
                    () => throw new Exception("Restricted remote HTTP was accepted"),
                    error =>
                {
                    message = error;
                    failures++;
                }
                );
                ExpectHttpStartFailure = false;
                Check(
                    failures == 1 && message != null && message.Contains("Development Only"),
                    "blocked HTTP request startup returns actionable failure without throwing"
                );
                var active = typeof(TrainingApiClient).GetField("activeRequest", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(api);
                Check(active == null, "request startup failure clears active request before native request disposal");
                PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
                bool recovered = false;
                yield return api.CheckHealth(() => recovered = true, error => throw new Exception(error));
                Check(recovered, "same API client can retry after HTTP setting correction without previous request lock");
            }
            finally
            {
                ExpectHttpStartFailure = false;
                PlayerSettings.insecureHttpOption = previous;
                api.Dispose();
            }
        }

        private static void Check(bool ok, string name)
        {
            if (!ok)
            {
                throw new InvalidOperationException(name);
            }

            Debug.Log("ROOM PASS: " + name);
        }

        public static void RunPure()
        {
            foreach (var host in new[]
            {
                "192.168.1.20",
                "training.example.com",
                "127.0.0.1",
                "::1"
            }

            )
            {
                Check(
                    RoomEndpoint.TryValidate(host, 6000, "https://localhost:7087", out _),
                    "valid room endpoint accepted " + host
                );
            }

            foreach (var host in new[]
            {
                "0.0.0.0",
                "::",
                "::ffff:0.0.0.0",
                "http://192.168.1.20",
                "localhost:6000",
                " localhost "
            }

            )
            {
                Check(
                    !RoomEndpoint.TryValidate(host, 6000, "http://192.168.1.20:5229", out var error) && !string.IsNullOrEmpty(error),
                    "invalid advertised room endpoint rejected " + host
                );
            }

            foreach (var host in new[]
            {
                "127.0.0.1",
                "localhost",
                "::1",
                "::ffff:127.0.0.1",
                "room.localhost"
            }

            )
            {
                Check(
                    !RoomEndpoint.TryValidate(host, 6000, "http://192.168.1.20:5229", out _),
                    "explicit remote API cannot advertise client loopback room " + host
                );
            }

            Check(!RoomEndpoint.TryValidate("192.168.1.20", 65536, null, out _), "room port validation rejects outside range");
            Check(
                RoomEndpoint.TryValidate("127.0.0.1", 6000, "http://training.example.com", out _),
                "API hostname DNS result is not guessed from its name"
            );
        }

        public static IEnumerator Run(TrainingApiClient api, JoinRoomResponse reservation)
        {
            string lanAddress = Environment.GetEnvironmentVariable("SMART_TRAINING_TEST_LAN_ADDRESS");
            if (string.IsNullOrEmpty(lanAddress))
            {
                yield break;
            }

            Check(
                reservation.room.ipAddress == lanAddress && new Uri(api.BaseUrl).Host == lanAddress,
                "LAN API and advertised Unity room address use selected server interface"
            );
            var wrong = JsonUtility.FromJson<JoinRoomResponse>(JsonUtility.ToJson(reservation));
            wrong.room.ipAddress = "127.0.0.1";
            var network = new RoomNetworkClient();
            string message = null;
            try
            {
                yield return network.Connect(
                    api.User.id,
                    wrong,
                    () => throw new Exception("Wrong LAN room endpoint accepted"),
                    error => message = error,
                    api.BaseUrl
                );
                Check(
                    !network.IsConnected && network.ServerEndpoint.Length == 0 && message != null && message.Contains("LAN IP"),
                    "room connect rejects remote loopback advertisement before UDP and explains server configuration"
                );
                yield return api.GetRoom(
                    reservation.room.lobbyId,
                    reservation.room.id,
                    room => Check(
                        room.reservedUsers == 1 && room.connectedUsers == 0,
                        "endpoint validation does not consume or confirm admission reservation"
                    ),
                    error => throw new Exception(error)
                );
            }
            finally
            {
                network.Dispose();
            }
        }
    }
}
