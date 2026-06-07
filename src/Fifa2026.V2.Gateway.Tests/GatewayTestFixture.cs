using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WireMock.Server;

namespace Fifa2026.V2.Gateway.Tests;

/// <summary>
/// Sobe o gateway YARP via <see cref="WebApplicationFactory{TEntryPoint}"/> com um
/// backend Function F1 mockado por <see cref="WireMockServer"/>. A env
/// <c>FunctionAppF1Url</c> aponta o cluster YARP para o WireMock (ADE-003 Inv 3 —
/// destination externalizada). Isola os testes de integração do Azure real.
///
/// Story 2.3 — fornece config de identidade VÁLIDA (EntraTenantId/EntraClientId,
/// fail-closed AC-6 exige tenant real, NÃO 'common') e sobrescreve a validação do
/// esquema "Entra" para usar uma chave RSA de teste conhecida em vez do JWKS do
/// Entra real — permitindo mintar tokens válidos/inválidos offline (AC-12).
/// </summary>
public sealed class GatewayTestFixture : WebApplicationFactory<Program>
{
    public WireMockServer Backend { get; }

    public GatewayTestFixture()
    {
        Backend = WireMockServer.Start();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting escreve direto na configuração do host que builder.Configuration
        // lê em tempo de build no Program.cs (precede appsettings.json) — garante que
        // a config de identidade fail-closed (AC-6) chegue antes da checagem de startup.
        builder.UseSetting("FunctionAppF1Url", Backend.Url);
        builder.UseSetting("Gateway:FrontendOrigin", "https://fifa2026-web.azurewebsites.net");
        builder.UseSetting("EntraTenantId", TestTokenFactory.TenantId);
        builder.UseSetting("EntraClientId", TestTokenFactory.ClientId);

        builder.ConfigureTestServices(services =>
        {
            // Substitui a validação do esquema "Entra": em vez de buscar o JWKS no
            // discovery do Entra (rede + chaves reais), valida contra a chave RSA de
            // teste. Issuer/audience/lifetime continuam validados EXPLICITAMENTE —
            // ou seja, os mesmos parâmetros de segurança do Program.cs (M-1), só que
            // com a chave de teste. Cenários de rejeição (AC-12) continuam valendo.
            services.PostConfigure<JwtBearerOptions>("Entra", options =>
            {
                options.Authority = null;
                options.RequireHttpsMetadata = false;
                options.ConfigurationManager = null!;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = TestTokenFactory.ValidIssuer,
                    ValidateAudience = true,
                    ValidAudiences = new[]
                    {
                        TestTokenFactory.ClientId,
                        $"api://{TestTokenFactory.ClientId}"
                    },
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = TestTokenFactory.PublicSigningKey,
                    ClockSkew = TimeSpan.Zero
                };
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Backend.Stop();
            Backend.Dispose();
        }
        base.Dispose(disposing);
    }
}
