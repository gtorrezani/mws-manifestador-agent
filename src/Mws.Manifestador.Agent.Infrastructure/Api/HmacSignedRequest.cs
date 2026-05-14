namespace Mws.Manifestador.Agent.Infrastructure.Api;

public sealed record HmacSignedRequest(
    string Timestamp,
    string Nonce,
    string BodyHash,
    string Signature);
