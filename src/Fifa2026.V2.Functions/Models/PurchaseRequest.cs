using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Fifa2026.V2.Functions.Models;

/// <summary>
/// Body do POST /api/v2/purchase.
/// Contrato (blueprint 4.F1.③): { matchId, category: VIP|Cat1|Cat2, userId, quantity }.
/// </summary>
public sealed class PurchaseRequest
{
    [JsonPropertyName("matchId")]
    [Range(1, int.MaxValue, ErrorMessage = "matchId deve ser um inteiro positivo.")]
    public int MatchId { get; set; }

    [JsonPropertyName("category")]
    [Required(ErrorMessage = "category é obrigatório.")]
    [RegularExpression("^(VIP|Cat1|Cat2)$", ErrorMessage = "category deve ser VIP, Cat1 ou Cat2.")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("userId")]
    [Range(1, int.MaxValue, ErrorMessage = "userId deve ser um inteiro positivo.")]
    public int UserId { get; set; }

    [JsonPropertyName("quantity")]
    [Range(1, 10, ErrorMessage = "quantity deve estar entre 1 e 10.")]
    public int Quantity { get; set; }
}
