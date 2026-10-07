using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace LaPichiRuleta.TikFinity.Twitch;

// Local-machine persistence avoids roaming credentials between computers.
internal sealed class WindowsTwitchCredentialStore : ITwitchCredentialStore
{
    private const string Target = "LaPichiRuleta/Twitch/" + TwitchApplication.ClientId;
    private const uint GenericCredential = 1;
    private const uint LocalMachine = 2;
    private const int NotFound = 1168;

    public TwitchTokens? Read()
    {
        if (!CredRead(Target, GenericCredential, 0, out var pointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == NotFound) return null;
            throw new Win32Exception(error);
        }
        try
        {
            var credential = Marshal.PtrToStructure<Credential>(pointer);
            if (credential.BlobSize > 2560) throw new InvalidDataException("Invalid credential size.");
            var bytes = new byte[credential.BlobSize];
            Marshal.Copy(credential.Blob, bytes, 0, bytes.Length);
            try
            {
                var saved = JsonSerializer.Deserialize(bytes, TwitchCredentialJson.Default.TwitchTokens);
                if (saved == null || string.IsNullOrEmpty(saved.AccessToken) || string.IsNullOrEmpty(saved.RefreshToken) ||
                    string.IsNullOrEmpty(saved.UserId) || string.IsNullOrEmpty(saved.Login))
                    throw new InvalidDataException("Invalid Twitch credential.");
                return saved;
            }
            finally { Array.Clear(bytes); }
        }
        finally { CredFree(pointer); }
    }

    public void Write(TwitchTokens tokens)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(tokens, TwitchCredentialJson.Default.TwitchTokens);
        if (bytes.Length > 2560) throw new InvalidDataException("Credential exceeds Windows limit.");
        var blob = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new Credential {
                Type = GenericCredential, TargetName = Target, UserName = tokens.Login,
                Persist = LocalMachine, BlobSize = (uint)bytes.Length, Blob = blob,
            };
            if (!CredWrite(ref credential, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            Array.Clear(bytes);
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            Marshal.FreeHGlobal(blob);
        }
    }

    public void Delete()
    {
        if (!CredDelete(Target, GenericCredential, 0) && Marshal.GetLastWin32Error() != NotFound)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        internal uint Flags;
        internal uint Type;
        internal string? TargetName;
        internal string? Comment;
        internal System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        internal uint BlobSize;
        internal IntPtr Blob;
        internal uint Persist;
        internal uint AttributeCount;
        internal IntPtr Attributes;
        internal string? TargetAlias;
        internal string? UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref Credential credential, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);
    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr credential);
}
