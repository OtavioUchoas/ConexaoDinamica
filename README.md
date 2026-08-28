# ConexaoDinamica — Web API (.NET 10)

Projeto de estudo sobre **configuração de conexões de banco em tempo de execução**
e **trilha de auditoria**.

A ideia central: em vez de as connection strings virem de `appsettings.json` ou
variáveis de ambiente, elas são configuradas por um **AdminCenter** e persistidas
localmente — sem reiniciar a aplicação. São duas conexões independentes:

- **PostgreSQL** — dados da aplicação (EF Core)
- **MongoDB** — trilha de auditoria

As configurações ficam num **LiteDB** embarcado, que resolve o problema de origem:
é preciso guardar a configuração do banco *antes* de existir um banco configurado.

> Sem Docker e sem nuvem, por escolha. Cenário alvo: on-premise, instância única.

## Arquitetura

```
ConexaoDinamica.API             -> Controllers, Middlewares, Program
ConexaoDinamica.Application     -> Casos de uso (Services, DTOs, Interfaces, Validators)
ConexaoDinamica.Domain          -> Entidades e regras de domínio
ConexaoDinamica.Infrastructure  -> EF Core, LiteDB, MongoDB, ClosedXML, Repositórios, DI
ConexaoDinamica.Tests           -> Testes do interceptor de auditoria (xUnit + SQLite)
ConexaoDinamica.Web             -> Frontend Angular, compilado para o wwwroot da API
```

Os drivers e bibliotecas de terceiros (LiteDB, MongoDB, Npgsql, ClosedXML) vivem
**apenas** na Infrastructure. As demais camadas enxergam somente as abstrações.

O frontend não é servido por um servidor próprio: o `ng build` publica direto em
`ConexaoDinamica.API/wwwroot`, e a API entrega os arquivos com
`MapFallbackToFile("index.html")` — sem esse fallback, recarregar a página em
`/admin/conexoes` daria 404, porque a rota só existe dentro do JavaScript já
carregado.

## Como funciona o setup

1. A aplicação sobe **sem banco nenhum configurado**
2. Rotas de negócio respondem **503** com `setupRequired: true` até que as duas
   conexões existam
3. O administrador entra com as credenciais de *bootstrap* (que não dependem do
   banco) e configura Postgres e MongoDB pelo AdminCenter
4. Aplica as migrations pelo próprio painel — que cria o **super administrador** e
   devolve a senha gerada **uma única vez**
5. A partir da requisição seguinte, tudo funciona. Sem restart.

O passo 3 tem uma tela dedicada (`/setup`), para onde o `setupInterceptor` leva
qualquer requisição que receba 503. O passo 4 pode ser disparado tanto pelo
assistente quanto pelo painel, e **os dois exibem a senha gerada** — o seed é
idempotente, então quem perder o cartão não tem como pedir de novo.

## Estado atual

### Pronto

- **Conexões configuradas em runtime**, com troca válida na requisição seguinte
- **AdminCenter**: testar conexão, salvar, ver status e aplicar migrations
- **Modo setup**: bloqueia rotas de negócio enquanto faltar alguma conexão,
  informando quais
- **Autenticação JWT** com claim de role (`Comum` / `Administrador`)
- **Admin bootstrap (break-glass)**: hash BCrypt em `user-secrets`, aceita username
  **ou** email. Não depende do banco de propósito — é o acesso usado para
  configurar e para recuperar o sistema
- **Seed do super administrador** com senha aleatória exibida uma vez
- **Auditoria no MongoDB**: adição, alteração, remoção, visualização e exportação
- **Agregado de exemplo** (Cliente, Pedido, ItemPedido) com CRUD completo,
  exercitando a auditoria de agregado
- **Frontend Angular 21** (standalone, zoneless, signals) servido pelo wwwroot da
  API: login, assistente de setup, AdminCenter, consulta da trilha e área da
  aplicação com CRUDs
- **Exportação da trilha em XLSX**, com duas abas e registro do próprio ato de
  exportar
