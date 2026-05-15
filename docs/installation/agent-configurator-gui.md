# Agent Configurator GUI

## Objetivo

O Agent Configurator GUI e o fluxo principal para usuario final ativar o MWS Manifestador NF-e Agent no Windows depois da instalacao pelo MSI. Ele substitui o uso direto de PowerShell para usuarios comuns e mantem scripts apenas para suporte tecnico, automacao corporativa, GPO/Intune e troubleshooting.

O navegador nao pode instalar, iniciar ou executar software local automaticamente. A Web pode baixar o instalador e gerar o codigo de ativacao; a execucao do instalador acontece no Windows por acao do usuario. Abrir algo local automaticamente exigiria um protocolo customizado previamente registrado, o que so existe depois de uma instalacao anterior.

## Telas minimas

1. Boas-vindas
   - Nome do produto.
   - Empresa/ambiente quando informado pelo instalador.
   - Aviso de que serao instalados Agent e Windows Service.

2. API URL
   - Campo para URL base da API.
   - Valor pre-preenchido pelo instalador, arquivo de configuracao ou parametro MSI.
   - Validacao de formato HTTPS em producao.

3. Activation code
   - Campo para codigo gerado na Web.
   - Botao para colar codigo da area de transferencia.
   - Aviso de validade/uso unico.

4. Testar conexao
   - Verificar alcance da API.
   - Mostrar erro claro para DNS, TLS, proxy, firewall ou API indisponivel.
   - Nao enviar PIN, senha ou segredo local.

5. Ativar Agent
   - Chamar `/api/agent/v1/activate`.
   - Receber credenciais do Agent.
   - Persistir credenciais com DPAPI.

6. Instalar/iniciar servico
   - Criar ou atualizar Windows Service.
   - Iniciar o servico.
   - Mostrar status do servico e proximo heartbeat esperado.

7. Diagnostico
   - Versao do Agent.
   - API URL sanitizada.
   - Usuario/processo.
   - Status de acesso ao Windows Certificate Store.
   - Contagem de certificados encontrados.
   - Link para logs locais.

## Tecnologia recomendada

Implementacao atual: WPF simples em .NET, empacotado junto com o Worker no MSI WiX.

Alternativa: WinUI 3, se o projeto decidir investir em UI mais moderna e no pipeline de empacotamento correspondente.

CLI/PowerShell deve permanecer para suporte tecnico, ambientes automatizados e empresas com GPO/Intune, mas nao deve ser o fluxo principal de usuario final.

## Comunicacao com o servico

Fluxo inicial recomendado:

- Configurator executa elevado quando precisar instalar o Windows Service.
- Configurator escreve configuracao nao sensivel, como API URL.
- Configurator chama ativacao e salva credenciais via o mesmo mecanismo DPAPI usado pelo Agent.
- Configurator inicia o servico e aguarda heartbeat ou consulta local de status.

Fluxo futuro:

- Se o Agent ja estiver instalado, o Configurator pode se comunicar por endpoint local restrito a `127.0.0.1` ou por named pipe com ACL local.
- Operacoes sensiveis devem exigir processo elevado ou servico auxiliar com permissao especifica.

## Credenciais e DPAPI

Credenciais do Agent devem ser armazenadas com DPAPI. O caminho atual e:

```text
%ProgramData%\MWS Manifestador Agent\agent-credentials.dpapi
```

Nunca armazenar:

- PIN de A3;
- senha de A1 em texto puro;
- segredo HMAC em texto puro;
- private key;
- XML fiscal em payload de diagnostico.

## Protocolo customizado futuro

Um protocolo `mws-agent://` pode melhorar a experiencia depois que algum componente local ja estiver instalado.

Uso futuro possivel:

```text
mws-agent://activate?code=123456
```

Regras:

- Registrar protocolo somente pelo instalador assinado.
- Validar origem e parametros.
- Nao aceitar segredo/PIN pela URL.
- Tratar activation code como temporario.
- Se o protocolo nao existir, a Web deve continuar usando download manual do instalador.

## Backlog tecnico

- Definir icone, assinatura digital e publisher.
- Melhorar teste de conectividade com endpoint dedicado de health.
- Implementar fluxo de reparo/reinstalacao.
- Implementar leitura segura de status local do servico.
- Definir estrategia de update assinado e rollback.
