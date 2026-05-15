using FluentAssertions;

namespace Mws.Manifestador.Agent.Tests;

public sealed class InstallationScriptsTests
{
    [Theory]
    [InlineData("install-service.ps1")]
    [InlineData("uninstall-service.ps1")]
    [InlineData("update-service.ps1")]
    public async Task ServiceScriptsDoNotReferencePinStorage(string scriptName)
    {
        string contents = await File.ReadAllTextAsync(ScriptPath(scriptName));

        contents.Should().NotContain("A3_PIN");
        contents.Should().NotContain("CertificatePin");
        contents.Should().NotContain("pin=");
    }

    [Fact]
    public async Task InstallScriptAcceptsOperationalParameters()
    {
        string contents = await File.ReadAllTextAsync(ScriptPath("install-service.ps1"));

        contents.Should().Contain("$ApiBaseUrl");
        contents.Should().Contain("$ActivationCode");
        contents.Should().Contain("$ServiceName");
        contents.Should().Contain("$InstallDirectory");
        contents.Should().Contain("Run this script from an elevated PowerShell session.");
    }

    [Fact]
    public async Task UpdateAndUninstallScriptsAcceptServiceParameters()
    {
        string update = await File.ReadAllTextAsync(ScriptPath("update-service.ps1"));
        string uninstall = await File.ReadAllTextAsync(ScriptPath("uninstall-service.ps1"));

        update.Should().Contain("$ServiceName");
        update.Should().Contain("$InstallDirectory");
        uninstall.Should().Contain("$ServiceName");
        uninstall.Should().Contain("$RemoveCredentials");
    }

    [Fact]
    public async Task InstallerBuildScriptProducesRealMsiArtifacts()
    {
        string contents = await File.ReadAllTextAsync(ScriptPath("build-installer.ps1"));

        contents.Should().Contain("dotnet publish");
        contents.Should().Contain("Mws.Manifestador.Agent.Worker.csproj");
        contents.Should().Contain("Mws.Manifestador.Agent.Configurator.csproj");
        contents.Should().Contain("MWS-Manifestador-Agent-Setup.msi");
        contents.Should().Contain("Get-FileHash -Algorithm SHA256");
        contents.Should().NotContain("MWS-Agent-Development-Package.txt");
    }

    [Fact]
    public async Task SignInstallerScriptDoesNotEmbedCertificateMaterial()
    {
        string contents = await File.ReadAllTextAsync(ScriptPath("sign-installer.ps1"));

        contents.Should().Contain("$CertificateThumbprint");
        contents.Should().Contain("signtool.exe");
        contents.Should().NotContain("BEGIN CERTIFICATE");
        contents.Should().NotContain("PFX");
    }

    private static string ScriptPath(string scriptName)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "scripts", scriptName));
    }
}
