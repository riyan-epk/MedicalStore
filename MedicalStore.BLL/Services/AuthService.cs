using MedicalStore.DAL;
using MedicalStore.DAL.Entities;
using MedicalStore.Common.Enums;
using MedicalStore.Common.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MedicalStore.BLL.Services
{
    public class AuthService
    {
        public (bool Success, string Message, User? User) Login(string username, string password)
        {
            using var db = new AppDbContext();
            var user = db.Users.FirstOrDefault(u => u.Username == username && u.IsActive);
            
            if (user == null)
                return (false, "Invalid username or password.", null);

            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                return (false, "Invalid username or password.", null);

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
