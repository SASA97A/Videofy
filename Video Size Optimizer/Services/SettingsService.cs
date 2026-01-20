using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Video_Size_Optimizer.Models;

namespace Video_Size_Optimizer.Services
{
    public class SettingsService
    {
        private readonly string _settingsPath;

        public SettingsService()
        {
            // Path: %AppData%/Videofy/settings.json
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "Videofy");

            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            _settingsPath = Path.Combine(folder, "settings.json");
        }
        public async Task SaveSettingsAsync(AppSettings settings)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(settings, options);
            await File.WriteAllTextAsync(_settingsPath, json);
        }

        public AppSettings LoadSettings()
        {
            if (!File.Exists(_settingsPath)) return new AppSettings();

            try
            {
                string json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings(); // Return defaults if file is corrupted
            }
        }





    }
}
