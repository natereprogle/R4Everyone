using R4Everyone.Binary4Everyone;
using R4Everyone.Web.Formatting;

namespace R4Everyone.Web.State;

public partial class EditorState
{
    public static string GetGameDisplayTitle(R4Game game)
    {
        if (!string.IsNullOrWhiteSpace(game.GameTitle))
        {
            return game.GameTitle;
        }

        return !string.IsNullOrWhiteSpace(game.GameId) ? game.GameId : "(untitled game)";
    }

    private void Revalidate()
    {
        var errors = new List<string>();
        foreach (var game in Database?.Games ?? [])
        {
            ValidateGame(game, errors);
        }

        _validationErrors = errors;
    }

    private void ValidateGame(R4Game game, List<string> errors)
    {
        var title = GetGameDisplayTitle(game);
        if (!string.IsNullOrEmpty(game.GameId) && game.GameId.Length != 4)
        {
            errors.Add($"Game '{title}' GameId must be 4 characters.");
        }

        // The checksum is required: the file format has no way to store a missing one.
        if (string.IsNullOrEmpty(game.GameChecksum))
        {
            errors.Add($"Game '{title}' needs a checksum. Enter 8 hex characters or calculate it from a ROM.");
        }
        else if (!HexWord.IsValid(game.GameChecksum))
        {
            errors.Add($"Game '{title}' checksum must be 8 hex characters.");
        }

        var masterCodes = GetMasterCodeText(game);
        for (var i = 0; i < masterCodes.Count; i++)
        {
            if (!HexWord.IsValid(masterCodes[i]))
            {
                errors.Add($"Game '{title}' master code {i + 1} must be 8 hex characters.");
            }
        }

        ValidateItems(title, game.Items, errors);
    }

    private void ValidateItems(string gameTitle, IEnumerable<IR4Item> items, List<string> errors)
    {
        foreach (var item in items)
        {
            switch (item)
            {
                case R4Folder folder:
                    ValidateItems(gameTitle, folder.Items, errors);
                    break;
                case R4Cheat cheat:
                    ValidateCheat(gameTitle, cheat, errors);
                    break;
            }
        }
    }

    private void ValidateCheat(string gameTitle, R4Cheat cheat, List<string> errors)
    {
        if (cheat.Code.Count % 2 != 0)
        {
            errors.Add($"Cheat '{cheat.Title}' in '{gameTitle}' must have an even number of code blocks.");
        }

        EnsureCheatCodeBuffer(cheat);
        var words = _cheatCodeWords[cheat];
        for (var i = 0; i < words.Count; i += 2)
        {
            var right = i + 1 < words.Count ? words[i + 1] : string.Empty;
            if (!HexWord.IsValid(words[i]) || !HexWord.IsValid(right))
            {
                errors.Add($"Cheat '{cheat.Title}' in '{gameTitle}' row {i / 2 + 1} must have 2 blocks of 8 hex characters.");
            }
        }
    }
}
