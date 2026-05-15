# Build do instalador MSI

O instalador oficial do MWS Manifestador Agent e um MSI gerado com WiX Toolset via projeto SDK-style em `installer/wix`.

## Gerar o MSI

Execute no repositorio do Agent:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -ProductVersion 1.0.0
```

Para a versao atual desta frente:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -ProductVersion 1.0.1
```

O script:

1. limpa `artifacts/publish/win-x64` e `artifacts/installer`;
2. publica o Worker self-contained para `win-x64`;
3. publica o Configurator WPF self-contained para `win-x64`;
4. publica o Tray Monitor WinForms self-contained para `win-x64`;
5. inclui scripts/documentacao de suporte;
6. gera o MSI com WiX;
7. grava checksum SHA-256.

Saidas:

```text
artifacts\installer\MWS-Manifestador-Agent-Setup.msi
artifacts\installer\MWS-Manifestador-Agent-Setup.msi.sha256
```

## Conteudo instalado

Diretorio padrao:

```text
C:\Program Files\MWS\MWS Manifestador Agent
```

Conteudo:

- Worker Service;
- Agent Configurator;
- Tray Monitor;
- DLLs e runtime self-contained;
- `appsettings.json` base;
- scripts de suporte em `Support`;
- documentacao operacional local em `Support`.

Atalhos instalados no Menu Iniciar:

- `MWS Agent Configurator`;
- `MWS Agent Tray Monitor`;
- `MWS Agent Logs`.

O MSI tambem cria um atalho do Tray na Startup Folder do usuario instalador. Em instalacoes elevadas por uma conta de TI, valide se o atalho ficou no perfil correto do usuario final ou distribua o startup por GPO/Intune.

Dados mutaveis ficam em:

```text
%ProgramData%\MWS Manifestador Agent
```

Use esse diretorio para logs, credenciais DPAPI, temporarios e configuracao local. Upgrades nao devem apagar essa pasta.

## Windows Service

O MSI registra:

- Service name: `MWSManifestadorAgent`
- Display name: `MWS Manifestador NF-e Agent`
- Start type: `Automatic`
- Conta padrao: `LocalSystem`

O MSI para/remove o servico no uninstall e usa major upgrade para substituir versoes anteriores sem duplicar o servico.

## Ativacao

O MSI nao contem activation code. A ativacao e feita pelo Configurator:

```text
C:\Program Files\MWS\MWS Manifestador Agent\Mws.Manifestador.Agent.Configurator.exe
```

O Configurator recebe URL da API e activation code, chama `/api/agent/v1/activate`, salva credenciais com DPAPI e reinicia o servico. O codigo de ativacao nao e gravado em configuracao persistente.

O Worker grava status local sanitizado em:

```text
%ProgramData%\MWS Manifestador Agent\status.json
```

Esse arquivo pode ser lido pelo Configurator e pelo Tray. Ele nao deve conter segredo, PIN, token, private key, PFX, XML fiscal ou activation code.

## Publicar na Web local

No repositorio Web:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-local-agent-installer.ps1 `
  -InstallerPath C:\Git\mws-manifestador-agent\artifacts\installer\MWS-Manifestador-Agent-Setup.msi
```

Configure `.env`:

```dotenv
MWS_AGENT_INSTALLER_LOCAL_DISK=local
MWS_AGENT_INSTALLER_LOCAL_PATH=installers/MWS-Manifestador-Agent-Setup.msi
MWS_AGENT_INSTALLER_FILE_NAME=MWS-Manifestador-Agent-Setup.msi
MWS_AGENT_INSTALLER_VERSION=1.0.1
MWS_AGENT_INSTALLER_SHA256=<sha256>
```

O MSI nao deve ser versionado no git.

## Assinatura digital

Producao exige assinatura digital. O script `scripts/sign-installer.ps1` prepara a etapa com `signtool.exe`, certificado de code signing e timestamp server.

Enquanto nao houver certificado de code signing, o MSI gerado e funcional, mas o Windows pode exibir alerta de publisher desconhecido.
