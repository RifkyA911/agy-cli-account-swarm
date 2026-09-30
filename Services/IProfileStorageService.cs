using System.Collections.Generic;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;

namespace AgyAccountSwarm.Services;

/// <summary>
/// Service providing persistent storage management for application settings,
/// account profiles, and directory initialization in roaming AppData.
/// </summary>
public interface IProfileStorageService
{
    /// <summary>
    /// Loads all configured account profiles from <c>%APPDATA%\AgyAccountSwarm\profiles.json</c>.
    /// Seeds initial default profiles if the storage file does not yet exist.
    /// </summary>
    /// <returns>A list of loaded <see cref="AccountProfile"/> objects.</returns>
    Task<List<AccountProfile>> LoadProfilesAsync();

    /// <summary>
    /// Persists the collection of account profiles to disk using indented UTF-8 JSON.
    /// </summary>
    /// <param name="profiles">The account profiles to persist.</param>
    Task SaveProfilesAsync(IEnumerable<AccountProfile> profiles);

    /// <summary>
    /// Loads global application preferences and UI configurations from <c>%APPDATA%\AgyAccountSwarm\settings.json</c>.
    /// </summary>
    /// <returns>The loaded <see cref="AppSettings"/> instance, or default settings if file is absent.</returns>
    Task<AppSettings> LoadSettingsAsync();

    /// <summary>
    /// Persists global application preferences to disk.
    /// </summary>
    /// <param name="settings">The application settings to save.</param>
    Task SaveSettingsAsync(AppSettings settings);

    /// <summary>
    /// Retrieves the absolute path to the application's roaming AppData storage directory.
    /// </summary>
    /// <returns>Directory path string (e.g. <c>%APPDATA%\AgyAccountSwarm</c>).</returns>
    string GetAppDataPath();
}
