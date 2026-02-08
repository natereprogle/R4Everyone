using System.Xml;
using System.Xml.Linq;

namespace R4Everyone.Web.Services;

public class XmlDataService(HttpClient http)
{
    public async Task<XElement?> FindGameByIdAsync(
        string relativeUrl,
        string targetId,
        CancellationToken ct = default)
    {
        await using var xmlStream = await http.GetStreamAsync(relativeUrl, ct);
        var settings = new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit, // good default
            IgnoreComments = true,
            IgnoreWhitespace = true,
        };

        using var reader = XmlReader.Create(xmlStream, settings);

        // Walk through the XML stream looking for <game> start elements
        while (await reader.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();

            if (reader.NodeType != XmlNodeType.Element ||
                reader.Name != "game") continue;
            // Read the entire <game>...</game> subtree as an XElement
            var gameElement = await XElement.LoadAsync(reader.ReadSubtree(), LoadOptions.None, ct);

            // Guaranteed <id> exists if the game exists (per your statement)
            var idValue = (string?)gameElement.Element("id");
            if (idValue == targetId)
                return gameElement;

            // Continue scanning
        }

        return null;
    }
}