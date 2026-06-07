using System.Threading.RateLimiting;
using Fifa2026.V2.Gateway.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Yarp.ReverseProxy.Transforms;

// =============================================================================
// Fifa2026.V2.Gateway — Gateway profissional em código C# com YARP (Story 2.2 / F2)
//
// Substitui o APIM Developer (ADE-004): rate-limit, output cache, CORS, header
// transform e JWT placeholder são MECANISMOS DE CÓDIGO, não policies XML opacas.
// Cada capacidade tem paridade 1:1 com uma policy APIM (ADE-004 Invariante 3).
//
// Pipeline (ORDEM IMPORTA — ADE-004 / story Task 2.6):
//   UseCors → UseRateLimiter → UseOutputCache → UseAuthentication
//           → UseAuthorization → MapReverseProxy
// =============================================================================

var builder = WebApplication.CreateBuilder(args);

// Constantes de configuração de pipeline.
const string RateLimiterPolicy = "fixed";              // partição fixed-window por IP (AC-5)
const string OutputCachePolicy = "purchase-status-30s"; // cache 30s no GET (AC-6)
const string CorsPolicy = "frontend";                   // origin restrito ao front (AC-7)
const string CorrelationHeader = "X-Correlation-ID";    // ADE-000 Inv 5 / AC-8
const string EntraOidHeader = "X-Entra-OID";            // Story 2.3 AC-7 / ADE-005 Inv 4

// Claim names do Microsoft Identity Platform (AC-14 anti-hallucination — validados
// contra docs oficiais "id-token-claims-reference" / "access-token-claims-reference").
//   - "oid": object id estável do usuário no tenant (token v2.0 / endpoint /v2.0).
//   - URI longa: nome do mesmo claim após o mapeamento de inbound claims do
//     JwtBearer handler (System.Security.Claims) — usado como fallback (ADE-005 Inv 4 /
//     story troubleshooting "Claim oid ausente").
const string OidClaim = "oid";
const string OidClaimUri = "http://schemas.microsoft.com/identity/claims/objectidentifier";

// -----------------------------------------------------------------------------
// YARP reverse proxy (ADE-004 Inv 1 e 2): rotas/clusters do appsettings.json +
// transforms programáticos (X-Correlation-ID, que exige geração de GUID novo).
// O IProxyConfigFilter sobrescreve a destination do cluster com a URL real da
// Function F1 (env FunctionAppF1Url — ADE-003 Inv 3, nunca hardcoded). A
// connection string SQL permanece NAS FUNCTIONS, não aqui.
// -----------------------------------------------------------------------------
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddConfigFilter<FunctionDestinationConfigFilter>()
    // Story 2.5 / F5 — injeta a URL real do McpServer no cluster mcp-server (AC-8).
    .AddConfigFilter<McpServerDestinationConfigFilter>()
    // Story 2.6 / F6 — injeta a URL real do serviço FlowEvents no cluster flow-events
    // (AC-3/AC-5). O gateway permanece o NÓ ZERO: injeta X-Correlation-ID nas requests
    // ao FlowEvents também (mesmo transform global de borda).
    .AddConfigFilter<FlowEventsDestinationConfigFilter>()
    .AddTransforms(transformBuilderContext =>
    {
        // AC-8 / ADE-000 Inv 5 — injeta X-Correlation-ID (novo GUID se ausente) em
        // CADA requisição encaminhada ao backend. Aplicado em TODAS as rotas
        // (gateway é o nó zero do Flow Visualizer de F6).
        transformBuilderContext.AddRequestTransform(transformContext =>
        {
            var incoming = transformContext.HttpContext.Request.Headers[CorrelationHeader].ToString();
            var correlationId = string.IsNullOrWhiteSpace(incoming)
                ? Guid.NewGuid().ToString()
                : incoming;

            transformContext.ProxyRequest.Headers.Remove(CorrelationHeader);
            transformContext.ProxyRequest.Headers.TryAddWithoutValidation(CorrelationHeader, correlationId);

            // Devolve o mesmo correlationId ao cliente (observabilidade de borda — AC-11).
            transformContext.HttpContext.Response.Headers[CorrelationHeader] = correlationId;

            return ValueTask.CompletedTask;
        });

        // Story 2.3 AC-7 / ADE-005 Inv 4 — propagação de identidade downstream.
        // Após o JWT ser validado pelo AddJwtBearer, extrai o claim `oid` do usuário
        // autenticado e o injeta como header X-Entra-OID na requisição encaminhada à
        // Function F1 (que grava entra_oid em SQL). A Function NUNCA valida o token —
        // confia no header propagado pelo gateway (guardião único de JWT).
        //
        // SEGURANÇA (defense-in-depth): SEMPRE remove qualquer X-Entra-OID que tenha
        // vindo do cliente ANTES de (eventualmente) injetar o valor derivado do token.
        // Isso impede spoofing de identidade — o cliente não consegue forjar o header.
        transformBuilderContext.AddRequestTransform(transformContext =>
        {
            // Anti-spoofing: descarta qualquer X-Entra-OID de origem externa.
            transformContext.ProxyRequest.Headers.Remove(EntraOidHeader);

            var user = transformContext.HttpContext.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                // Token v2.0 traz o claim "oid"; após o mapeamento inbound do handler
                // o mesmo valor pode aparecer sob a URI longa (fallback — ADE-005 Inv 4).
                var oid = user.FindFirst(OidClaim)?.Value
                    ?? user.FindFirst(OidClaimUri)?.Value;

                if (!string.IsNullOrWhiteSpace(oid))
                {
                    transformContext.ProxyRequest.Headers.TryAddWithoutValidation(EntraOidHeader, oid);
                }
            }

            // NÃO logamos o token nem o oid em texto (AC-12 / CodeRabbit focus area —
            // oid é PII de identidade; nunca aparece em log de aplicação).
            return ValueTask.CompletedTask;
        });
    });

