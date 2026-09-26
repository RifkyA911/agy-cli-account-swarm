using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;

namespace AgyAccountSwarm.Services;

public class ProfileStorageService : IProfileStorageService
{
    private readonly string _storageDir;
    private readonly string _profilesFile;
    private readonly string _settingsFile;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ProfileStorageService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _storageDir = Path.Combine(appData, "AgyAccountSwarm");
        _profilesFile = Path.Combine(_storageDir, "profiles.json");
        _settingsFile = Path.Combine(_storageDir, "settings.json");

        if (!Directory.Exists(_storageDir))
        {
            Directory.CreateDirectory(_storageDir);
        }
    }

    public string GetAppDataPath() => _storageDir;

    public async Task<List<AccountProfile>> LoadProfilesAsync()
    {
        if (!File.Exists(_profilesFile))
        {
            // Seed initial profiles for an out-of-the-box great experience
            var initialProfiles = CreateInitialProfiles();
            await SaveProfilesAsync(initialProfiles);
            return initialProfiles;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_profilesFile);
            var profiles = JsonSerializer.Deserialize<List<AccountProfile>>(json, JsonOptions);
            return profiles ?? CreateInitialProfiles();
        }
        catch
        {
            return CreateInitialProfiles();
        }
    }

    public async Task SaveProfilesAsync(IEnumerable<AccountProfile> profiles)
    {
        var json = JsonSerializer.Serialize(profiles, JsonOptions);
        await File.WriteAllTextAsync(_profilesFile, json);
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        if (!File.Exists(_settingsFile))
        {
            var defaultSettings = new AppSettings();
            await SaveSettingsAsync(defaultSettings);
            return defaultSettings;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_settingsFile);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return settings ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await File.WriteAllTextAsync(_settingsFile, json);
    }

    private static List<AccountProfile> CreateInitialProfiles()
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return
        [
            new AccountProfile
            {
                Name = "Default (Main Account)",
                Description = "Primary Google Account connected to default ~/.gemini profile",
                ColorTag = "#10B981", // Emerald green
                CustomProfilePath = userHome, // Points to standard %USERPROFILE%
                IsSelectedForSwarm = true
            },
            new AccountProfile
            {
                Name = "Worker Alpha (Account 2)",
                Description = "Secondary account profile running in isolated environment",
                ColorTag = "#3B82F6", // Blue
                CustomProfilePath = Path.Combine(userHome, ".gemini-profiles", "worker-alpha"),
                IsSelectedForSwarm = true
            }
        ];
    }
}
