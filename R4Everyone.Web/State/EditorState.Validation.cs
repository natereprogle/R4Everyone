using R4Everyone.Binary4Everyone;

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

        if (Database != null)
        {
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

    private void SyncCheatCodes(R4Cheat cheat, List<string> blocks)
    {
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
}
