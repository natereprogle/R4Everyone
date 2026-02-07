using System.Text;
using R4Everyone.Binary4Everyone;

namespace R4Everyone.Web.State;

public class EditorState
{
    // UI-only text buffers so users can type invalid hex without mutating the model.
    private readonly Dictionary<R4Game, string[]> _masterCodeText = new(ReferenceEqualityComparer<R4Game>.Instance);
    // UI-only cache of code blocks shown as two 4-byte segments per row.
    private readonly Dictionary<R4Cheat, List<string>>
        _cheatCodeText = new(ReferenceEqualityComparer<R4Cheat>.Instance);

    // UI-only expand/collapse state for tree nodes.
    private readonly Dictionary<object, bool> _expandedState = new(ReferenceEqualityComparer<object>.Instance);
    private List<string> _validationErrors = [];

    public R4Database? Database { get; private set; }
    public bool HasLoadedFile { get; private set; }
    public string? LoadedFileName { get; private set; }
    public bool IsDirty { get; private set; }

    public SelectionKind SelectionKind { get; private set; } = SelectionKind.None;
    public R4Game? SelectedGame { get; private set; }
    public R4Folder? SelectedFolder { get; private set; }
    public R4Cheat? SelectedCheat { get; private set; }

    public IReadOnlyList<string> ValidationErrors => _validationErrors;
    public bool IsValid => _validationErrors.Count == 0;

    public event Action? StateChanged;

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

