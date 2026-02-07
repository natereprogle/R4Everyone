using R4Everyone.Binary4Everyone;

namespace R4Everyone.Web.State;

public partial class EditorState
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

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }
}
