using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LeaveFlow.Domain;
using Microsoft.IdentityModel.Tokens;

namespace LeaveFlow.Api.Auth;

public class JwtTokens(IConfiguration config, TimeProvider clock)
{
    public const string Issuer = "leaveflow";
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);

    public static SymmetricSecurityKey SigningKey(IConfiguration config)
    {
        var key = config["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
            throw new InvalidOperationException("Jwt:Key must be set (at least 32 characters)");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }

    public string Issue(Employee e)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Issuer,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, e.Id.ToString()),
                new Claim(ClaimTypes.Name, e.Name),
                new Claim(ClaimTypes.Role, e.Role.ToString()),
            ],
            notBefore: now,
            expires: now + Lifetime,
            signingCredentials: new SigningCredentials(SigningKey(config), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public static class ClaimsExtensions
{
    public static int UserId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("No user id claim"));

    public static bool IsHr(this ClaimsPrincipal user) => user.IsInRole(nameof(Role.HrAdmin));
}
