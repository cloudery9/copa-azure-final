using System.ComponentModel.DataAnnotations;
using Fifa2026.V2.Functions.Models;
using Xunit;

namespace Fifa2026.V2.Functions.Tests;

/// <summary>
/// AC-3 — validação do body do POST /api/v2/purchase via DataAnnotations.
/// </summary>
public sealed class PurchaseRequestValidationTests
{
    private static IReadOnlyList<ValidationResult> Validate(PurchaseRequest request)
    {
        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, context, results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Valid_request_passes_validation()
    {
        var request = new PurchaseRequest { MatchId = 1, Category = "VIP", UserId = 5, Quantity = 2 };

        var results = Validate(request);

        Assert.Empty(results);
    }

    [Theory]
    [InlineData("VIP")]
    [InlineData("Cat1")]
    [InlineData("Cat2")]
    public void Allowed_categories_pass(string category)
    {
        var request = new PurchaseRequest { MatchId = 1, Category = category, UserId = 1, Quantity = 1 };

        Assert.Empty(Validate(request));
    }

    [Theory]
    [InlineData("vip")]
    [InlineData("Cat3")]
    [InlineData("")]
    [InlineData("VVIP")]
    public void Invalid_category_fails(string category)
    {
        var request = new PurchaseRequest { MatchId = 1, Category = category, UserId = 1, Quantity = 1 };

        Assert.NotEmpty(Validate(request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_matchId_fails(int matchId)
    {
        var request = new PurchaseRequest { MatchId = matchId, Category = "VIP", UserId = 1, Quantity = 1 };

        Assert.NotEmpty(Validate(request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Quantity_out_of_range_fails(int quantity)
    {
        var request = new PurchaseRequest { MatchId = 1, Category = "VIP", UserId = 1, Quantity = quantity };

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void Non_positive_userId_fails()
    {
        var request = new PurchaseRequest { MatchId = 1, Category = "VIP", UserId = 0, Quantity = 1 };

        Assert.NotEmpty(Validate(request));
    }
}
