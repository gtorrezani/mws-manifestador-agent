using Mws.Manifestador.Agent.Domain.Enums;

namespace Mws.Manifestador.Agent.Application.DTOs;

public sealed record PollCommandsRequest(int Limit, IReadOnlyCollection<CommandType> Capabilities);
