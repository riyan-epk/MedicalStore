using System.Security.Cryptography;
using System.Text;
using MedicalStore.Common.Constants;
using MedicalStore.Common.Helpers;

namespace MedicalStore.BLL.Services
{
    public enum LicenseState { Trial, Licensed, Expired }

    public class LicenseStatus
    {
        public LicenseState State { get; set; }
        public int DaysRemaining { get; set; }      // trial or licensed days left (int.MaxValue = lifetime)
        public DateTime? ExpiryDate { get; set; }
        public bool Lifetime { get; set; }
        public string Message { get; set; } = "";
        public bool IsUsable => State != LicenseState.Expired;
    }

    /// <summary>
    /// Offline trial + license manager. Trial state is stored in a per-user file under
    /// LocalApplicationData (survives deleting the app's database) and is HMAC-stamped so
    /// it cannot be edited to extend the trial. See <see cref="LicenseKey"/> for keys.
    /// </summary>
    public class LicenseService
    {
        private static readonly byte[] StoreSecret =
            Encoding.UTF8.GetBytes("MedicalStorePro::state::Riyan::0309-8480389");

        private readonly string _path;

        public LicenseService()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MedicalStorePro");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "license.dat");
        }

        /// <summary>The Machine ID the customer must send to the vendor to get a key.</summary>
        public string MachineId => HardwareId.Get();

        private class State
        {
            public DateTime FirstRun;
            public DateTime LastRun;
            public string Key = "";
        }

        private State Load()
        {
            try
            {
                if (File.Exists(_path))
                {
                    var lines = File.ReadAllLines(_path);
                    var map = new Dictionary<string, string>();
                    foreach (var l in lines)
                    {
                        int i = l.IndexOf('=');
                        if (i > 0) map[l.Substring(0, i)] = l.Substring(i + 1);
                    }
                    if (map.TryGetValue("first", out var f) && map.TryGetValue("last", out var la) &&
                        map.TryGetValue("sig", out var sig))
                    {
                        var key = map.TryGetValue("key", out var k) ? k : "";
                        // Tamper check: signature must match the stored fields.
                        if (Sign(f, la, key) == sig &&
                            DateTime.TryParse(f, out var firstRun) &&
                            DateTime.TryParse(la, out var lastRun))
                        {
                            return new State { FirstRun = firstRun, LastRun = lastRun, Key = key };
                        }
                    }
                    // File exists but is tampered/corrupt → treat trial as consumed.
                    return new State { FirstRun = DateTime.UtcNow.AddDays(-AppConstants.TrialDays - 1), LastRun = DateTime.UtcNow, Key = "" };
                }
            }
            catch { /* fall through to a fresh trial */ }

            // First ever launch on this machine/user.
            var fresh = new State { FirstRun = DateTime.UtcNow, LastRun = DateTime.UtcNow, Key = "" };
            Save(fresh);
            return fresh;
        }

        private void Save(State s)
        {
            try
            {
                var f = s.FirstRun.ToString("o");
                var la = s.LastRun.ToString("o");
                var sig = Sign(f, la, s.Key);
                File.WriteAllLines(_path, new[]
                {
                    "first=" + f,
                    "last=" + la,
                    "key=" + s.Key,
                    "sig=" + sig
                });
            }
            catch { /* best-effort; if it can't write, trial simply re-evaluates next run */ }
        }

        private static string Sign(string first, string last, string key)
        {
            using var h = new HMACSHA256(StoreSecret);
            var b = h.ComputeHash(Encoding.UTF8.GetBytes(first + "|" + last + "|" + key));
            return Convert.ToHexString(b);
        }

        /// <summary>Evaluate current status and advance the "last run" stamp.</summary>
        public LicenseStatus Evaluate()
        {
            var s = Load();

            // Clock-rollback guard: never let the clock going backwards grant more trial.
            var now = DateTime.UtcNow;
            if (now < s.LastRun) now = s.LastRun;
            s.LastRun = now;

            // Valid activated key wins (must be a key issued for THIS machine).
            if (!string.IsNullOrWhiteSpace(s.Key) &&
                LicenseKey.Validate(s.Key, HardwareId.Get(), out var expiry, out var lifetime))
            {
                if (lifetime)
                {
                    Save(s);
                    return new LicenseStatus { State = LicenseState.Licensed, Lifetime = true, DaysRemaining = int.MaxValue, Message = "Licensed (lifetime)" };
                }
                if (expiry.HasValue && expiry.Value.Date >= now.Date)
                {
                    Save(s);
                    int left = (int)(expiry.Value.Date - now.Date).TotalDays;
                    return new LicenseStatus { State = LicenseState.Licensed, ExpiryDate = expiry, DaysRemaining = left,
                        Message = $"Licensed — expires {expiry.Value:dd MMM yyyy} ({left} day{(left == 1 ? "" : "s")} left)" };
                }
                // Key expired → fall through to expired.
                Save(s);
                return new LicenseStatus { State = LicenseState.Expired, ExpiryDate = expiry, DaysRemaining = 0,
                    Message = $"License expired on {expiry:dd MMM yyyy}. Enter a new key to continue." };
            }

            // Trial window.
            int used = (int)(now.Date - s.FirstRun.Date).TotalDays;
            int remaining = AppConstants.TrialDays - used;
            Save(s);

            if (remaining > 0)
                return new LicenseStatus { State = LicenseState.Trial, DaysRemaining = remaining,
                    ExpiryDate = s.FirstRun.Date.AddDays(AppConstants.TrialDays),
                    Message = $"Trial — {remaining} of {AppConstants.TrialDays} day{(remaining == 1 ? "" : "s")} remaining" };

            return new LicenseStatus { State = LicenseState.Expired, DaysRemaining = 0,
                Message = $"Your {AppConstants.TrialDays}-day trial has ended. Enter a license key to continue." };
        }

        /// <summary>Try to activate a key. On success persists it and returns the new status.</summary>
        public (bool Success, string Message, LicenseStatus Status) Activate(string key)
        {
            if (!LicenseKey.Validate(key, HardwareId.Get(), out var expiry, out var lifetime))
                return (false, "Invalid key, or this key was issued for a different computer. Make sure the Machine ID matches.", Evaluate());

            if (!lifetime && expiry.HasValue && expiry.Value.Date < DateTime.UtcNow.Date)
                return (false, $"This key expired on {expiry.Value:dd MMM yyyy}.", Evaluate());

            var s = Load();
            s.Key = key.Trim().ToUpperInvariant();
            s.LastRun = DateTime.UtcNow;
            Save(s);

            var status = Evaluate();
            return (true, lifetime ? "Activated. Thank you! (lifetime license)"
                                   : $"Activated. Licensed until {expiry:dd MMM yyyy}.", status);
        }
    }
}
