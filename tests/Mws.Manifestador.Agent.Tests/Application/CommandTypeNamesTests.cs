using FluentAssertions;
using Mws.Manifestador.Agent.Application.Commands;
using Mws.Manifestador.Agent.Domain.Enums;

namespace Mws.Manifestador.Agent.Tests.Application;

public sealed class CommandTypeNamesTests
{
    [Fact]
    public void ToWireNameUsesAgentApiContractNames()
    {
        CommandTypeNames.ToWireName(CommandType.ManifestConfirmation)
            .Should()
            .Be("manifest_confirmation");
    }

    [Fact]
    public void FromWireNameRejectsUnknownCommand()
    {
        Action act = static () => CommandTypeNames.FromWireName("unknown");

        act.Should().Throw<NotSupportedException>();
    }
}
