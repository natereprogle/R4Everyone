using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace R4Everyone.Web.Services;

/// <summary>
/// Looks up games in the bundled GameTDB database (assets/dstdb.xml).
/// </summary>
public sealed class GameTdbService(HttpClient http, ILogger<GameTdbService> logger)
{
    private const string DatabasePath = "assets/dstdb.xml";

    private Task<Dictionary<string, XElement>>? _gamesById;

    /// <summary>
    /// False when the database could not be loaded. Lookups then return no results.
    /// </summary>
    public bool IsAvailable { get; private set; } = true;

    public Task LoadAsync() => GetGamesByIdAsync();

    /// <summary>
    /// Returns the languages GameTDB has cover art for, English first, or an empty list for unknown games.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetCoverLanguagesAsync(string gameId)
    {
        var games = await GetGamesByIdAsync();
        return games.TryGetValue(gameId, out var game) ? GetLanguages(game) : [];
    }

    private Task<Dictionary<string, XElement>> GetGamesByIdAsync() => _gamesById ??= LoadGamesByIdAsync();

    private async Task<Dictionary<string, XElement>> LoadGamesByIdAsync()
    {
        try
        {
            await using var stream = await http.GetStreamAsync(DatabasePath);
            var settings = new XmlReaderSettings
            {
                Async = true,
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreComments = true,
                IgnoreWhitespace = true,
            };

            using var reader = XmlReader.Create(stream, settings);
            var document = await XDocument.LoadAsync(reader, LoadOptions.None, CancellationToken.None);

            var games = new Dictionary<string, XElement>(StringComparer.Ordinal);
            foreach (var game in document.Descendants("game"))
            {
                var id = (string?)game.Element("id");
                if (!string.IsNullOrEmpty(id))
                {
                    games.TryAdd(id, game);
                }
            }

            return games;
        }
        catch (Exception ex) when (ex is HttpRequestException or XmlException or IOException)
        {
            logger.LogError(ex, "Could not load the GameTDB database from {Path}. Cover art is disabled.", DatabasePath);
            IsAvailable = false;
            return [];
        }
    }

    private static List<string> GetLanguages(XElement game)
    {
        var languages = game.Elements("locale")
            .Select(locale => locale.Attribute("lang")?.Value)
            .OfType<string>()
            .Where(lang => !string.IsNullOrWhiteSpace(lang))
            .ToList();

        if (languages.Count == 0)
        {
            languages = ((string?)game.Element("languages") ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        return languages
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(lang => lang.Equals("EN", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ToList();
    }
}
