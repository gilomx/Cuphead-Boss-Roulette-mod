using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Gilomx.CupheadBossRoulette
{
    // Optional, one-shot notification. No HTTP polling or dependency on Gililili's port.
    internal static class CreatorToolsOverlayReady
    {
        internal static string PipeName(string username)
        {
            using (var hash = SHA256.Create())
            {
                var bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(username.ToLowerInvariant()));
                var suffix = new StringBuilder(24);
                for (int i = 0; i < 12; i++) suffix.Append(bytes[i].ToString("x2"));
                return "gililili-overlay-ready-" + suffix;
            }
        }

        internal static void Publish(int port)
        {
            // Runtime harnesses use isolated random ports and must not notify a real OBS.
            if (port != 18081) return;
            var instanceId = Guid.NewGuid().ToString("N");
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { TryNotify(PipeName(Environment.UserName), instanceId, port, 250); }
                catch { /* Gililili is optional; this must never affect the game. */ }
            });
        }

        internal static bool TryNotify(string pipeName, string instanceId, int port, int timeoutMilliseconds)
        {
            if (port != 18081 || string.IsNullOrEmpty(instanceId) || instanceId.Length != 32) return false;
            foreach (char character in instanceId)
                if (!(character >= '0' && character <= '9') && !(character >= 'a' && character <= 'f')) return false;
            try
            {
                using (var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out))
                {
                    pipe.Connect(timeoutMilliseconds);
                    using (var writer = new StreamWriter(pipe, new UTF8Encoding(false)))
                    {
                        writer.WriteLine("{\"version\":1,\"server\":\"pichi-ruleta\",\"instanceId\":\"" + instanceId + "\",\"port\":18081}");
                        writer.Flush();
                    }
                }
                return true;
            }
            catch { return false; }
        }
    }
}
