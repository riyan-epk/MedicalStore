using MedicalStore.DAL;
using MedicalStore.DAL.Entities;
using System.Linq;

namespace MedicalStore.BLL.Services
{
    public class SettingService
    {
        public string GetSetting(string key, string defaultValue = "")
        {
            using var db = new AppDbContext();
            var setting = db.Settings.FirstOrDefault(s => s.Key == key);
            return setting != null ? setting.Value : defaultValue;
        }

        public void SaveSetting(string key, string value)
        {
            using var db = new AppDbContext();
            var setting = db.Settings.FirstOrDefault(s => s.Key == key);
            if (setting != null)
            {
                setting.Value = value;
                db.Settings.Update(setting);
            }
            else
            {
                db.Settings.Add(new Setting { Key = key, Value = value });
            }
            db.SaveChanges();
        }
    }
}
