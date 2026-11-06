namespace Knightage.Platform.Core.Interfaces;

/// <summary>
/// Reads which screens each role may open in the caller's CRM (CRM's Access Control screen), which are
/// the CRM defaults the Users screen shows and that CRM itself starts from before a user's exceptions.
/// </summary>
public interface ICrmRoleAccessClient
{
    /// <summary>Role -> screen key -> allowed, or null when CRM couldn't be asked (the built-in defaults apply then).</summary>
    Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>>?> GetRoleScreensAsync(string? authorizationHeader, CancellationToken cancellationToken = default);
}
