using PlantProcess.Application.Common.Results;

namespace PlantProcess.Application.Relationships;

/// <summary>
/// T-058. Deterministic path resolution over the PUBLISHED relationship model.
///
/// Three rules decide everything here, and all three exist because the
/// alternative is a number nobody can explain:
///
///   RL03  there is no declared path, so there is no join. Refuse; do not
///         invent one from column names that happen to match.
///   RL01  there is more than one shortest path and no preferred one. Refuse
///         and NAME every candidate, because picking one silently would make
///         the answer depend on iteration order.
///   RL02  the path runs through a relationship nobody has proven against real
///         data. Exploration may proceed; anything automated may not.
///
/// Determinism is structural, not incidental: candidates are ordered by hop
/// count and then by their relationship codes, so the same model always yields
/// the same answer and the same refusal text.
/// </summary>
public sealed class RelationshipResolver : IRelationshipResolver
{
    private readonly IRelationshipService _relationships;

    public RelationshipResolver(IRelationshipService relationships)
    {
        _relationships = relationships;
    }

    private sealed record Edge(RelationshipDto Relationship, string From, string To);

    public async Task<ApplicationResult<RelationshipResolutionDto>> ResolveAsync(
        string fromEntity, string toEntity, string purpose, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fromEntity) || string.IsNullOrWhiteSpace(toEntity))
            return Invalid("A resolution needs an entity on both sides.");

        if (string.IsNullOrWhiteSpace(purpose))
            return Invalid("A resolution needs a purpose: an unproven relationship is usable by exploration and not by anything automated, and that cannot be decided without knowing who is asking.");

        if (!RelationshipConsumerPurposes.All.Contains(purpose, StringComparer.Ordinal))
            return Invalid($"'{purpose}' is not a declared consumer purpose.");

        var published = await _relationships.GetPublishedAsync(null, cancellationToken);
        if (published.IsFailure)
            return ApplicationResult<RelationshipResolutionDto>.Failure(published.Error!);

        var from = fromEntity.Trim();
        var to = toEntity.Trim();

        if (string.Equals(from, to, StringComparison.Ordinal))
        {
            // An entity joins to itself without a relationship. Saying so is not
            // the same as claiming a path exists.
            return Ok(new RelationshipResolutionDto(
                from, to, purpose, true, Array.Empty<RelationshipPathStepDto>(),
                false, null, null, Array.Empty<string>()));
        }

        var edges = BuildEdges(published.Value!);
        var candidates = FindShortestPaths(edges, from, to);

        if (candidates.Count == 0)
        {
            return Ok(Refuse(from, to, purpose,
                RelationshipRefusalCodes.NoPath,
                $"No declared relationship path connects '{from}' to '{to}'. The model has to say how two things relate before anything may join them.",
                Array.Empty<string>()));
        }

        var names = candidates.Select(Describe).ToList();

        var chosen = candidates.Count == 1 ? candidates[0] : ChoosePreferred(candidates);

        if (chosen is null)
        {
            return Ok(Refuse(from, to, purpose,
                RelationshipRefusalCodes.AmbiguousPath,
                $"'{from}' reaches '{to}' by more than one declared path and none is marked preferred. Both are named rather than one being chosen, because the answer would otherwise depend on which was looked at first: {string.Join(" | ", names)}.",
                names));
        }

        // RL02. Exploration is manual and may traverse an unproven relationship.
        // Everything else is automated, and an automated consumer that trains,
        // scores or projects across an unproven join produces a result nobody
        // can defend.
        if (RelationshipConsumerPurposes.IsAutomated(purpose))
        {
            var unproven = chosen
                .Where(e => !string.Equals(e.Relationship.ValidationState, RelationshipValidationStates.Validated, StringComparison.Ordinal))
                .Select(e => e.Relationship.RelationshipCode)
                .OrderBy(c => c, StringComparer.Ordinal)
                .ToList();

            if (unproven.Count > 0)
            {
                return Ok(Refuse(from, to, purpose,
                    RelationshipRefusalCodes.UnprovenRelationship,
                    $"The path from '{from}' to '{to}' runs through {string.Join(", ", unproven)}, which nobody has proven against real data. '{purpose}' is automated, so it may not use it; manual exploration still may.",
                    names));
            }
        }

        var steps = chosen
            .Select(e => new RelationshipPathStepDto(e.Relationship.Id, e.From, e.To))
            .ToList();

        return Ok(new RelationshipResolutionDto(
            from, to, purpose, true, steps,
            chosen.Any(e => e.Relationship.IsGrainConverting),
            null, null, names));
    }

    /// <summary>
    /// A relationship is declared once but joins both ways, so each one becomes
    /// two traversable edges. The declaration is not duplicated; only the
    /// direction of travel differs.
    /// </summary>
    private static IReadOnlyDictionary<string, List<Edge>> BuildEdges(IReadOnlyList<RelationshipDto> published)
    {
        var byEntity = new Dictionary<string, List<Edge>>(StringComparer.Ordinal);

        void Add(string key, Edge edge)
        {
            if (!byEntity.TryGetValue(key, out var list))
            {
                list = new List<Edge>();
                byEntity[key] = list;
            }
            list.Add(edge);
        }

        foreach (var relationship in published.OrderBy(r => r.RelationshipCode, StringComparer.Ordinal))
        {
            Add(relationship.LeftEntity, new Edge(relationship, relationship.LeftEntity, relationship.RightEntity));
            Add(relationship.RightEntity, new Edge(relationship, relationship.RightEntity, relationship.LeftEntity));
        }

        return byEntity;
    }

    /// <summary>
    /// Every path of the shortest hop count, not the first one found. Finding
    /// one path and stopping is how a two-path model quietly becomes a one-path
    /// answer; RL01 cannot exist unless every candidate is collected.
    /// </summary>
    private static List<List<Edge>> FindShortestPaths(
        IReadOnlyDictionary<string, List<Edge>> edges, string from, string to)
    {
        var results = new List<List<Edge>>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { from };

        // Bounded. A plant model that needs more than six hops to relate two
        // things is telling us something other than "join these".
        const int maxHops = 6;

        var current = new List<(string Entity, List<Edge> Path)> { (from, new List<Edge>()) };

        for (var hop = 0; hop < maxHops && results.Count == 0 && current.Count > 0; hop++)
        {
            var next = new List<(string Entity, List<Edge> Path)>();
            var reachedThisHop = new HashSet<string>(StringComparer.Ordinal);

            foreach (var (entity, path) in current)
            {
                if (!edges.TryGetValue(entity, out var outgoing)) continue;

                foreach (var edge in outgoing)
                {
                    if (path.Any(e => e.Relationship.Id == edge.Relationship.Id)) continue;
                    if (visited.Contains(edge.To) && !string.Equals(edge.To, to, StringComparison.Ordinal)) continue;

                    var extended = new List<Edge>(path) { edge };

                    if (string.Equals(edge.To, to, StringComparison.Ordinal))
                    {
                        results.Add(extended);
                        continue;
                    }

                    reachedThisHop.Add(edge.To);
                    next.Add((edge.To, extended));
                }
            }

            foreach (var reached in reachedThisHop) visited.Add(reached);
            current = next;
        }

        // Ordered so the refusal text and the chosen path never depend on
        // dictionary iteration order.
        return results
            .OrderBy(p => p.Count)
            .ThenBy(Describe, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// A path is preferred only if EVERY hop on it is. One preferred hop inside
    /// an otherwise unmarked route does not make the route the intended one.
    /// </summary>
    private static List<Edge>? ChoosePreferred(List<List<Edge>> candidates)
    {
        var preferred = candidates.Where(p => p.All(e => e.Relationship.IsPreferredPath)).ToList();
        return preferred.Count == 1 ? preferred[0] : null;
    }

    private static string Describe(List<Edge> path) =>
        string.Join(" > ", path.Select(e => e.Relationship.RelationshipCode));

    private static RelationshipResolutionDto Refuse(
        string from, string to, string purpose, string code, string message, IReadOnlyList<string> candidates) =>
        new(from, to, purpose, false, Array.Empty<RelationshipPathStepDto>(), false, code, message, candidates);

    private static ApplicationResult<RelationshipResolutionDto> Ok(RelationshipResolutionDto dto) =>
        ApplicationResult<RelationshipResolutionDto>.Success(dto);

    private static ApplicationResult<RelationshipResolutionDto> Invalid(string message) =>
        ApplicationResult<RelationshipResolutionDto>.Failure(ApplicationError.Validation(message));
}