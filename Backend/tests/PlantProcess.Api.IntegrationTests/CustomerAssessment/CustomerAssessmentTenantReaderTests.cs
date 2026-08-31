// ============================================================================
// Tenant reader tests.
//
// The endpoint layer must never invent a tenant. These tests exercise the
// resolver directly against a real HttpContext, so they hold for whichever
// reader variant the compiler selected: the delegating variant bound to the
// existing tenancy authority, or the claims variant.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PlantProcess.Api.Endpoints;
using Xunit;

namespace PlantProcess.Api.Tests.CustomerAssessment
{
    public sealed class CustomerAssessmentTenantReaderTests
    {
        private static HttpContext Anonymous()
        {
            return new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            };
        }

        private static HttpContext Authenticated(params Claim[] claims)
        {
            return new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthentication"))
            };
        }

        [Fact]
        public void An_anonymous_request_resolves_no_tenant()
        {
            var reader = new CustomerAssessmentTenantReader();

            Guid tenantId;
            bool resolved = reader.TryResolve(Anonymous(), out tenantId);

            Assert.False(resolved);
            Assert.Equal(Guid.Empty, tenantId);
        }

        [Fact]
        public void An_authenticated_request_carrying_no_tenant_claim_resolves_no_tenant()
        {
            var reader = new CustomerAssessmentTenantReader();

            Guid tenantId;
            bool resolved = reader.TryResolve(
                Authenticated(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())),
                out tenantId);

            Assert.False(resolved);
            Assert.Equal(Guid.Empty, tenantId);
        }

        [Fact]
        public void There_is_no_fallback_demo_or_system_tenant()
        {
            var reader = new CustomerAssessmentTenantReader();

            var contexts = new List<HttpContext>
            {
                Anonymous(),
                Authenticated(),
                Authenticated(new Claim("tenant_id", string.Empty)),
                Authenticated(new Claim("tenant_id", "not-a-guid")),
                Authenticated(new Claim("tenant_id", Guid.Empty.ToString()))
            };

            foreach (HttpContext context in contexts)
            {
                Guid tenantId;
                Assert.False(reader.TryResolve(context, out tenantId));
                Assert.Equal(Guid.Empty, tenantId);
            }
        }

        [Fact]
        public void A_tenant_supplied_only_in_the_request_body_is_never_the_authority()
        {
            var reader = new CustomerAssessmentTenantReader();

            HttpContext context = Anonymous();
            context.Request.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString();
            context.Request.QueryString = new QueryString("?tenantId=" + Guid.NewGuid());

            Guid tenantId;
            Assert.False(reader.TryResolve(context, out tenantId));
        }

        [Fact]
        public void A_null_context_resolves_no_tenant_rather_than_throwing_into_the_pipeline()
        {
            var reader = new CustomerAssessmentTenantReader();

            Guid tenantId;
            Assert.False(reader.TryResolve(null!, out tenantId));
        }
    }
}
