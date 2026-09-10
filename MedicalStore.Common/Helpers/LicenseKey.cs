using System.Security.Cryptography;
using System.Text;

namespace MedicalStore.Common.Helpers
{
    /// <summary>
    /// Offline license-key algorithm shared by the application (to validate keys)
    /// and the key generator (to create them). A key encodes an expiry date and is
    /// signed with an HMAC so it cannot be forged or edited without the secret.
    ///
    /// Key layout (before formatting): 4 bytes little-endian "expiry code" + 5 bytes
    /// HMAC-SHA256 tag, Base32-encoded and grouped as  RYN-XXXXX-XXXXX-XXXX .
    /// expiry code 0 = lifetime (never expires); otherwise days since 2020-01-01.
    /// </summary>
    public static class LicenseKey
    {
        // NOTE: symmetric secret embedded in the app. Adequate for a small commercial
        // desktop product; keep the key generator private to the vendor.
        private static readonly byte[] Secret =
            Encoding.UTF8.GetBytes("MedicalStorePro::Riyan::0309-8480389::lic-v1");

        private static readonly DateTime Epoch = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"; // RFC4648 base32

        /// <summary>Create a key. days &lt;= 0 produces a lifetime (non-expiring) key.</summary>
        public static string Generate(int days)
        {
            int code = 0;
            if (days > 0)
                code = (int)(DateTime.UtcNow.Date.AddDays(days) - Epoch).TotalDays;

            var payload = new byte[9];
            BitConverter.GetBytes(code).CopyTo(payload, 0);      // 4 bytes
            var tag = Tag(code);
            Array.Copy(tag, 0, payload, 4, 5);                    // 5 bytes

            var b32 = Base32Encode(payload);
            return "RYN-" + Group(b32);
        }

        /// <summary>
        /// Validate a key. Returns true when the signature checks out. On success
        /// <paramref name="lifetime"/> is true for non-expiring keys, otherwise
        /// <paramref name="expiryUtc"/> holds the (inclusive) last valid date.
        /// </summary>
        public static bool Validate(string? key, out DateTime? expiryUtc, out bool lifetime)
        {
            expiryUtc = null;
            lifetime = false;
            if (string.IsNullOrWhiteSpace(key)) return false;

            var cleaned = key.Trim().ToUpperInvariant().Replace("RYN-", "").Replace("-", "").Replace(" ", "");
            byte[] payload;
            try { payload = Base32Decode(cleaned); }
            catch { return false; }
            if (payload.Length != 9) return false;

            int code = BitConverter.ToInt32(payload, 0);
            var expected = Tag(code);
            for (int i = 0; i < 5; i++)
                if (payload[4 + i] != expected[i]) return false; // signature mismatch

            if (code == 0) { lifetime = true; return true; }
            expiryUtc = Epoch.AddDays(code);
            return true;
        }

        private static byte[] Tag(int code)
        {
            using var h = new HMACSHA256(Secret);
            return h.ComputeHash(Encoding.UTF8.GetBytes("LIC|" + code));
        }

        private static string Group(string s)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && i % 5 == 0) sb.Append('-');
                sb.Append(s[i]);
            }
            return sb.ToString();
        }

        private static string Base32Encode(byte[] data)
        {
            var sb = new StringBuilder();
            int buffer = 0, bits = 0;
            foreach (var b in data)
            {
                buffer = (buffer << 8) | b;
                bits += 8;
                while (bits >= 5)
                {
                    bits -= 5;
                    sb.Append(Alphabet[(buffer >> bits) & 31]);
                }
            }
            if (bits > 0)
                sb.Append(Alphabet[(buffer << (5 - bits)) & 31]);
            return sb.ToString();
        }

        private static byte[] Base32Decode(string s)
        {
            int buffer = 0, bits = 0;
            var bytes = new List<byte>();
            foreach (var c in s)
            {
                int v = Alphabet.IndexOf(c);
                if (v < 0) throw new FormatException("bad char");
                buffer = (buffer << 5) | v;
                bits += 5;
                if (bits >= 8)
                {
                    bits -= 8;
                    bytes.Add((byte)((buffer >> bits) & 0xFF));
                }
            }
            return bytes.ToArray();
        }
    }
}
