using Xunit;

namespace R4Everyone.Binary4Everyone.Tests;

public class R4LazyGameSlotAndDatabaseStateTests
{
    [Fact]
    public void CreateNew_SetsDirtyAndMaterializedFlags()
    {
        var db = new R4Database();
        var game = new R4Game("ABCD");

        var slot = R4LazyGameSlot.CreateNew(db, game);

        Assert.True(slot.IsDirty);
        Assert.True(slot.IsMaterialized);
        Assert.Equal(0u, slot.SourceOffset);
        Assert.Equal(0u, slot.SourceLength);
    }

    [Fact]
    public void CreateExisting_StartsCleanAndNotMaterialized()
    {
        var db = new R4Database();
        var game = new R4Game("ABCD");

        var slot = R4LazyGameSlot.CreateExisting(db, game, 12, 20);

        Assert.False(slot.IsDirty);
        Assert.False(slot.IsMaterialized);
        Assert.Equal(12u, slot.SourceOffset);
        Assert.Equal(20u, slot.SourceLength);
    }

    [Fact]
    public void EnsureMaterialized_WithoutSnapshot_MarksMaterializedWithoutParsing()
    {
        var db = new R4Database();
        var game = new R4Game("ABCD") { GameTitle = "Original" };
        var slot = R4LazyGameSlot.CreateExisting(db, game, 0, 20);

        slot.EnsureMaterialized();

        Assert.True(slot.IsMaterialized);
        Assert.Equal("Original", game.GameTitle);
    }

    [Fact]
    public void EnsureMaterialized_WithSnapshot_ParsesAndAppliesGame()
    {
        var parsedGame = TestHelpers.CreateGame();
        parsedGame.Items.Add(TestHelpers.CreateCheat());
        var gameBytes = TestHelpers.SerializeGame(parsedGame);

        var db = new R4Database();
        db.SetSnapshot(gameBytes);

        var target = new R4Game("ABCD")
        {
            GameTitle = "Old",
            GameEnabled = false,
            MasterCodes = new uint[8]
        };
        target.Items.Add(new R4Folder());

        var slot = R4LazyGameSlot.CreateExisting(db, target, 0, (uint)gameBytes.Length);
        slot.EnsureMaterialized();

        Assert.True(slot.IsMaterialized);
        Assert.Equal("T", target.GameTitle);
        Assert.True(target.GameEnabled);
        Assert.Equal(parsedGame.MasterCodes, target.MasterCodes);
        Assert.Single(target.Items);
        Assert.IsType<R4Cheat>(target.Items[0]);
    }

    [Fact]
    public void MarkDirty_SetsFlag()
    {
        var slot = R4LazyGameSlot.CreateExisting(new R4Database(), new R4Game("ABCD"), 0, 8);

        slot.MarkDirty();

        Assert.True(slot.IsDirty);
    }

    [Fact]
    public void CanUseRawSource_RequiresCleanSourceLengthAndSnapshot()
    {
        var db = new R4Database();
        var game = new R4Game("ABCD");
        var cleanSlot = R4LazyGameSlot.CreateExisting(db, game, 0, 4);

        Assert.False(cleanSlot.CanUseRawSource());

        db.SetSnapshot([1, 2, 3, 4]);
        Assert.True(cleanSlot.CanUseRawSource());

        cleanSlot.MarkDirty();
        Assert.False(cleanSlot.CanUseRawSource());
    }

    [Fact]
    public void GetRawSlice_ReturnsConfiguredSegment()
    {
        var db = new R4Database();
        db.SetSnapshot([10, 11, 12, 13, 14, 15]);
        var slot = R4LazyGameSlot.CreateExisting(db, new R4Game("ABCD"), 2, 3);

        var slice = slot.GetRawSlice().ToArray();

        Assert.Equal(new byte[] { 12, 13, 14 }, slice);
    }

    [Fact]
    public void MarkGameDirty_CreatesSlotForNewGame()
    {
        var db = new R4Database();
        var game = new R4Game("ABCD");

        db.MarkGameDirty(game);

        Assert.True(db.LazySlots.ContainsKey(game));
        Assert.True(db.LazySlots[game].IsDirty);
    }

    [Fact]
    public void MarkGameDirty_ExistingSlot_BecomesDirty()
    {
        var db = new R4Database();
        var game = new R4Game("ABCD");
        db.LazySlots[game] = R4LazyGameSlot.CreateExisting(db, game, 0, 4);

        db.MarkGameDirty(game);

        Assert.True(db.LazySlots[game].IsDirty);
    }

    [Fact]
    public void EnsureGameMaterialized_MaterializesOnlyTargetGame()
    {
        var gameAData = TestHelpers.CreateGame("ABCD");
        gameAData.Items.Add(TestHelpers.CreateCheat());
        var bytesA = TestHelpers.SerializeGame(gameAData);

        var gameBData = TestHelpers.CreateGame("WXYZ");
        gameBData.Items.Add(TestHelpers.CreateCheat());
        var bytesB = TestHelpers.SerializeGame(gameBData);

        var snapshot = bytesA.Concat(bytesB).ToArray();
        var db = new R4Database();
        db.SetSnapshot(snapshot);

        var gameA = new R4Game("ABCD");
        var gameB = new R4Game("WXYZ");
        db.LazySlots[gameA] = R4LazyGameSlot.CreateExisting(db, gameA, 0, (uint)bytesA.Length);
        db.LazySlots[gameB] = R4LazyGameSlot.CreateExisting(db, gameB, (uint)bytesA.Length, (uint)bytesB.Length);

        db.EnsureGameMaterialized(gameA);

        Assert.Single(gameA.Items);
        Assert.Empty(gameB.Items);
    }

    [Fact]
    public void MaterializeAllGames_MaterializesAllSlots()
    {
        var gameData = TestHelpers.CreateGame("ABCD");
        gameData.Items.Add(TestHelpers.CreateCheat());
        var bytes = TestHelpers.SerializeGame(gameData);

        var db = new R4Database();
        db.SetSnapshot(bytes.Concat(bytes).ToArray());
        var gameA = new R4Game("ABCD");
        var gameB = new R4Game("WXYZ");
        db.LazySlots[gameA] = R4LazyGameSlot.CreateExisting(db, gameA, 0, (uint)bytes.Length);
        db.LazySlots[gameB] = R4LazyGameSlot.CreateExisting(db, gameB, (uint)bytes.Length, (uint)bytes.Length);

        db.MaterializeAllGames();

        Assert.Single(gameA.Items);
        Assert.Single(gameB.Items);
    }

    [Fact]
    public void GetOrCreateSlot_ReturnsExistingSlotWhenPresent()
    {
        var db = new R4Database();
        var game = new R4Game("ABCD");
        var existing = R4LazyGameSlot.CreateExisting(db, game, 1, 2);
        db.LazySlots[game] = existing;

        var slot = db.GetOrCreateSlot(game);

        Assert.Same(existing, slot);
    }

    [Fact]
    public async Task ValidateDatabaseAsync_FilePathMissing_ThrowsFileNotFound()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() => R4Database.ValidateDatabaseAsync("Z:\\missing\\db.dat"));
    }

    [Fact]
    public async Task DisposeAsync_CompletesWithoutThrowing()
    {
        var db = new R4Database();

        await db.DisposeAsync();
    }
}
