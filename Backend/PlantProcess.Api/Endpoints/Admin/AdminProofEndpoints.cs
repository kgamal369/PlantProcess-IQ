using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PlantProcess.Api.Security;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Admin;

public static class AdminProofEndpoints
{
    public static IEndpointRouteBuilder MapAdminProofEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/admin/users")
            .WithTags("Admin - Users")
            .RequireAuthorization("PlantProcessAdmin");

        users.MapGet("/configured-summary", GetConfiguredUsersSummary)
            .WithSummary("Authorization proof endpoint for configured user roles");

        var widgets = app.MapGroup("/admin/widgets")
            .WithTags("Admin - Widgets")
            .RequireAuthorization("PlantProcessAdmin");

        widgets.MapGet("/proof", GetWidgetProofAsync)
            .WithSummary("Authorization proof endpoint for widget administration surface");

        return app;
    }

    private static IResult GetConfiguredUsersSummary(IOptions<AuthOptions> options)
    {
        var configuredUsers = options.Value.Users
            .Select(user => new
            {
                user.UserName,
                user.Role,
                user.DisplayName,
                user.IsBootstrapAdmin,
                user.ForcePasswordChangeOnFirstLogin
            })
            .OrderBy(x => x.UserName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Results.Ok(new
        {
            generatedAtUtc = DateTime.UtcNow,
            configuredUserCount = configuredUsers.Length,
            hasAdmin = configuredUsers.Any(x => string.Equals(x.Role, "Admin", StringComparison.OrdinalIgnoreCase)),
            hasDataManager = configuredUsers.Any(x => string.Equals(x.Role, "DataManager", StringComparison.OrdinalIgnoreCase)),
            users = configuredUsers
        });
    }

    private static async Task<IResult> GetWidgetProofAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var widgetCount = await dbContext.DashboardWidgetDefinitions
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var expressionEnabledCount = await dbContext.DashboardWidgetDefinitions
            .CountAsync(x => !x.IsDeleted && x.ExpressionEnabled, cancellationToken);

        return Results.Ok(new
        {
            generatedAtUtc = DateTime.UtcNow,
            widgetCount,
            expressionEnabledCount,
            message = "Widget administration route is protected by PlantProcessAdmin policy."
        });
    }
}
