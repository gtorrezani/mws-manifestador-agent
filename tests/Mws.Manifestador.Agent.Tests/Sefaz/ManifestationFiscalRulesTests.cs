using System.Text.Json;
using FluentAssertions;
using Mws.Manifestador.Agent.Domain.Entities;
using Mws.Manifestador.Agent.Domain.Enums;
using Mws.Manifestador.Agent.Sefaz;
using Mws.Manifestador.Agent.Sefaz.Models;

namespace Mws.Manifestador.Agent.Tests.Sefaz;

public sealed class ManifestationFiscalRulesTests
{
    [Theory]
    [InlineData(ManifestationEventCode.OperationAcknowledgement, 210210)]
    [InlineData(ManifestationEventCode.OperationConfirmation, 210200)]
    [InlineData(ManifestationEventCode.OperationUnknown, 210220)]
    [InlineData(ManifestationEventCode.OperationNotPerformed, 210240)]
    public void ManifestationEventCodesMatchNfeSpecification(ManifestationEventCode eventCode, int expectedCode)
    {
        ((int)eventCode).Should().Be(expectedCode);
    }

    [Fact]
    public void PayloadReaderDoesNotInventJustificationForOperationNotPerformed()
    {
        using JsonDocument payload = JsonDocument.Parse("""
            {
              "uf": "SP",
              "environment": "production",
              "cnpj": "12345678000195",
              "access_key": "12345678901234567890123456789012345678901234",
              "certificate_thumbprint": "ABC",
              "correlation_id": "corr-1"
            }
            """);

        AgentCommand command = new(
            Guid.NewGuid(),
            CommandType.ManifestNotPerformed,
            10,
            payload.RootElement.Clone(),
            null,
            null,
            0,
            3);

        ManifestationEventRequest request = new CommandPayloadReader()
            .ReadManifestationRequest(command, ManifestationEventCode.OperationNotPerformed);

        request.Justification.Should().BeNull();
        request.EventCode.Should().Be(ManifestationEventCode.OperationNotPerformed);
    }
}