- FluentValidation, middleware global de exceções, rate limiting, CORS, security
  headers, Swagger com esquema Bearer

### Em aberto

- [ ] **Padrão Outbox** para a auditoria. Hoje, se o MongoDB cair com o sistema no
      ar, o evento é perdido (a operação de negócio segue e a falha vai para o log).
      O Outbox gravaria o evento no Postgres na mesma transação, publicando depois
- [ ] **Índice TTL para visualizações.** É o tipo de evento que mais infla a trilha
      e o que envelhece mais rápido. Um campo `expiraEm` preenchido só nelas deixa o
      Mongo expirar sozinho, sem separar em coleções
- [ ] **Rotação da senha do admin de bootstrap.** O hash saiu do repositório e foi
      para o `user-secrets`, mas o histórico ainda guarda o antigo — se a senha
      original for reaproveitada em outro lugar, troque-a

## Rodando

### 1. Configure os segredos

Os dois nascem vazios no repositório, de propósito. Na pasta de
`ConexaoDinamica.API`:

```powershell
dotnet user-secrets set 'Jwt:Secret' 'sua-chave-de-32-caracteres-ou-mais'
```

A aplicação **não sobe sem ele**, e falha com instrução na tela. Use
`user-secrets` e não `appsettings.json`: o primeiro fica fora do repositório.
O mínimo de 32 caracteres é exigência do HMAC-SHA256 (chave de 256 bits).

> ⚠️ **Aspas simples, e nada além do valor entre elas.** Dois jeitos de gravar um
> segredo corrompido, os dois silenciosos até muito depois:
>
> - Aspas duplas no PowerShell: em `"$2y$10$abc"`, o `$2y`, o `$10` e o `$abc`
>   viram variáveis vazias, e o valor chega sem os `$`
> - Colchetes de placeholder: `'<meu-hash>'` grava os `<` e `>` junto
>
> Os dois aparecem no login como "salt inválido", que não menciona nem aspas nem
> colchetes. Confira sempre com `dotnet user-secrets list` antes de subir.

Depois o admin de bootstrap — sem `SenhaHash` nenhum login é aceito, e é por ele
que se chega ao AdminCenter para configurar as conexões. Gere o hash com o SDK,
sem precisar de projeto auxiliar:

```bash
# gerar-hash.fsx
#r "nuget: BCrypt.Net-Next, 4.1.0"
printfn "%s" (BCrypt.Net.BCrypt.HashPassword "sua-senha-aqui")
```

```powershell
dotnet fsi gerar-hash.fsx
dotnet user-secrets set 'AdminBootstrap:SenhaHash' '$2a$12$cole.o.hash.impresso.acima'
```

Apague o `.fsx` depois — ele contém a senha em texto puro.

Gere localmente, não em site de terceiros: um gerador online recebe a sua senha
de administrador em texto puro, e é a credencial que configura as conexões e
recupera o sistema.

Para conferir o que ficou gravado, `dotnet user-secrets list` — o hash tem de ter
60 caracteres e três `$`.

### 2. Ajuste o restante em `appsettings.json`

| Chave | Descrição |
|---|---|
| `Jwt:Issuer` / `Jwt:Audience` | Emissor e audiência do token |
| `AdminBootstrap:*` | Username, email e nome do admin. O `SenhaHash` vai em `user-secrets` (passo 1) |
| `Cors:AllowedOrigins` | Origens permitidas do frontend |
| `Storage:ConfigDbPath` | Opcional. Padrão: `%LOCALAPPDATA%\ConexaoDinamica\config.db` |

### 3. Compile o frontend

O `ng build` publica em `ConexaoDinamica.API/wwwroot`, de onde a API serve a SPA.
Sem esse passo a API sobe, mas a raiz devolve 404. Na pasta `ConexaoDinamica.Web`:

```bash
npm install
npm run build
```

Para desenvolver o frontend com recarga automática, `npm start` sobe o dev server
do Angular à parte — nesse caso a origem precisa estar em `Cors:AllowedOrigins`.

### 4. Rode

```bash
dotnet run --project ConexaoDinamica.API
```