        // Serialize via library API only; no custom codecs in the UI layer.
        await using var memStream = new MemoryStream();
        await R4CheatDat.SaveAsync(Database, memStream);
        return memStream.ToArray();
    }

    public void MarkSaved()
    {
        IsDirty = false;
        NotifyStateChanged();
    }

    public void ToggleGameExpand(R4Game game)
    {
        ToggleExpanded(game);
    }

    public void ToggleFolderExpand(R4Folder folder)
    {
        ToggleExpanded(folder);
    }

    public bool IsExpanded(R4Game game) => GetExpanded(game);

    public bool IsExpanded(R4Folder folder) => GetExpanded(folder);

    public void Select(SelectionInfo? selection)
    {
        if (selection == null)
        {
            ClearSelection();
            NotifyStateChanged();
            return;
        }

        SelectionKind = selection.Kind;
        SelectedGame = selection.Game;
        SelectedFolder = selection.Folder;
        SelectedCheat = selection.Cheat;

        if (SelectedGame != null)
        {
            // Normalize master codes for editing in-place.
            EnsureMasterCodes(SelectedGame);
            EnsureMasterCodeBuffer(SelectedGame);
        }

        if (SelectedCheat != null)
        {
            // Populate editable text blocks from byte arrays.
            EnsureCheatCodeBuffer(SelectedCheat);
        }

        NotifyStateChanged();
    }

    private void ClearSelection()
    {
        SelectionKind = SelectionKind.None;
        SelectedGame = null;
        SelectedFolder = null;
        SelectedCheat = null;
    }

    public void Deselect()
    {
        ClearSelection();
        NotifyStateChanged();
    }

    public void AddGame()
    {
        EnsureDatabase();
        var game = new R4Game(string.Empty)
        {
            GameTitle = "New Game",
            GameEnabled = true,
            GameChecksum = string.Empty,
            MasterCodes = new uint[8]
        };

        Database!.Games.Add(game);
        Select(SelectionInfo.ForGame(game));
        MarkDirty();
    }

    public void AddFolder()
    {
        if (SelectedGame == null || SelectionKind != SelectionKind.Game)
        {
            return;
        }

        var folder = new R4Folder
        {
            Title = "New Folder",
            Description = string.Empty,
            OneHot = false
        };

        SelectedGame.Items.Add(folder);
        Select(SelectionInfo.ForFolder(SelectedGame, folder));
        MarkDirty();
    }

    public void AddCheat()
    {
        if (SelectedGame == null)
        {
            return;
        }

        var cheat = new R4Cheat
        {
            Title = "New Cheat",
            Description = string.Empty,
            Enabled = false
        };

        if (SelectionKind == SelectionKind.Folder && SelectedFolder != null)
        {
            SelectedFolder.Items.Add(cheat);
            Select(SelectionInfo.ForCheat(SelectedGame, SelectedFolder, cheat));
        }
        else if (SelectionKind == SelectionKind.Game)
        {
            SelectedGame.Items.Add(cheat);
            Select(SelectionInfo.ForCheat(SelectedGame, null, cheat));
        }
        else
        {
            return;
        }

        MarkDirty();
    }

    public void RemoveSelected()
    {
        if (SelectionKind == SelectionKind.None)
        {
            return;
        }

        if (SelectionKind == SelectionKind.Game && SelectedGame != null)
        {
            Database?.Games.Remove(SelectedGame);
            ClearSelection();
            MarkDirty();
            return;
        }

        if (SelectedGame == null)
        {
            return;
        }

        if (SelectionKind == SelectionKind.Folder && SelectedFolder != null)
        {
            SelectedGame.Items.Remove(SelectedFolder);
            Select(SelectionInfo.ForGame(SelectedGame));
            MarkDirty();
            return;
        }

        if (SelectionKind == SelectionKind.Cheat && SelectedCheat != null)
        {
            if (SelectedFolder != null)
            {
                SelectedFolder.Items.Remove(SelectedCheat);
                Select(SelectionInfo.ForFolder(SelectedGame, SelectedFolder));
            }
            else
            {
                SelectedGame.Items.Remove(SelectedCheat);
                Select(SelectionInfo.ForGame(SelectedGame));
            }

            MarkDirty();
        }
    }

    public void UpdateGameId(string value)
    {
        if (SelectedGame == null)
        {
            return;
        }

        SelectedGame.GameId = value;
        MarkDirty();
    }

    public void UpdateGameTitle(string value)
    {
        if (SelectedGame == null)
        {
            return;
        }

        SelectedGame.GameTitle = value;
        MarkDirty();
    }

    public void UpdateGameEnabled(bool value)
    {
        if (SelectedGame == null)
        {
            return;
        }

        SelectedGame.GameEnabled = value;
        MarkDirty();
    }

    public void UpdateGameChecksum(string value)
    {
        if (SelectedGame == null)
        {
            return;
        }

        SelectedGame.GameChecksum = value;
        MarkDirty();
    }

    public string[] GetMasterCodeText(R4Game game)
    {
        EnsureMasterCodes(game);
        EnsureMasterCodeBuffer(game);
        return _masterCodeText[game];
    }

    public void UpdateMasterCode(R4Game game, int index, string value)
    {
        EnsureMasterCodes(game);
        EnsureMasterCodeBuffer(game);
        var buffer = _masterCodeText[game];

        if (index < 0 || index >= buffer.Length)
        {
            return;
        }

        buffer[index] = value;

        if (TryParseHex8ToUInt(value, out var parsed))
        {
            game.MasterCodes[index] = parsed;
        }

        MarkDirty();
    }

    public void UpdateFolderTitle(string value)
    {
        if (SelectedFolder == null)
        {
            return;
        }

        SelectedFolder.Title = value;
        MarkDirty();
    }

    public void UpdateFolderDescription(string value)
    {
        if (SelectedFolder == null)
        {
            return;
        }

        SelectedFolder.Description = value;
        MarkDirty();
    }

    public void UpdateFolderOneHot(bool value)
    {
        if (SelectedFolder == null)
        {
            return;
        }

        SelectedFolder.OneHot = value;
        MarkDirty();
    }

    public void UpdateCheatTitle(string value)
    {
        if (SelectedCheat == null)
        {
            return;
        }

        SelectedCheat.Title = value;
        MarkDirty();
    }

    public void UpdateCheatDescription(string value)
    {
        if (SelectedCheat == null)
        {
            return;
        }

        SelectedCheat.Description = value;
        MarkDirty();
    }

    public void UpdateCheatEnabled(bool value)
    {
        if (SelectedCheat == null)
        {
            return;
        }

        SelectedCheat.Enabled = value;

        // Optional OneHot enforcement for the active folder.
        if (value && SelectedFolder is { OneHot: true })
        {
            foreach (var item in SelectedFolder.Items.OfType<R4Cheat>())
            {
                if (!ReferenceEquals(item, SelectedCheat))
                {
                    item.Enabled = false;
                }
            }
        }

        MarkDirty();
    }

    public List<string> GetCheatCodeBlocks(R4Cheat cheat)
    {
        EnsureCheatCodeBuffer(cheat);
        return _cheatCodeText[cheat];
    }

    public void UpdateCheatBlock(R4Cheat cheat, int index, string value)
    {
        EnsureCheatCodeBuffer(cheat);
        var blocks = _cheatCodeText[cheat];

        if (index < 0 || index >= blocks.Count)
        {
            return;
        }

        blocks[index] = value;
        SyncCheatCodes(cheat, blocks);
        MarkDirty();
    }

    public void AddCheatRow(R4Cheat cheat)
    {
        EnsureCheatCodeBuffer(cheat);
        var blocks = _cheatCodeText[cheat];
        blocks.Add(string.Empty);
        blocks.Add(string.Empty);
        MarkDirty();
    }

    public void RemoveCheatRow(R4Cheat cheat, int rowIndex)
    {
        EnsureCheatCodeBuffer(cheat);
        var blocks = _cheatCodeText[cheat];
        var start = rowIndex * 2;

        if (start < 0 || start >= blocks.Count)
        {
            return;
        }

        var removeCount = Math.Min(2, blocks.Count - start);
        blocks.RemoveRange(start, removeCount);
        SyncCheatCodes(cheat, blocks);
        MarkDirty();
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

    public static string GetGameDisplayTitle(R4Game game)
    {
        if (!string.IsNullOrWhiteSpace(game.GameTitle))
        {
            return game.GameTitle;
        }

        return !string.IsNullOrWhiteSpace(game.GameId) ? game.GameId : "(untitled game)";
    }

    private void MarkDirty()
    {
        if (!HasLoadedFile)
        {
            HasLoadedFile = true;
            LoadedFileName = "(new)";
        }

        IsDirty = true;
        Revalidate();
        NotifyStateChanged();
    }

    private void Revalidate()
    {
        var errors = new List<string>();

        if (Database != null)
        {
            // Validate all games so Save reflects the full in-memory document state.
            foreach (var game in Database.Games)
            {
                ValidateGame(game, errors);
            }
        }

        _validationErrors = errors;
    }

    private void ValidateGame(R4Game game, List<string> errors)
    {
        if (!string.IsNullOrEmpty(game.GameId) && game.GameId.Length != 4)
        {
            errors.Add($"Game '{GetGameDisplayTitle(game)}' GameId must be 4 characters.");
        }

        if (!string.IsNullOrEmpty(game.GameChecksum) && !IsHex(game.GameChecksum, 8))
        {
            errors.Add($"Game '{GetGameDisplayTitle(game)}' checksum must be 8 hex characters.");
        }

        EnsureMasterCodes(game);
        var masterCodes = GetMasterCodeText(game);
        for (var i = 0; i < masterCodes.Length; i++)
        {
            if (!IsHex(masterCodes[i], 8))
            {
                errors.Add($"Game '{GetGameDisplayTitle(game)}' master code {i + 1} must be 8 hex characters.");
            }
        }

        foreach (var item in game.Items)
        {
            switch (item)
            {
                case R4Folder folder:
                    ValidateFolder(game, folder, errors);
                    break;
                case R4Cheat cheat:
                    ValidateCheat(game, cheat, errors);
                    break;
            }
        }
    }

    private void ValidateFolder(R4Game game, R4Folder folder, List<string> errors)
    {
        foreach (var item in folder.Items)
        {
            switch (item)
            {
                case R4Cheat cheat:
                    ValidateCheat(game, cheat, errors);
                    break;
                case R4Folder childFolder:
                    ValidateFolder(game, childFolder, errors);
                    break;
            }
        }
    }

    private void ValidateCheat(R4Game game, R4Cheat cheat, List<string> errors)
    {
        var blocks = GetCheatCodeBlocks(cheat);
        if (cheat.Code.Count % 2 != 0)
        {
            errors.Add(
                $"Cheat '{cheat.Title}' in '{GetGameDisplayTitle(game)}' must have an even number of code blocks.");
        }

        for (var i = 0; i < blocks.Count; i++)
        {
            if (!IsHex(blocks[i], 8))
            {
                errors.Add(
                    $"Cheat '{cheat.Title}' in '{GetGameDisplayTitle(game)}' block {i + 1} must be 8 hex characters.");
            }
        }
    }

    private static void EnsureMasterCodes(R4Game game)
    {
        if (game.MasterCodes is { Length: 8 })
        {
            return;
        }

        // Keep the model array length normalized to 8 for the UI.
        var next = new uint[8];
        Array.Copy(game.MasterCodes, next, Math.Min(game.MasterCodes.Length, next.Length));

        game.MasterCodes = next;
    }

    private void EnsureMasterCodeBuffer(R4Game game)
    {
        if (_masterCodeText.ContainsKey(game))
        {
            return;
        }

        var buffer = new string[8];
        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] = game.MasterCodes.Length > i ? game.MasterCodes[i].ToString("X8") : string.Empty;
        }

        _masterCodeText[game] = buffer;
    }

    private void EnsureCheatCodeBuffer(R4Cheat cheat)
    {
        if (_cheatCodeText.ContainsKey(cheat))
        {
            return;
        }

        // Cache display blocks so users can edit two 4-byte blocks per row.
        var blocks = cheat.Code.Select(BytesToHex).ToList();
        if (blocks.Count == 0)
        {
            blocks.Add(string.Empty);
            blocks.Add(string.Empty);
        }
        else if (blocks.Count % 2 != 0)
        {
            blocks.Add(string.Empty);
        }

        _cheatCodeText[cheat] = blocks;
    }

    private static void SyncCheatCodes(R4Cheat cheat, List<string> blocks)
    {
        // Only update the model when all blocks are valid hex and evenly paired.
        if (blocks.Count % 2 != 0 || blocks.Any(block => !IsHex(block, 8)))
        {
            return;
        }

        var codes = new List<byte[]>();
        foreach (var block in blocks)
        {
            if (TryParseHex8ToBytes(block, out var bytes))
            {
                codes.Add(bytes);
            }
        }

        cheat.Code = codes;
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

    private bool GetExpanded(object node)
    {
        return !_expandedState.TryGetValue(node, out var expanded) || expanded;
    }

    private void ToggleExpanded(object node)
    {
        var next = !GetExpanded(node);
        _expandedState[node] = next;
        NotifyStateChanged();
    }

    private static bool IsHex(string value, int length)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != length)
        {
            return false;
        }

        return value.Select(ch => ch is >= '0' and <= '9' || ch is >= 'a' and <= 'f' || ch is >= 'A' and <= 'F')
            .All(isHex => isHex);
    }

    private static bool TryParseHex8ToUInt(string value, out uint result)
    {
        if (IsHex(value, 8)) return uint.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out result);
        result = 0;
        return false;
    }

    private static bool TryParseHex8ToBytes(string value, out byte[] bytes)
    {
        bytes = [];
        if (!IsHex(value, 8))
        {
            return false;
        }

        bytes = new byte[4];
        for (var i = 0; i < 4; i++)
        {
            bytes[i] = Convert.ToByte(value.Substring(i * 2, 2), 16);
        }

        return true;
    }

    private static string BytesToHex(byte[] bytes)
    {
        return bytes.Length != 4 ? string.Empty : string.Concat(bytes.Select(b => b.ToString("X2")));
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }
}
