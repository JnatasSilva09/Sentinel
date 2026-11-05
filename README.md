# SENTINEL — Plataforma de Monitoramento e Gestão de Segurança Digital

Aplicação desktop que simula o painel de uma equipe de segurança da informação (SOC): dispositivos, eventos, incidentes, dashboard, assistente de IA e relatórios.

> **Aviso:** todo o monitoramento usa **dados simulados**. O projeto não realiza ataques, varreduras nem interfere em redes reais.

## Status

| Semana | Entrega | Situação |
|---|---|---|
| 1 | Estrutura, banco, dispositivos | ✅ |
| 2 | Simulador e regras de detecção | ✅ |
| 3 | Incidentes e dashboard | ✅ |
| 4 | Assistente de IA | ✅ |
| 5 | Relatórios PDF, testes, README | ⏳ |

## Stack

C# · .NET 8 · WinForms · SQLite · Entity Framework Core · xUnit

## Estrutura

```
Sentinel.sln
├── Sentinel.Core   → entidades, enums, regras de negócio e interfaces (sem dependência de tela ou banco)
├── Sentinel.Data   → DbContext, repositórios, seed (EF Core + SQLite)
├── Sentinel.IA     → integração com o Claude (HttpClient), prompts e validação das respostas
├── Sentinel.App    → telas WinForms
└── Sentinel.Tests  → testes xUnit
```

## Como rodar (Windows)

