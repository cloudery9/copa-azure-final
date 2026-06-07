using System.Text;
using System.Text.Json;
using Fifa2026.V2.Functions.Functions;
using Fifa2026.V2.Functions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fifa2026.V2.Functions.Tests;

/// <summary>
/// Story 2.3 AC-9 — a PurchaseEntryFunction lê o header X-Entra-OID propagado pelo
/// gateway YARP e o carrega na PurchaseMessage publicada no Service Bus (que o
/// consumer grava como entra_oid em SQL). Regressão: sem o header (fluxo anônimo /
/// alunos antigos), EntraOid fica null — não quebra o fluxo v1/v2 existente.
/// </summary>
public sealed class PurchaseEntryFunctionTests
{
    private static HttpRequest BuildRequest(object body, string? entraOidHeader = null)
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        var json = JsonSerializer.Serialize(body);
        request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        request.ContentType = "application/json";
        request.ContentLength = json.Length;
        if (entraOidHeader is not null)
        {
            request.Headers["X-Entra-OID"] = entraOidHeader;
        }
        return request;
    }

    private static PurchaseMessage? DeserializeMessage(string? message) =>
        message is null
            ? null
            : JsonSerializer.Deserialize<PurchaseMessage>(
                message,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    private static readonly object ValidBody = new { matchId = 1, category = "VIP", userId = 7, quantity = 2 };

    [Fact]
    public async Task Reads_XEntraOid_Header_Into_Message()
    {
        const string oid = "33333333-4444-5555-6666-777777777777";
        var sut = new PurchaseEntryFunction(NullLogger<PurchaseEntryFunction>.Instance);

        var output = await sut.RunAsync(BuildRequest(ValidBody, entraOidHeader: oid));

        Assert.IsType<AcceptedResult>(output.HttpResponse);
        var message = DeserializeMessage(output.Message);
        Assert.NotNull(message);
        Assert.Equal(Guid.Parse(oid), message!.EntraOid);
    }

    [Fact]
    public async Task NoHeader_Leaves_EntraOid_Null_Regression()
    {
        var sut = new PurchaseEntryFunction(NullLogger<PurchaseEntryFunction>.Instance);

        var output = await sut.RunAsync(BuildRequest(ValidBody));

        Assert.IsType<AcceptedResult>(output.HttpResponse);
        var message = DeserializeMessage(output.Message);
        Assert.NotNull(message);
        Assert.Null(message!.EntraOid);
    }

    [Fact]
    public async Task InvalidGuid_Header_Is_Ignored_EntraOid_Null()
    {
        var sut = new PurchaseEntryFunction(NullLogger<PurchaseEntryFunction>.Instance);

        var output = await sut.RunAsync(BuildRequest(ValidBody, entraOidHeader: "not-a-guid"));

        Assert.IsType<AcceptedResult>(output.HttpResponse);
        var message = DeserializeMessage(output.Message);
        Assert.NotNull(message);
        Assert.Null(message!.EntraOid);
    }
}
