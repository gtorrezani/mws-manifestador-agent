using FluentAssertions;
using Mws.Manifestador.Agent.Domain.Exceptions;
using Mws.Manifestador.Agent.Domain.ValueObjects;

namespace Mws.Manifestador.Agent.Tests.Domain;

public sealed class AccessKeyTests
{
    [Fact]
    public void ConstructorAccepts44NumericCharacters()
    {
        AccessKey accessKey = new("12345678901234567890123456789012345678901234");

        accessKey.Value.Should().Be("12345678901234567890123456789012345678901234");
    }

    [Fact]
    public void ConstructorRejectsInvalidValue()
    {
        Action act = static () => _ = new AccessKey("invalid");

        act.Should().Throw<InvalidAccessKeyException>();
    }
}