Pré-requisito: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet tool install --global dotnet-ef        # só na primeira vez
dotnet ef migrations add Inicial --project Sentinel.Data
dotnet test
dotnet run --project Sentinel.App
```

O banco é criado em `%LocalAppData%\Sentinel\sentinel.db`, com dispositivos de exemplo na primeira execução.
Para recomeçar do zero, feche o app e apague esse arquivo.

Depois de alterar uma entidade, crie uma nova migration:
`dotnet ef migrations add NomeDaMudanca --project Sentinel.Data`

## Semana 1 — o que existe

- Modelo de dados: `Device`, `SecurityEvent`, `Incident`, `IncidentEvent`, `IncidentNote`.
- CRUD de dispositivos com busca por nome/IP, filtro por sistema operacional, validação de IPv4 e IP único.
- Seed com 7 dispositivos fictícios.
- Testes das validações, do serviço e do repositório (SQLite em memória).
- Na Semana 3: testes do ciclo de vida dos incidentes (transições, justificativa obrigatória, auditoria, filtros, vínculo com alerta) e do dashboard (janelas de tempo, contagens, ranking, série temporal).
- Na Semana 2: testes das regras (bordas da janela, dispositivos misturados, deduplicação), do simulador (semente fixa, distribuição) e do serviço de monitoramento.

## Semana 2 — simulador e detecção

Abra **Monitoramento** na tela de dispositivos. Os botões geram eventos fictícios e as regras decidem se viram alerta.

| Regra | Condição (mesmo dispositivo) | Gravidade |
|---|---|---|
| Força bruta de login | 5 logins falhos em 2 min | Alta |
| Varredura de portas repetida | 3 varreduras em 10 min | Alta |
| Acessos fora do horário | 2 acessos em 60 min | Média |
| Malware detectado | 1 detecção | Crítica |

Como funciona:
- **Simulador** (`SimuladorDeEventos`): sorteia o tipo por pesos (login falho é o mais comum, malware o mais raro). Só cria registros de texto; não abre conexão nem executa varredura. Os IPs de origem vêm das faixas de documentação RFC 5737 (`203.0.113.0/24` e `198.51.100.0/24`), que nunca pertencem a máquinas reais.
- **Regras** (`RegraPorContagem`): "N eventos do tipo X em T minutos". Funções puras, fáceis de testar.
- **Motor** (`MotorDeDeteccao`): aplica todas as regras e **não repete o alerta** enquanto o mesmo problema continua na janela (o 6º login falho não gera outro alerta).
- **Gravação atômica:** evento e alertas vão na mesma transação, então nunca sobra um sem o outro.
- Para criar uma regra nova, basta instanciar outra `RegraPorContagem` em `MotorDeDeteccao.Padrao()`.

Demonstração rápida: clique em **Simular força bruta** e veja 6 eventos e 1 alerta; clique de novo e veja que o alerta não se duplica.

> Esta semana entrou a tabela `Alerts`. Se você já tinha criado a migration `Inicial` na Semana 1 e ainda não tem dados que queira guardar, apague a pasta `Sentinel.Data/Migrations` e o arquivo `sentinel.db` e rode `dotnet ef migrations add Inicial --project Sentinel.Data` de novo. Se preferir manter o histórico, crie uma migration nova: `dotnet ef migrations add Alertas --project Sentinel.Data`.

## Semana 3 — incidentes e dashboard

**Incidentes** (botão *Incidentes* na tela principal, ou *Abrir incidente do alerta selecionado* no Monitoramento):
- **A partir de um alerta:** o incidente herda risco e descrição e já vem com os eventos que dispararam a regra (mesmo dispositivo, mesmo tipo, dentro da janela da regra). Um alerta vira no máximo um incidente (garantido por índice único no banco).
- **Manual:** título, risco e descrição.
- **Ciclo de vida:** `Aberto → Em análise → Resolvido`. Também é possível resolver direto (falso positivo), devolver para Aberto e reabrir um resolvido. Qualquer outra transição é recusada.
- **Resolver exige justificativa** escrita, como num SOC de verdade.
- **Trilha de auditoria:** cada mudança de status ou de risco gera uma nota automática no histórico, junto com a justificativa. Notas são só acrescentadas; não há edição nem exclusão de incidentes, para preservar o histórico.
- Se os dados simulados forem limpos, os incidentes ficam, mas perdem o vínculo com os eventos apagados.

**Dashboard** (botão *Dashboard*; pode ficar aberto ao lado do Monitoramento, atualizando sozinho a cada 3 s):
- Cartões: eventos e alertas no período, incidentes em andamento e dispositivos monitorados.
- Gráficos: eventos por gravidade, por tipo, ao longo do tempo, dispositivos com mais eventos e incidentes por status.
- Períodos: última hora (colunas de 5 min), 24 horas (1 h) e 7 dias (1 dia). As colunas são alinhadas ao relógio.
- As contagens são feitas no banco (`GROUP BY`), sem carregar todos os eventos na memória. Os gráficos são desenhados com GDI+, sem biblioteca externa.

Demonstração: com o Dashboard aberto, use *Simular força bruta* e depois *Abrir incidente do alerta selecionado* no Monitoramento. Mude o status, escreva uma justificativa, resolva e veja os cartões e gráficos mudarem.

> Esta semana o banco ganhou o vínculo do incidente com o alerta (`AlertaOrigemId`). Gere uma migration nova: `dotnet ef migrations add Incidentes --project Sentinel.Data`. Se ainda não tiver dados que queira guardar, também pode apagar `Sentinel.Data/Migrations` e o `sentinel.db` e criar a `Inicial` de novo.

## Semana 4 — assistente de IA

O assistente faz três coisas, sempre como **sugestão** (quem decide é a pessoa):

| Onde | Botão | O que faz |
|---|---|---|
| Monitoramento (alertas) | Explicar alerta (IA) | Explica o alerta, lista medidas preventivas e sugere um risco |
| Monitoramento (eventos) | Sugerir gravidade do evento (IA) | Sugere uma gravidade e justifica; só muda se você clicar em *Aplicar* |
| Detalhe do incidente | Resumir incidente (IA) | Resume o caso; só entra no histórico se você clicar em *Salvar no histórico* (marcado como gerado por IA) |

### Dois modos

- **Modo offline (padrão):** funciona sem internet e sem chave, com uma base de conhecimento local. É o que roda se nada for configurado.
- **Claude (opcional):** ativa quando existe a variável de ambiente `ANTHROPIC_API_KEY`. Se a chamada falhar (sem internet, chave inválida, limite, resposta estranha), o app responde em modo offline e avisa na própria janela. O rodapé da tela principal mostra qual assistente está ativo.

Configurar a chave no PowerShell, só para a sessão atual (feche e abra o terminal para remover):

```powershell
$env:ANTHROPIC_API_KEY = "cole-sua-chave-aqui"
dotnet run --project Sentinel.App
```

Variáveis opcionais: `SENTINEL_CLAUDE_MODEL` (padrão `claude-haiku-5-5`) e `SENTINEL_IA_OFFLINE=1` (força o modo offline mesmo com chave).

### Segurança e privacidade da IA

- **Só dados simulados** saem do computador, e **sem IP nem ids**: o contexto leva nome do dispositivo, tipo, sistema e textos curtos (limites de quantidade e de tamanho).
- **Chave nunca no código:** só variável de ambiente, só no cabeçalho `x-api-key`; nunca em log, mensagem de erro ou `ToString()`. Chave com caracteres estranhos é ignorada.
- **Prompt injection:** descrições de eventos e notas são tratadas como dados não confiáveis, dentro de `<dados>`, com `<`, `>` e `&` escapados para ninguém fechar a tag; o prompt de sistema manda tratá-las como texto.
- **Resposta validada:** só JSON no formato esperado, textos limpos e limitados, gravidade dentro do enum. Qualquer desvio cai no modo offline.
- **Erros genéricos:** 401, 429, 5xx etc. viram frases fixas; o corpo da resposta nunca é repassado.
- **Humano no comando:** a IA nunca altera dados sozinha.

Não há mudança no banco nesta semana: não é preciso nova migration.

## Decisões de segurança

- **Sem SQL montado na mão:** todo acesso ao banco passa pelo EF Core, que usa consultas parametrizadas. No filtro de busca, `%` e `_` digitados pelo usuário são tratados como texto, não como curingas.
- **Validação em camadas:** a tela valida para ajudar o usuário, o serviço valida de novo (a regra de negócio não depende da tela) e o banco garante a unicidade do IP com um índice único.
- **IPv4 estrito:** quatro partes decimais, 0 a 255, sem zeros à esquerda (`010.0.0.1` é ambíguo e é recusado).
- **Nomes sem caracteres de controle:** evita quebras de linha e caracteres invisíveis nos dados e, mais tarde, nos relatórios e nos prompts da IA.
- **Erros sem vazamento:** o usuário vê uma mensagem clara; o detalhe técnico vai para `%LocalAppData%\Sentinel\logs\sentinel.log`.
- **Incidentes com trilha de auditoria:** nada é apagado e toda mudança de status/risco fica registrada com a justificativa.
- **Dados 100% simulados:** o app avisa isso na tela. A única conexão de rede possível é a consulta opcional ao Claude (Semana 4), e só quando existe a variável `ANTHROPIC_API_KEY`.
- **Segredos fora do código:** a chave da API vive só em variável de ambiente, nunca no código nem no repositório.

## Mapeamento com os cursos da Cisco

| Conhecimento | Aplicação no SENTINEL |
|---|---|
| Introduction to Cybersecurity | Conceitos de ameaças e vulnerabilidades |
| Network Defense | Monitoramento de eventos de rede simulados |
| Endpoint Security | Cadastro e acompanhamento de dispositivos |
| Networking Basics | Endereços IP, dispositivos e conexões |
| Inteligência Artificial | Classificação e análise de eventos |
| Engenharia de Prompts | Assistente para explicar alertas |
| C# e .NET | Desenvolvimento do sistema |
| SQLite / Entity Framework | Armazenamento dos dados |
| Git e GitHub | Versionamento e documentação |
