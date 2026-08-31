// ============================================================================
// Tenant reader - VARIANT A.
//
// Delegates to the repository's existing tenancy authority. This variant is
// applied when the pack finds a TenantClaims type in the API project, so that
// T-213 reads the tenant through the same authority as every other endpoint
// rather than creating a second one.
//
// The using directive below is written by the pack from the namespace of the
// file where TenantClaims was actually found; it is not assumed here.
//
// If it does not compile against the real signature, the pack restores it and
// applies VARIANT B, and the compiler - not a guess - decides which is correct.
// ============================================================================

using System;
using Microsoft.AspNetCore.Http;
using PlantProcess.Application.Security.Tenancy;

namespace PlantProcess.Api.Endpoints
{
    public sealed class CustomerAssessmentTenantReader : ICustomerAssessmentTenantReader
    {
        public bool TryResolve(HttpContext context, out Guid tenantId)
        {
            tenantId = Guid.Empty;

            if (context == null || context.User == null)
            {
                return false;
            }

            if (context.User.Identity == null || !context.User.Identity.IsAuthenticated)
            {
                return false;
            }

            Guid resolved;
            if (!TenantClaims.TryResolve(context.User, out resolved))
            {
                return false;
            }

            if (resolved == Guid.Empty)
            {
                return false;
            }

            tenantId = resolved;
            return true;
        }
    }
}
