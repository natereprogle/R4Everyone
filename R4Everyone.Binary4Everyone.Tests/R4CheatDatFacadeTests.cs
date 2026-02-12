using Xunit;

namespace R4Everyone.Binary4Everyone.Tests;

public class R4CheatDatFacadeTests
{
    [Fact]
    public async Task SaveAndLoad_WithStreamFacade_RoundTrips()
    {
        var db = TestHelpers.CreateDatabaseWithOneGame();
        await using var stream = new MemoryStream();

        await R4CheatDat.SaveAsync(db, stream);
        stream.Position = 0;
        var loaded = await R4CheatDat.LoadAsync(stream);

        Assert.Equal(R4Encoding.UTF8, loaded.FileEncoding);
        Assert.Single(loaded.Games);
        Assert.Equal("ABCD", loaded.Games[0].GameId);
    }

    [Fact]
    public async Task SaveAndLoad_WithFileFacade_RoundTrips()
    {
        var db = TestHelpers.CreateDatabaseWithOneGame();
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.dat");
        await File.WriteAllBytesAsync(path, [0x00]);

        try
        {
            await R4CheatDat.SaveAsync(db, path);
            var loaded = await R4CheatDat.LoadAsync(path);

            Assert.Equal(path, loaded.R4FilePath);
            Assert.Single(loaded.Games);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
