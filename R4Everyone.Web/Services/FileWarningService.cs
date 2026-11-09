using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using R4Everyone.Binary4Everyone;

namespace R4Everyone.Web.Services;

/// <summary>
/// Decides whether to warn that an opened CycloDS Evolution or EDGE file wasn't created by R4Everyone, and remembers
/// whether the user turned that warning off.
/// </summary>
public sealed class FileWarningService(IJSRuntime js, ILogger<FileWarningService> logger)
{
    public async Task<bool> ShouldWarnAsync(byte[] file)
    {
        // Only CycloDS Evolution and EDGE files record their creator. Other formats, and unrecognized files (which
        // report the real error when opened), skip the warning.
        if (R4CheatFormats.Detect(file) is not { } format || !R4CheatFormats.HasCreatorTag(format))
        {
            return false;
        }

        return !R4CheatFormats.IsCreatedByR4Everyone(file) && !await IsUnofficialFileWarningDisabledAsync();
    }

    public async Task DisableUnofficialFileWarningAsync()
    {
        try
        {
            await js.InvokeVoidAsync("r4eWarnings.disableUnofficialFileWarning");
        }
        catch (JSException ex)
        {
            logger.LogWarning(ex, "Could not save the unofficial file warning setting.");
        }
    }

    private async Task<bool> IsUnofficialFileWarningDisabledAsync()
    {
        try
        {
            return await js.InvokeAsync<bool>("r4eWarnings.isUnofficialFileWarningDisabled");
        }
        catch (JSException ex)
        {
            logger.LogWarning(ex, "Could not read the unofficial file warning setting.");
            return false;
        }
    }
}
