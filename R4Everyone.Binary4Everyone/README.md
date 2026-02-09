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

Notes
- Streams must be readable/seekable for load and writable/seekable for save.
- R4Database implements IAsyncDisposable to flush logging resources; use `await using`
  when possible.
