using System.ComponentModel;
using Fifa2026.V2.McpServer.Data;
using ModelContextProtocol.Server;

namespace Fifa2026.V2.McpServer.Tools;

/// <summary>
/// AC-2/3/4/5 — as 3 tools MCP do FIFA 2026 Tickets, expostas via o SDK oficial
/// (ADE-002 Inv 1/2). O atributo <see cref="McpServerToolTypeAttribute"/> faz o
/// SDK descobrir a classe (WithToolsFromAssembly em Program.cs); cada método com
/// <see cref="McpServerToolAttribute"/> vira uma tool listada em <c>tools/list</c>
/// e despachada em <c>tools/call</c> — o framing JSON-RPC 2.0 é do SDK (não
/// implementamos à mão; AC-15 anti-hallucination).
///
/// O JSON Schema de input de cada tool é DERIVADO pelo SDK a partir da assinatura
/// do método + atributos [Description] (System.ComponentModel) — não inventamos
/// schema manual. Dependências (repositório, contexto de oid) são INJETADAS por DI
/// nos parâmetros do método (o SDK resolve do IServiceProvider da request).
///
/// Identidade (AC-9): cada tool lê X-Entra-OID via EntraOidContext SOMENTE para
/// logging mascarado — nunca revalida JWT (gateway é o guardião).
/// </summary>
[McpServerToolType]
public static class FifaTicketTools
{
    [McpServerTool(Name = "consultar_disponibilidade", ReadOnly = true)]
    [Description(
        "Consulta disponibilidade e preços de ingressos para uma partida da Copa 2026. " +
        "Use quando o usuário perguntar se há ingressos para um jogo ou quanto custam. " +
        "Informe matchId (numérico) OU matchDescription (ex.: 'Brasil x Argentina').")]
    public static async Task<AvailabilityResult> ConsultarDisponibilidadeAsync(
        IFifaQueryRepository repository,
        EntraOidContext oidContext,
        ILogger<DiagnosticsCategory> logger,
        [Description("ID numérico da partida (opcional se matchDescription for informado).")]
        int? matchId = null,
        [Description("Descrição da partida, ex.: 'Brasil x Argentina' (opcional se matchId for informado).")]
        string? matchDescription = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "tool=consultar_disponibilidade oid={Oid} matchId={MatchId}",
            oidContext.GetMaskedOidForLog(), matchId);

        return await repository.ConsultarDisponibilidadeAsync(matchId, matchDescription, cancellationToken);
    }

    [McpServerTool(Name = "verificar_ingresso", ReadOnly = true)]
    [Description(
        "Verifica se um ingresso é válido e retorna dados da compra (comprador, partida, " +
        "categoria, data). Use quando o usuário perguntar se um ingresso/ID é válido.")]
    public static async Task<TicketVerificationResult> VerificarIngressoAsync(
        IFifaQueryRepository repository,
        EntraOidContext oidContext,
        ILogger<DiagnosticsCategory> logger,
        [Description("ID numérico do ingresso (compra) a verificar.")]
        int ingressoId,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "tool=verificar_ingresso oid={Oid} ingressoId={IngressoId}",
            oidContext.GetMaskedOidForLog(), ingressoId);

        return await repository.VerificarIngressoAsync(ingressoId, cancellationToken);
    }

    [McpServerTool(Name = "consultar_bracket", ReadOnly = true)]
    [Description(
        "Consulta os jogos de uma rodada do mata-mata (oitavas, quartas, semifinal, final) " +
        "com placares e classificados. Use quando o usuário perguntar sobre confrontos/resultados de uma fase.")]
    public static async Task<IReadOnlyList<BracketMatchResult>> ConsultarBracketAsync(
        IFifaQueryRepository repository,
        EntraOidContext oidContext,
        ILogger<DiagnosticsCategory> logger,
        [Description("Rodada do mata-mata, ex.: 'oitavas', 'quartas', 'semifinal', 'final'.")]
        string rodada,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "tool=consultar_bracket oid={Oid} rodada={Rodada}",
            oidContext.GetMaskedOidForLog(), rodada);

        return await repository.ConsultarBracketAsync(rodada, cancellationToken);
    }

    /// <summary>Marcador de categoria para o ILogger das tools (mantém logs agrupados).</summary>
    public sealed class DiagnosticsCategory { }
}
