using Xunit;

namespace R4Everyone.Binary4Everyone.Tests;

public class R4GameActivatorTests
{
    [Fact]
    public void BuildCheatCodeWithActivator_NoActivator_ReturnsPayloadAsIs()
    {
        var payload = new uint[] { 0x11111111, 0x22222222 };

        var result = R4Game.BuildCheatCodeWithActivator(payload, null);

        Assert.Equal(payload, result);
    }

    [Fact]
    public void BuildAndAnalyzeCheatActivator_UsesExpectedRowsAndFlags()
    {
        var states = R4Game.CreateDefaultButtonStates();
        states[R4Game.ActivatorButton.A] = R4Game.ActivatorKeyState.Hold;
        states[R4Game.ActivatorButton.X] = R4Game.ActivatorKeyState.Release;

        var options = new R4Game.CheatActivatorOptions(states);
        var payload = new uint[] { 0x12345678, 0x9ABCDEF0 };

        var result = R4Game.BuildCheatCodeWithActivator(payload, options);

        Assert.Equal(
            [
                0x94000130, 0xFFFE0000,
                0x94000136, 0xFFFE0001,
                0x12345678, 0x9ABCDEF0,
                0xD2000000, 0x00000000
            ],
            result);

        var analysis = R4Game.AnalyzeCheatActivator(result);

        Assert.Equal(payload, analysis.PayloadWords);
        Assert.True(analysis.HadLeadingActivatorRows);
        Assert.True(analysis.HadTrailingResetRow);
        Assert.Equal(R4Game.XyActivatorMode.Standard, analysis.Options.XyMode);
        Assert.Equal(R4Game.ActivatorKeyState.Hold, analysis.Options.ButtonStates[R4Game.ActivatorButton.A]);
        Assert.Equal(R4Game.ActivatorKeyState.Release, analysis.Options.ButtonStates[R4Game.ActivatorButton.X]);
    }

    [Fact]
    public void ReplaceCheatPayloadPreservingActivator_ReplacesPayloadOnly()
    {
        var states = R4Game.CreateDefaultButtonStates();
        states[R4Game.ActivatorButton.R] = R4Game.ActivatorKeyState.Hold;
        var options = new R4Game.CheatActivatorOptions(states);

        var cheat = new R4Cheat
        {
            Code = R4Game.BuildCheatCodeWithActivator(
                    [0xAAAAAAAA, 0xBBBBBBBB],
                    options)
                .Select(BitConverter.GetBytes)
                .ToList()
        };

        var newPayload = new uint[] { 0x11111111, 0x22222222, 0x33333333 };

        R4Game.ReplaceCheatPayloadPreservingActivator(cheat, newPayload);

        var analysis = R4Game.AnalyzeCheatActivator(cheat);
        Assert.Equal(newPayload, analysis.PayloadWords);
        Assert.Equal(R4Game.ActivatorKeyState.Hold, analysis.Options.ButtonStates[R4Game.ActivatorButton.R]);
        Assert.True(analysis.HadLeadingActivatorRows);
        Assert.True(analysis.HadTrailingResetRow);
    }

    [Fact]
    public void ApplyCheatActivator_AddsActivatorToExistingPayload()
    {
        var payload = new[] { 0xCAFEBABE, 0xDEADBEEF };
        var cheat = new R4Cheat
        {
            Code = payload.Select(BitConverter.GetBytes).ToList()
        };

        var states = R4Game.CreateDefaultButtonStates();
        states[R4Game.ActivatorButton.Y] = R4Game.ActivatorKeyState.Release;
        var options = new R4Game.CheatActivatorOptions(states, R4Game.XyActivatorMode.ActionReplay);

        R4Game.ApplyCheatActivator(cheat, options);

        var analysis = R4Game.AnalyzeCheatActivator(cheat);
        Assert.Equal(payload, analysis.PayloadWords);
        Assert.Equal(R4Game.XyActivatorMode.ActionReplay, analysis.Options.XyMode);
        Assert.Equal(R4Game.ActivatorKeyState.Release, analysis.Options.ButtonStates[R4Game.ActivatorButton.Y]);
    }
}
