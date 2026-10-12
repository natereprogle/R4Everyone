using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace R4Everyone.Binary4Everyone;

/// <summary>
/// Converts cheat databases to and from the "codelist" XML format used by melonDS-style tools and R4XmlDump.
/// </summary>
/// <remarks>
/// Besides the standard elements (<c>name</c>, <c>gameid</c>, <c>folder</c>, <c>cheat</c>, <c>note</c>,
/// <c>codes</c>), exports include <c>enabled</c>, <c>onehot</c>, and <c>mastercodes</c> so a database round-trips
/// without loss. Other tools ignore these elements, and imports treat them as optional.
/// </remarks>
public static class R4CodelistXml
{
    // The header reserves 0x10-0x4B for the database title.
    private const int MaxTitleLength = 0x4B - 0x10 + 1;

    public static string Export(R4Database db)
    {
        db.MaterializeAllGames();

        var root = new XElement("codelist", new XElement("name", db.Title.TrimEnd('\0', ' ')));
        foreach (var game in db.Games)
        {
            root.Add(new XElement("game",
                new XElement("name", game.GameTitle),
                new XElement("gameid", $"{game.GameId} {game.GameChecksum}".Trim()),
                new XElement("enabled", game.GameEnabled ? 1 : 0),
                new XElement("mastercodes", string.Join(' ', game.MasterCodes.Select(FormatWord))),
                ExportItems(game.Items)));
        }

        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        using var writer = new Utf8StringWriter();
        document.Save(writer);
        return writer.ToString();
    }

    /// <summary>
    /// Reads a codelist document into a new database. Games that share a Game ID and checksum are merged: their
    /// folders are combined by name, and cheats already present with the same name and code are skipped.
    /// </summary>
    /// <exception cref="FormatException">The XML is malformed or is not a codelist.</exception>
    public static R4Database Import(string xml)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (XmlException ex)
        {
            throw new FormatException($"The file is not valid XML: {ex.Message}", ex);
        }

        var root = document.Root;
        if (root?.Name.LocalName != "codelist")
        {
            throw new FormatException("The file is not a codelist XML file (expected a <codelist> root element).");
        }

        var db = new R4Database { FileEncoding = R4Encoding.UTF8 };
        var title = (string?)root.Element("name");
        if (!string.IsNullOrEmpty(title) && title.Length <= MaxTitleLength && title.All(char.IsAscii))
        {
            db.Title = title;
        }

