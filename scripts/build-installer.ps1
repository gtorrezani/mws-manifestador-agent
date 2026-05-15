param(
    [string] $Configuration = 'Release',
    [string] $Runtime = 'win-x64',
    [string] $ProductVersion = '1.0.0'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$publishDir = Join-Path $repoRoot 'artifacts\publish\win-x64'
$installerDir = Join-Path $repoRoot 'artifacts\installer'
$wixIntermediateDir = Join-Path $repoRoot 'artifacts\wix'
$generatedWix = Join-Path $wixIntermediateDir 'GeneratedFiles.wxs'
$workerProject = Join-Path $repoRoot 'src\Mws.Manifestador.Agent.Worker\Mws.Manifestador.Agent.Worker.csproj'
$configuratorProject = Join-Path $repoRoot 'src\Mws.Manifestador.Agent.Configurator\Mws.Manifestador.Agent.Configurator.csproj'
$trayProject = Join-Path $repoRoot 'src\Mws.Manifestador.Agent.Tray\Mws.Manifestador.Agent.Tray.csproj'
$wixProject = Join-Path $repoRoot 'installer\wix\Mws.Manifestador.Agent.Installer.wixproj'
$targetMsi = Join-Path $installerDir 'MWS-Manifestador-Agent-Setup.msi'
$targetChecksum = "$targetMsi.sha256"

function New-WixId {
    param([string] $Prefix, [string] $Value)

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Value))
    }
    finally {
        $sha256.Dispose()
    }
    $suffix = -join ($hash[0..7] | ForEach-Object { $_.ToString('x2') })
    return "$Prefix$suffix"
}

function Convert-ToWixPath {
    param([string] $Path)

    return $Path.Replace('&', '&amp;').Replace('"', '&quot;').Replace('<', '&lt;').Replace('>', '&gt;')
}

