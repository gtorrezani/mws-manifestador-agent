namespace Mws.Manifestador.Agent.Sefaz.Models;

public enum ManifestationEventCode
{
    None = 0,
    OperationAcknowledgement = 210210,
    OperationConfirmation = 210200,
    OperationUnknown = 210220,
    OperationNotPerformed = 210240,
}
