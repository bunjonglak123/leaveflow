using LeaveFlow.Api.Auth;
using LeaveFlow.Api.Data;
using LeaveFlow.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeaveFlow.Api.Controllers;

public record LoginRequest(string Email, string Password);
public record DemoLoginRequest(Role Role);
public record UserDto(int Id, string Name, string Email, Role Role, string Department)
{
    public static UserDto From(Employee e) => new(e.Id, e.Name, e.Email, e.Role, e.Department);
}
public record LoginResponse(string Token, UserDto User);

[ApiController]
[Route("api")]
public class AuthController(AppDbContext db, JwtTokens tokens, IConfiguration config) : ControllerBase
{
    private static readonly PasswordHasher<Employee> Hasher = new();

    [HttpPost("auth/login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req)
    {
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        var e = await db.Employees.SingleOrDefaultAsync(x => x.Email == email && x.Active);
        if (e is null || Hasher.VerifyHashedPassword(e, e.PasswordHash, req.Password ?? "") == PasswordVerificationResult.Failed)
        {
            return Problem(title: "อีเมลหรือรหัสผ่านไม่ถูกต้อง", statusCode: StatusCodes.Status401Unauthorized);
        }
        return new LoginResponse(tokens.Issue(e), UserDto.From(e));
    }

    /// <summary>One-click login for the public demo. Returns 404 unless <c>Demo:Enabled</c>.</summary>
    [HttpPost("auth/demo")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Demo(DemoLoginRequest req)
    {
        if (!config.GetValue<bool>("Demo:Enabled")) return NotFound();
        var email = req.Role switch
        {
            Role.HrAdmin => "hr@leaveflow.demo",
            Role.Manager => "manager@leaveflow.demo",
            _ => "employee@leaveflow.demo",
        };
        var e = await db.Employees.SingleOrDefaultAsync(x => x.Email == email);
        if (e is null) return NotFound();
        return new LoginResponse(tokens.Issue(e), UserDto.From(e));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me()
    {
        var e = await db.Employees.FindAsync(User.UserId());
        return e is null ? Unauthorized() : UserDto.From(e);
    }
}
