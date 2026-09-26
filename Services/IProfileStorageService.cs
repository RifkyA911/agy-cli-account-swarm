using System.Collections.Generic;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;

namespace AgyAccountSwarm.Services;

public interface IProfileStorageService
{
    Task<List<AccountProfile>> LoadProfilesAsync();
    Task SaveProfilesAsync(IEnumerable<AccountProfile> profiles);
    Task<AppSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
    string GetAppDataPath();
}
