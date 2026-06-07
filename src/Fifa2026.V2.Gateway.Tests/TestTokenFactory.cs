using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Fifa2026.V2.Gateway.Tests;

/// <summary>
/// Story 2.3 AC-12 — fábrica de tokens JWT de teste assinados com uma chave RSA
/// conhecida (compartilhada com a validação de teste no <see cref="GatewayTestFixture"/>).
/// Permite mintar tokens válidos e tokens deliberadamente inválidos (expirado,
/// issuer errado, audience errado) sem depender do Entra real.
/// </summary>
public static class TestTokenFactory
{
    // Valores que o gateway (sob teste) espera — devem casar com os do fixture.
    public const string TenantId = "11111111-2222-3333-4444-555555555555";
    public const string ClientId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
    public static string ValidIssuer => $"https://login.microsoftonline.com/{TenantId}/v2.0";

    /// <summary>Chave RSA única do processo de teste (substitui o JWKS do Entra).</summary>
    private static readonly RsaSecurityKey SigningKey = CreateKey();

    /// <summary>SecurityKey pública usada pela validação de teste (IssuerSigningKey).</summary>
    public static SecurityKey PublicSigningKey => SigningKey;

    private static RsaSecurityKey CreateKey()
    {
        var rsa = RSA.Create(2048);
        return new RsaSecurityKey(rsa) { KeyId = "test-key-1" };
    }

    /// <summary>
    /// Gera um token. Por padrão é VÁLIDO (issuer/aud corretos, não expirado, com
    /// claim oid). Sobrescreva os parâmetros para produzir os cenários de rejeição.
    /// </summary>
    public static string Create(
        string? issuer = null,
        string? audience = null,
        DateTime? expires = null,
        string? oid = "99999999-8888-7777-6666-555555555555")
    {
        var credentials = new SigningCredentials(SigningKey, SecurityAlgorithms.RsaSha256);

        var claims = new List<Claim>
        {
            new("sub", "test-subject"),
        };
        if (!string.IsNullOrEmpty(oid))
        {
            claims.Add(new Claim("oid", oid));
        }

        var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(30);
        // notBefore no passado (token válido começa a valer já), mas SEMPRE antes do
        // expires — cobre tanto o token válido (expires futuro) quanto o expirado
        // (expires no passado, cenário AC-12).
        var earliest = DateTime.UtcNow.AddMinutes(-5);
        var notBefore = expiresAt < earliest ? expiresAt.AddMinutes(-5) : earliest;

        var token = new JwtSecurityToken(
            issuer: issuer ?? ValidIssuer,
            audience: audience ?? ClientId,
            claims: claims,
            notBefore: notBefore,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
