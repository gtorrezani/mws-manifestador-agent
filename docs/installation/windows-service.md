# Windows Service

## Desenvolvimento em console

Use console mode para desenvolvimento e diagnostico de certificado A3:

```powershell
.\scripts\local-agent.ps1 -ActivationCode "123456"
```

Ou:

```powershell
dotnet run --project src\Mws.Manifestador.Agent.Worker\Mws.Manifestador.Agent.Worker.csproj --environment Development
```

Console mode roda no usuario atual. Isso facilita acesso a certificados em `CurrentUser` e permite que drivers de token A3 exibam prompt quando necessario.

## Instalacao como servico via MSI

O fluxo principal de usuario final e o MSI gerado pelo WiX:

```powershell
msiexec /i .\artifacts\installer\MWS-Manifestador-Agent-Setup.msi
```

O MSI instala em:

```text
C:\Program Files\MWS\MWS Manifestador Agent
```

Ele registra o Windows Service:

- Nome: `MWSManifestadorAgent`
- Display name: `MWS Manifestador NF-e Agent`
- Start type: `Automatic`

O serviço e iniciado ao final da instalacao. Se ainda nao houver credenciais DPAPI, ele fica em `pending_activation` e registra aviso de que a ativacao ainda e necessaria.

O MSI tambem instala:

- `Mws.Manifestador.Agent.Worker.exe`
- `Mws.Manifestador.Agent.Configurator.exe`
- `Mws.Manifestador.Agent.Tray.exe`
- dependencias publicadas self-contained;
- scripts de suporte em `Support`;
- documentacao operacional local em `Support`.

O instalador cria atalhos no Menu Iniciar:

- `MWS Agent Configurator`: abre o assistente de ativacao e status local.
- `MWS Agent Tray Monitor`: abre o icone de bandeja.
- `MWS Agent Logs`: abre a pasta de logs em `%ProgramData%`.

Para ativar pelo fluxo principal, execute:

```text
C:\Program Files\MWS\MWS Manifestador Agent\Mws.Manifestador.Agent.Configurator.exe
```

Informe a URL da API e o codigo de ativacao gerado na Web. O Configurator chama a API, salva credenciais com DPAPI em `%ProgramData%` e permite iniciar/reiniciar o servico.

## Tray Monitor

O Tray Monitor roda no contexto do usuario logado e mostra um icone na bandeja do Windows. Ele nao substitui o Windows Service e sair do monitor nao para o servico.

Menu disponivel:

- Abrir Configurador.
- Iniciar servico.
- Reiniciar servico.
- Parar servico.
- Abrir pasta de logs.
- Copiar diagnostico basico.
- Sair do monitor.

Controlar o servico pode exigir permissao administrativa. Quando a instalacao e elevada por uma conta de TI diferente do usuario final, a inicializacao automatica por Startup Folder pode ficar vinculada ao usuario que executou o MSI. Nesse caso, abra o monitor pelo Menu Iniciar ou configure politica corporativa de inicializacao.

## Status local

O Worker grava status operacional sanitizado em:

```text
%ProgramData%\MWS Manifestador Agent\status.json
```

Campos permitidos:

- `agent_id`
- `installation_id`
- `api_base_url`
- `activated`
- `last_heartbeat_at`
- `last_poll_at`
- `version`
- `service_status`
- `last_error_message` sanitizada

O arquivo nao deve conter segredo, PIN, private key, PFX, token, XML fiscal ou activation code.

## Instalacao como servico por suporte tecnico

Publique o Worker e execute PowerShell como administrador:

```powershell
.\scripts\install-service.ps1 `
  -InstallDirectory "C:\Program Files\MWS Manifestador Agent" `
  -ApiBaseUrl "https://api.example.com" `
  -ActivationCode "123456" `
  -ServiceName "MwsManifestadorAgent"
```

Parametros principais:

- `ApiBaseUrl`: URL base da Web/API.
- `ActivationCode`: codigo temporario de ativacao. O script usa somente na primeira partida e remove do comando persistido do servico.
- `ServiceName`: nome interno do Windows Service.
- `InstallDirectory`: pasta onde esta `Mws.Manifestador.Agent.Worker.exe`.

Nao passe PIN de certificado A3, senha de A1, private key ou segredo HMAC para o script.

## Atualizacao

```powershell
.\scripts\update-service.ps1 `
  -PackageDirectory "C:\Temp\MwsAgentPackage" `
  -InstallDirectory "C:\Program Files\MWS Manifestador Agent" `
  -ServiceName "MwsManifestadorAgent"
```

Esse script para o servico, copia o pacote publicado e inicia o servico. Atualizacao final de producao deve usar pacote assinado, verificacao de integridade e rollback.

## Remocao

```powershell
.\scripts\uninstall-service.ps1 -ServiceName "MwsManifestadorAgent"
```

Para remover credenciais DPAPI locais:

```powershell
.\scripts\uninstall-service.ps1 -ServiceName "MwsManifestadorAgent" -RemoveCredentials
```

Credenciais ficam em:

```text
%ProgramData%\MWS Manifestador Agent\agent-credentials.dpapi
```

O arquivo e protegido por DPAPI `LocalMachine`. Remover esse arquivo exige nova ativacao.

## Logs

Por padrao, o Worker grava logs em:

```text
%ProgramData%\MWS Manifestador Agent\logs\mws-agent-*.log
```

Dados mutaveis nao devem ser gravados em `Program Files`. Use `%ProgramData%\MWS Manifestador Agent` para logs, credenciais DPAPI, temporarios e configuracao local.

## Diagnostico local

`LocalDiagnosticsService` fica desabilitado por padrao. Para suporte local, habilite apenas em loopback:

```json
{
  "LocalDiagnostics": {
    "Enabled": true,
    "ListenUrl": "http://127.0.0.1:8787"
  }
}
```

Endpoints:

- `http://127.0.0.1:8787/health`
- `http://127.0.0.1:8787/certificates`
- `http://127.0.0.1:8787/diagnostics`

Nao exponha esses endpoints em IP externo. O servico bloqueia hosts que nao sejam loopback.

## A3 e conta do servico

Problemas comuns:

- O certificado esta em `CurrentUser`, mas o servico roda como `LocalSystem`.
- Driver/token A3 precisa de sessao interativa para prompt de PIN.
- A conta do servico nao tem permissao no provider criptografico.
- O token nao esta conectado ou o driver nao foi carregado para a conta do servico.

Procedimento recomendado:

1. Teste em console no usuario que possui o certificado.
2. Liste certificados pela Web ou `/certificates`.
3. Teste o certificado.
4. Se falhar apenas como servico, ajuste conta do servico ou mova o certificado para o store correto conforme politica de seguranca.

O Agent nunca armazena PIN de A3.