        var gamesByKey = new Dictionary<string, R4Game>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in root.Elements("game"))
        {
            var game = ImportGame(element);
            var key = $"{game.GameId} {game.GameChecksum}";
            if (game.GameId.Length > 0 && gamesByKey.TryGetValue(key, out var existing))
            {
                MergeItems(existing.Items, game.Items);
                continue;
            }

            gamesByKey.TryAdd(key, game);
            db.Games.Add(game);
            db.MarkGameDirty(game);
        }

        return db;
    }

    private static IEnumerable<XElement> ExportItems(IEnumerable<IR4Item> items)
    {
        foreach (var item in items)
        {
            switch (item)
            {
                case R4Folder folder:
                    yield return new XElement("folder",
                        new XElement("name", folder.Title),
                        new XElement("note", folder.Description),
                        new XElement("onehot", folder.OneHot ? 1 : 0),
                        ExportItems(folder.Items));
                    break;
                case R4Cheat cheat:
                    yield return new XElement("cheat",
                        new XElement("name", cheat.Title),
                        new XElement("note", cheat.Description),
                        new XElement("enabled", cheat.Enabled ? 1 : 0),
                        new XElement("codes", FormatCodes(cheat.Code)));
                    break;
            }
        }
    }

    private static R4Game ImportGame(XElement element)
    {
        var gameName = (string?)element.Element("name") ?? string.Empty;
        var idParts = ((string?)element.Element("gameid") ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        var game = new R4Game(idParts.ElementAtOrDefault(0) ?? string.Empty)
        {
            GameTitle = gameName,
            GameChecksum = (idParts.ElementAtOrDefault(1) ?? string.Empty).ToUpperInvariant(),
            GameEnabled = ReadFlag(element, "enabled") ?? true
        };

        if (element.Element("mastercodes") is { } masterCodes)
        {
            var words = ParseWords((string)masterCodes, $"master codes of '{gameName}'");
            if (words.Count != game.MasterCodes.Length)
            {
                throw new FormatException(
                    $"Game '{gameName}' must have {game.MasterCodes.Length} master codes, but has {words.Count}.");
            }

            game.MasterCodes = words.ToArray();
        }

        game.Items.AddRange(ImportItems(element, gameName));
        return game;
    }

    private static IEnumerable<IR4Item> ImportItems(XElement container, string gameName)
    {
        foreach (var element in container.Elements())
        {
            switch (element.Name.LocalName)
            {
                case "folder":
                    var folder = new R4Folder
                    {
                        Title = (string?)element.Element("name") ?? string.Empty,
                        Description = (string?)element.Element("note") ?? string.Empty,
                        OneHot = ReadFlag(element, "onehot") ?? false
                    };
                    folder.Items.AddRange(ImportItems(element, gameName));
                    yield return folder;
                    break;
                case "cheat":
                    var cheatName = (string?)element.Element("name") ?? string.Empty;
                    var words = ParseWords((string?)element.Element("codes") ?? string.Empty,
                        $"cheat '{cheatName}' in '{gameName}'");
                    if (words.Count % 2 != 0)
                    {
                        // Codes are stored as address/value pairs; pad an unfinished pair as R4XmlDump does.
                        words.Add(0);
                    }

                    yield return new R4Cheat
                    {
                        Title = cheatName,
                        Description = (string?)element.Element("note") ?? string.Empty,
                        Enabled = ReadFlag(element, "enabled") ?? false,
                        Code = words.Select(BitConverter.GetBytes).ToList()
                    };
                    break;
            }
        }
    }

    private static void MergeItems(List<IR4Item> target, List<IR4Item> incoming)
    {
        foreach (var item in incoming)
        {
            switch (item)
            {
                case R4Folder folder when target.OfType<R4Folder>().FirstOrDefault(existing =>
                    string.Equals(existing.Title, folder.Title, StringComparison.OrdinalIgnoreCase)) is { } match:
                    MergeItems(match.Items, folder.Items);
                    break;
                case R4Cheat cheat when target.OfType<R4Cheat>().Any(existing =>
                    existing.Title == cheat.Title && existing.Code.SelectMany(b => b).SequenceEqual(cheat.Code.SelectMany(b => b))):
                    break;
                default:
                    target.Add(item);
                    break;
            }
        }
    }

    private static bool? ReadFlag(XElement element, string name)
    {
        var value = ((string?)element.Element(name))?.Trim();
        return value switch
        {
            null or "" => null,
            "1" => true,
            "0" => false,
            _ when bool.TryParse(value, out var flag) => flag,
            _ => throw new FormatException($"<{name}> must be 0 or 1, but was '{value}'.")
        };
    }

    private static List<uint> ParseWords(string text, string context)
    {
        var words = new List<uint>();
        foreach (var token in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Length != 8 ||
                !uint.TryParse(token, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var word))
            {
                throw new FormatException($"Invalid code '{token}' in {context}. Codes must be 8 hex characters.");
            }

            words.Add(word);
        }

        return words;
    }

    private static string FormatCodes(List<byte[]> code)
    {
        var words = code.Select(bytes => bytes.Length == 4 ? BitConverter.ToUInt32(bytes) : 0).ToList();
        var rows = new List<string>();
        for (var i = 0; i < words.Count; i += 2)
        {
            var right = i + 1 < words.Count ? words[i + 1] : 0;
            rows.Add($"{FormatWord(words[i])} {FormatWord(right)}");
        }

        return string.Join('\n', rows);
    }

    private static string FormatWord(uint word) => word.ToString("X8", CultureInfo.InvariantCulture);

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
