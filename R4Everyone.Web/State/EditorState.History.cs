using R4Everyone.Binary4Everyone;

namespace R4Everyone.Web.State;

/// <summary>
/// Undo/redo. Before each change, the game list and the contents of the affected game are snapshotted; undoing
/// restores that snapshot in place, so the <see cref="R4Game"/> instances the UI holds stay valid.
/// </summary>
public partial class EditorState
{
    private const int MaxHistoryEntries = 100;

    // Most recent entry last.
    private readonly List<HistoryEntry> _undoStack = [];
    private readonly List<HistoryEntry> _redoStack = [];

    // Consecutive edits to the same field (for example typing a title) are merged into one undo step.
    private (object Target, string Field)? _lastMergeKey;

    // Each distinct state gets a version; the file is dirty when the current version differs from the saved one.
    private int _version;
    private int _savedVersion;
    private int _lastVersion;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public bool IsDirty => _version != _savedVersion;

    public void Undo() => MoveThroughHistory(_undoStack, _redoStack);

    public void Redo() => MoveThroughHistory(_redoStack, _undoStack);

    /// <summary>
    /// Records an undo step, applies <paramref name="mutate"/>, then revalidates and notifies listeners.
    /// </summary>
    /// <param name="game">The game whose contents change, or null if only the game list changes.</param>
    /// <param name="mergeTarget">With <paramref name="mergeField"/>, lets repeated edits merge into one step.</param>
    private void Change(R4Game? game, Action mutate, object? mergeTarget = null, string? mergeField = null)
    {
        var mergeKey = mergeTarget != null && mergeField != null ? (mergeTarget, mergeField) : ((object, string)?)null;
        var continuesLastEdit = mergeKey is { } key && _lastMergeKey is { } last &&
                                ReferenceEquals(key.Item1, last.Target) && key.Item2 == last.Field && CanUndo;

        if (!continuesLastEdit)
        {
            Push(_undoStack, new HistoryEntry(Capture(game), _version));
            _redoStack.Clear();
            _version = ++_lastVersion;
        }

        _lastMergeKey = mergeKey;
        mutate();
        MarkDirty(game);
    }

    private void ResetHistory(bool isDirty)
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _lastMergeKey = null;
        _version = ++_lastVersion;

        // A file opened with changes (such as a corrected encoding marker) stays dirty until saved.
        _savedVersion = isDirty ? -1 : _version;
    }

    private void MarkHistorySaved()
    {
        _savedVersion = _version;
        _lastMergeKey = null;
    }

    private void MoveThroughHistory(List<HistoryEntry> from, List<HistoryEntry> to)
    {
        if (from.Count == 0)
        {
            return;
        }

        var entry = from[^1];
        from.RemoveAt(from.Count - 1);
        Push(to, new HistoryEntry(Capture(entry.Snapshot.Game), _version));

        Restore(entry.Snapshot);
        _version = entry.Version;
        _lastMergeKey = null;

        // Folders and cheats of the restored game are new copies, so a selection inside it falls back to the game.
        if (SelectedGame != null && Database?.Games.Contains(SelectedGame) == true)
        {
            if (ReferenceEquals(SelectedGame, entry.Snapshot.Game))
            {
                Select(SelectionInfo.ForGame(SelectedGame));
                return;
            }
        }
        else
        {
            ClearSelection();
        }

        Revalidate();
        NotifyStateChanged();
    }

    private static void Push(List<HistoryEntry> stack, HistoryEntry entry)
    {
        stack.Add(entry);
        if (stack.Count > MaxHistoryEntries)
        {
            stack.RemoveAt(0);
        }
    }

    private Snapshot Capture(R4Game? game)
    {
        if (game != null)
        {
            Database?.EnsureGameMaterialized(game);
        }

        return new Snapshot(Database?.Games.ToList() ?? [], game, game == null ? null : GameContent.From(game));
    }

    private void Restore(Snapshot snapshot)
    {
        var database = EnsureDatabase();
        database.Games.Clear();
        database.Games.AddRange(snapshot.Games);

        if (snapshot is { Game: { } game, Content: { } content })
        {
            ForgetBuffers(game.Items);
            _masterCodeText.Remove(game);
            content.ApplyTo(game);
            database.MarkGameDirty(game);
        }
    }

    private void ForgetBuffers(IEnumerable<IR4Item> items)
    {
        foreach (var item in items)
        {
            switch (item)
            {
                case R4Folder folder:
                    ForgetBuffers(folder.Items);
                    break;
                case R4Cheat cheat:
                    _cheatCodeWords.Remove(cheat);
                    _xyModePreference.Remove(cheat);
                    break;
            }
        }
    }

    private static IR4Item CloneItem(IR4Item item) => item switch
    {
        R4Folder folder => CloneFolder(folder),
        R4Cheat cheat => new R4Cheat
        {
            Title = cheat.Title,
            Description = cheat.Description,
            Enabled = cheat.Enabled,
            Code = cheat.Code.Select(word => word.ToArray()).ToList()
        },
        _ => throw new NotSupportedException($"Cannot copy item of type {item.GetType().Name}.")
    };

    private static R4Folder CloneFolder(R4Folder folder)
    {
        var copy = new R4Folder { Title = folder.Title, Description = folder.Description, OneHot = folder.OneHot };
        copy.Items.AddRange(folder.Items.Select(CloneItem));
        return copy;
    }

    private sealed record HistoryEntry(Snapshot Snapshot, int Version);

    private sealed record Snapshot(List<R4Game> Games, R4Game? Game, GameContent? Content);

    private sealed record GameContent(
        string GameId,
        string Title,
        string Checksum,
        bool Enabled,
        uint[] MasterCodes,
        List<IR4Item> Items)
    {
        public static GameContent From(R4Game game) => new(
            game.GameId,
            game.GameTitle,
            game.GameChecksum,
            game.GameEnabled,
            game.MasterCodes.ToArray(),
            game.Items.Select(CloneItem).ToList());

        // Items are copied again so later edits never change the snapshot.
        public void ApplyTo(R4Game game)
        {
            game.GameId = GameId;
            game.GameTitle = Title;
            game.GameChecksum = Checksum;
            game.GameEnabled = Enabled;
            game.MasterCodes = MasterCodes.ToArray();
            game.Items.Clear();
            game.Items.AddRange(Items.Select(CloneItem));
        }
    }
}
