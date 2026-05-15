$ErrorActionPreference = 'Stop'

$Root = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $Root

dotnet restore Mws.Manifestador.Agent.sln
dotnet build Mws.Manifestador.Agent.sln --configuration Release --no-restore
dotnet format Mws.Manifestador.Agent.sln --verify-no-changes --no-restore
dotnet test Mws.Manifestador.Agent.sln --configuration Release --no-build