A aplicação abre em `https://localhost:<porta>` e o Swagger em `/swagger`. As
conexões de banco são configuradas pelo AdminCenter, não aqui.

> ⚠️ **Derrube a API antes de usar `dotnet ef`.** As ferramentas do EF sobem o
> `Program.cs`, o que abre o arquivo do LiteDB — e os dois processos disputam o
> mesmo lock. O erro resultante não menciona LiteDB em lugar nenhum.

## Endpoints

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| POST | `/api/v1/login` | — | Login de usuário |
| POST | `/api/v1/cadastro` | — | Cadastro de usuário |
| GET | `/api/v1/usuarios/{id}` | JWT | Detalhe (registra visualização) |
| GET/POST | `/api/v1/clientes` | JWT | Listar (busca + paginação) / criar |
| GET/PUT/DELETE | `/api/v1/clientes/{id}` | JWT | Detalhe (registra visualização) / editar / remover |
| GET/POST | `/api/v1/pedidos` | JWT | Listar / criar |
| GET/PUT/DELETE | `/api/v1/pedidos/{id}` | JWT | Detalhe com itens / editar / remover |
| POST | `/api/v1/admin/login` | — | Login do admin bootstrap |
| GET/PUT | `/api/v1/admin/conexao/postgres` | Admin | Ler / salvar conexão |
| POST | `/api/v1/admin/conexao/postgres/testar` | Admin | Testar sem salvar |
| GET/POST | `/api/v1/admin/conexao/postgres/migrations` | Admin | Status / aplicar |
| GET/PUT | `/api/v1/admin/conexao/mongo` | Admin | Ler / salvar conexão |
| POST | `/api/v1/admin/conexao/mongo/testar` | Admin | Testar sem salvar |
| GET | `/api/v1/admin/auditoria` | Admin | Consultar a trilha (filtros + paginação) |
| GET | `/api/v1/admin/auditoria/exportar` | Admin | Baixar o resultado do filtro em XLSX |
| GET | `/api/v1/admin/auditoria/tipos-entidade` | Admin | Entidades presentes na trilha |

Cliente e Pedido exigem apenas sessão; a trilha exige role `Administrador`. A
diferença é deliberada: quem lê a auditoria de um cliente vê os dados dele sem
precisar de permissão sobre clientes, então o controle ali tem de ser pelo menos
tão restrito quanto o dos dados originais.

## Auditoria

Eventos gravados na coleção `eventos_auditoria`:

| Tipo | Como é capturado | Conteúdo |
|---|---|---|
| Adição | `SaveChangesInterceptor` | snapshot do estado inicial |
| Alteração | `SaveChangesInterceptor` | diff + snapshot final |
| Remoção | `SaveChangesInterceptor` | snapshot do último estado |
| Visualização | chamada explícita | quem acessou o quê |
| Exportação | chamada explícita | critério da consulta e volume |
| Autenticação | chamada explícita | quem entrou, por qual credencial |
| Falha de autenticação | chamada explícita | identificador tentado e origem |
| Configuração | chamada explícita | para onde a aplicação passou a apontar |

Tudo que não é adição, alteração ou remoção é explícito porque não passa por
`SaveChanges` — o interceptor não tem como enxergar consulta, login ou uma
configuração que vive no LiteDB.

A trilha cobre três ações privilegiadas além dos dados:

- **Autenticação** — sucesso e falha, para usuários e para o admin de bootstrap.
  Falha tem tipo próprio (a pergunta "quantas tentativas falharam nesta conta?" é
  feita sozinha) e é o único evento sem usuário: quem não autenticou não tem
  identidade, então o que identifica é a origem. O motivo gravado não distingue
  "conta inexistente" de "senha errada", para não devolver na trilha a enumeração
  de contas que o 401 genérico evita. Cadastro **não** gera evento próprio: ele
  grava um `Usuario` e o interceptor já o registra como adição.
