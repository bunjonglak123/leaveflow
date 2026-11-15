using LeaveFlow.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LeaveFlow.Api;

/// <summary>Turns a broken business rule into 400 with the Thai message as the problem title.</summary>
public class DomainExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        if (ex is not DomainException) return false;
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = ctx,
            ProblemDetails = new ProblemDetails { Status = 400, Title = ex.Message },
        });
    }
}
