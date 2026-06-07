using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Fifa2026.V2.Functions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Fifa2026.V2.Functions.Functions;

/// <summary>
/// AC-3 — Entrada do fluxo de compra v2.
/// HTTP POST /api/v2/purchase → gera correlationId (GUID) → publica em `tickets-purchase`
/// via output binding declarativo → responde 202 { correlationId, status: "queued" }.
/// authLevel Anonymous em F1 (segurança entra na F2 com gateway — blueprint troubleshooting).
/// </summary>
public sealed class PurchaseEntryFunction
{
    /// <summary>
    /// Story 2.3 AC-9 / ADE-005 Inv 4 — header de identidade propagado pelo gateway
    /// YARP após validar o JWT Entra (claim `oid`). A Function confia neste header
    /// (não valida token) porque o gateway é o guardião único. O cliente nunca chama
    /// a Function diretamente (URL real não exposta — ADE-004 Inv 1/5).
    /// </summary>
    private const string EntraOidHeader = "X-Entra-OID";

    private readonly ILogger<PurchaseEntryFunction> _logger;

    public PurchaseEntryFunction(ILogger<PurchaseEntryFunction> logger)
    {
        _logger = logger;
    }

    /// <summary>Saída do binding HTTP + mensagem para o Service Bus.</summary>
    public sealed class EntryOutput
    {
        // SEM EntityPath na connection — o nome da queue vem deste atributo (blueprint troubleshooting).
        [ServiceBusOutput("tickets-purchase", Connection = "ServiceBusConnection")]
        public string? Message { get; set; }

        public IActionResult? HttpResponse { get; set; }
    }

    [Function(nameof(PurchaseEntryFunction))]
    public async Task<EntryOutput> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v2/purchase")] HttpRequest req)
    {
        PurchaseRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<PurchaseRequest>(
                req.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Body JSON inválido em POST /api/v2/purchase.");
            return new EntryOutput
            {
                HttpResponse = new BadRequestObjectResult(new { error = "JSON inválido." })
            };
        }

        if (request is null)
        {
            return new EntryOutput
            {
                HttpResponse = new BadRequestObjectResult(new { error = "Body obrigatório." })
            };
        }

        // Validação via DataAnnotations (matchId/userId positivos, category enum, quantity 1-10).
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, validationContext, validationResults, validateAllProperties: true))
        {
            var errors = validationResults.ConvertAll(r => r.ErrorMessage);
            _logger.LogWarning("Validação falhou em POST /api/v2/purchase: {Errors}", string.Join("; ", errors));
            return new EntryOutput
            {
                HttpResponse = new BadRequestObjectResult(new { error = "Validação falhou.", details = errors })
            };
        }

        var correlationId = Guid.NewGuid();

        // Story 2.3 AC-9 — lê o X-Entra-OID propagado pelo gateway (claim `oid` do
        // token validado). Ausente/inválido → null (fluxo segue sem identidade Entra;
        // entra_oid fica NULL no SQL). NÃO logamos o oid (PII de identidade — AC-12).
        Guid? entraOid = null;
        var entraOidHeader = req.Headers[EntraOidHeader].ToString();
        if (!string.IsNullOrWhiteSpace(entraOidHeader) && Guid.TryParse(entraOidHeader, out var parsedOid))
        {
            entraOid = parsedOid;
        }

        // BeginScope → App Insights captura como customDimensions.CorrelationId (ADE-000 Inv 5).
        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            _logger.LogInformation(
                "Compra v2 recebida: matchId={MatchId} category={Category} userId={UserId} quantity={Quantity} hasEntraIdentity={HasEntraIdentity}",
                request.MatchId, request.Category, request.UserId, request.Quantity, entraOid.HasValue);

            var message = new PurchaseMessage
            {
                CorrelationId = correlationId,
                MatchId = request.MatchId,
                Category = request.Category,
                UserId = request.UserId,
                Quantity = request.Quantity,
                EntraOid = entraOid
            };

            return new EntryOutput
            {
                Message = JsonSerializer.Serialize(message),
                HttpResponse = new AcceptedResult(
                    location: $"/api/v2/purchase/{correlationId}",
                    value: new { correlationId, status = "queued" })
            };
        }
    }
}