// -----------------------------------------------------------------------------
// AC-5 — Rate limiting em código (paridade com APIM rate-limit-by-key).
// Fixed window: 5 requisições/min por IP. 6ª chamada em < 1min → HTTP 429.
// -----------------------------------------------------------------------------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimiterPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

// -----------------------------------------------------------------------------
// AC-6 — Output cache em código (paridade com APIM cache-lookup/cache-store).
// Policy de 30s + header X-Cache HIT/MISS (XCacheOutputCachePolicy).
// -----------------------------------------------------------------------------
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy(OutputCachePolicy, builderPolicy =>
        builderPolicy
            .AddPolicy<XCacheOutputCachePolicy>()
            .Expire(TimeSpan.FromSeconds(30)));
});

// -----------------------------------------------------------------------------
// AC-7 — CORS restrito ao domínio do frontend (paridade com APIM cors).
// -----------------------------------------------------------------------------
var frontendOrigin = builder.Configuration["Gateway:FrontendOrigin"]
    ?? "https://fifa2026-web.azurewebsites.net";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
        policy.WithOrigins(frontendOrigin)
            .AllowAnyMethod()
            .AllowAnyHeader());
});

// -----------------------------------------------------------------------------
// Story 2.3 AC-6 / AC-12 — Validação de JWT Entra ATIVADA no gateway YARP
// (ADE-004 Inv 4 / ADE-005 Inv 4). O placeholder de F2 ganha vida: AddJwtBearer
// valida iss/aud/assinatura/expiração contra o discovery do issuer Entra workforce.
//
// CARRY-FORWARD M-1 (gate S2.2) — FAIL-CLOSED: o tenant NÃO tem default "common".
// "common" aceitaria tokens de QUALQUER tenant Entra (multi-tenant) — brecha de
// segurança. O tenant é configuração OBRIGATÓRIA: ausência → a app não sobe.
// O issuer e o audience são validados EXPLICITAMENTE (ValidIssuer / ValidAudiences),
// não apenas inferidos do Authority (ADE-005 Inv 1).
//
// Config requerida (App Settings do Container App, sem valores reais no repo):
//   EntraTenantId  — GUID do tenant workforce do aluno (Portal → Entra ID → Overview)
//   EntraClientId  — Application (client) ID da App Registration SPA (= aud do token)
// -----------------------------------------------------------------------------
var entraTenantId = builder.Configuration["EntraTenantId"]
    // Compat: aceita a chave Jwt:TenantId herdada de F2, mas SEM default "common".
    ?? builder.Configuration["Jwt:TenantId"];
