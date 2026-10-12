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

    public string SaveFileName => string.IsNullOrWhiteSpace(LoadedFileName) || LoadedFileName == NewFileName
        ? DefaultSaveFileName
        : LoadedFileName;

    public void NewFile()
    {
        OpenDatabase(new R4Database(), NewFileName);
    }

    /// <summary>
    /// Loads a cheat database. The current file is left untouched if loading fails.
    /// </summary>
    /// <param name="encoding">
    /// Replaces the file's encoding marker before loading. The file is then marked as changed, so saving writes the
    /// corrected marker.
    /// </param>
    public async Task LoadAsync(byte[] file, string fileName, R4Encoding? encoding = null)
    {
        if (encoding is { } chosen)
        {
            file = DatabaseFileHeader.WithEncoding(file, chosen);
        }

        using var stream = new MemoryStream(file, writable: false);
        var database = await R4CheatDat.LoadAsync(stream);
        OpenDatabase(database, fileName, isDirty: encoding != null);
    }

    public async Task<byte[]> BuildSaveBytesAsync()
    {
        if (Database == null)
        {
            throw new InvalidOperationException("There is no open file to save.");
        }

        await using var memStream = new MemoryStream();
        await R4CheatDat.SaveAsync(Database, memStream);
        return memStream.ToArray();
    }

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
        OpenDatabase(database, Path.ChangeExtension(fileName, ".dat"), isDirty: true);
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

    private void OpenDatabase(R4Database database, string fileName, bool isDirty = false)
    {
        Database = database;
        HasLoadedFile = true;
        LoadedFileName = string.IsNullOrWhiteSpace(fileName) ? NewFileName : fileName;
        ResetHistory(isDirty);
        ResetCaches();
        ClearSelection();
        Revalidate();
        NotifyStateChanged();
    }

    private void ResetCaches()
    {
        _masterCodeText.Clear();
        _cheatCodeWords.Clear();
        _xyModePreference.Clear();
        _expandedState.Clear();
        _validationErrors = [];
    }

    private R4Database EnsureDatabase()
    {
        if (Database != null)
        {
            return Database;
        }

        Database = new R4Database();
        HasLoadedFile = true;
        LoadedFileName = NewFileName;
        return Database;
    }
}
