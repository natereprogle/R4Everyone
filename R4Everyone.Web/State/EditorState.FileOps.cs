using System.Text;
using R4Everyone.Binary4Everyone;

namespace R4Everyone.Web.State;

public partial class EditorState
{
    public const string NewFileName = "(new)";
    public const string DefaultSaveFileName = "cheats.dat";

    // The ROM header holds the game ID at 0x0C; the R4 checksum is the CRC32 of the first 512 bytes.
    public const int RomHeaderLength = 512;
    private const int RomGameIdOffset = 0x0C;
    private const int RomGameIdLength = 4;

    /// <summary>
    /// The format the open file was read from. Saving writes this format; new and imported files use R4.
    /// </summary>
    public R4CheatFormat FileFormat { get; private set; } = R4CheatFormat.R4;

    /// <summary>
    /// The settings an open M3 DS Real file was read with, so saving writes it the same way. Null for other formats.
    /// </summary>
    public R4M3RealOptions? M3RealOptions { get; private set; }

    public string SaveFileName => string.IsNullOrWhiteSpace(LoadedFileName) || LoadedFileName == NewFileName
        ? DefaultSaveFileName
        : LoadedFileName;

    public void NewFile()
    {
        OpenDatabase(CreateEmptyDatabase(), NewFileName, R4CheatFormat.R4);
    }

    /// <summary>
    /// Loads an R4, CycloDS Evolution, EDGE, or M3 DS Real cheat database. The current file is left untouched if
    /// loading fails.
    /// </summary>
    /// <param name="encoding">
    /// Replaces an R4 file's encoding marker before loading. The file is then marked as changed, so saving writes the
    /// corrected marker.
    /// </param>
    /// <param name="m3RealOptions">Settings for an M3 DS Real file; null values are detected.</param>
    public async Task LoadAsync(byte[] file, string fileName, R4Encoding? encoding = null,
        R4M3RealReadOptions? m3RealOptions = null)
    {
        if (encoding is { } chosen)
        {
            file = DatabaseFileHeader.WithEncoding(file, chosen);
        }

        var (database, format) = await R4CheatFormats.LoadAsync(file, m3RealOptions);
        var m3WriteOptions = format == R4CheatFormat.M3Real
            ? R4M3RealDb.Inspect(file, m3RealOptions).ToWriteOptions()
            : null;
        OpenDatabase(database, fileName, format, isDirty: encoding != null);
        M3RealOptions = m3WriteOptions;
    }

    public Task<byte[]> BuildSaveBytesAsync() => BuildExportBytesAsync(FileFormat, M3RealOptions);

    /// <summary>
    /// Serializes the open file in any supported format without changing the open file's format.
    /// </summary>
    public async Task<byte[]> BuildExportBytesAsync(R4CheatFormat format, R4M3RealOptions? m3RealOptions = null)
    {
        if (Database == null)
        {
            throw new InvalidOperationException("There is no open file to save.");
        }

        return await R4CheatFormats.SaveAsync(Database, format, m3RealOptions);
    }

    /// <summary>
    /// The download name for an export: the open file's name with the format's usual extension.
    /// </summary>
    public string GetExportFileName(R4CheatFormat format) =>
        Path.ChangeExtension(SaveFileName, R4CheatFormats.GetFileExtension(format));

    /// <summary>
    /// Cheats longer than CycloDS Evolution and EDGE carts may accept.
    /// </summary>
    public IReadOnlyList<(R4Game Game, R4Cheat Cheat)> FindOverlongEvolutionCheats() =>
        Database == null ? [] : R4EvolutionCheats.FindOverlongCheats(Database).ToList();

    public void MarkSaved()
    {
        MarkHistorySaved();
        NotifyStateChanged();
    }

    /// <summary>
    /// Replaces the open file with games imported from codelist XML. The result has not been saved as a .dat file
    /// yet, so it opens with unsaved changes.
    /// </summary>
    /// <exception cref="FormatException">The XML is not a valid codelist.</exception>
    public void ImportXml(string xml, string fileName)
    {
        var database = R4CodelistXml.Import(xml);
        OpenDatabase(database, Path.ChangeExtension(fileName, ".dat"), R4CheatFormat.R4, isDirty: true);
    }

    public string ExportXml()
    {
        if (Database == null)
        {
            throw new InvalidOperationException("There is no open file to export.");
        }

        return R4CodelistXml.Export(Database);
    }

    /// <summary>
    /// Sets a game's ID and checksum from the start of a ROM file.
    /// </summary>
    public void ApplyRomHeader(R4Game game, byte[] header)
    {
        if (header.Length < RomGameIdOffset + RomGameIdLength)
        {
            throw new InvalidDataException("The ROM file is too small to contain a valid header.");
        }

        var gameId = Encoding.ASCII.GetString(header, RomGameIdOffset, RomGameIdLength);
        var checksum = Crc32Helper.ConvertCrc32ToString(Crc32Helper.CalculateCrc32(header)).ToUpperInvariant();
        Change(game, () =>
        {
            game.GameId = gameId;
            game.GameChecksum = checksum;
        });
    }

    private void OpenDatabase(R4Database database, string fileName, R4CheatFormat format, bool isDirty = false)
    {
        Database = database;
        FileFormat = format;
        M3RealOptions = null;
        HasLoadedFile = true;
        LoadedFileName = string.IsNullOrWhiteSpace(fileName) ? NewFileName : fileName;
        ResetHistory(isDirty);
        ResetCaches();
        ClearSelection();
        Revalidate();
        NotifyStateChanged();
    }

    // R4Database defaults to the GBK marker (the first R4Encoding value), but text is always written as UTF-8, so
    // new files must say UTF-8 for non-ASCII titles to display correctly on the cart.
    private static R4Database CreateEmptyDatabase() => new() { FileEncoding = R4Encoding.UTF8 };

    private void ResetCaches()
    {
        _masterCodeText.Clear();
        _cheatCodeWords.Clear();
        _xyModePreference.Clear();
        _expandedState.Clear();
        _nodeIds.Clear();
        _nodesById.Clear();
        _validationErrors = [];
    }

    private R4Database EnsureDatabase()
    {
        if (Database != null)
        {
            return Database;
        }

        Database = CreateEmptyDatabase();
        FileFormat = R4CheatFormat.R4;
        M3RealOptions = null;
        HasLoadedFile = true;
        LoadedFileName = NewFileName;
        return Database;
    }
}
