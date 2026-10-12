using System.Globalization;
using R4Everyone.Binary4Everyone;
using R4Everyone.Web.Formatting;

namespace R4Everyone.Web.State;

/// <summary>
/// Master code and cheat code editing. The editor shows each code word in big-endian hex; the model stores cheat
/// code words as little-endian bytes.
/// </summary>
public partial class EditorState
{
    public const int MasterCodeCount = 8;

    private const uint ResetCodeWord = 0xD2000000;

    public IReadOnlyList<string> GetMasterCodeText(R4Game game)
    {
        EnsureMasterCodeBuffer(game);
        return _masterCodeText[game];
    }

    /// <summary>
    /// Updates one master code from user input. Invalid input is kept for display but not written to the model.
    /// </summary>
    public void UpdateMasterCode(R4Game game, int index, string input)
    {
        EnsureMasterCodeBuffer(game);
        var buffer = _masterCodeText[game];
        if (index < 0 || index >= buffer.Length)
        {
            return;
        }

        var text = HexWord.Sanitize(input);
        buffer[index] = text;
        if (HexWord.TryParse(HexWord.SwapEndian(text), out var value))
        {
            game.MasterCodes[index] = value;
        }

        MarkDirty(game);
    }

    public string GetCheatCodeText(R4Cheat cheat)
    {
        EnsureCheatCodeBuffer(cheat);
        return CheatCodeText.FromWords(_cheatCodeWords[cheat]);
    }

    public bool HasActivator(R4Cheat cheat) => R4Game.AnalyzeCheatActivator(cheat).Options.HasActivator;

    public R4Game.CheatActivatorOptions GetActivatorOptions(R4Cheat cheat) =>
        ApplyXyModePreference(cheat, R4Game.AnalyzeCheatActivator(cheat).Options);

    /// <summary>
    /// Updates the selected cheat from the code textarea. When every word is valid the code is rebuilt so that
    /// any activator rows are normalized; otherwise the partial input is kept for display only.
    /// </summary>
    public void UpdateCheatCodeText(string text)
    {
        if (SelectedCheat == null)
        {
            return;
        }

        var words = CheatCodeText.ParseWords(text);
        var values = new List<uint>(words.Count);
        foreach (var word in words)
        {
            if (!HexWord.TryParse(word, out var value))
            {
                SetCheatCodeWords(SelectedCheat, words);
                return;
            }

            values.Add(value);
        }

        var analysis = R4Game.AnalyzeCheatActivator(values);
        RememberXyMode(SelectedCheat, analysis.Options);
        var options = ApplyXyModePreference(SelectedCheat, analysis.Options);
        SetCheatCodeWords(SelectedCheat, BuildCodeWords(analysis.PayloadWords, options));
    }

    public void UpdateActivatorOptions(R4Game.CheatActivatorOptions options)
    {
        if (SelectedCheat == null)
        {
            return;
        }

        _xyModePreference[SelectedCheat] = options.XyMode;
        var payload = R4Game.AnalyzeCheatActivator(SelectedCheat).PayloadWords;
        SetCheatCodeWords(SelectedCheat, BuildCodeWords(payload, options));
    }

    private void SetCheatCodeWords(R4Cheat cheat, List<string> words)
    {
        _cheatCodeWords[cheat] = words;

        // Keep the last valid code in the model until the user finishes typing.
        if (words.Count % 2 == 0 && words.All(HexWord.IsValid))
        {
            cheat.Code = words
                .Select(word => BitConverter.GetBytes(uint.Parse(word, NumberStyles.AllowHexSpecifier)))
                .ToList();
        }

        MarkDirty(SelectedGame);
    }

    private static List<string> BuildCodeWords(IReadOnlyList<uint> payload, R4Game.CheatActivatorOptions options)
    {
        // BuildCheatCodeWithActivator appends a reset row, so drop any already present to avoid duplicates.
        if (options.HasActivator)
        {
            payload = TrimTrailingResetRows(payload);
        }

        return R4Game.BuildCheatCodeWithActivator(payload, options).Select(HexWord.FromUInt).ToList();
    }

    private static List<uint> TrimTrailingResetRows(IReadOnlyList<uint> words)
    {
        var list = words.ToList();
        while (list is [.., ResetCodeWord, 0x00000000])
        {
            list.RemoveRange(list.Count - 2, 2);
        }

        return list;
    }

    private void RememberXyMode(R4Cheat cheat, R4Game.CheatActivatorOptions options)
    {
        if (UsesXyButtons(options))
        {
            _xyModePreference[cheat] = options.XyMode;
        }
    }

    /// <summary>
    /// A code without X/Y conditions does not encode an X/Y mode, so fall back to the one the user last chose.
    /// </summary>
    private R4Game.CheatActivatorOptions ApplyXyModePreference(R4Cheat cheat, R4Game.CheatActivatorOptions options)
    {
        if (UsesXyButtons(options) || !_xyModePreference.TryGetValue(cheat, out var mode))
        {
            return options;
        }

        return new R4Game.CheatActivatorOptions(options.ButtonStates, mode);
    }

    private static bool UsesXyButtons(R4Game.CheatActivatorOptions options) =>
        options.ButtonStates.GetValueOrDefault(R4Game.ActivatorButton.X, R4Game.ActivatorKeyState.Ignore) !=
        R4Game.ActivatorKeyState.Ignore ||
        options.ButtonStates.GetValueOrDefault(R4Game.ActivatorButton.Y, R4Game.ActivatorKeyState.Ignore) !=
        R4Game.ActivatorKeyState.Ignore;

    private void EnsureMasterCodeBuffer(R4Game game)
    {
        if (game.MasterCodes.Length != MasterCodeCount)
        {
            var resized = new uint[MasterCodeCount];
            Array.Copy(game.MasterCodes, resized, Math.Min(game.MasterCodes.Length, MasterCodeCount));
            game.MasterCodes = resized;
        }

        if (!_masterCodeText.ContainsKey(game))
        {
            _masterCodeText[game] = game.MasterCodes
                .Select(code => HexWord.SwapEndian(HexWord.FromUInt(code)))
                .ToArray();
        }
    }

    private void EnsureCheatCodeBuffer(R4Cheat cheat)
    {
        if (_cheatCodeWords.ContainsKey(cheat))
        {
            return;
        }

        var words = cheat.Code
            .Select(bytes => bytes.Length == 4 ? HexWord.FromUInt(BitConverter.ToUInt32(bytes)) : string.Empty)
            .ToList();
        // A new cheat starts with one empty row, which validation flags until the user enters a code.
        if (words.Count == 0)
        {
            words.AddRange([string.Empty, string.Empty]);
        }
        else if (words.Count % 2 != 0)
        {
            words.Add(string.Empty);
        }

        _cheatCodeWords[cheat] = words;
    }
}
