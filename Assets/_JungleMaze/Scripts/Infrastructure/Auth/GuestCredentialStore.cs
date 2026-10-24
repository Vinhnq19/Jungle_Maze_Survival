using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace JungleMaze.Infrastructure.Auth
{
    public sealed class GuestCredential
    {
        [JsonProperty("guestProfileId")] public string ProfileId;
        [JsonProperty("credential")] public string Proof;
    }

    /// <summary>Windows current-user DPAPI; each API origin has a separate encrypted credential file.</summary>
    public sealed class GuestCredentialStore
    {
        private readonly string _directory;
        public GuestCredentialStore(string directory) => _directory = directory;

        public GuestCredential Load(Uri endpoint)
        {
            var path = PathFor(endpoint);
            if (!File.Exists(path)) return null;
            var plain = Transform(File.ReadAllBytes(path), endpoint, false);
            try
            {
                var identity = JsonConvert.DeserializeObject<GuestCredential>(Encoding.UTF8.GetString(plain));
                if (identity == null || string.IsNullOrEmpty(identity.ProfileId) || string.IsNullOrEmpty(identity.Proof))
                    throw new InvalidDataException("Guest credential file is invalid; it was not replaced.");
                return identity;
            }
            finally { Array.Clear(plain, 0, plain.Length); }
        }

        public void Save(Uri endpoint, GuestCredential identity)
        {
            var plain = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(identity));
            byte[] protectedBytes;
            try { protectedBytes = Transform(plain, endpoint, true); }
            finally { Array.Clear(plain, 0, plain.Length); }
            Directory.CreateDirectory(_directory);
            var path = PathFor(endpoint);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, protectedBytes);
                // First creator wins across two client processes. Never silently replace an existing profile.
                try { File.Move(temporary, path); }
                catch (IOException) when (File.Exists(path)) { }
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private string PathFor(Uri endpoint)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(Origin(endpoint)));
                return Path.Combine(_directory, BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant() + ".dpapi");
            }
        }

        private static string Origin(Uri endpoint) => endpoint.GetLeftPart(UriPartial.Authority).ToLowerInvariant();

        [StructLayout(LayoutKind.Sequential)]
        private struct Blob { public int Length; public IntPtr Data; }
        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptProtectData(ref Blob input, string description, ref Blob entropy,
            IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, ref Blob entropy,
            IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
        [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr pointer);

        private static byte[] Transform(byte[] bytes, Uri endpoint, bool protect)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                throw new PlatformNotSupportedException("Guest persistence requires Windows DPAPI; anonymous play remains available.");
            var entropyBytes = Encoding.UTF8.GetBytes("Lastaway.Guest.v1|" + Origin(endpoint));
            var input = new Blob { Length = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) };
            var entropy = new Blob { Length = entropyBytes.Length, Data = Marshal.AllocHGlobal(entropyBytes.Length) };
            var output = new Blob();
            try
            {
                Marshal.Copy(bytes, 0, input.Data, bytes.Length);
                Marshal.Copy(entropyBytes, 0, entropy.Data, entropyBytes.Length);
                // CRYPTPROTECT_UI_FORBIDDEN; deliberately omit LOCAL_MACHINE so another Windows user cannot recover it.
                var succeeded = protect
                    ? CryptProtectData(ref input, "Lastaway Guest", ref entropy, IntPtr.Zero, IntPtr.Zero, 1, out output)
                    : CryptUnprotectData(ref input, IntPtr.Zero, ref entropy, IntPtr.Zero, IntPtr.Zero, 1, out output);
                if (!succeeded) throw new CryptographicException("Could not protect/restore Guest credentials for this Windows user.");
                var result = new byte[output.Length];
                Marshal.Copy(output.Data, result, 0, result.Length);
                return result;
            }
            finally
            {
                for (var i = 0; i < input.Length; i++) Marshal.WriteByte(input.Data, i, 0);
                Marshal.FreeHGlobal(input.Data);
                Marshal.FreeHGlobal(entropy.Data);
                if (output.Data != IntPtr.Zero) LocalFree(output.Data);
            }
        }
    }
}
