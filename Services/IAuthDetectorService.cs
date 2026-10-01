using System.Collections.Generic;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;

namespace AgyAccountSwarm.Services;

/// <summary>
/// Service responsible for probing and detecting Google OAuth authentication state,
/// extracting JWT claims (email, name, picture), and discovering conversation history sessions.
/// </summary>
public interface IAuthDetectorService
{
    /// <summary>
    /// Evaluates the authentication status of an account profile by inspecting
    /// stored OAuth tokens, decoding JWT id_tokens, and checking validity against expiration timestamps.
    /// </summary>
    /// <param name="profile">The account profile to examine.</param>
    /// <param name="allowCliSpawn">Whether spawning the CLI process to query usage is permitted.</param>
    /// <returns>A populated <see cref="ProfileAuthStatus"/> object detailing identity and freshness.</returns>
    Task<ProfileAuthStatus> DetectAuthStatusAsync(AccountProfile profile, bool allowCliSpawn = true);

    /// <summary>
    /// Discovers existing conversation sessions within a profile's history and SQLite databases,
    /// returning structured items suitable for the session selection dropdown and conversation resumption.
    /// </summary>
    /// <param name="profile">The account profile whose sessions should be listed.</param>
    /// <returns>A list of available <see cref="ConversationSessionItem"/> objects.</returns>
    List<ConversationSessionItem> GetAvailableSessions(AccountProfile profile);
}
