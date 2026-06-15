using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PlantProcess.Api.Security;
using PlantProcess.Application.Security.Tenancy;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Security;

public sealed class TenantAccessorTests
{
    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));

    private static HttpTenantAccessor Accessor(ClaimsPrincipal? user)
    {
        var ctx = new DefaultHttpContext();
        if (user is not null) ctx.User = user;
        return new HttpTenantAccessor(new HttpContextAccessor { HttpContext = ctx });
    }

    [SkippableFact]
    public void Returns_tenant_from_claim()
    {
        var tenant = Guid.NewGuid();
        var acc = Accessor(Principal(new Claim("tenant_id", tenant.ToString())));
        Assert.True(acc.TryGetTenantId(out var got));
        Assert.Equal(tenant, got);
        Assert.Equal(tenant, acc.TenantId);
    }

    [SkippableFact]
    public void Fails_closed_when_claim_absent()
    {
        var acc = Accessor(Principal(new Claim("role", "viewer")));
        Assert.False(acc.TryGetTenantId(out _));
        Assert.Throws<TenantResolutionException>(() => acc.TenantId);
    }

    [SkippableFact]
    public void Fails_closed_when_claim_malformed()
    {
        var acc = Accessor(Principal(new Claim("tenant_id", "not-a-guid")));
        Assert.False(acc.TryGetTenantId(out _));
        Assert.Throws<TenantResolutionException>(() => acc.TenantId);
    }

    [SkippableFact]
    public void Fails_closed_when_no_principal()
    {
        var acc = Accessor(null);
        Assert.Throws<TenantResolutionException>(() => acc.TenantId);
    }
}
