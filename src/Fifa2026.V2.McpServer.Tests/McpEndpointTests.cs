using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using ModelContextProtocol.Client;
using Xunit;

namespace Fifa2026.V2.McpServer.Tests;

/// <summary>
/// AC-2 / AC-15 — testes de PROTOCOLO do endpoint /mcp ponta-a-ponta via
/// WebApplicationFactory&lt;Program&gt; (TestServer) + o CLIENTE MCP oficial
/// (ModelContextProtocol.Client 1.4.0). O cliente faz o handshake JSON-RPC 2.0
/// real (initialize → tools/list) sobre o HttpClient do TestServer, provando que
/// o SDK servidor está expondo as 3 tools corretamente — sem reimplementar o
/// protocolo à mão (AC-15).
///
/// tools/list é metadado (nenhum handler roda) → não exige SqlConnectionString.
/// </summary>
public sealed class McpEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public McpEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_endpoint_responds_healthy()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("healthy", body.GetProperty("status").GetString());
        Assert.Equal("mcp-server", body.GetProperty("service").GetString());
    }

    [Fact]
    public async Task Mcp_client_lists_the_three_tools_over_streamable_http()
    {
        // HttpClient do TestServer (in-memory) aponta para o /mcp do nosso servidor.
        var httpClient = _factory.CreateClient();

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

        var tools = await mcpClient.ListToolsAsync();

        var names = tools.Select(t => t.Name).ToHashSet();

        Assert.Contains("consultar_disponibilidade", names);
        Assert.Contains("verificar_ingresso", names);
        Assert.Contains("consultar_bracket", names);
        Assert.Equal(3, names.Count);
    }
}