- **Configuração** — trocar a conexão do Postgres ou do Mongo e aplicar migrations.
  São as ações de maior alcance do sistema: apontar o Postgres para outro servidor
  substitui todos os dados que a aplicação enxerga sem alterar registro nenhum. O
  evento guarda host, porta, banco, usuário e o destino anterior — **nunca a
  senha**. O da troca do Mongo cai no destino novo: a trilha nova começa dizendo
  de onde veio e quem a trouxe, e a antiga fica com uma lacuna no fim que o campo
  `Anterior` permite rastrear.
- **Consulta da trilha** — ler a auditoria é uma via alternativa de leitura de todo
  o sistema, então registrar só a exportação deixava passar o mesmo acesso, feito
  pela tela em vez do arquivo. Gera um evento em `TrilhaAuditoria` com o critério e
  o alcance do filtro. Consultar a trilha grava na trilha; o ruído fica contido no
  tipo de entidade próprio, que qualquer outro filtro remove da tela.

**A auditoria nunca derruba a operação de negócio.** A fase 2 do interceptor roda
depois do commit (é onde as referências são resolvidas, com consulta ao banco), e
uma exceção ali reportaria como falha uma operação que deu certo. As duas fases são
blindadas: perda de trilha vira log em nível `Error`.

Visualização é registrada ao abrir o **detalhe** de um registro, nunca em
listagens: leituras superam escritas por ordens de grandeza, e "viu uma lista onde
X aparecia" não é um fato auditável.

Exportação grava o **critério** e a quantidade, jamais um evento por linha
exportada. É o registro mais importante da trilha: é o momento em que os dados saem
do alcance de qualquer controle de acesso — sem ele, a única ação que realmente
esvazia a auditoria seria a única que ela não veria.

### Escopo por agregado

```
IAuditavelRaiz       -> tem trilha própria (Usuario, Cliente, Pedido)
IAuditavelComoParte  -> entra no evento da raiz (ItemPedido)
(sem marcação)       -> ignorado
```

Alterar o status de um pedido, mudar a quantidade de um item e adicionar outro
gera **um único evento**, com o diff qualificado:

```
Status: Rascunho -> Confirmado
Itens[11]: null -> adicionado
Itens[9].Quantidade: 10 -> 25
```

Duas sutilezas de leitura do evento:

- **`Partes` traz as partes ALTERADAS**, não o conteúdo do agregado. O interceptor
  só enxerga o que passou pelo ChangeTracker, e um item intocado nunca chega lá. Já
  o `Snapshot` da raiz é completo — a assimetria é real, e a tela rotula "Itens
  alterados" para não sugerir o contrário. Tornar `Partes` completo exigiria
  carregar a coleção durante o `SaveChanges`, disparando consultas no meio da
  gravação
- **`PartesRemovidas` guarda o último estado do que saiu.** O diff registra apenas
  o id (`Itens[15]: existente -> removido`), e sem esse campo a descrição, a
  quantidade e o preço do item apagado não sobreviviam em lugar nenhum do evento

### Atributos

- `[NaoAuditar]` — exclui a propriedade da trilha. Usado em `SenhaHash`: sem ele, a
  auditoria viraria uma cópia paralela das senhas do sistema
- `[AuditarReferencia]` — grava a chave estrangeira com descrição legível
  (`{ Id: "5", Descricao: "Padaria do João" }`). Um id sozinho perde o sentido
  quando a entidade é renomeada ou removida

### Exportação

`GET /admin/auditoria/exportar` aceita os mesmos filtros da consulta e **ignora a
paginação**: quem exporta quer o resultado inteiro, não a página que está na tela.
A planilha sai com duas abas, porque o evento é aninhado e a planilha é plana —
qualquer achatamento único perderia alguma coisa:

| Aba | Uma linha por | Responde |
|---|---|---|
| `Eventos` | evento | o que aconteceu, quem fez, quando |
| `Alterações` | campo alterado | o que mudou, campo a campo |

As duas se ligam pelo id do evento. A segunda é tabela dinâmica pronta: dá para
agrupar por campo, coisa impossível com o diff condensado numa célula.

