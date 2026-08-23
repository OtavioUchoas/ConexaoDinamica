# ConexaoDinamica — Web API (.NET 10)

Projeto de estudo sobre **configuração de conexões de banco em tempo de execução**.

A ideia central: em vez de as connection strings virem de `appsettings.json` ou variáveis de
ambiente, elas são configuradas por um **AdminCenter** no frontend e persistidas localmente —
sem reiniciar a aplicação. São duas conexões independentes:

- **PostgreSQL** — dados da aplicação (EF Core)
- **MongoDB** — logs de auditoria

As configurações ficam num **LiteDB** embarcado, que resolve o problema de origem: é preciso
guardar a configuração do banco *antes* de existir um banco configurado.

> Sem Docker e sem nuvem, por escolha. Cenário alvo: on-premise, instância única.

## Arquitetura (4 camadas)

```
ConexaoDinamica.API             -> Controllers, Middlewares, Program
ConexaoDinamica.Application     -> Casos de uso (Services, DTOs, Interfaces, Validators)
ConexaoDinamica.Domain          -> Entidades e regras de domínio
ConexaoDinamica.Infrastructure  -> EF Core, LiteDB, Repositórios, Migrations, DI
```

## Estado atual

### Pronto

- **Autenticação JWT** com claim de role (`PerfilUsuario`: `Comum` / `Administrador`)
- **Admin bootstrap (break-glass)** — credenciais em `appsettings` com hash BCrypt, aceita
  username **ou** email. Não depende do banco de propósito: é o acesso usado para configurar
  as conexões e para recuperar o acesso caso o Postgres fique indisponível
- **Store de configuração em LiteDB** (`IConexaoConfigStore`) — singleton com cache em memória
- **EF Core + PostgreSQL** (Npgsql) com migrations
- FluentValidation, middleware global de exceções, rate limiting, CORS, security headers,
  Swagger UI com esquema Bearer

### Em aberto

- [ ] `AddDbContext` lendo a connection string do store (hoje ainda lê do `appsettings`)
- [ ] `IDesignTimeDbContextFactory` — necessário para o `dotnet ef` continuar funcionando
      quando a connection string sair do `appsettings`
- [ ] Endpoints do AdminCenter: testar conexão → salvar → aplicar migrations
- [ ] Middleware de "modo setup" (bloquear rotas enquanto não houver configuração)
- [ ] Auditoria em MongoDB
- [ ] Seed do super usuário no Postgres

## Rodando

1. Configure em `appsettings.json` (ou via `dotnet user-secrets`):

   | Chave | Descrição |
   |---|---|
   | `Jwt:Secret` | Chave de assinatura — **32+ caracteres** |
   | `Jwt:Issuer` / `Jwt:Audience` | Emissor e audiência do token |
   | `AdminBootstrap:*` | Credenciais do admin (`SenhaHash` em BCrypt) |
   | `ConnectionStrings:DefaultConnection` | Postgres — temporário, até o AdminCenter assumir |
   | `Cors:AllowedOrigins` | Origens permitidas do frontend |
   | `Storage:ConfigDbPath` | Opcional. Padrão: `%LOCALAPPDATA%\ConexaoDinamica\config.db` |

2. Rode:

   ```bash
   dotnet run --project ConexaoDinamica.API
   ```

   Swagger em `https://localhost:<porta>/swagger`.

> ⚠️ A migração automática do startup está **desativada** — a aplicação precisa subir mesmo sem
> banco configurado, que é a premissa do projeto. Para aplicar migrations manualmente:
>
> ```bash
> dotnet ef database update --project ConexaoDinamica.Infrastructure --startup-project ConexaoDinamica.API
> ```

## Endpoints

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| POST | `/api/v1/login` | — | Login de usuário (Postgres) |
| POST | `/api/v1/cadastro` | — | Cadastro de usuário |
| POST | `/api/v1/admin/login` | — | Login do admin bootstrap (username ou email) |

## Notas de implementação

**Por que o store é síncrono.** A lambda do `AddDbContext` é síncrona; expor só métodos `async`
levaria a bloquear uma chamada assíncrona ali dentro — deadlock esperando acontecer.

**Por que os campos ficam separados.** A configuração guarda `Host`, `Porta`, `Database`,
`Usuario` e `Senha` em vez da connection string pronta, para o AdminCenter reexibir o formulário
sem precisar parsear a string. A montagem usa `NpgsqlConnectionStringBuilder`.

**Senha em texto puro.** Decisão consciente para ambiente local de estudo. Para proteger, basta
criptografar na borda do store (ASP.NET Data Protection) — o contrato não muda.
