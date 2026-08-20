using MedicalStore.DAL;
using MedicalStore.DAL.Entities;
using MedicalStore.Common.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MedicalStore.BLL.Services
{
    public class AuditService
    {
        /// <summary>
        /// Writes an audit entry using the currently logged-in user. Best-effort:
        /// auditing must never break the operation being audited, so failures are swallowed.
        /// </summary>
        public static void Log(string action, string entity, int entityId, string details)
        {
            try
            {
                using var db = new AppDbContext();
                db.AuditLogs.Add(new AuditLog
                {
                    Date     = DateTime.Now,
                    UserId   = AppSession.CurrentUserId,
                    Username = string.IsNullOrWhiteSpace(AppSession.CurrentUsername) ? "system" : AppSession.CurrentUsername,
                    Action   = action,
                    Entity   = entity,
                    EntityId = entityId,
                    Details  = details
                });
                db.SaveChanges();
            }
            catch { /* auditing is best-effort */ }
        }

        public List<AuditLog> GetRecent(int count = 200)
        {
            using var db = new AppDbContext();
            return db.AuditLogs
                .OrderByDescending(a => a.Date)
                .Take(count)
                .ToList();
        }

        public List<AuditLog> GetByDateRange(DateTime from, DateTime to)
        {
            using var db = new AppDbContext();
            return db.AuditLogs
                .Where(a => a.Date >= from && a.Date <= to)
                .OrderByDescending(a => a.Date)
                .AsEnumerable()
                .ToList();
        }
    }
}
