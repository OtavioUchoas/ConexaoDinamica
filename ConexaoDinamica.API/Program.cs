using ConexaoDinamica.API.Middlewares;
using ConexaoDinamica.Application.Validador.FluentValidatorExtensions;
using ConexaoDinamica.Infrastructure.Data.DependencyInjections;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {

        var securitySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            [JwtBearerDefaults.AuthenticationScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                Description = "Adicione Token de Autenticação aqui",
                Name = HeaderNames.Authorization,
                Scheme = JwtBearerDefaults.AuthenticationScheme,
                In = ParameterLocation.Header,
                BearerFormat = "JWT"
            }
        };
        document.Components ??= new();
        document.Components.SecuritySchemes = securitySchemes;

        foreach (var operation in document.Paths.Values.SelectMany(path => path.Operations ?? []))
        {
            operation.Value.Security ??= [];
            operation.Value.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, document)] = []
            });
        }
        return Task.CompletedTask;
    });
});

builder.Services.AddFluentValidationConfig();

builder.Services.AddInfrastructure(builder.Configuration);

var jwtSecret = builder.Configuration["Jwt:Secret"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;

// Falha cedo e com instrução, em vez de tarde e sem contexto.
//
// Sem esta verificação, um segredo ausente só se manifestava no primeiro login,
// como erro genérico do middleware de exceções; e um segredo curto demais
// quebrava apenas na assinatura, com a mensagem obscura do HMAC-SHA256 sobre
// tamanho de chave. Nenhum dos dois aponta para o appsettings.
//
// O mínimo de 32 caracteres não é arbitrário: HMAC-SHA256 exige chave de 256
// bits, e uma chave menor é rejeitada pelo próprio algoritmo.
const int tamanhoMinimoSegredo = 32;

if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < tamanhoMinimoSegredo)
{
    throw new InvalidOperationException(
        $"""
         Jwt:Secret ausente ou com menos de {tamanhoMinimoSegredo} caracteres.

         A aplicação não sobe sem ele. Para configurar em desenvolvimento, na pasta
         de ConexaoDinamica.API:

             dotnet user-secrets set "Jwt:Secret" "<chave-com-32-caracteres-ou-mais>"

         user-secrets fica fora do repositório, diferente do appsettings.json.
         Em produção, use variável de ambiente ou um cofre de segredos.
         """);
}

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("PoliticaCors", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(15);
        opt.PermitLimit = 10;
        opt.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "ConexaoDinamica API");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "ConexaoDinamica API Documentation";
    });
}

app.UseCors("PoliticaCors");

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRateLimiter();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    await next();
});

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

// Posição importa. Precisa vir DEPOIS do UseCors: sem os headers de CORS, o
// navegador descarta a resposta 503 e o frontend nunca enxerga o setupRequired
// — o sintoma seria um erro de rede genérico em vez do redirecionamento para o
// AdminCenter. Vem depois do tratador global de exceções (para ficar protegido
// por ele) e antes da autenticação, já que não depende de identidade.
app.UseMiddleware<ModoSetupMiddleware>();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

// O Angular é servido a partir do wwwroot, e cuida do próprio roteamento.
// Sem este fallback, recarregar a página em /admin/conexoes devolveria 404:
// o servidor procuraria um arquivo com esse nome, que não existe — as rotas
// só existem dentro do JavaScript já carregado.
//
// Fica depois de MapControllers para que /api/... continue chegando aos
// controllers; o fallback só atende o que nenhuma rota reivindicou.
app.MapFallbackToFile("index.html");

app.Run();
