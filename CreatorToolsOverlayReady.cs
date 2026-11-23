using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Gilomx.CupheadBossRoulette
{
    // Optional local notifications. No dependency on Gililili's HTTP port.
    internal static class CreatorToolsOverlayReady
    {
        private const int VoteHeartbeatMilliseconds = 5000;
        private const uint GenericWrite = 0x40000000;
        private const uint OpenExisting = 3;
        private const uint FileFlagOverlapped = 0x40000000;
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly object VoteLock = new object();
        private static readonly string VoteInstance = Guid.NewGuid().ToString("N");
        private static readonly Timer VoteTimer = new Timer(SendVoteRound, null, Timeout.Infinite, Timeout.Infinite);
        private static string voteRound = "null";
        private static long voteSequence;
        private static readonly object TapFarmingLock = new object();
        private static readonly string TapFarmingInstance = Guid.NewGuid().ToString("N");
        private static readonly Timer TapFarmingTimer = new Timer(SendTapFarmingBattleState, null, Timeout.Infinite, Timeout.Infinite);
        private static string tapFarmingState;
        private static long tapFarmingSequence;

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

        // The controller supplies copied primitives. This timer never reads a
        // Unity object, including while the game is paused or lacks focus.
        internal static void PublishTapFarmingBattleState(string attemptId, bool active, bool paused)
        {
            if (string.IsNullOrEmpty(attemptId) || attemptId.Length != 32) return;
            foreach (char character in attemptId)
                if (!(character >= '0' && character <= '9') && !(character >= 'a' && character <= 'f')) return;
            var state = "{\"attemptId\":\"" + attemptId + "\",\"active\":" +
                (active ? "true" : "false") + ",\"paused\":" + (active && paused ? "true" : "false") + "}";
            lock (TapFarmingLock)
            {
                if (state == tapFarmingState) return;
                tapFarmingState = state;
                TapFarmingTimer.Change(0, active ? VoteHeartbeatMilliseconds : Timeout.Infinite);
            }
        }

        private static void SendTapFarmingBattleState(object ignored)
        {
            try
            {
                string frame;
                lock (TapFarmingLock)
                {
                    if (tapFarmingState == null) return;
                    var sentAt = (DateTime.UtcNow.Ticks - UnixEpoch.Ticks) / TimeSpan.TicksPerMillisecond;
                    frame = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{{\"version\":1,\"type\":\"tap-farming\",\"server\":\"pichi-ruleta\",\"instanceId\":\"{0}\",\"sequence\":{1},\"sentAt\":{2},\"state\":{3}}}",
                        TapFarmingInstance, ++tapFarmingSequence, sentAt, tapFarmingState);
                }
                TrySend(PipeName(Environment.UserName), frame, 250);
            }
            catch { /* The optional receiver cannot interrupt the game. */ }
        }

        internal static bool TrySend(string pipeName, string frame, int timeoutMilliseconds)
        {
            // Cuphead's Mono profile has no System.IO.Pipes implementation.
            // Keep this optional transport on the Windows API, including its timeout.
            if (Environment.OSVersion.Platform != PlatformID.Win32NT || timeoutMilliseconds <= 0 ||
                string.IsNullOrEmpty(pipeName) || pipeName.IndexOf('\\') >= 0 || pipeName.IndexOf('/') >= 0)
                return false;
            try
            {
                var bytes = new UTF8Encoding(false).GetBytes(frame + "\n");
                if (bytes.Length > 4096) return false;
                var timer = Stopwatch.StartNew();
                var path = "\\\\.\\pipe\\" + pipeName;
                while (true)
                {
                    var remaining = RemainingMilliseconds(timer, timeoutMilliseconds);
                    if (remaining <= 0) return false;
                    var pipe = CreateFileW(path, GenericWrite, 0, IntPtr.Zero, OpenExisting, FileFlagOverlapped, IntPtr.Zero);
                    if (pipe != new IntPtr(-1))
                    {
                        using (var write = new NativePipeWrite(pipe))
                            return write.Send(bytes, timer, timeoutMilliseconds);
                    }
                    // Another sender may own the only listening instance. Never
                    // use WaitNamedPipe's zero/default or infinite timeout values.
                    if (Marshal.GetLastWin32Error() != 231) return false; // ERROR_PIPE_BUSY
                    remaining = RemainingMilliseconds(timer, timeoutMilliseconds);
                    if (remaining <= 0 || !WaitNamedPipeW(path, (uint)remaining)) return false;
                }
            }
            catch { return false; }
        }

        private static int RemainingMilliseconds(Stopwatch timer, int timeoutMilliseconds)
        {
            var remaining = timeoutMilliseconds - timer.ElapsedMilliseconds;
            return remaining <= 0 ? 0 : (int)remaining;
        }

        // Native buffers must outlive an overlapped WriteFile, even when the
        // receiver stops reading. Cancellation cleanup cannot hold up the caller.
        private sealed class NativePipeWrite : IDisposable
        {
            private readonly IntPtr pipe;
            private IntPtr signal, buffer, overlapped;
            private bool issued;

            internal NativePipeWrite(IntPtr pipe) { this.pipe = pipe; }

            internal bool Send(byte[] bytes, Stopwatch timer, int timeoutMilliseconds)
            {
                signal = CreateEventW(IntPtr.Zero, true, false, null);
                if (signal == IntPtr.Zero) return false;
                buffer = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, buffer, bytes.Length);
                overlapped = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(PipeOverlapped)));
                Marshal.StructureToPtr(new PipeOverlapped { Event = signal }, overlapped, false);
                var remaining = RemainingMilliseconds(timer, timeoutMilliseconds);
                if (remaining <= 0) return false;
                var completed = WriteFile(pipe, buffer, (uint)bytes.Length, IntPtr.Zero, overlapped);
                if (!completed && Marshal.GetLastWin32Error() != 997) return false; // ERROR_IO_PENDING
                issued = true;
                if (!completed)
                {
                    remaining = RemainingMilliseconds(timer, timeoutMilliseconds);
                    if (remaining <= 0 || WaitForSingleObject(signal, (uint)remaining) != 0) return false;
                }
                uint written;
                return GetOverlappedResult(pipe, overlapped, out written, false) && written == bytes.Length;
            }

            public void Dispose()
            {
                if (issued)
                {
                    // CancelIo runs on the same thread that issued WriteFile.
                    CancelIo(pipe);
                    uint ignored;
                    if (!GetOverlappedResult(pipe, overlapped, out ignored, false) &&
                        Marshal.GetLastWin32Error() == 996) // ERROR_IO_INCOMPLETE
                    {
                        // The OS still owns both pointers. Reap cancellation off
                        // the sender before releasing any buffer, event or handle.
                        ThreadPool.QueueUserWorkItem(delegate
                        {
                            try { GetOverlappedResult(pipe, overlapped, out ignored, true); }
                            finally { Release(); }
                        });
                        return;
                    }
                }
                Release();
            }

            private void Release()
            {
                CloseHandle(pipe);
                if (signal != IntPtr.Zero) CloseHandle(signal);
                if (overlapped != IntPtr.Zero) Marshal.FreeHGlobal(overlapped);
                if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PipeOverlapped
        {
            internal IntPtr Internal, InternalHigh;
            internal uint Offset, OffsetHigh;
            internal IntPtr Event;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        private static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr security,
            uint disposition, uint flags, IntPtr template);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        private static extern bool WaitNamedPipeW(string name, uint timeout);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        private static extern IntPtr CreateEventW(IntPtr security, bool manualReset, bool initialState, string name);
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern bool WriteFile(IntPtr file, IntPtr buffer, uint count, IntPtr written, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern bool GetOverlappedResult(IntPtr file, IntPtr overlapped, out uint transferred, bool wait);
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern bool CancelIo(IntPtr file);
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
