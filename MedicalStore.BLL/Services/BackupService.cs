using MedicalStore.Common.Constants;

namespace MedicalStore.BLL.Services
{
    public class BackupService
    {
        public (bool Success, string Message) BackupDatabase(string backupPath)
        {
            try
            {
                var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppConstants.DbFileName);
                if (!File.Exists(dbPath))
                    return (false, "Database file not found.");

                var backupFile = Path.Combine(backupPath, $"MedicalStore_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");
                File.Copy(dbPath, backupFile, true);
                return (true, $"Backup saved to: {backupFile}");
            }
            catch (Exception ex)
            {
                return (false, $"Backup failed: {ex.Message}");
            }
        }

        public (bool Success, string Message) RestoreDatabase(string backupFilePath)
        {
            try
            {
                var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppConstants.DbFileName);
                if (!File.Exists(backupFilePath))
                    return (false, "Backup file not found.");

                File.Copy(backupFilePath, dbPath, true);
                return (true, "Database restored successfully. Please restart the application.");
            }
            catch (Exception ex)
            {
                return (false, $"Restore failed: {ex.Message}");
            }
        }
    }
}
