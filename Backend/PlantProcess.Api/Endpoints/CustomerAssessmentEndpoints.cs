// ============================================================================
// Customer assessment endpoints.
//
// Four operations and no more: Assess, Latest, Exact version, Diff. T-213 is
// not an onboarding frontend project and owns no UI.
//
// TENANCY
//   The tenant is read from authenticated request identity through
//   ICustomerAssessmentTenantReader. There is no fallback tenant, no generated
//   tenant, no demo tenant, and a client-supplied tenant is never the sole
//   authority. AssertedTenantId, when present, is a consistency assertion
//   only: a mismatch against the claim is 403 and mutates nothing.
//
// LEAK
//   A tenant that does not own a lineage receives the same response shape as a
//   tenant asking for a lineage that does not exist. Nothing in the payload
//   distinguishes the two cases.
//
// EF
//   No EF record type is named in this file. The API surface speaks only in
//   Application contracts and the DTOs below.
// ============================================================================

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using PlantProcess.Application.CustomerAssessment;

namespace PlantProcess.Api.Endpoints
{
    public interface ICustomerAssessmentTenantReader
    {
        /// <summary>
        /// Resolve the tenant from authenticated request identity. Returns
        /// false when no tenant can be resolved. There is no default.
        /// </summary>
        bool TryResolve(HttpContext context, out Guid tenantId);
    }

    public sealed class CustomerAssessmentRequest
    {
        /// <summary>
        /// Consistency assertion only. When supplied it must equal the tenant
        /// carried by the authenticated identity; it never becomes the tenant.
        /// </summary>
        public Guid? AssertedTenantId { get; set; }

        public CustomerIntake? Intake { get; set; }
    }

    public sealed class CustomerAssessmentResponse
    {
        public Guid AssessmentId { get; set; }
        public Guid AssessmentVersionId { get; set; }
        public string LineageCode { get; set; } = string.Empty;
        public int VersionNumber { get; set; }
        public string ContractVersion { get; set; } = string.Empty;
        public string RuleVersion { get; set; } = string.Empty;
        public string SemanticFingerprint { get; set; } = string.Empty;
        public bool Reused { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public CustomerAssessmentReport Report { get; set; } = new CustomerAssessmentReport();

        public static CustomerAssessmentResponse From(CustomerAssessmentVersionResult result)
        {
            return new CustomerAssessmentResponse
            {
                AssessmentId = result.AssessmentId,
                AssessmentVersionId = result.AssessmentVersionId,
                LineageCode = result.LineageCode,
                VersionNumber = result.VersionNumber,
                ContractVersion = result.ContractVersion,
                RuleVersion = result.RuleVersion,
                SemanticFingerprint = result.SemanticFingerprint,
                Reused = result.Reused,
                CreatedAtUtc = result.CreatedAtUtc,
                Report = result.Report
            };
        }
    }

    public static class CustomerAssessmentEndpoints
    {
        public const string BasePath = "/api/onboarding/assessments";

        // The tenant reader is stateless and delegates to the existing tenancy
        // authority. It is held here rather than injected, because an
        // interface-typed handler parameter that is not a registered service
        // makes ASP.NET Core infer it as a request body: two inferred bodies
        // on POST and an inferred body on GET both throw while the endpoint
        // table is built, which fails host startup - and every test sharing
        // that host then reports a disposed service provider. Nothing about
        // the reader's semantics changed; only how the handlers reach it.
        private static readonly ICustomerAssessmentTenantReader Tenants = new CustomerAssessmentTenantReader();

        public static IEndpointRouteBuilder MapCustomerAssessmentEndpoints(this IEndpointRouteBuilder routes)
        {
            RouteGroupBuilder group = routes.MapGroup(BasePath).RequireAuthorization();

            group.MapPost("/", AssessAsync);
            group.MapGet("/{lineageCode}/latest", LatestAsync);
            group.MapGet("/{lineageCode}/versions/{versionNumber:int}", VersionAsync);
            group.MapGet("/{lineageCode}/diff", DiffAsync);

            return routes;
        }