function Get-RelativePath {
    param([string] $BasePath, [string] $TargetPath)

    $baseUri = [System.Uri]::new(($BasePath.TrimEnd('\') + '\'))
    $targetUri = [System.Uri]::new($TargetPath)
    return [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($targetUri).ToString()).Replace('/', '\')
}

function Invoke-CheckedCommand {
    param([scriptblock] $Command)

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE."
    }
}

function Write-DirectoryComponents {
    param(
        [System.Text.StringBuilder] $Builder,
        [System.Collections.Generic.List[string]] $ComponentIds,
        [string] $DirectoryPath,
        [string] $DirectoryId,
        [string[]] $ExcludedFileNames
    )

    foreach ($file in Get-ChildItem -LiteralPath $DirectoryPath -File | Sort-Object Name) {
        if ($ExcludedFileNames -contains $file.Name) {
            continue
        }

        $relativePath = Get-RelativePath -BasePath $publishDir -TargetPath $file.FullName
        $componentId = New-WixId -Prefix 'cmp_' -Value $relativePath
        $fileId = New-WixId -Prefix 'fil_' -Value $relativePath
        [void] $ComponentIds.Add($componentId)
        [void] $Builder.AppendLine("      <Component Id=`"$componentId`" Guid=`"*`" Bitness=`"always64`">")
        [void] $Builder.AppendLine("        <File Id=`"$fileId`" Source=`"$(Convert-ToWixPath $file.FullName)`" KeyPath=`"yes`" />")
        [void] $Builder.AppendLine('      </Component>')
    }

    foreach ($directory in Get-ChildItem -LiteralPath $DirectoryPath -Directory | Sort-Object Name) {
        $relativePath = Get-RelativePath -BasePath $publishDir -TargetPath $directory.FullName
        $childDirectoryId = New-WixId -Prefix 'dir_' -Value $relativePath
        [void] $Builder.AppendLine("      <Directory Id=`"$childDirectoryId`" Name=`"$(Convert-ToWixPath $directory.Name)`">")
        Write-DirectoryComponents -Builder $Builder -ComponentIds $ComponentIds -DirectoryPath $directory.FullName -DirectoryId $childDirectoryId -ExcludedFileNames $ExcludedFileNames
        [void] $Builder.AppendLine('      </Directory>')
    }
}

if ($ProductVersion -notmatch '^\d+\.\d+\.\d+$') {
    throw 'ProductVersion must use the MSI-compatible format major.minor.patch, for example 1.0.0.'
}

Remove-Item -LiteralPath $publishDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $installerDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $wixIntermediateDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $publishDir, $installerDir, $wixIntermediateDir | Out-Null

Invoke-CheckedCommand { dotnet publish $workerProject --configuration $Configuration --runtime $Runtime --self-contained true --output $publishDir }
Invoke-CheckedCommand { dotnet publish $configuratorProject --configuration $Configuration --runtime $Runtime --self-contained true --output $publishDir }
Invoke-CheckedCommand { dotnet publish $trayProject --configuration $Configuration --runtime $Runtime --self-contained true --output $publishDir }

$supportDir = Join-Path $publishDir 'Support'
New-Item -ItemType Directory -Path $supportDir | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'scripts\install-service.ps1') -Destination $supportDir
Copy-Item -LiteralPath (Join-Path $repoRoot 'scripts\uninstall-service.ps1') -Destination $supportDir
Copy-Item -LiteralPath (Join-Path $repoRoot 'scripts\update-service.ps1') -Destination $supportDir
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\installation\windows-service.md') -Destination $supportDir

$builder = [System.Text.StringBuilder]::new()
$componentIds = [System.Collections.Generic.List[string]]::new()
[void] $builder.AppendLine('<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">')
[void] $builder.AppendLine('  <Fragment>')
[void] $builder.AppendLine('    <DirectoryRef Id="INSTALLFOLDER">')
Write-DirectoryComponents `
    -Builder $builder `
    -ComponentIds $componentIds `
    -DirectoryPath $publishDir `
    -DirectoryId 'INSTALLFOLDER' `
    -ExcludedFileNames @('Mws.Manifestador.Agent.Worker.exe', 'Mws.Manifestador.Agent.Configurator.exe', 'Mws.Manifestador.Agent.Tray.exe')
[void] $builder.AppendLine('    </DirectoryRef>')
[void] $builder.AppendLine('  </Fragment>')
[void] $builder.AppendLine('  <Fragment>')
[void] $builder.AppendLine('    <ComponentGroup Id="PublishedFiles">')
foreach ($componentId in $componentIds) {
    [void] $builder.AppendLine("      <ComponentRef Id=`"$componentId`" />")
}
[void] $builder.AppendLine('    </ComponentGroup>')
[void] $builder.AppendLine('  </Fragment>')
[void] $builder.AppendLine('</Wix>')
Set-Content -LiteralPath $generatedWix -Value $builder.ToString() -Encoding UTF8

Invoke-CheckedCommand {
    dotnet build $wixProject `
        --configuration $Configuration `
        -p:Platform=x64 `
        -p:ProductVersion=$ProductVersion `
        -p:PublishDir=$publishDir `
        -p:GeneratedWixSource=$generatedWix `
        -p:OutputPath=$installerDir
}

$builtMsi = Get-ChildItem -LiteralPath $installerDir -Filter '*.msi' -Recurse |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($null -eq $builtMsi) {
    throw 'WiX build finished without producing an MSI.'
}

if ($builtMsi.FullName -ne $targetMsi) {
    Copy-Item -LiteralPath $builtMsi.FullName -Destination $targetMsi -Force
}

$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $targetMsi
Set-Content -LiteralPath $targetChecksum -Value "$($hash.Hash.ToLowerInvariant())  MWS-Manifestador-Agent-Setup.msi" -Encoding ASCII

Write-Host "Installer: $targetMsi"
Write-Host "SHA-256:   $($hash.Hash.ToLowerInvariant())"
