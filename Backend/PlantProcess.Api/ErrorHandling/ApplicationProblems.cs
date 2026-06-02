using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace PlantProcess.Api.ErrorHandling;

/// <summary>
/// P1-02: typed RFC-7807 problem factory. Endpoints should return these instead of
/// ad-hoc `new { error = ... }` payloads. Every problem carries a machine-readable
/// errorCode in the extensions bag and an application/problem+json content type.
/// </summary>
public static class ApplicationProblems
{
    private static IResult Build(int status, string title, string code, string detail) =>
        Results.Problem(
            detail: detail,
            statusCode: status,
            title: title,
            type: $"urn:ppiq:error:{code}",
            extensions: new Dictionary<string, object?> { ["errorCode"] = code });

    public static IResult Validation(string detail, string code = "validation")   => Build(400, "Validation failed", code, detail);
    public static IResult NotFound(string detail, string code = "not_found")       => Build(404, "Resource not found", code, detail);
    public static IResult Conflict(string detail, string code = "conflict")        => Build(409, "Conflict", code, detail);
    public static IResult Unauthorized(string detail, string code = "unauthorized")=> Build(401, "Unauthorized", code, detail);
    public static IResult Forbidden(string detail, string code = "forbidden")      => Build(403, "Forbidden", code, detail);
    public static IResult Timeout(string detail, string code = "timeout")          => Build(504, "Upstream timeout", code, detail);
    public static IResult Internal(string detail, string code = "internal")        => Build(500, "An unexpected error occurred", code, detail);
}
