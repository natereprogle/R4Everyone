using R4Everyone.Binary4Everyone;
using R4Everyone.Web.State;

namespace R4Everyone.Web.Services;

/// <summary>
/// Editor actions that need the user's confirmation or feedback. Components call these instead of
/// <see cref="EditorState"/> directly when an action deletes content or can surprise the user.
/// </summary>
public sealed class EditorCommands(EditorState editor, ConfirmService confirm, ToastService toasts)
{
    private const string UndoHint = "You can undo this.";

    /// <summary>
    /// Removes a game after the user confirms.
    /// </summary>
    /// <returns>True if the game was removed.</returns>
    public async Task<bool> RemoveGameAsync(R4Game game)
    {
        var contents = DescribeContents(game.Items);
        var message = contents == null
            ? $"'{EditorState.GetGameDisplayTitle(game)}' will be removed. {UndoHint}"
            : $"'{EditorState.GetGameDisplayTitle(game)}' and its {contents} will be removed. {UndoHint}";

        if (!await confirm.AskAsync(new ConfirmRequest("Remove game?", message, "Remove game")))
        {
            return false;
        }

        editor.RemoveGame(game);
        return true;
    }

    /// <summary>
    /// Removes the selected folder or cheat. Cheats and folders with content need confirmation; empty folders are
    /// removed straight away.
    /// </summary>
    public async Task RemoveSelectedItemAsync()
    {
        var item = editor.SelectedItem;
        var request = item switch
        {
            R4Cheat cheat => new ConfirmRequest("Remove cheat?",
                $"'{DisplayName(cheat)}' will be removed. {UndoHint}", "Remove cheat"),
            R4Folder folder when DescribeContents(folder.Items) is { } contents => new ConfirmRequest("Remove folder?",
                $"'{DisplayName(folder)}' and the {contents} inside it will be removed. {UndoHint}", "Remove folder"),
            R4Folder => null,
            _ => null
        };

        if (item == null || (request != null && !await confirm.AskAsync(request)))
        {
            return;
        }

        // The selection may have changed while the dialog was open.
        if (ReferenceEquals(editor.SelectedItem, item))
        {
            editor.RemoveSelectedItem();
        }
    }

    /// <summary>
    /// If the open file has unsaved changes, asks whether to discard them.
    /// </summary>
    /// <param name="action">What is about to replace the file, for example "Opening another file".</param>
    /// <returns>True if it is fine to continue.</returns>
    public Task<bool> ConfirmDiscardChangesAsync(string action)
    {
        if (!editor.IsDirty)
        {
            return Task.FromResult(true);
        }

        return confirm.AskAsync(new ConfirmRequest(
            "Discard unsaved changes?",
            $"{editor.LoadedFileName} has changes that haven't been saved. {action} will discard them.",
            "Discard changes"));
    }

    public void MoveSelectedCheat(R4Folder? destination)
    {
        if (editor.MoveSelectedCheat(destination))
        {
            toasts.ShowWarning(
                $"'{DisplayName(destination!)}' is a One Hot folder that already has an enabled cheat, so the moved cheat was disabled.");
        }
    }

    private static string DisplayName(IR4Item item) => string.IsNullOrWhiteSpace(item.Title)
        ? item is R4Folder ? "(untitled folder)" : "(untitled cheat)"
        : item.Title;

    /// <summary>
    /// Describes what a list contains, for example "2 folders and 5 cheats", or null if it is empty.
    /// </summary>
    private static string? DescribeContents(IEnumerable<IR4Item> items)
    {
        var folders = 0;
        var cheats = 0;
        Count(items);

        var parts = new List<string>();
        if (folders > 0) parts.Add(Plural(folders, "folder"));
        if (cheats > 0) parts.Add(Plural(cheats, "cheat"));
        return parts.Count == 0 ? null : string.Join(" and ", parts);

        void Count(IEnumerable<IR4Item> list)
        {
            foreach (var item in list)
            {
                if (item is R4Folder folder)
                {
                    folders++;
                    Count(folder.Items);
                }
                else
                {
                    cheats++;
                }
            }
        }
    }

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
