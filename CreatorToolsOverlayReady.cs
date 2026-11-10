using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Gilomx.CupheadBossRoulette
{
    // Optional local notifications. No dependency on Gililili's HTTP port.
    internal static class CreatorToolsOverlayReady
    {
        private const int VoteHeartbeatMilliseconds = 5000;
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly object VoteLock = new object();
        private static readonly string VoteInstance = Guid.NewGuid().ToString("N");
        private static readonly Timer VoteTimer = new Timer(SendVoteRound, null, Timeout.Infinite, Timeout.Infinite);
        private static string voteRound = "null";
        private static long voteSequence;

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
            return TrySend(pipeName,
                "{\"version\":1,\"server\":\"pichi-ruleta\",\"instanceId\":\"" + instanceId + "\",\"port\":18081}",
                timeoutMilliseconds);
        }

        // Renew only while voting is open. If the game exits, the receiver's lease expires.
        internal static void PublishVoteRound(string round)
        {
            lock (VoteLock)
            {
                voteRound = round;
                VoteTimer.Change(0, round == "null" ? Timeout.Infinite : VoteHeartbeatMilliseconds);
            }
        }

        private static void SendVoteRound(object ignored)
        {
            try
            {
                string frame;
                lock (VoteLock)
                {
                    var sentAt = (DateTime.UtcNow.Ticks - UnixEpoch.Ticks) / TimeSpan.TicksPerMillisecond;
                    frame = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{{\"version\":1,\"type\":\"chat-chooses\",\"server\":\"pichi-ruleta\",\"instanceId\":\"{0}\",\"sequence\":{1},\"sentAt\":{2},\"round\":{3}}}",
                        VoteInstance, ++voteSequence, sentAt, voteRound);
                }
                TrySend(PipeName(Environment.UserName), frame, 250);
            }
            catch { /* The optional receiver cannot interrupt the game. */ }
        }

        private static bool TrySend(string pipeName, string frame, int timeoutMilliseconds)
        {
            try
            {
                using (var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out))
                {
                    pipe.Connect(timeoutMilliseconds);
                    using (var writer = new StreamWriter(pipe, new UTF8Encoding(false)))
                    {
                        writer.WriteLine(frame);
                        writer.Flush();
                    }
                }
                return true;
            }
            catch { return false; }
        }
    }
}
