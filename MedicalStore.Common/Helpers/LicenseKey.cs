using System.Security.Cryptography;
using System.Text;

namespace MedicalStore.Common.Helpers
{
    /// <summary>
    /// Offline, hardware-locked license-key algorithm shared by the application (to
    /// validate keys) and the vendor key generator (to create them).
    ///
    /// A key is bound to a specific Machine ID and encodes an expiry date, signed with an
    /// HMAC so it cannot be forged, edited, or moved to another machine.
    ///
    /// Payload (before formatting): 4 bytes little-endian expiry code + 4 bytes machine
    /// fingerprint + 5 bytes HMAC-SHA256 tag, Base32-encoded and grouped as
    ///   RYN-XXXXX-XXXXX-XXXXX-XXXXX .
    /// expiry code 0 = lifetime (never expires); otherwise days since 2020-01-01.
    /// </summary>
    public static class LicenseKey
    {
        // Symmetric secret embedded in the app and the generator. Keep it stable across
        // releases or previously issued keys will stop validating.
        private static readonly byte[] Secret =
            Encoding.UTF8.GetBytes("MedicalStorePro::Riyan::0309-8480389::lic-v2-hwid");

        private static readonly DateTime Epoch = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"; // RFC4648 base32

        /// <summary>
        /// Create a key locked to <paramref name="machineId"/>. days &lt;= 0 = lifetime.
        /// </summary>
        public static string Generate(string machineId, int days)
        {
            int code = 0;
            if (days > 0)
                code = (int)(DateTime.UtcNow.Date.AddDays(days) - Epoch).TotalDays;

            var fp = Fingerprint(machineId);
            var payload = new byte[13];
            BitConverter.GetBytes(code).CopyTo(payload, 0);   // 4 bytes expiry
            Array.Copy(fp, 0, payload, 4, 4);                  // 4 bytes machine fingerprint
            var tag = Tag(code, fp);
            Array.Copy(tag, 0, payload, 8, 5);                 // 5 bytes signature

            return "RYN-" + Group(Base32Encode(payload));
        }

        /// <summary>
        /// Validate a key against the current machine. Returns true only when the
        /// signature checks out AND the key was issued for <paramref name="machineId"/>.
        /// </summary>
        public static bool Validate(string? key, string machineId, out DateTime? expiryUtc, out bool lifetime)
        {
            expiryUtc = null;
            lifetime = false;
            if (string.IsNullOrWhiteSpace(key)) return false;

            var cleaned = key.Trim().ToUpperInvariant().Replace("RYN-", "").Replace("-", "").Replace(" ", "");
            byte[] payload;
            try { payload = Base32Decode(cleaned); }
            catch { return false; }
            if (payload.Length != 13) return false;

            int code = BitConverter.ToInt32(payload, 0);
            var keyFp = new byte[4];
            Array.Copy(payload, 4, keyFp, 0, 4);

            // 1) authenticity: signature must match this expiry + fingerprint
            var expected = Tag(code, keyFp);
            for (int i = 0; i < 5; i++)
                if (payload[8 + i] != expected[i]) return false;

            // 2) machine binding: the key's fingerprint must match THIS machine
            var localFp = Fingerprint(machineId);
            for (int i = 0; i < 4; i++)
                if (keyFp[i] != localFp[i]) return false;

            if (code == 0) { lifetime = true; return true; }
            expiryUtc = Epoch.AddDays(code);
            return true;
        }

        /// <summary>4-byte fingerprint of a normalized Machine ID.</summary>
        private static byte[] Fingerprint(string machineId)
        {
            var norm = (machineId ?? "").Trim().ToUpperInvariant().Replace(" ", "").Replace("-", "");
            using var sha = SHA256.Create();
            var h = sha.ComputeHash(Encoding.UTF8.GetBytes("HWID|" + norm));
            var fp = new byte[4];
            Array.Copy(h, 0, fp, 0, 4);
            return fp;
        }

        private static byte[] Tag(int code, byte[] fp)
        {
            using var h = new HMACSHA256(Secret);
            return h.ComputeHash(Encoding.UTF8.GetBytes("LIC|" + code + "|" + Convert.ToHexString(fp)));
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