        private static async Task<IResult> AssessAsync(
            HttpContext context,
            CustomerAssessmentRequest request,
            [FromServices] ICustomerAssessmentService service,
            CancellationToken cancellationToken)
        {
            Guid tenantId;
            if (!Tenants.TryResolve(context, out tenantId) || tenantId == Guid.Empty)
            {
                return Results.Forbid();
            }

            if (request == null || request.Intake == null)
            {
                return Results.BadRequest(new { error = "intake_required" });
            }

            if (request.AssertedTenantId.HasValue && request.AssertedTenantId.Value != tenantId)
            {
                // Consistency assertion failed. Nothing is assessed and nothing
                // is written.
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            AssessmentOutcome<CustomerAssessmentVersionResult> outcome =
                await service.AssessAsync(tenantId, request.Intake, cancellationToken).ConfigureAwait(false);

            if (!outcome.Succeeded)
            {
                return Refusal(outcome.Reason);
            }

            return Results.Ok(CustomerAssessmentResponse.From(outcome.Value!));
        }

        private static async Task<IResult> LatestAsync(
            HttpContext context,
            string lineageCode,
            [FromServices] ICustomerAssessmentService service,
            CancellationToken cancellationToken)
        {
            Guid tenantId;
            if (!Tenants.TryResolve(context, out tenantId) || tenantId == Guid.Empty)
            {
                return Results.Forbid();
            }

            AssessmentOutcome<CustomerAssessmentVersionResult> outcome =
                await service.GetLatestAsync(tenantId, lineageCode, cancellationToken).ConfigureAwait(false);

            if (!outcome.Succeeded)
            {
                return Refusal(outcome.Reason);
            }

            return Results.Ok(CustomerAssessmentResponse.From(outcome.Value!));
        }

        private static async Task<IResult> VersionAsync(
            HttpContext context,
            string lineageCode,
            int versionNumber,
            [FromServices] ICustomerAssessmentService service,
            CancellationToken cancellationToken)
        {
            Guid tenantId;
            if (!Tenants.TryResolve(context, out tenantId) || tenantId == Guid.Empty)
            {
                return Results.Forbid();
            }

            AssessmentOutcome<CustomerAssessmentVersionResult> outcome =
                await service.GetVersionAsync(tenantId, lineageCode, versionNumber, cancellationToken)
                             .ConfigureAwait(false);

            if (!outcome.Succeeded)
            {
                return Refusal(outcome.Reason);
            }

            return Results.Ok(CustomerAssessmentResponse.From(outcome.Value!));
        }

        private static async Task<IResult> DiffAsync(
            HttpContext context,
            string lineageCode,
            int from,
            int to,
            [FromServices] ICustomerAssessmentService service,
            CancellationToken cancellationToken)
        {
            Guid tenantId;
            if (!Tenants.TryResolve(context, out tenantId) || tenantId == Guid.Empty)
            {
                return Results.Forbid();
            }

            AssessmentOutcome<CustomerAssessmentDiff> outcome =
                await service.GetDiffAsync(tenantId, lineageCode, from, to, cancellationToken)
                             .ConfigureAwait(false);

            if (!outcome.Succeeded)
            {
                return Refusal(outcome.Reason);
            }

            return Results.Ok(outcome.Value!);
        }

        /// <summary>
        /// One refusal shape for every not-found class. A caller cannot tell a
        /// lineage owned by another tenant from a lineage that never existed.
        /// </summary>
        private static IResult Refusal(AssessmentRefusalReason reason)
        {
            switch (reason)
            {
                case AssessmentRefusalReason.TenantNotResolved:
                case AssessmentRefusalReason.TenantMismatch:
                    return Results.StatusCode(StatusCodes.Status403Forbidden);
                case AssessmentRefusalReason.IntakeInvalid:
                    return Results.BadRequest(new { error = "intake_invalid" });
                default:
                    return Results.NotFound(new { error = "assessment_not_found" });
            }
        }
    }
}
