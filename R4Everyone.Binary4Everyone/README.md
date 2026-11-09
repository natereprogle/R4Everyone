R4Everyone.Binary4Everyone is the core library for reading, editing, and writing R4
cheat databases (usrcheat.dat). It provides strongly-typed models for games, folders,
and cheats, plus a codec that handles the binary format, encoding, and checksums.

Key concepts
- R4CheatDat: the main entry point for loading/saving databases.
- R4Database: in-memory representation of a cheat database, including metadata and
  a game list. Uses lazy game loading to avoid parsing full game payloads until needed.
- R4Game: a game entry with GameId, GameChecksum, MasterCodes, and ordered Items.
- R4Folder: a folder with OneHot and ordered Items (folders and cheats).
- R4Cheat: a cheat with Title, Description, Enabled, and Code blocks.

Lazy loading and dirty tracking
- LoadAsync reads the header and game table first. Each game is backed by a lazy slot.
- Call EnsureGameMaterialized(game) before reading or editing game.Items.
- Call MarkGameDirty(game) after editing a game or any of its nested items, or when
  you add a new game. This ensures the game will be reserialized on save.
- MaterializeAllGames() forces full parsing of every game in the database.

Database validation
- Use R4Database.ValidateDatabaseAsync(path or stream) to check the magic string,
  minimum size, and encoding markers before load.

Game and cheat data
- R4Game.GameId is a 4-character string.
- R4Game.GameChecksum is an 8-hex-character string.
- R4Game.MasterCodes is an array of 8 uint values (4 bytes each).
- R4Cheat.Code is a List<byte[]> where each entry is exactly 4 bytes (one 8-hex block).
  The library writes these bytes as-is, so callers should provide the exact byte order
  expected by the target database.

Basic usage
```csharp
using R4Everyone.Binary4Everyone;

await using var db = await R4CheatDat.LoadAsync("usrcheat.dat");

// Work with an existing game.
var game = db.Games.First();
db.EnsureGameMaterialized(game);

var cheat = new R4Cheat
{
    Title = "Infinite HP",
    Description = "Always full health",
    Enabled = true,
    Code = new List<byte[]>
    {
        new byte[] { 0x94, 0x00, 0x01, 0x30 },
        new byte[] { 0xFC, 0xFF, 0x00, 0x00 }
    }
};

game.Items.Add(cheat);
db.MarkGameDirty(game);

await R4CheatDat.SaveAsync(db, "usrcheat.dat");
```

Creating a new game
```csharp
var newGame = new R4Game("TEST")
{
    GameTitle = "My Test Game",
    GameChecksum = "00000000",
    GameEnabled = true,
    MasterCodes = new uint[8]
};

db.Games.Add(newGame);
db.MarkGameDirty(newGame);
```

Saving to a stream
```csharp
await using var output = new MemoryStream();
await R4CheatDat.SaveAsync(db, output);
```

Other cheat formats
The library also reads and writes the formats r4cce supports:

| Format | Extension | Read | Write | Class |
|---|---|---|---|---|
| R4 usrcheat.dat | .dat | yes | yes | R4CheatDat |
| CycloDS Evolution | .evoCheats | yes | yes | R4EvolutionCheats |
| EDGE | .dat | yes | yes | R4EvolutionCheats |
| M3 DS Real | .db | yes | yes | R4M3RealDb |

R4CheatFormats detects a file's format from its magic bytes (or, for M3 DS Real, its index structure) and dispatches
to the right codec:
```csharp
var (db, format) = await R4CheatFormats.LoadAsync(File.ReadAllBytes("user.evoCheats"));
var edge = await R4CheatFormats.SaveAsync(db, R4CheatFormat.Edge);
var m3 = await R4CheatFormats.SaveAsync(db, R4CheatFormat.M3Real,
    new R4M3RealOptions { Mode = R4M3RealMode.Dht, Encoding = R4Encoding.SJIS });

// M3 DS Real files don't record their settings. Inspect guesses them; pass known values to override.
var info = R4M3RealDb.Inspect(m3);
var reread = R4M3RealDb.Load(m3, new R4M3RealReadOptions { Encoding = R4Encoding.GBK });
var sameBytes = R4M3RealDb.Export(reread, info.ToWriteOptions());
```

