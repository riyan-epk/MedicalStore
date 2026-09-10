using MedicalStore.DAL;
using MedicalStore.DAL.Entities;
using MedicalStore.Common.Enums;
using MedicalStore.Common.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MedicalStore.BLL.Services
{
    public class AuthService
    {
        // ── Simple brute-force throttle (single-terminal desktop app) ─────────────
        private const int MaxAttempts = 5;
        private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(1);
        private static int _failedAttempts;
        private static DateTime _lockedUntil = DateTime.MinValue;

        public (bool Success, string Message, User? User) Login(string username, string password)
        {
            // Enforce lockout after repeated failures.
            if (DateTime.Now < _lockedUntil)
            {
                int secs = (int)Math.Ceiling((_lockedUntil - DateTime.Now).TotalSeconds);
                return (false, $"Too many failed attempts. Please wait {secs} second{(secs == 1 ? "" : "s")} and try again.", null);
            }

            using var db = new AppDbContext();
            var user = db.Users.FirstOrDefault(u => u.Username == username && u.IsActive);

            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                _failedAttempts++;
                if (_failedAttempts >= MaxAttempts)
                {
                    _lockedUntil = DateTime.Now.Add(LockDuration);
                    _failedAttempts = 0;
                    return (false, $"Too many failed attempts. Login locked for {(int)LockDuration.TotalSeconds} seconds.", null);
                }
                return (false, "Invalid username or password.", null);
            }

            // Successful login — clear the throttle.
            _failedAttempts = 0;
            _lockedUntil = DateTime.MinValue;

            // Set session
            AppSession.CurrentUserId = user.Id;
            AppSession.CurrentUsername = user.Username;
            AppSession.CurrentFullName = user.FullName;
            AppSession.CurrentRole = user.Role;
            AppSession.CurrentPermissions = user.Permissions;

            return (true, "Login successful.", user);
        }

        public void Logout()
        {
            AppSession.Clear();
        }
    }
}
