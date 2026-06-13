using System;
using System.IO;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Hardening;

// P2-T05: regression guard that the refresh-token store keeps its invalidation
// contract - validation filters revoked/expired tokens, revoke writes the marker,
// and raw tokens are persisted only as hashes. Reads the live source so the guard
// fails if a future edit removes the protection (same style as the FE contract tests).
public sealed class RefreshTokenInvalidationContractTests
{
    private static string ReadAuthStoreSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Backend", "PlantProcess.Api", "Security", "AuthStore.cs");
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = dir.Parent;
        }
        throw new FileNotFoundException("AuthStore.cs not found by climbing from " + AppContext.BaseDirectory);
    }

    [Fact]
    public void Validation_excludes_revoked_and_expired_tokens()
    {
        var src = ReadAuthStoreSource();
        Assert.Contains("revoked_at_utc IS NULL", src);
        Assert.Contains("expires_at_utc > now()", src);
    }

    [Fact]
    public void Revoke_marks_token_revoked()
    {
        var src = ReadAuthStoreSource();
        Assert.Contains("RevokeRefreshTokenAsync", src);
        Assert.Contains("revoked_at_utc", src);
    }

    [Fact]
    public void Tokens_are_persisted_hashed_not_plaintext()
    {
        var src = ReadAuthStoreSource();
        Assert.Contains("PasswordHasher.Sha256(rawToken)", src);
    }
}