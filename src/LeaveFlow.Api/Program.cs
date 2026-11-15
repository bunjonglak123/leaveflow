using System.Text.Json.Serialization;
using LeaveFlow.Api;
using LeaveFlow.Api.Auth;
using LeaveFlow.Api.Data;
using LeaveFlow.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// Public demo without a configured key: use a random one (tokens just expire on restart).
// Real deployments must set Jwt:Key; startup fails otherwise.
if (string.IsNullOrWhiteSpace(config["Jwt:Key"]) && config.GetValue<bool>("Demo:Enabled"))
    config["Jwt:Key"] = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddLeaveFlowDb(config);
builder.Services.AddScoped<JwtTokens>();
builder.Services.AddScoped<LeaveFlow.Api.Services.LeaveService>();
if (config.GetValue<bool>("Demo:ResetDaily")) builder.Services.AddHostedService<DemoReset>();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddOpenApi();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = JwtTokens.Issuer,
            ValidAudience = JwtTokens.Issuer,
            IssuerSigningKey = JwtTokens.SigningKey(config),
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        };
    });
// Validate token lifetimes with the same clock that issues them.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<TimeProvider>((o, clock) =>
    {
        o.TimeProvider = clock;
        var skew = TimeSpan.FromMinutes(2);
        o.TokenValidationParameters.LifetimeValidator = (notBefore, expires, _, _) =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            return (notBefore is null || notBefore.Value <= now + skew) && (expires is null || expires.Value > now - skew);
        };
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Manager", p => p.RequireRole(nameof(Role.Manager), nameof(Role.HrAdmin)))
    .AddPolicy("Hr", p => p.RequireRole(nameof(Role.HrAdmin)));

var app = builder.Build();

app.UseExceptionHandler();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.InitialiseDatabase();
app.Run();

public partial class Program;
