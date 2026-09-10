using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace MedicalStore.BLL.Services
{
    /// <summary>
    /// Produces a stable, machine-unique identifier used to lock a license key to one PC.
    /// Primary source is the Windows install's MachineGuid (stable across reboots and
    /// hardware tweaks); machine name + CPU count are mixed in as a fallback/salt so the
    /// value still differs between machines if the registry read is unavailable.
    /// The SAME machine always yields the SAME id, so a key issued for it keeps working;
    /// a different PC yields a different id, so a copied key will not validate there.
    /// </summary>
    public static class HardwareId
    {
        private static string? _cached;

        /// <summary>The customer-facing Machine ID, e.g. "MSP-8FA2-3C71-9BD0-4E15".</summary>
        public static string Get()
        {
            if (_cached != null) return _cached;

            var seed = RawSeed();
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(seed));

            // 8 bytes -> 16 hex chars, grouped as 4 blocks of 4 for readability.
            var hex = Convert.ToHexString(hash, 0, 8);
            var sb = new StringBuilder("MSP-");
            for (int i = 0; i < hex.Length; i += 4)
            {
                if (i > 0) sb.Append('-');
                sb.Append(hex, i, 4);
            }
            _cached = sb.ToString();
            return _cached;
        }

        private static string RawSeed()
        {
            string machineGuid = "";
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    using var key = RegistryKey
                        .OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                        .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                    machineGuid = key?.GetValue("MachineGuid")?.ToString() ?? "";
                }
            }
            catch { /* fall back to environment values below */ }

            // Combine with a couple of stable environment facts. MachineGuid dominates
            // when present; the rest keeps ids distinct if it isn't.
            return string.Join("|",
                machineGuid,
                Environment.MachineName,
                Environment.ProcessorCount,
                Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "");
        }
    }
}
