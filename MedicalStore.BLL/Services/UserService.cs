using MedicalStore.DAL;
using MedicalStore.DAL.Entities;
using MedicalStore.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace MedicalStore.BLL.Services
{
    public class UserService
    {
        public List<User> GetAll()
        {
            using var db = new AppDbContext();
            return db.Users.OrderBy(u => u.FullName).ToList();
        }

        public User? GetById(int id)
        {
            using var db = new AppDbContext();
            return db.Users.Find(id);
        }

        public (bool Success, string Message) Create(string username, string password, string fullName, UserRole role, Permission permissions)
        {
            using var db = new AppDbContext();

            if (db.Users.Any(u => u.Username == username))
                return (false, "Username already exists.");

            var user = new User
            {
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                FullName = fullName,
                Role = role,
                Permissions = permissions,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            db.Users.Add(user);
            db.SaveChanges();
            return (true, "User created successfully.");
        }

        public (bool Success, string Message) Update(int id, string fullName, UserRole role, Permission permissions, bool isActive)
        {
            using var db = new AppDbContext();
            var user = db.Users.Find(id);
            if (user == null) return (false, "User not found.");

            user.FullName = fullName;
            user.Role = role;
            user.Permissions = permissions;
            user.IsActive = isActive;
            db.SaveChanges();
            return (true, "User updated successfully.");
        }

        public (bool Success, string Message) ResetPassword(int id, string newPassword)
        {
            using var db = new AppDbContext();
            var user = db.Users.Find(id);
            if (user == null) return (false, "User not found.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            db.SaveChanges();
            return (true, "Password reset successfully.");
        }

        public (bool Success, string Message) Delete(int id)
        {
            using var db = new AppDbContext();
            var user = db.Users.Find(id);
            if (user == null) return (false, "User not found.");
            if (user.Username == "admin") return (false, "Cannot delete the default admin user.");

            user.IsActive = false;
            db.SaveChanges();
            return (true, "User deactivated successfully.");
        }
    }
}
