namespace PlantProcess.Application.Definitions;

/// <summary>
/// PPIQ T-090. Resolves the canonical tenant and owner identities a definition
/// write requires.
///
/// WHY THIS IS AN INTERFACE IN THE APPLICATION LAYER. The canonical writer
/// refuses synthesised identity, so every caller needs real ids. Callers that
/// live in the Application project cannot read them from the database
/// themselves: DbConnection and DbTransaction access comes from EF Core
/// Relational, which Application does not reference and should not, because
/// opening a connection is not an application-layer concern.
///
/// An earlier draft put raw ADO into an Application-layer helper and did not
/// compile for exactly that reason. The lookup belongs in Infrastructure; this
/// contract is how the Application layer asks for it.
///
/// UNKNOWN IDENTITY RETURNS NULL. Never a fallback GUID - a definition written
/// under an invented owner would look governed and be untraceable.
/// </summary>
public interface ICanonicalIdentityResolver
{
    /// <summary>
    /// The tenant for a tenant code, or for the single tenant when the caller
    /// carries no code. Two tenants and no code is ambiguous and returns null
    /// rather than picking one.
    /// </summary>
    Task<Guid?> ResolveTenantAsync(string? tenantCode, CancellationToken cancellationToken);

    /// <summary>
    /// The application user behind a user name, or the permanent system account
    /// for product-owned writes such as system templates.
    /// </summary>
    Task<Guid?> ResolveOwnerAsync(string? userName, CancellationToken cancellationToken);
}
