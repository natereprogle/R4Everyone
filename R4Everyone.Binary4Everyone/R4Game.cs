using System.Text;

namespace R4Everyone.Binary4Everyone;

public sealed class R4Game(string id) : IR4Container
{
    public enum ActivatorButton
    {
        L,
        R,
        A,
        B,
        X,
        Y,
        Up,
        Down,
        Left,
        Right,
        Start,
        Select
    }

    public enum ActivatorKeyState
    {
        Ignore,
        Hold,
        Release
    }

    public enum XyActivatorMode
    {
        Standard,
        ActionReplay
    }

    public sealed class CheatActivatorOptions(
        IReadOnlyDictionary<ActivatorButton, ActivatorKeyState>? buttonStates = null,
        XyActivatorMode xyMode = XyActivatorMode.Standard)
    {
        public IReadOnlyDictionary<ActivatorButton, ActivatorKeyState> ButtonStates { get; } = CloneButtonStates(buttonStates);
        public XyActivatorMode XyMode { get; } = xyMode;
        public bool HasActivator => ButtonStates.Values.Any(state => state != ActivatorKeyState.Ignore);
    }

    public sealed class CheatActivatorAnalysis(
        IReadOnlyList<uint> payloadWords,
        CheatActivatorOptions options,
        bool hadLeadingActivatorRows,
        bool hadTrailingResetRow)
    {
        public static CheatActivatorAnalysis Empty { get; } =
            new([], new CheatActivatorOptions(), false, false);

        public IReadOnlyList<uint> PayloadWords { get; } = payloadWords;
        public CheatActivatorOptions Options { get; } = options;
        public bool HadLeadingActivatorRows { get; } = hadLeadingActivatorRows;
        public bool HadTrailingResetRow { get; } = hadTrailingResetRow;
    }

    /// <summary>
    /// The game's ID
    /// </summary>
    public string GameId { get; set; } = id;

    /// <summary>
    /// The title of the game
    /// </summary>
    public string GameTitle { get; set; } = "My Game Title";

    /// <summary>
    /// Whether the game's cheats are enabled
    /// </summary>
    public bool GameEnabled { get; set; }

    /// <summary>
    /// The game's checksum. Calculated via the CRC32 Helper
    /// </summary>
    public string GameChecksum { get; set; } = "2F00B549";

    public uint[] MasterCodes { get; set; } =
        [0x00000000, 0x01000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000];

    /// Nested cheats and folders within this folder. Uses a List&lt;object&gt; to maintain order
    public List<IR4Item> Items { get; } = [];

    /// <summary>
    /// The game's size, calculated by adding the sizes of all folders and cheats
    /// This does not take into account the ID or Checksum, as those are used in the address block of the R4 database and not the game itself
    /// </summary>
    public uint Size
    {
        get
        {
            var titleLength = Encoding.UTF8.GetByteCount(GameTitle);
            var paddingNeeded = (4 - titleLength % 4) % 4;
            if (paddingNeeded == 0) paddingNeeded = 4;

            var totalSize = (uint)(titleLength + paddingNeeded);
            totalSize += 4 + 32; // item count + enabled flag + master codes

            return Items.Aggregate(totalSize, (current, item) => current + item switch
            {
                R4Folder f => f.Size,
                R4Cheat c => c.Size,
                _ => 0
            });
        }
    }

    public uint FlattenedItemCount => (uint)CountFlattened(Items);

    private static int CountFlattened(IEnumerable<IR4Item> items)
    {
        var count = 0;
        foreach (var item in items)
        {
            count += 1;
            if (item is R4Folder f)
                count += CountFlattened(f.Items);
        }

        return count;
    }

    public static CheatActivatorAnalysis AnalyzeCheatActivator(R4Cheat cheat)
    {
        var words = cheat.Code
            .Where(block => block.Length == 4)
            .Select(b => BitConverter.ToUInt32(b))
            .ToList();

        return AnalyzeCheatActivator(words);
    }

    public static CheatActivatorAnalysis AnalyzeCheatActivator(IReadOnlyList<uint> words)
    {
        if (words.Count < 2)
        {
            return new CheatActivatorAnalysis(words.ToList(), new CheatActivatorOptions(), false, false);
        }

        var states = CreateDefaultButtonStates();
        var xyMode = XyActivatorMode.Standard;
        var index = 0;
        var hasActivatorRows = false;

        while (index + 1 < words.Count)
        {
            var addressWord = words[index];
            var conditionWord = words[index + 1];

            if (!TryParseActivatorRow(addressWord, conditionWord, states, ref xyMode))
            {
                break;
            }

            hasActivatorRows = true;
            index += 2;
        }

        var payloadEnd = words.Count;
        var hasResetRow = false;
        if (hasActivatorRows && payloadEnd - index >= 2 && words[^2] == 0xD2000000 && words[^1] == 0x00000000)
        {
            payloadEnd -= 2;
            hasResetRow = true;
        }

        var payload = words.Skip(index).Take(payloadEnd - index).ToList();
        var options = new CheatActivatorOptions(states, xyMode);
        return new CheatActivatorAnalysis(payload, options, hasActivatorRows, hasResetRow);
    }

