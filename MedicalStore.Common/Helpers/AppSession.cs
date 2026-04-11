using MedicalStore.Common.Enums;

namespace MedicalStore.Common.Helpers
{
    public static class AppSession
    {
        public static int CurrentUserId { get; set; }
        public static string CurrentUsername { get; set; } = string.Empty;
        public static string CurrentFullName { get; set; } = string.Empty;
        public static UserRole CurrentRole { get; set; }
        public static Permission CurrentPermissions { get; set; }

        public static bool HasPermission(Permission permission)
        {
            return (CurrentPermissions & permission) == permission;
        }

        public static void Clear()
        {
            CurrentUserId = 0;
            CurrentUsername = string.Empty;
            CurrentFullName = string.Empty;
            CurrentRole = default;
            CurrentPermissions = Permission.None;
        }
    }
}