O teto é de **50.000 eventos por exportação**. Acima disso a resposta é 400 pedindo
para estreitar o filtro — numa auditoria, uma planilha truncada em silêncio é pior
que erro nenhum, porque quem analisa concluiria que os eventos ausentes não
existiram.

### Testes

```bash
dotnet test
```

Não precisa de banco, Docker nem configuração: o `ConexaoDinamica.Tests` sobe um
SQLite em memória com o interceptor ligado e um repositório falso no lugar do Mongo.

SQLite e não o provider `InMemory` por um motivo específico: o `InMemory` atribui
as chaves no `Add`, e o ponto mais delicado do interceptor — a chave temporária
negativa que só vira id real depois do `INSERT` — simplesmente não aconteceria. Os
testes passariam sem exercitar a armadilha que a classe existe para contornar.

Cobrem o que quebra em silêncio: a resolução da chave definitiva no snapshot, o
`[NaoAuditar]` mantendo o hash de senha fora da trilha, o diff que ignora
atribuição do mesmo valor, a descoberta do agregado raiz por chave estrangeira
temporária, as partes removidas e a garantia de que uma falha de auditoria não
chega ao chamador do `SaveChanges`.

## Frontend

Angular 21 com Material, **standalone e zoneless** (sem `zone.js`), usando signals
e a sintaxe nova de controle de fluxo (`@if` / `@for`). Guards e interceptors são
funcionais.

```
/login          entrada da aplicação
/admin/login    entrada do AdminCenter (separada de propósito)
/setup          assistente de configuração inicial
/app/...        área da aplicação (clientes, pedidos) — exige sessão
/admin/...      conexões e trilha de auditoria — exige role Administrador
```

Os dois logins são telas distintas porque as credenciais são de origens
diferentes: a da aplicação vem da tabela de usuários, a do AdminCenter vem da
configuração e não depende do banco.

Dois interceptors sustentam o fluxo: um injeta o JWT, outro traduz o 503 de
`setupRequired` em navegação para `/setup`.

> O `adminGuard` é **conveniência de interface, não segurança**. Quem valida a role
> é o backend, em toda requisição; o guard só evita abrir uma tela que carregaria
> vazia e terminaria em 403.

## Notas de implementação

**Por que o store de configuração é síncrono.** A lambda do `AddDbContext` é
síncrona; expor só métodos `async` levaria a bloquear uma chamada assíncrona ali
dentro — deadlock esperando acontecer.

**Por que os campos de conexão ficam separados.** A configuração guarda `Host`,
`Porta`, `Database`, `Usuario` e `Senha` em vez da connection string pronta, para o
AdminCenter reexibir o formulário sem parsear. A montagem usa
`NpgsqlConnectionStringBuilder` e `MongoClientSettings` — senha com `;`, `=` ou `@`
corrompe a string se concatenada à mão.

**Por que o interceptor usa duas fases.** Em `SavingChanges` o ChangeTracker ainda
tem os valores originais, mas entidades novas não têm id (só é gerado no INSERT).
Em `SavedChanges` os ids existem, mas o diff já desapareceu. Coleta na primeira
fase, resolve as chaves na segunda.

**Por que o download da planilha passa pelo HttpClient.** O JWT viaja no cabeçalho
`Authorization`, e um `<a href>` faz o *navegador* navegar — fora do HttpClient,
portanto sem interceptor e sem token, resultando em 401. O arquivo é buscado como
blob e o download é disparado depois, a partir do que já chegou.

**Senhas de banco em texto puro no LiteDB.** Decisão consciente para ambiente local
de estudo. Para proteger, basta criptografar na borda do store (ASP.NET Data
Protection) — o contrato não muda.

**Uma coleção só para a auditoria.** Consulta indexada custa proporcional ao que
casa com o filtro, não ao tamanho da coleção — separar por entidade não reduziria
dado nem índice, só distribuiria os mesmos documentos em mais arquivos, e faria a
consulta global da tela virar `$unionWith` com ordenação fora de índice. Os motivos
legítimos para separar seriam retenção ou permissão distintas por tipo, não
desempenho.
