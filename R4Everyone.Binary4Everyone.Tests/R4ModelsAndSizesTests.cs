using Xunit;

namespace R4Everyone.Binary4Everyone.Tests;

public class R4ModelsAndSizesTests
{
    [Fact]
    public void Models_AreImplementedByConcreteTypes()
    {
        Assert.IsAssignableFrom<IR4Item>(new R4Cheat());
        Assert.IsAssignableFrom<IR4Item>(new R4Folder());
        Assert.IsAssignableFrom<IR4Container>(new R4Folder());
        Assert.IsAssignableFrom<IR4Container>(new R4Game("ABCD"));
    }

    [Fact]
    public void R4Cheat_DefaultValues_AreSet()
    {
        var cheat = new R4Cheat();

        Assert.Equal("New Cheat", cheat.Title);
        Assert.Equal("New Cheat Description", cheat.Description);
        Assert.True(cheat.Enabled);
        Assert.Empty(cheat.Code);
    }

    [Fact]
    public void R4Cheat_Size_UsesPaddingAndCodeCount()
    {
        var cheat = new R4Cheat
        {
            Title = "AB",
            Description = "C",
            Code = [BitConverter.GetBytes(1u), BitConverter.GetBytes(2u)]
        };

        Assert.Equal(24u, cheat.Size);
    }

    [Fact]
    public void R4Folder_Size_AggregatesNestedItems()
    {
        var cheat = TestHelpers.CreateCheat();
        var folder = new R4Folder
        {
            Title = "AB",
            Description = "C"
        };
        folder.Items.Add(cheat);

        Assert.Equal(32u, folder.Size);
    }

    [Fact]
    public void R4Folder_Size_IgnoresUnknownItemTypes()
    {
        var folder = new R4Folder();
        folder.Items.Add(new UnknownItem());

        var combined = System.Text.Encoding.UTF8.GetByteCount(folder.Title) + 1 + System.Text.Encoding.UTF8.GetByteCount(folder.Description);
        var padding = (4 - combined % 4) % 4;
        if (padding == 0) padding = 4;
        var expected = (uint)(combined + padding + 4);
        Assert.Equal(expected, folder.Size);
    }

    [Fact]
    public void R4Game_Size_IncludesHeaderAndChildren()
    {
        var game = new R4Game("ABCD")
        {
            GameTitle = "GAME"
        };
        game.Items.Add(TestHelpers.CreateCheat());

        Assert.Equal(64u, game.Size);
    }

    [Fact]
    public void R4Game_FlattenedItemCount_CountsDescendants()
    {
        var game = new R4Game("ABCD");
        var folder = new R4Folder();
        folder.Items.Add(TestHelpers.CreateCheat());
        game.Items.Add(folder);
        game.Items.Add(TestHelpers.CreateCheat());

        Assert.Equal(3u, game.FlattenedItemCount);
    }

    [Fact]
    public void CreateDefaultButtonStates_ContainsAllButtonsWithIgnoreState()
    {
        var states = R4Game.CreateDefaultButtonStates();

        Assert.Equal(Enum.GetValues<R4Game.ActivatorButton>().Length, states.Count);
        Assert.All(states.Values, state => Assert.Equal(R4Game.ActivatorKeyState.Ignore, state));
    }

    [Fact]
    public void CheatActivatorOptions_ClonesIncomingDictionary()
    {
        var source = new Dictionary<R4Game.ActivatorButton, R4Game.ActivatorKeyState>
        {
            [R4Game.ActivatorButton.A] = R4Game.ActivatorKeyState.Hold
        };

        var options = new R4Game.CheatActivatorOptions(source);
        source[R4Game.ActivatorButton.A] = R4Game.ActivatorKeyState.Release;

        Assert.Equal(R4Game.ActivatorKeyState.Hold, options.ButtonStates[R4Game.ActivatorButton.A]);
        Assert.True(options.HasActivator);
    }

    [Fact]
    public void AnalyzeCheatActivator_WithLessThanTwoWords_ReturnsInputAsPayload()
    {
        var words = new List<uint> { 0x11111111 };

        var analysis = R4Game.AnalyzeCheatActivator(words);

        Assert.Equal(words, analysis.PayloadWords);
        Assert.False(analysis.HadLeadingActivatorRows);
        Assert.False(analysis.HadTrailingResetRow);
    }

    [Fact]
    public void AnalyzeCheatActivator_FromCheat_IgnoresNonFourByteBlocks()
    {
        var cheat = new R4Cheat
        {
            Code =
            [
                new byte[] { 1, 2, 3 },
                BitConverter.GetBytes(0xCAFEBABEu)
            ]
        };

        var analysis = R4Game.AnalyzeCheatActivator(cheat);

        Assert.Single(analysis.PayloadWords);
        Assert.Equal(0xCAFEBABEu, analysis.PayloadWords[0]);
    }

    private sealed class UnknownItem : IR4Item
    {
        public string Title { get; set; } = "X";
        public string Description { get; set; } = "Y";
    }
}
