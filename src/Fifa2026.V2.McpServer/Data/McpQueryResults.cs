using System.Text.Json.Serialization;

namespace Fifa2026.V2.McpServer.Data;

/// <summary>
/// AC-3 — resultado de <c>consultar_disponibilidade</c>. Disponibilidade e preço por
/// categoria de uma partida (tabela ticket_categories — schema real do projeto).
/// </summary>
public sealed class AvailabilityResult
{
    [JsonPropertyName("encontrado")]
    public bool Encontrado { get; init; }

    [JsonPropertyName("partida")]
    public string? Partida { get; init; }

    [JsonPropertyName("vipDisponivel")]
    public int VipDisponivel { get; init; }

    [JsonPropertyName("cat1Disponivel")]
    public int Cat1Disponivel { get; init; }

    [JsonPropertyName("cat2Disponivel")]
    public int Cat2Disponivel { get; init; }

    [JsonPropertyName("precoVip")]
    public decimal? PrecoVip { get; init; }

    [JsonPropertyName("precoCat1")]
    public decimal? PrecoCat1 { get; init; }

    [JsonPropertyName("precoCat2")]
    public decimal? PrecoCat2 { get; init; }
}

/// <summary>
/// AC-4 — resultado de <c>verificar_ingresso</c>. Validade de um ingresso (compra) e
/// dados associados (tabelas purchases + ticket_categories + matches + users).
/// </summary>
public sealed class TicketVerificationResult
{
    [JsonPropertyName("valido")]
    public bool Valido { get; init; }

    [JsonPropertyName("comprador")]
    public string? Comprador { get; init; }

    [JsonPropertyName("partida")]
    public string? Partida { get; init; }

    [JsonPropertyName("categoria")]
    public string? Categoria { get; init; }

    [JsonPropertyName("dataCompra")]
    public DateTime? DataCompra { get; init; }
}

/// <summary>
/// AC-5 — uma linha do bracket (jogo de uma rodada). Times podem ser NULL no
/// mata-mata até a classificação (ver migration knockout-matches). Placar NULL
/// quando o jogo ainda não foi disputado.
/// </summary>
public sealed class BracketMatchResult
{
    [JsonPropertyName("jogo")]
    public string Jogo { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public DateTime Data { get; init; }

    [JsonPropertyName("horario")]
    public string? Horario { get; init; }

    [JsonPropertyName("estadio")]
    public string? Estadio { get; init; }

    [JsonPropertyName("placarMandante")]
    public int? PlacarMandante { get; init; }

    [JsonPropertyName("placarVisitante")]
    public int? PlacarVisitante { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }
}
