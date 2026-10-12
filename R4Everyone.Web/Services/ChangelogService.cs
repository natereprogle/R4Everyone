using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace R4Everyone.Web.Services;

/// <summary>
/// Tracks whether the user has seen the changelog for the running version.
/// </summary>
public sealed class ChangelogService(IJSRuntime js, ILogger<ChangelogService> logger)
{
    public string AppVersion { get; } = ReadAppVersion();

    public async Task<bool> HasUnseenChangesAsync()
    {
        try
        {
            var seenVersion = await js.InvokeAsync<string>("r4eChangelog.getSeenVersion");
            return !string.Equals(seenVersion, AppVersion, StringComparison.OrdinalIgnoreCase);
        }
        catch (JSException ex)
        {
            logger.LogWarning(ex, "Could not read the last seen changelog version.");
            return false;
        }
    }

    public async Task MarkSeenAsync()
    {
        try
        {
            await js.InvokeVoidAsync("r4eChangelog.setSeenVersion", AppVersion);
        }
        catch (JSException ex)
        {
            logger.LogWarning(ex, "Could not save the last seen changelog version.");
        }
    }

    private static string ReadAppVersion()
    {
        var assembly = typeof(ChangelogService).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            // Strip the "+<commit hash>" suffix the SDK appends.
            return informational.Split('+')[0];
        }

        return assembly.GetName().Version?.ToString() ?? "1.0.0";
    }
}