var entraClientId = builder.Configuration["EntraClientId"]
    ?? builder.Configuration["Jwt:Audience"];

if (string.IsNullOrWhiteSpace(entraTenantId) ||
    string.Equals(entraTenantId, "common", StringComparison.OrdinalIgnoreCase))
{
    // Fail-closed: recusa subir com tenant ausente ou multi-tenant ("common").
    throw new InvalidOperationException(
        "Configuração de identidade ausente/insegura: defina 'EntraTenantId' com o GUID " +
        "do tenant workforce (não use 'common' — aceitaria tokens de qualquer tenant). " +
        "Story 2.3 AC-6 / carry-forward M-1 do gate S2.2.");
}

if (string.IsNullOrWhiteSpace(entraClientId))
{
    throw new InvalidOperationException(
        "Configuração de identidade ausente: defina 'EntraClientId' com o Application " +
        "(client) ID da App Registration SPA (= audience esperado do access token). " +
        "Story 2.3 AC-6.");
}

// Authority v2.0 (token v2.0 traz claim `oid`). O issuer EXATO esperado é derivado
// do tenant — validado explicitamente abaixo (não confiamos só no metadata).
var entraAuthority = $"https://login.microsoftonline.com/{entraTenantId}/v2.0";
var entraIssuerV2 = $"https://login.microsoftonline.com/{entraTenantId}/v2.0";

// O esquema "Entra" é também o DEFAULT (authenticate + challenge). Em F2 o handler
// foi registrado sob "Entra" mas o default ficou "Bearer" — inofensivo enquanto as
// rotas eram anônimas; com RequireAuthorization() em F3 o default precisa apontar
// para o esquema que existe, senão o challenge falha (No DefaultChallengeScheme).
const string EntraScheme = "Entra";
builder.Services
    .AddAuthentication(EntraScheme)
    .AddJwtBearer(EntraScheme, options =>
    {
        // Discovery (.well-known/openid-configuration) fornece JWKS para validar a
        // assinatura RS256. iss/aud/lifetime são checados EXPLICITAMENTE abaixo.
        options.Authority = entraAuthority;
        options.Audience = entraClientId;
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            // ValidIssuer EXPLÍCITO (M-1): só aceita o issuer do tenant configurado.
            ValidIssuer = entraIssuerV2,
            ValidateAudience = true,
            // ValidAudiences EXPLÍCITO (M-1): aceita o client id e o App ID URI
            // (api://<client-id>) — ambos formatos de `aud` que o Entra emite p/ SPA.
            ValidAudiences = new[] { entraClientId, $"api://{entraClientId}" },
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            // Sem tolerância extra de relógio: token expirado → 401 (AC-12).
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

// Observabilidade de borda (AC-11 / ADE-000 Inv 5) — App Insights se a connection
// string estiver presente (APPLICATIONINSIGHTS_CONNECTION_STRING). No-op sem ela.
builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

// Pipeline na ORDEM correta (Task 2.6 / ADE-004):
app.UseCors(CorsPolicy);          // 1. CORS
app.UseRateLimiter();             // 2. Rate limiter (429)
app.UseMiddleware<XCacheMiddleware>(); // 2.5 default X-Cache: MISS (antes do cache)
app.UseOutputCache();             // 3. Output cache (30s) — seta X-Cache: HIT no store
app.UseAuthentication();          // 4. Authentication (JWT placeholder F2-anônimo)
app.UseAuthorization();           // 5. Authorization

// Endpoint de saúde para smoke test / Container App health probe.
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "gateway-yarp" }));

// 6. MapReverseProxy com rate-limit em todas as rotas, cache na rota GET e, agora
//    em F3, EXIGÊNCIA de JWT Entra válido em todas as rotas v2 (AC-6).
//    Sem Bearer válido → 401 (UseAuthentication/UseAuthorization rejeitam antes do
//    proxy). Token expirado/issuer errado/aud errado → 401 (AC-12). Esta linha é o
//    "ganhar vida" do placeholder comentado de F2.
app.MapReverseProxy()
    .RequireRateLimiting(RateLimiterPolicy)
    .CacheOutput(OutputCachePolicy)
    .RequireAuthorization();

app.Run();

// Necessário para WebApplicationFactory<Program> nos testes de integração (Task de testes).
public partial class Program { }
