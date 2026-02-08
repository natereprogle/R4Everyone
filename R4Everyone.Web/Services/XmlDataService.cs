using System.Collections.Concurrent;
using System.Xml;
using System.Xml.Linq;

namespace R4Everyone.Web.Services;

public class XmlDataService(HttpClient http)
{
    private readonly ConcurrentDictionary<string, Lazy<Task<XDocument>>> _cache = new();

    public Task PreloadAsync(string relativeUrl)
    {
        return GetDocumentAsync(relativeUrl);
    }

    public async Task<XElement?> FindGameByIdAsync(
        string relativeUrl,
        string targetId,
        CancellationToken ct = default)
    {
        var document = await GetDocumentAsync(relativeUrl);

        foreach (var gameElement in document.Descendants("game"))
        {
            ct.ThrowIfCancellationRequested();
            var idValue = (string?)gameElement.Element("id");
            if (idValue == targetId)
                return gameElement;
        }

        return null;
    }

    private Task<XDocument> GetDocumentAsync(string relativeUrl)
    {
        var lazy = _cache.GetOrAdd(
            relativeUrl,
            url => new Lazy<Task<XDocument>>(() => LoadDocumentAsync(url)));
        return lazy.Value;
    }

    private async Task<XDocument> LoadDocumentAsync(string relativeUrl)
    {
        await using var xmlStream = await http.GetStreamAsync(relativeUrl, CancellationToken.None);
        var settings = new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            IgnoreComments = true,
            IgnoreWhitespace = true,
        };

        using var reader = XmlReader.Create(xmlStream, settings);
        return await XDocument.LoadAsync(reader, LoadOptions.None, CancellationToken.None);
    }
}
