using R4Everyone.Binary4Everyone;

namespace R4Everyone.Web.State;

public partial class EditorState
{
    public bool IsExpanded(object node) => !_expandedState.TryGetValue(node, out var expanded) || expanded;

    public void ToggleExpanded(object node)
    {
        _expandedState[node] = !IsExpanded(node);
        NotifyStateChanged();
    }

    /// <summary>
    /// Selects a game, folder, or cheat. Selecting a game that has not been read yet parses it from the file,
    /// which throws <see cref="InvalidDataException"/> if that part of the file is corrupt.
    /// </summary>
    public void Select(SelectionInfo selection)
    {
        var game = selection.Game;
        Database?.EnsureGameMaterialized(game);

        SelectionKind = selection.Kind;
        SelectedGame = game;
        SelectedFolder = selection.Folder;
        SelectedCheat = selection.Cheat;

        EnsureMasterCodeBuffer(game);
        if (SelectedCheat != null)
        {
            EnsureCheatCodeBuffer(SelectedCheat);
            RememberXyMode(SelectedCheat, R4Game.AnalyzeCheatActivator(SelectedCheat).Options);
        }

        // Materializing a game exposes its cheats to validation for the first time.
        Revalidate();
        NotifyStateChanged();
    }

    public void Deselect()
    {
        ClearSelection();
        NotifyStateChanged();
    }

    private void ClearSelection()
    {
        SelectionKind = SelectionKind.None;
        SelectedGame = null;
        SelectedFolder = null;
        SelectedCheat = null;
    }
}
