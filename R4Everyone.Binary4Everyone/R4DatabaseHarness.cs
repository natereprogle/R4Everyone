using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace R4Everyone.Binary4Everyone;

internal static class R4DatabaseHarness
{
    public static async Task VerifyNoClickRoundTripAsync(byte[] snapshot)
    {
        await using var input = new MemoryStream(snapshot);
        var db = await R4CheatDat.LoadAsync(input);

        await using var output = new MemoryStream();
        await R4CheatDat.SaveAsync(db, output);

        var roundTrip = output.ToArray();
        if (!snapshot.SequenceEqual(roundTrip))
        {
            throw new InvalidOperationException("No-click round trip mismatch.");
        }
    }

    public static async Task VerifyModifyOneGameAsync(byte[] snapshot)
    {
        await using var input = new MemoryStream(snapshot);
        var db = await R4CheatDat.LoadAsync(input);

        if (db.Games.Count == 0)
        {
            throw new InvalidOperationException("No games found to modify.");
        }

        var game = db.Games[0];
        db.EnsureGameMaterialized(game);

        var cheat = new R4Cheat
        {
            Title = "Harness Cheat",
            Description = "Injected by harness",
            Enabled = false,
            Code = [new byte[4], new byte[4]]
        };

        game.Items.Add(cheat);
        db.MarkGameDirty(game);

        await using var output = new MemoryStream();
        await R4CheatDat.SaveAsync(db, output);

        await using var verifyStream = new MemoryStream(output.ToArray());
        var verifyDb = await R4CheatDat.LoadAsync(verifyStream);

        var reloaded = verifyDb.Games.FirstOrDefault(g => g.GameId == game.GameId);
        if (reloaded == null)
        {
            throw new InvalidOperationException("Modified game was not found after reload.");
        }

        verifyDb.EnsureGameMaterialized(reloaded);
        if (reloaded.Items.OfType<R4Cheat>().All(c => c.Title != cheat.Title))
        {
            throw new InvalidOperationException("Modified cheat not found after reload.");
        }
    }

    public static async Task VerifyAddGameAsync(byte[] snapshot)
    {
        await using var input = new MemoryStream(snapshot);
        var db = await R4CheatDat.LoadAsync(input);

        var newGame = new R4Game("TEST")
        {
            GameTitle = "Harness Game",
            GameChecksum = "00000000",
            GameEnabled = true,
            MasterCodes = new uint[8]
        };

        db.Games.Add(newGame);
        db.MarkGameDirty(newGame);

        await using var output = new MemoryStream();
        await R4CheatDat.SaveAsync(db, output);

        await using var verifyStream = new MemoryStream(output.ToArray());
        var verifyDb = await R4CheatDat.LoadAsync(verifyStream);

        if (verifyDb.Games.All(g => g.GameId != newGame.GameId))
        {
            throw new InvalidOperationException("Added game was not found after reload.");
        }
    }
}