- CycloDS Evolution and EDGE files are RC4-encrypted, zlib-compressed, and MD5-checked. Loading throws
  InvalidDataException if a check fails. They store UTF-8 text, one level of folders, and on/off states, but not
  the database title, master codes, or game enable flags. Nested folders are merged into their top-level folder on
  save. R4EvolutionCheats.FindOverlongCheats lists cheats over 255 rows, which carts may reject.
- M3 DS Real does not store on/off states, master codes, or game enable flags, so cheats load disabled.
  Extended mode loses the One Hot flag; DHT mode loses notes and game titles (the game ID is used instead). The file
  doesn't record its encoding, note position, mode, or Character Fix; R4M3RealDb.Inspect detects UTF-8 (otherwise it
  assumes Shift-JIS, since GBK and Big5 can't be told apart), the note position, the mode, and whether names are
  already character-fixed. Text changed by Character Fix can't be restored. The "Character Fix" option checks each
  character's own encoded bytes rather than r4cce's previous-byte test, so it is also correct for UTF-8, GBK, and Big5.
- Games loaded from these formats are fully materialized and marked dirty.
- R4Md5 is a managed MD5 for Blazor WebAssembly, where System.Security.Cryptography.MD5 is not supported.
- CycloDS Evolution and EDGE files are saved with the creator tag R4CheatFormats.CreatorTag (`R4EO`) at inner-header
  offset 0x24, where r4cce writes `USAY`; r4cce still opens these files but may call them unofficial when overwriting
  them. R4CheatFormats.IsCreatedByR4Everyone checks the tag. R4 and M3 DS Real files have no creator field and are
  written without any marker (R4CheatFormats.HasCreatorTag returns false for them).

Activator helpers (R4Game)
```csharp
using R4Everyone.Binary4Everyone;

var cheat = new R4Cheat
{
    Title = "Sample Cheat",
    Description = "Payload only to start",
    Enabled = true,
    Code = new List<byte[]>
    {
        // Payload row(s), 4-byte words
        new byte[] { 0x12, 0x34, 0x56, 0x78 },
        new byte[] { 0x9A, 0xBC, 0xDE, 0xF0 }
    }
};

// Analyze existing code for activator rows and payload split.
var analysis = R4Game.AnalyzeCheatActivator(cheat);
var payloadWords = analysis.PayloadWords;

// Build activator options:
// Hold L + R, Release A, Ignore others.
var states = R4Game.CreateDefaultButtonStates();
states[R4Game.ActivatorButton.L] = R4Game.ActivatorKeyState.Hold;
states[R4Game.ActivatorButton.R] = R4Game.ActivatorKeyState.Hold;
states[R4Game.ActivatorButton.A] = R4Game.ActivatorKeyState.Release;

var options = new R4Game.CheatActivatorOptions(
    states,
    R4Game.XyActivatorMode.Standard // uses 94000136 for X/Y when needed
);

// Apply activator to the cheat's current payload.
R4Game.ApplyCheatActivator(cheat, options);

// Preserve current activator while replacing payload.
R4Game.ReplaceCheatPayloadPreservingActivator(
    cheat,
    new List<uint>
    {
        0x12345678,
        0x9ABCDEF0
    });

// Build words directly if you want explicit control:
var fullWords = R4Game.BuildCheatCodeWithActivator(payloadWords, options);
```

Action Replay-compatible X/Y example
```csharp
var states = R4Game.CreateDefaultButtonStates();
states[R4Game.ActivatorButton.X] = R4Game.ActivatorKeyState.Hold;
states[R4Game.ActivatorButton.Y] = R4Game.ActivatorKeyState.Release;

var actionReplayOptions = new R4Game.CheatActivatorOptions(
    states,
    R4Game.XyActivatorMode.ActionReplay // uses 927FFFA8 for X/Y line
);

R4Game.ApplyCheatActivator(cheat, actionReplayOptions);
```

Notes
- Streams must be readable/seekable for load and writable/seekable for save.
- R4Database implements IAsyncDisposable to flush logging resources; use `await using`
  when possible.
