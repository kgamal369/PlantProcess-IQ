using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace PlantProcess.Application.Assistant.Planning;

/// <summary>
/// T-179. WHAT THE PLANNER IS ALLOWED TO KNOW, AND WHAT IT IS ALLOWED TO SAY.
///
/// The planner answers one question: given an already-resolved intent, already-resolved
/// canonical entities, an already-resolved permission context and a declared tool
/// registry, which governed tools may and must be used?
///
/// It does not answer the user's question, execute a tool, retrieve evidence or call a
/// model. Those belong to later components, and the separation is the point: tool
/// selection has to be reproducible and auditable, which it cannot be if a model
/// participates in it.
///
/// THE REQUEST CARRIES NO QUESTION TEXT. There is deliberately no field on
/// <see cref="PlanningRequest"/> for the words the user typed. Equivalent meaning
/// cannot produce a different plan because wording changed, and the strongest way to
/// guarantee that is to make the wording unreachable rather than to promise it is
/// ignored. A test asserts that no such field exists.
/// </summary>
public enum PlanningOutcome
{
    /// <summary>A permitted tool set was selected.</summary>
    Planned = 0,

    /// <summary>Required entities or intent are genuinely ambiguous. Nothing is planned.</summary>
    ClarificationRequired = 1,

    /// <summary>No permitted declared tool can support the request. Nothing is planned.</summary>
    Unsupported = 2
}

/// <summary>
/// What a tool is entitled to claim, and what an intent requires.
///
/// Planning matches these exactly. That is what stops an association tool being planned
/// for a question about cause, a prediction being planned for a question about what was
/// observed, a similarity search being planned as an explanation, or a remediation
/// candidate being planned as an instruction to the plant. Preserving the claim
/// downstream is later work; refusing to plan the wrong class is this task's job.
/// </summary>
public enum ClaimClass
{
    ObservedFact = 0,
    Association = 1,
    CausalEffect = 2,
    Prediction = 3,
    Similarity = 4,
    Novelty = 5,
    RemediationCandidate = 6
}

/// <summary>Which engine layer a tool belongs to.</summary>
public enum ToolLayer
{
    /// <summary>Structured aggregation over governed data. Exact by construction.</summary>
    LayerA = 0,

    /// <summary>Learned or statistical intelligence. Never exact.</summary>
    LayerB = 1
}

/// <summary>
/// Whether a tool returns the value or an estimate of it.
///
/// An exact question answered by an estimate is wrong even when the estimate is close,
/// because the caller asked for the number and was given an opinion about it.
/// </summary>
public enum ToolExactness
{
    Exact = 0,
    Approximate = 1
}

/// <summary>One declared, governed tool. The planner never invents one.</summary>
public sealed record DeclaredTool(
    string ToolId,
    ToolLayer Layer,
    ToolExactness Exactness,
    ClaimClass ProvidesClaim,
    ImmutableArray<string> RequiredEntityRoles)
{
    public static DeclaredTool Create(
        string toolId,
        ToolLayer layer,
        ToolExactness exactness,
        ClaimClass providesClaim,
        params string[] requiredEntityRoles)
    {
        if (string.IsNullOrWhiteSpace(toolId))
        {
            throw new ArgumentException("A declared tool must carry an identifier.", nameof(toolId));
        }

        return new DeclaredTool(
            toolId,
            layer,
            exactness,
            providesClaim,
            requiredEntityRoles.OrderBy(r => r, StringComparer.Ordinal).ToImmutableArray());
    }

    /// <summary>
    /// What makes two tools interchangeable for planning.
    ///
    /// Two registry entries that answer the same claim, in the same layer, at the same
    /// exactness, from the same entity roles are the same step. Planning one of them
    /// twice under two identifiers would produce a plan that executes the same work
    /// twice and reads as if it had corroborated itself.
    /// </summary>
    public string EquivalenceKey() =>
        string.Join(
            "|",
            Layer.ToString(),
            Exactness.ToString(),
            ProvidesClaim.ToString(),
            string.Join(",", RequiredEntityRoles));
}

