using System.Text;
using R4Everyone.Binary4Everyone;

namespace R4Everyone.Web.State;

public partial class EditorState
{
    public void NewFile()
    {
        Database = new R4Database();
        HasLoadedFile = true;
        LoadedFileName = "(new)";
        IsDirty = false;
        ResetCaches();
        ClearSelection();
        Revalidate();
        NotifyStateChanged();
    }

    public async Task LoadAsync(Stream stream, string? fileName)
    {
        Database = await R4CheatDat.LoadAsync(stream);
        HasLoadedFile = true;
        LoadedFileName = string.IsNullOrWhiteSpace(fileName) ? "(loaded)" : fileName;
        IsDirty = false;
        ResetCaches();
        ClearSelection();
        Revalidate();
        NotifyStateChanged();
    }

    public async Task<byte[]?> BuildSaveBytesAsync()
    {
        if (Database == null)
        {
            return null;
        }

        await using var memStream = new MemoryStream();
        await R4CheatDat.SaveAsync(Database, memStream);
        return memStream.ToArray();
    }

    public void MarkSaved()
    {
        IsDirty = false;
        NotifyStateChanged();
    }

    public void ApplyRomMetadata(byte[] headerBytes)
    {
        if (SelectedGame == null || headerBytes.Length < 0x10)
        {
            return;
        }

        var idBytes = headerBytes.Skip(0x0C).Take(4).ToArray();
        var gameId = Encoding.ASCII.GetString(idBytes);
        var checksum = Crc32Helper.ConvertCrc32ToString(Crc32Helper.CalculateCrc32(headerBytes)).ToUpper();

        SelectedGame.GameId = gameId;
        SelectedGame.GameChecksum = checksum;
        MarkDirty();
    }

    private void ResetCaches()
    {
        _masterCodeText.Clear();
        _cheatCodeText.Clear();
        _expandedState.Clear();
        _validationErrors = [];
    }

    private void EnsureDatabase()
    {
        if (Database != null)
        {
            return;
        }

        Database = new R4Database();
        HasLoadedFile = true;
        LoadedFileName = "(new)";
        IsDirty = false;
    }
}