    public static IReadOnlyList<uint> BuildCheatCodeWithActivator(
        IReadOnlyList<uint> payloadWords,
        CheatActivatorOptions? options)
    {
        var safeOptions = options ?? new CheatActivatorOptions();
        var result = new List<uint>(payloadWords.Count + 6);
        var hasActivatorRows = false;

        if (TryBuildConditionWord(safeOptions.ButtonStates, KeyInputBits, out var keyInputCondition))
        {
            result.Add(0x94000130);
            result.Add(keyInputCondition);
            hasActivatorRows = true;
        }

        var xyBits = safeOptions.XyMode switch
        {
            XyActivatorMode.ActionReplay => MirrorXyBits,
            _ => ExtKeyInBits
        };

        if (TryBuildConditionWord(safeOptions.ButtonStates, xyBits, out var xyCondition))
        {
            result.Add(safeOptions.XyMode == XyActivatorMode.ActionReplay
                ? 0x927FFFA8
                : 0x94000136);
            result.Add(xyCondition);
            hasActivatorRows = true;
        }

        result.AddRange(payloadWords);
        if (!hasActivatorRows) return result;
        result.Add(0xD2000000);
        result.Add(0x00000000);

        return result;
    }

    public static void ReplaceCheatPayloadPreservingActivator(R4Cheat cheat, IReadOnlyList<uint> payloadWords)
    {
        var analysis = AnalyzeCheatActivator(cheat);
        cheat.Code = BuildCheatCodeWithActivator(payloadWords, analysis.Options)
            .Select(BitConverter.GetBytes)
            .ToList();
    }

    public static void ApplyCheatActivator(R4Cheat cheat, CheatActivatorOptions? options)
    {
        var analysis = AnalyzeCheatActivator(cheat);
        cheat.Code = BuildCheatCodeWithActivator(analysis.PayloadWords, options)
            .Select(BitConverter.GetBytes)
            .ToList();
    }

    public static Dictionary<ActivatorButton, ActivatorKeyState> CreateDefaultButtonStates()
    {
        return Enum.GetValues<ActivatorButton>()
            .ToDictionary(button => button, _ => ActivatorKeyState.Ignore);
    }

    private static bool TryParseActivatorRow(
        uint addressWord,
        uint conditionWord,
        Dictionary<ActivatorButton, ActivatorKeyState> states,
        ref XyActivatorMode xyMode)
    {
        var map = addressWord switch
        {
            0x94000130 => KeyInputBits,
            0x94000136 => ExtKeyInBits,
            0x927FFFA8 => MirrorXyBits,
            _ => null
        };

        if (map == null)
        {
            return false;
        }

        if (addressWord == 0x927FFFA8)
        {
            xyMode = XyActivatorMode.ActionReplay;
        }

        var zzzz = (ushort)(conditionWord >> 16);
        var yyyy = (ushort)(conditionWord & 0xFFFF);
        var mask = (ushort)(~zzzz & 0xFFFF);

        foreach (var (button, bit) in map)
        {
            if ((mask & bit) == 0)
            {
                continue;
            }

            states[button] = (yyyy & bit) != 0 ? ActivatorKeyState.Release : ActivatorKeyState.Hold;
        }

        return true;
    }

    private static bool TryBuildConditionWord(
        IReadOnlyDictionary<ActivatorButton, ActivatorKeyState> states,
        IReadOnlyDictionary<ActivatorButton, ushort> bitMap,
        out uint conditionWord)
    {
        ushort mask = 0;
        ushort requiredReleased = 0;

        foreach (var (button, bit) in bitMap)
        {
            var state = states.TryGetValue(button, out var value) ? value : ActivatorKeyState.Ignore;
            if (state == ActivatorKeyState.Ignore)
            {
                continue;
            }

            mask |= bit;
            if (state == ActivatorKeyState.Release)
            {
                requiredReleased |= bit;
            }
        }

        if (mask == 0)
        {
            conditionWord = 0;
            return false;
        }

        var zzzz = (ushort)(~mask & 0xFFFF);
        conditionWord = ((uint)zzzz << 16) | requiredReleased;
        return true;
    }

    private static Dictionary<ActivatorButton, ActivatorKeyState> CloneButtonStates(
        IReadOnlyDictionary<ActivatorButton, ActivatorKeyState>? states)
    {
        var copy = CreateDefaultButtonStates();
        if (states == null)
        {
            return copy;
        }

        foreach (var (button, state) in states)
        {
            copy[button] = state;
        }

        return copy;
    }

    private static readonly IReadOnlyDictionary<ActivatorButton, ushort> KeyInputBits =
        new Dictionary<ActivatorButton, ushort>
        {
            [ActivatorButton.A] = 1 << 0,
            [ActivatorButton.B] = 1 << 1,
            [ActivatorButton.Select] = 1 << 2,
            [ActivatorButton.Start] = 1 << 3,
            [ActivatorButton.Right] = 1 << 4,
            [ActivatorButton.Left] = 1 << 5,
            [ActivatorButton.Up] = 1 << 6,
            [ActivatorButton.Down] = 1 << 7,
            [ActivatorButton.R] = 1 << 8,
            [ActivatorButton.L] = 1 << 9
        };

    private static readonly IReadOnlyDictionary<ActivatorButton, ushort> ExtKeyInBits =
        new Dictionary<ActivatorButton, ushort>
        {
            [ActivatorButton.X] = 1 << 0,
            [ActivatorButton.Y] = 1 << 1
        };

    private static readonly IReadOnlyDictionary<ActivatorButton, ushort> MirrorXyBits =
        new Dictionary<ActivatorButton, ushort>
        {
            [ActivatorButton.X] = 1 << 10,
            [ActivatorButton.Y] = 1 << 11
        };
}