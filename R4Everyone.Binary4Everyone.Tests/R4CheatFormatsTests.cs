using Xunit;

namespace R4Everyone.Binary4Everyone.Tests;

public class R4CheatFormatsTests
{
    [Theory]
    [InlineData(R4CheatFormat.R4, ".dat")]
    [InlineData(R4CheatFormat.CycloDsEvolution, ".evoCheats")]
    [InlineData(R4CheatFormat.Edge, ".dat")]
    [InlineData(R4CheatFormat.M3Real, ".db")]
    public void GetFileExtension_ReturnsUsualExtension(R4CheatFormat format, string extension)
    {
        Assert.Equal(extension, R4CheatFormats.GetFileExtension(format));
        Assert.False(string.IsNullOrWhiteSpace(R4CheatFormats.GetDisplayName(format)));
    }

    [Fact]
    public void CanRead_IsTrueForEveryFormat()
    {
        Assert.All(Enum.GetValues<R4CheatFormat>(), format => Assert.True(R4CheatFormats.CanRead(format)));
    }

    [Fact]
    public void Detect_RecognizesEachReadableFormat()
    {
        Assert.Equal(R4CheatFormat.CycloDsEvolution, R4CheatFormats.Detect(ReferenceFixtures.CycloEvo));
        Assert.Equal(R4CheatFormat.Edge, R4CheatFormats.Detect(ReferenceFixtures.Edge));
        Assert.Equal(R4CheatFormat.R4, R4CheatFormats.Detect("R4 CheatCode\0\0\0\0"u8));
        Assert.Equal(R4CheatFormat.M3Real, R4CheatFormats.Detect(ReferenceFixtures.M3RealExtendedSjis));
        Assert.Equal(R4CheatFormat.M3Real, R4CheatFormats.Detect(ReferenceFixtures.M3RealDhtSjis));
        Assert.Null(R4CheatFormats.Detect([]));
        Assert.Null(R4CheatFormats.Detect("hello world, not a cheat file"u8));
    }

    [Theory]
    [InlineData(R4CheatFormat.R4)]
    [InlineData(R4CheatFormat.CycloDsEvolution)]
    [InlineData(R4CheatFormat.Edge)]
    public async Task SaveThenLoad_RoundTripsThroughDispatcher(R4CheatFormat format)
    {
        R4Database Build() => format == R4CheatFormat.R4
            ? ReferenceFixtures.BuildAsciiDatabase()
            : ReferenceFixtures.BuildDatabase();
        var bytes = await R4CheatFormats.SaveAsync(Build(), format);

        var (db, detected) = await R4CheatFormats.LoadAsync(bytes);

        Assert.Equal(format, detected);
        ReferenceFixtures.AssertEquivalent(Build(), db);
    }

    [Theory]
    [InlineData(R4CheatFormat.CycloDsEvolution, true)]
    [InlineData(R4CheatFormat.Edge, true)]
    [InlineData(R4CheatFormat.R4, false)]
    [InlineData(R4CheatFormat.M3Real, false)]
    public async Task IsCreatedByR4Everyone_OnlyTrueForFormatsWithCreatorTag(R4CheatFormat format, bool hasTag)
    {
        var bytes = await R4CheatFormats.SaveAsync(ReferenceFixtures.BuildAsciiDatabase(), format);

        Assert.Equal(hasTag, R4CheatFormats.HasCreatorTag(format));
        Assert.Equal(hasTag, R4CheatFormats.IsCreatedByR4Everyone(bytes));
    }

    [Fact]
    public void IsCreatedByR4Everyone_FalseForOtherTools()
    {
        // The reference fixtures come from r4cce, which tags Evo/EDGE files with "USAY".
        Assert.False(R4CheatFormats.IsCreatedByR4Everyone(ReferenceFixtures.CycloEvo));
        Assert.False(R4CheatFormats.IsCreatedByR4Everyone(ReferenceFixtures.Edge));
        Assert.False(R4CheatFormats.IsCreatedByR4Everyone([]));
        Assert.False(R4CheatFormats.IsCreatedByR4Everyone("hello world, R4EO"u8));
    }

    [Fact]
    public async Task SaveAsync_M3RealUsesOptions()
    {
        var options = new R4M3RealOptions { Mode = R4M3RealMode.Dht };

        var bytes = await R4CheatFormats.SaveAsync(ReferenceFixtures.BuildDatabase(), R4CheatFormat.M3Real, options);

        Assert.Equal(ReferenceFixtures.M3RealDhtSjis, bytes);
    }

    [Fact]
    public async Task LoadAsync_ConvertsEvolutionFileToR4()
    {
        var (db, _) = await R4CheatFormats.LoadAsync(ReferenceFixtures.Edge);
        db.Games.RemoveAt(1);

        var r4 = await R4CheatFormats.SaveAsync(db, R4CheatFormat.R4);
        var (reloaded, format) = await R4CheatFormats.LoadAsync(r4);

        Assert.Equal(R4CheatFormat.R4, format);
        ReferenceFixtures.AssertEquivalent(ReferenceFixtures.BuildAsciiDatabase(), reloaded);
    }

    [Fact]
    public async Task LoadAsync_ReadsM3RealFile()
    {
        var (db, format) = await R4CheatFormats.LoadAsync(ReferenceFixtures.M3RealExtendedUtf8NoFix);

        Assert.Equal(R4CheatFormat.M3Real, format);
        Assert.Equal(2, db.Games.Count);
    }

    [Fact]
    public async Task LoadAsync_UnknownFormat_Throws()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => R4CheatFormats.LoadAsync("not a cheat file"u8.ToArray()));
    }
}
