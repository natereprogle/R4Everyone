using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace R4Everyone.Web.Services;

public enum ThemePreference
{
    System,
    Light,
    Dark
}

/// <summary>
/// Reads and applies the user's light/dark theme preference, which is persisted in local storage.
/// </summary>
public sealed class ThemeService(IJSRuntime js, ILogger<ThemeService> logger)
{
    public async Task<ThemePreference> GetPreferenceAsync()
    {
        try
        {
            var value = await js.InvokeAsync<string>("r4eTheme.getPreference");
            return Enum.TryParse<ThemePreference>(value, ignoreCase: true, out var preference)
                ? preference
                : ThemePreference.System;
        }
        catch (JSException ex)
        {
            logger.LogWarning(ex, "Could not read the theme preference.");
            return ThemePreference.System;
        }
    }

    public async Task SetPreferenceAsync(ThemePreference preference)
    {
        try
        {
            await js.InvokeVoidAsync("r4eTheme.setPreference", preference.ToString().ToLowerInvariant());
        }
        catch (JSException ex)
        {
            logger.LogWarning(ex, "Could not apply the theme preference {Preference}.", preference);
        }
    }
}
