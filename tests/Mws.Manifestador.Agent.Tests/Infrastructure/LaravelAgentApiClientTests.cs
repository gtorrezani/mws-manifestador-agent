using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Mws.Manifestador.Agent.Application.Configuration;
using Mws.Manifestador.Agent.Application.DTOs;
using Mws.Manifestador.Agent.Domain.Enums;
using Mws.Manifestador.Agent.Infrastructure.Api;

namespace Mws.Manifestador.Agent.Tests.Infrastructure;

public sealed class LaravelAgentApiClientTests
{
    [Fact]
    public async Task ApiFailureThrowsAgentApiExceptionWithSanitizedBodyAndCorrelationId()
    {
        using StaticResponseHandler handler = new(
            HttpStatusCode.Unauthorized,
            "{\"message\":\"Invalid signature\",\"secret\":\"raw-secret\",\"nested\":{\"a3_pin\":\"1234\"}}",
            "corr-123");
        using HttpClient httpClient = new(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://api.example.test"),
        };

        LaravelAgentApiClient client = new(
            httpClient,
            Options.Create(new AgentApiOptions { BaseUrl = new Uri("https://api.example.test") }));

        Func<Task> act = async () => await client.SendHeartbeatAsync(
            new AgentCredentials(Guid.Parse("11111111-1111-1111-1111-111111111111"), "agent-secret"),
            new HeartbeatRequest(AgentStatus.Online, "1.0.0", "MWS-CLIENTE", new { }, []),
            CancellationToken.None).ConfigureAwait(false);

        AgentApiException exception = (await act.Should().ThrowAsync<AgentApiException>().ConfigureAwait(false)).Which;

        exception.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        exception.CorrelationId.Should().Be("corr-123");
        exception.SanitizedBody.Should().Contain("\"secret\":\"[redacted]\"");
        exception.SanitizedBody.Should().Contain("\"a3_pin\":\"[redacted]\"");
        exception.SanitizedBody.Should().NotContain("raw-secret");
        exception.SanitizedBody.Should().NotContain("1234");
    }

    private sealed class StaticResponseHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode statusCode;
        private readonly string body;
        private readonly string correlationId;

        public StaticResponseHandler(HttpStatusCode statusCode, string body, string correlationId)
        {
            this.statusCode = statusCode;
            this.body = body;
            this.correlationId = correlationId;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = new(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            response.Headers.Add("X-MWS-Correlation-Id", correlationId);

            return Task.FromResult(response);
        }
    }
}
