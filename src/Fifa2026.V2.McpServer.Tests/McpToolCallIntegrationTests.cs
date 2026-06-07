using System.Text.Json;
using Fifa2026.V2.McpServer.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using Moq;
using Xunit;

namespace Fifa2026.V2.McpServer.Tests;

/// <summary>
/// AC-2/3 ponta-a-ponta: prova que tools/call REALMENTE despacha para o handler com
/// DI funcionando — o SDK injeta IFifaQueryRepository (mockado aqui) e EntraOidContext
/// nos parâmetros do método da tool. Substitui o repositório por um mock via
/// WebApplicationFactory.WithWebHostBuilder (ConfigureTestServices), então NÃO toca SQL.
/// </summary>
public sealed class McpToolCallIntegrationTests
{
    [Fact]
    public async Task ToolsCall_consultar_disponibilidade_dispatches_to_handler_with_DI()
    {
        var repo = new Mock<IFifaQueryRepository>();
        repo.Setup(r => r.ConsultarDisponibilidadeAsync(7, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AvailabilityResult
            {
                Encontrado = true,
                Partida = "Brasil x Argentina",
                VipDisponivel = 3,
                PrecoVip = 999m,
            });

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.ConfigureTestServices(services =>
            {
                // Substitui o repositório real (Dapper/SQL) pelo mock.
                services.AddSingleton(repo.Object);
            });
        });

        var httpClient = factory.CreateClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
            },
            httpClient,
            loggerFactory: null!,
            ownsHttpClient: false);

        await using var mcpClient = await McpClient.CreateAsync(transport);

        var result = await mcpClient.CallToolAsync(
            "consultar_disponibilidade",
            new Dictionary<string, object?> { ["matchId"] = 7 }!);

        // O resultado da tool deve refletir o mock (DI funcionou). O SDK pode
        // entregar o resultado em StructuredContent (objeto) e/ou no Content textual.
        var structured = result.StructuredContent.HasValue
            ? JsonSerializer.Serialize(result.StructuredContent.Value)
            : string.Empty;
        var textual = string.Join(
            "\n",
            result.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Select(c => c.Text));
        var combined = structured + "\n" + textual;

        Assert.False(result.IsError ?? false, $"tool retornou erro. Content={textual}");
        Assert.Contains("Brasil x Argentina", combined);
        repo.Verify(r => r.ConsultarDisponibilidadeAsync(7, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
