using Microsoft.JSInterop;

namespace R4Everyone.Web.Services;

/// <summary>
/// Finds cover art for a game on art.gametdb.com.
/// </summary>
public sealed class CoverArtService(GameTdbService gameTdb, IJSRuntime js)
{
    /// <summary>
    /// Returns the URL of the first cover image that exists, preferring English, or null if none is found.
    /// </summary>
    public async Task<string?> FindCoverUrlAsync(string gameId)
    {
        var escapedId = Uri.EscapeDataString(gameId);
        foreach (var language in await gameTdb.GetCoverLanguagesAsync(gameId))
        {
            var url = $"https://art.gametdb.com/ds/coverM/{Uri.EscapeDataString(language)}/{escapedId}.jpg";
            if (await js.InvokeAsync<bool>("r4eImages.exists", url))
            {
                return url;
            }
        }

        return null;
    }
}
