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

## Arquitetura (4 camadas)

```
ConexaoDinamica.API             -> Controllers, Middlewares, Program
ConexaoDinamica.Application     -> Casos de uso (Services, DTOs, Interfaces, Validators)
ConexaoDinamica.Domain          -> Entidades e regras de domínio
ConexaoDinamica.Infrastructure  -> EF Core, LiteDB, MongoDB, Repositórios, Migrations, DI
```

Os drivers (LiteDB, MongoDB, Npgsql) vivem **apenas** na Infrastructure. As demais
camadas enxergam somente as abstrações.

## Como funciona o setup

1. A aplicação sobe **sem banco nenhum configurado**
2. Rotas de negócio respondem **503** com `setupRequired: true` até que as duas
   conexões existam
3. O administrador entra com as credenciais de *bootstrap* (que não dependem do
   banco) e configura Postgres e MongoDB pelo AdminCenter
4. Aplica as migrations pelo próprio painel — que cria o **super administrador** e
   devolve a senha gerada **uma única vez**
5. A partir da requisição seguinte, tudo funciona. Sem restart.

## Estado atual

### Pronto

- **Conexões configuradas em runtime**, com troca válida na requisição seguinte
- **AdminCenter**: testar conexão, salvar, ver status e aplicar migrations
- **Modo setup**: bloqueia rotas de negócio enquanto faltar alguma conexão,
  informando quais
- **Autenticação JWT** com claim de role (`Comum` / `Administrador`)
- **Admin bootstrap (break-glass)**: credenciais em `appsettings` com hash BCrypt,
  aceita username **ou** email. Não depende do banco de propósito — é o acesso
  usado para configurar e para recuperar o sistema
- **Seed do super administrador** com senha aleatória exibida uma vez
- **Auditoria no MongoDB**: adição, alteração, remoção e visualização
- **Agregado de exemplo** (Cliente, Pedido, ItemPedido) exercitando a auditoria
- FluentValidation, middleware global de exceções, rate limiting, CORS, security
  headers, Swagger com esquema Bearer

### Em aberto

- [ ] **Padrão Outbox** para a auditoria. Hoje, se o MongoDB cair com o sistema no
      ar, o evento é perdido (a operação de negócio segue e a falha vai para o log).
      O Outbox gravaria o evento no Postgres na mesma transação, publicando depois
- [ ] Endpoints de negócio para Pedido e Cliente (hoje existem só as entidades)
- [ ] Auditoria de exportações e relatórios, registrando o critério da consulta

## Rodando

### 1. Configure o segredo do JWT

A aplicação **não sobe sem ele**, e falha com instrução na tela. Na pasta de
`ConexaoDinamica.API`:

```bash
dotnet user-secrets set "Jwt:Secret" "<chave-com-32-caracteres-ou-mais>"
```

Use `user-secrets` e não `appsettings.json`: o primeiro fica fora do repositório.
O mínimo de 32 caracteres é exigência do HMAC-SHA256 (chave de 256 bits).

### 2. Ajuste o restante em `appsettings.json`

| Chave | Descrição |
|---|---|
| `Jwt:Issuer` / `Jwt:Audience` | Emissor e audiência do token |
| `AdminBootstrap:*` | Credenciais do admin (`SenhaHash` em BCrypt) |
| `Cors:AllowedOrigins` | Origens permitidas do frontend |
| `Storage:ConfigDbPath` | Opcional. Padrão: `%LOCALAPPDATA%\ConexaoDinamica\config.db` |

### 3. Rode

```bash
dotnet run --project ConexaoDinamica.API
```

Swagger em `https://localhost:<porta>/swagger`. As conexões de banco são
configuradas pelo AdminCenter, não aqui.

> ⚠️ **Derrube a API antes de usar `dotnet ef`.** As ferramentas do EF sobem o
> `Program.cs`, o que abre o arquivo do LiteDB — e os dois processos disputam o
> mesmo lock. O erro resultante não menciona LiteDB em lugar nenhum.

## Endpoints

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| POST | `/api/v1/login` | — | Login de usuário |
| POST | `/api/v1/cadastro` | — | Cadastro de usuário |
| GET | `/api/v1/usuarios/{id}` | JWT | Detalhe (registra visualização) |
| POST | `/api/v1/admin/login` | — | Login do admin bootstrap |
| GET/PUT | `/api/v1/admin/conexao/postgres` | Admin | Ler / salvar conexão |
| POST | `/api/v1/admin/conexao/postgres/testar` | Admin | Testar sem salvar |
| GET/POST | `/api/v1/admin/conexao/postgres/migrations` | Admin | Status / aplicar |
| GET/PUT | `/api/v1/admin/conexao/mongo` | Admin | Ler / salvar conexão |
| POST | `/api/v1/admin/conexao/mongo/testar` | Admin | Testar sem salvar |

## Auditoria

Eventos gravados na coleção `eventos_auditoria`:

| Tipo | Como é capturado | Conteúdo |
|---|---|---|
| Adição | `SaveChangesInterceptor` | snapshot do estado inicial |
| Alteração | `SaveChangesInterceptor` | diff + snapshot final |
| Remoção | `SaveChangesInterceptor` | snapshot do último estado |
| Visualização | chamada explícita | quem acessou o quê |

Visualização é explícita porque uma consulta não passa por `SaveChanges` — o
interceptor não tem como enxergá-la. É registrada ao abrir o **detalhe** de um
registro, nunca em listagens: leituras superam escritas por ordens de grandeza, e
"viu uma lista onde X aparecia" não é um fato auditável.

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

### Atributos

- `[NaoAuditar]` — exclui a propriedade da trilha. Usado em `SenhaHash`: sem ele, a
  auditoria viraria uma cópia paralela das senhas do sistema
- `[AuditarReferencia]` — grava a chave estrangeira com descrição legível
  (`{ Id: "5", Descricao: "Padaria do João" }`). Um id sozinho perde o sentido
  quando a entidade é renomeada ou removida

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

**Senhas de banco em texto puro no LiteDB.** Decisão consciente para ambiente local
de estudo. Para proteger, basta criptografar na borda do store (ASP.NET Data
Protection) — o contrato não muda.
