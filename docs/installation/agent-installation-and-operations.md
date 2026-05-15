# Agent Windows: instalacao e operacao

Este documento descreve como o MWS Manifestador NF-e Agent deve ser instalado, ativado e operado em Windows.

## Modelo operacional

O Agent usa Agent Pull Model. A Web/API cria comandos, mas o Agent local precisa estar instalado e em execucao para:

- enviar heartbeat;
- fazer polling;
- acessar Windows Certificate Store;
- interagir com certificados A3;
- executar comandos e devolver `complete` ou `fail`.

A API cloud nao inicia conexao inbound para a rede do cliente. Ela nao atravessa firewall/NAT e nao consegue iniciar um Windows Service parado sem um componente local rodando.

## Ciclo

1. A Web gera um codigo de ativacao.
2. O usuario instala o Agent com MSI/WiX ou script PowerShell administrativo.
3. O Agent chama `/api/agent/v1/activate`.
4. A API emite credenciais.
5. O Agent salva credenciais com DPAPI.
6. O Agent envia heartbeat e faz polling.
7. A Web calcula online/offline por `last_seen_at`.
8. Comandos operacionais so sao executados se o Agent estiver online.

## Estados

- `not_installed`: sem componente local instalado ou sem registro conhecido.
- `pending_activation`: codigo emitido ou Agent ainda sem credenciais.
- `online`: heartbeat recente.
- `offline`: heartbeat expirou.
- `outdated`: versao menor que a minima suportada.
- `revoked`: agente revogado na Web/API.
- `error`: Agent informou erro operacional.
- `service_stopped`: conhecido apenas por informacao local, service manager ou watchdog.
- `unknown`: dados insuficientes.

## Instalacao recomendada

Curto prazo:

- gerar MSI real via WiX Toolset;
- instalar Worker + Configurator + Tray Monitor via MSI;
- criar atalhos visiveis no Menu Iniciar para Configurator, Tray e logs;
- manter scripts PowerShell para suporte e homologacao.

Medio prazo:

- auto-update controlado, assinado e com rollback.

Futuro:

- Watchdog Service separado para reiniciar o Agent principal quando ele cair.

## Ativacao

O activation code e temporario. Ele deve ser informado no Configurator local, CLI local ou console apenas para ativar o Agent. O MSI nao embute activation code fixo. Depois da ativacao, as credenciais ficam protegidas por DPAPI em `%ProgramData%\MWS Manifestador Agent\agent-credentials.dpapi`.

Nunca gravar:

- PIN de certificado A3;
- private key;
- segredo HMAC em texto puro;
- XML fiscal em diagnostico operacional.

## Windows Service

Em producao, o Worker roda como Windows Service com startup automatico. Em desenvolvimento, use console mode para facilitar logs, breakpoints e prompts de certificado A3.

O MSI WiX instala o servico `MWSManifestadorAgent`, com display name `MWS Manifestador NF-e Agent`, em modo Automatic. Upgrade deve ser feito por major upgrade do MSI e nao deve apagar `%ProgramData%`.

O usuario deve perceber a instalacao por tres pontos locais:

- Menu Iniciar com `MWS Agent Configurator`.
- Menu Iniciar com `MWS Agent Tray Monitor`.
- Icone de bandeja quando o Tray estiver em execucao.

O Tray roda no contexto do usuario logado. Ele mostra status basico, abre o Configurator, abre logs e tenta iniciar/parar/reiniciar o servico quando a conta tem permissao. Sair do Tray fecha apenas o monitor visual, nao o Windows Service.

O Worker grava status sanitizado em `%ProgramData%\MWS Manifestador Agent\status.json` sempre que inicia, ativa, envia heartbeat, faz polling ou registra erro operacional relevante. Esse arquivo nao deve conter segredo, PIN, token, private key, PFX, XML fiscal ou activation code.

Conta de servico:

- `LocalSystem`: facil de operar, mas pode nao enxergar certificados em `CurrentUser`.
- `NetworkService`: menor privilegio, com a mesma limitacao de `CurrentUser`.
- Usuario dedicado/interativo: pode ser necessario para A3, mas exige administracao de credenciais e permissoes.

Para A3, valide primeiro em console no mesmo usuario que possui o certificado/token. Se funcionar no console e falhar como servico, a causa mais comum e store/permissao/contexto do usuario.

## Diagnostico local

`LocalDiagnosticsService` deve ficar desabilitado por padrao e, quando habilitado, escutar apenas em loopback:

```json
{
  "LocalDiagnostics": {
    "Enabled": true,
    "ListenUrl": "http://127.0.0.1:8787"
  }
}
```

Endpoints locais:

- `/health`: status simples.
- `/certificates`: inventario sanitizado de certificados.

Nao publicar esse endpoint em rede externa. Ele existe para desenvolvimento e suporte local.

## Controle remoto

Comandos operacionais planejados:

- `agent_restart_requested`
- `agent_update_requested`
- `agent_diagnostics_requested`
- `agent_collect_logs_requested`
- `agent_refresh_certificate_inventory`

Eles so funcionam se o Agent estiver online. Se o Agent estiver parado e nao houver watchdog, a acao correta e iniciar o servico localmente, por `services.msc`, PowerShell administrativo ou ferramenta corporativa.
