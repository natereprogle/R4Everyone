using R4Everyone.Binary4Everyone;

namespace R4Everyone.Web.State;

/// <summary>
/// Holds the open cheat database, the current selection, and UI-only editing buffers. Components read from this
/// class and call its methods to mutate the database; every mutation raises <see cref="StateChanged"/>.
/// </summary>
public partial class EditorState
{
    // UI-only text buffers so users can type invalid hex without mutating the model. Words are stored in display
    // (big-endian) order.
    private readonly Dictionary<R4Game, string[]> _masterCodeText = new(ReferenceEqualityComparer<R4Game>.Instance);
    private readonly Dictionary<R4Cheat, List<string>> _cheatCodeWords = new(ReferenceEqualityComparer<R4Cheat>.Instance);

    // Remembers the X/Y activator mode chosen for a cheat, so it survives the X/Y buttons being set back to Ignore.
    private readonly Dictionary<R4Cheat, R4Game.XyActivatorMode> _xyModePreference =
        new(ReferenceEqualityComparer<R4Cheat>.Instance);

    // UI-only expand/collapse state for tree nodes.
    private readonly Dictionary<object, bool> _expandedState = new(ReferenceEqualityComparer<object>.Instance);
    private List<string> _validationErrors = [];

    public R4Database? Database { get; private set; }
    public bool HasLoadedFile { get; private set; }
    public string? LoadedFileName { get; private set; }

    public SelectionKind SelectionKind { get; private set; } = SelectionKind.None;
    public R4Game? SelectedGame { get; private set; }
    public R4Folder? SelectedFolder { get; private set; }
    public R4Cheat? SelectedCheat { get; private set; }

    public IReadOnlyList<string> ValidationErrors => _validationErrors;
    public bool IsValid => _validationErrors.Count == 0;

    public event Action? StateChanged;

    private void MarkDirty(R4Game? game)
    {
        if (!HasLoadedFile)
        {
            HasLoadedFile = true;
            LoadedFileName = NewFileName;
        }

        if (game != null)
        {
            Database?.MarkGameDirty(game);
        }

        Revalidate();
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }
}
