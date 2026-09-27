using System.Collections.Generic;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;

namespace AgyAccountSwarm.Services;

public interface IAuthDetectorService
{
    Task<ProfileAuthStatus> DetectAuthStatusAsync(AccountProfile profile);
    List<ConversationSessionItem> GetAvailableSessions(AccountProfile profile);
}