/// <summary>The declared registry. Order of declaration never affects a plan.</summary>
public sealed record ToolRegistry(ImmutableArray<DeclaredTool> Tools)
{
    public static ToolRegistry Of(params DeclaredTool[] tools)
    {
        var duplicates = tools
            .GroupBy(t => t.ToolId, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        if (duplicates.Length > 0)
        {
            throw new ArgumentException(
                "A registry may not declare the same tool identifier twice: "
                    + string.Join(", ", duplicates),
                nameof(tools));
        }

        return new ToolRegistry(tools.ToImmutableArray());
    }

    /// <summary>
    /// The registry in canonical order, so a reordered declaration cannot move a plan.
    /// </summary>
    public ImmutableArray<DeclaredTool> Canonical() =>
        Tools.OrderBy(t => t.ToolId, StringComparer.Ordinal).ToImmutableArray();
}

/// <summary>
/// One canonical entity the resolver produced.
///
/// Candidates carries what the resolver could not decide between. An entity with more
/// than one candidate is ambiguous and is reported as such; the planner never takes the
/// first one.
/// </summary>
public sealed record ResolvedEntity(
    string Role,
    string? CanonicalId,
    ImmutableArray<string> Candidates)
{
    public static ResolvedEntity Bound(string role, string canonicalId) =>
        new(role, canonicalId, ImmutableArray<string>.Empty);

    public static ResolvedEntity Ambiguous(string role, params string[] candidates) =>
        new(
            role,
            null,
            candidates.OrderBy(c => c, StringComparer.Ordinal).ToImmutableArray());

    public static ResolvedEntity Unresolved(string role) =>
        new(role, null, ImmutableArray<string>.Empty);

    public bool IsBound => !string.IsNullOrWhiteSpace(CanonicalId);
}

/// <summary>What the resolver decided the caller is asking for.</summary>
public sealed record ResolvedIntent(
    string IntentCode,
    ClaimClass RequiredClaim,
    bool RequiresExactValue,
    ImmutableArray<string> RequiredEntityRoles)
{
    public static ResolvedIntent Create(
        string intentCode,
        ClaimClass requiredClaim,
        bool requiresExactValue,
        params string[] requiredEntityRoles)
    {
        if (string.IsNullOrWhiteSpace(intentCode))
        {
            throw new ArgumentException("A resolved intent must carry a code.", nameof(intentCode));
        }

        return new ResolvedIntent(
            intentCode,
            requiredClaim,
            requiresExactValue,
            requiredEntityRoles.OrderBy(r => r, StringComparer.Ordinal).ToImmutableArray());
    }
}

/// <summary>
/// The already-resolved permission context.
///
/// RBAC is not implemented here and is not re-derived here. The caller supplies the set
/// of tools this tenant and role may use, and the planner treats anything outside it as
/// invisible rather than as something a later layer will strip out.
/// </summary>
public sealed record PermissionContext(
    string TenantId,
    string CallerRole,
    ImmutableHashSet<string> PermittedToolIds)
{
    public static PermissionContext Of(string tenantId, string callerRole, params string[] permittedToolIds)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("A permission context must carry a tenant.", nameof(tenantId));
        }

        return new PermissionContext(
            tenantId,
            callerRole,
            permittedToolIds.ToImmutableHashSet(StringComparer.Ordinal));
    }

    public bool Permits(string toolId) => PermittedToolIds.Contains(toolId);
}

/// <summary>Everything the planner is allowed to see. Note the absence of question text.</summary>
public sealed record PlanningRequest(
    PermissionContext Permission,
    ResolvedIntent Intent,
    ImmutableArray<ResolvedEntity> Entities,
    ToolRegistry Registry);

/// <summary>Why one declared tool was or was not planned.</summary>
public enum ToolDecisionCode
{
    Selected = 0,
    OmittedNotPermitted = 1,
    OmittedClaimMismatch = 2,
    OmittedApproximateForExactValue = 3,
    OmittedMissingRequiredEntity = 4,
    OmittedEquivalentAlreadySelected = 5
}

/// <summary>One line of the audit: a tool, a verdict and a sentence.</summary>
public sealed record ToolDecision(string ToolId, ToolDecisionCode Code, string Reason)
{
    public bool Selected => Code == ToolDecisionCode.Selected;
}

/// <summary>Which entity roles the caller must disambiguate before anything can be planned.</summary>
public sealed record ClarificationRequirement(
    string Role,
    ImmutableArray<string> Candidates,
    string Reason);

/// <summary>
/// The plan. Auditable by construction: every declared tool appears with a verdict, and
/// the selected ones appear in a canonical order that does not depend on registry order.
///
/// It carries no evidence, no retrieved text and no answer. Those belong to T-180 and
/// beyond.
/// </summary>
public sealed record ToolPlan(
    PlanningOutcome Outcome,
    ImmutableArray<string> SelectedToolIds,
    ResolvedIntent Intent,
    ImmutableArray<ResolvedEntity> Entities,
    string TenantId,
    string CallerRole,
    ImmutableArray<ToolDecision> Decisions,
    ImmutableArray<ClarificationRequirement> Clarifications,
    string Reason)
{
    /// <summary>
    /// A canonical fingerprint of the decision, for proving that two resolutions of
    /// equivalent meaning produced the same plan.
    /// </summary>
    public string PlanFingerprint()
    {
        var builder = new StringBuilder();
        builder.Append("ppiq.assistant.plan/1|");
        builder.Append(Outcome).Append('|');
        builder.Append(TenantId).Append('|');
        builder.Append(CallerRole).Append('|');
        builder.Append(Intent.IntentCode).Append('|');
        builder.Append(Intent.RequiredClaim).Append('|');
        builder.Append(Intent.RequiresExactValue).Append('|');
        builder.Append(string.Join(",", SelectedToolIds)).Append('|');

        foreach (var entity in Entities.OrderBy(e => e.Role, StringComparer.Ordinal))
        {
            builder.Append(entity.Role).Append('=').Append(entity.CanonicalId ?? "?").Append(';');
        }

        builder.Append('|');
        foreach (var clarification in Clarifications)
        {
            builder.Append(clarification.Role).Append('?');
        }

        return builder.ToString();
    }
}
