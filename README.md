# Projeto Base — Web API (.NET 10)

Template base para novos projetos de API em C#, com Clean Architecture, autenticação JWT,
EF Core (PostgreSQL) e uma série de configurações prontas — para não precisar montar tudo do zero.

> Este projeto base **não** inclui Docker nem qualquer configuração de nuvem, por escolha.

## Arquitetura (4 camadas)

```
ConexaoDinamica.API             -> Camada de apresentação (Controllers, Middlewares, Program)
ConexaoDinamica.Application     -> Casos de uso (Services, DTOs, Interfaces, Validators)
ConexaoDinamica.Domain          -> Entidades e regras de domínio
ConexaoDinamica.Infrastructure  -> EF Core, Repositórios, DbContext, Migrations, DI
```

## O que já vem configurado

- **Autenticação JWT** (login + cadastro de usuário com hash de senha)
- **FluentValidation** com registro automático dos validators
- **Middleware global de exceções** (`GlobalExceptionHandlingMiddleware`)
- **Rate limiting** (janela fixa na política `auth`)
- **CORS** por configuração (`Cors:AllowedOrigins`)
- **Security headers** (X-Frame-Options, X-Content-Type-Options, X-XSS-Protection)
- **OpenAPI / Swagger UI** com esquema de segurança Bearer
- **EF Core + PostgreSQL** (Npgsql) com `AppDbContext`, configurações e migrations
- **Migração automática** na inicialização (`context.Database.Migrate()`)

## Como usar este template

### 1. Instalar como template `dotnet new`

Na raiz deste diretório:

```bash
dotnet new install .
```

### 2. Criar um projeto novo a partir dele

```bash
dotnet new estudologs -n MinhaApi
```

Isso cria a pasta `MinhaApi/` com todos os namespaces, pastas e arquivos renomeados
de `ConexaoDinamica` para `MinhaApi` automaticamente.

### 3. Atualizar / desinstalar o template

```bash
dotnet new install . --force   # reinstalar após alterações no template
dotnet new uninstall .         # remover
```

## Rodando o projeto gerado

1. Configure `appsettings.json` (ou user-secrets):
   - `ConnectionStrings:DefaultConnection` — string de conexão do PostgreSQL
   - `Jwt:Secret` — chave secreta (mínimo recomendado: 32+ caracteres)
   - `Jwt:Issuer` / `Jwt:Audience`
   - `Cors:AllowedOrigins` — origens permitidas do frontend

2. Aplicar migrations (ou deixar o `Migrate()` da inicialização cuidar disso):

   ```bash
   dotnet ef database update --project MinhaApi.Infrastructure --startup-project MinhaApi.API
   ```

3. Rodar:

   ```bash
   dotnet run --project MinhaApi.API
   ```

   Swagger em `https://localhost:<porta>/swagger`.

## Endpoints prontos

| Método | Rota            | Descrição                    |
|--------|-----------------|------------------------------|
| POST   | `/api/v1/login` | Login (retorna JWT)          |
| POST   | `/api/v1/cadastro` | Cadastro de usuário       |
