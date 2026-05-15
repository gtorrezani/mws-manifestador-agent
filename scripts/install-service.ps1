param(
    [string] $ApiBaseUrl = '',
    [string] $ActivationCode = '',
    [string] $ServiceName = 'MwsManifestadorAgent',
    [string] $InstallDirectory = '',
    [string] $PublishDirectory = '',
    [int] $ActivationWaitSeconds = 30
)

$ErrorActionPreference = 'Stop'

Write-Host 'MWS Agent technical support installer. For end users, prefer the signed MSI/EXE installer and GUI configurator.'

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Run this script from an elevated PowerShell session.'
    }
}

function Join-CommandLine {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ExePath,
        [string[]] $Arguments = @()
    )

    $quotedExe = '"' + $ExePath.Replace('"', '\"') + '"'
    $quotedArgs = foreach ($argument in $Arguments) {
        '"' + $argument.Replace('"', '\"') + '"'
    }

    return (@($quotedExe) + $quotedArgs) -join ' '
}

Assert-Administrator

if ([string]::IsNullOrWhiteSpace($InstallDirectory)) {
    $InstallDirectory = if ([string]::IsNullOrWhiteSpace($PublishDirectory)) {
        Join-Path -Path $env:ProgramFiles -ChildPath 'MWS Manifestador Agent'
    } else {
        $PublishDirectory
    }
}

$displayName = 'MWS Manifestador NF-e Agent'
$exePath = Join-Path -Path $InstallDirectory -ChildPath 'Mws.Manifestador.Agent.Worker.exe'

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Worker executable not found at $exePath"
}

$baseArguments = @()
if (-not [string]::IsNullOrWhiteSpace($ApiBaseUrl)) {
    $baseArguments += "--AgentApi:BaseUrl=$ApiBaseUrl"
}

$serviceBinaryPath = Join-CommandLine -ExePath $exePath -Arguments $baseArguments

if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

New-Service -Name $ServiceName -BinaryPathName $serviceBinaryPath -DisplayName $displayName -StartupType Automatic

if ([string]::IsNullOrWhiteSpace($ActivationCode)) {
    Start-Service -Name $ServiceName
    return
}

# ActivationCode is intentionally used only for the first service start, then removed
# from the persisted service command line. Do not pass PINs or real secrets here.
$activationBinaryPath = Join-CommandLine -ExePath $exePath -Arguments ($baseArguments + "--AgentApi:ActivationCode=$ActivationCode")

try {
    sc.exe config $ServiceName binPath= $activationBinaryPath | Out-Null
    Start-Service -Name $ServiceName
    Start-Sleep -Seconds $ActivationWaitSeconds
}
finally {
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe config $ServiceName binPath= $serviceBinaryPath | Out-Null
    Start-Service -Name $ServiceName
}
