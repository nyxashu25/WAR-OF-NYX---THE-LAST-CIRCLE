using System.Collections;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WarOfNyx.App;
using WarOfNyx.Core.Networking;

namespace WarOfNyx.Tests.PlayMode
{
    /// <summary>Phase 12: the dedicated server's HTTP health endpoint answers real TCP requests.</summary>
    public class ServerRuntimePlayModeTests
    {
        private static string Get(int port, string path)
        {
            using (var client = new TcpClient("127.0.0.1", port))
            {
                var stream = client.GetStream();
                var request = Encoding.ASCII.GetBytes($"GET {path} HTTP/1.1\r\nHost: localhost\r\n\r\n");
                stream.Write(request, 0, request.Length);
                var buffer = new byte[4096];
                var sb = new StringBuilder();
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, read));
                return sb.ToString();
            }
        }

        [UnityTest]
        public IEnumerator HealthEndpointReportsStartingAsUnavailable()
        {
            const int port = 18765;
            var runtime = ServerRuntime.Start(ServerLaunchOptions.Parse(new[] { "-server", "-session", "TEST-HEALTH", "-healthPort", port.ToString() }));
            yield return new WaitForSecondsRealtime(0.6f); // a few polls

            var health = Task.Run(() => Get(port, "/health"));
            while (!health.IsCompleted)
                yield return null;
            var missing = Task.Run(() => Get(port, "/secret"));
            while (!missing.IsCompleted)
                yield return null;
            Object.Destroy(runtime.gameObject);

            // No online session in a test: the server is "starting" → 503 for load balancers.
            StringAssert.StartsWith("HTTP/1.1 503", health.Result);
            StringAssert.Contains("\"status\":\"starting\"", health.Result);
            StringAssert.Contains("\"session\":\"TEST-HEALTH\"", health.Result);
            StringAssert.StartsWith("HTTP/1.1 404", missing.Result);
        }
    }
}
