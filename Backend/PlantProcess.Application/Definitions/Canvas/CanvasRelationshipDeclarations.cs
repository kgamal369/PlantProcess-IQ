using System.Text.Json;
using System.Text.Json.Nodes;
using PlantProcess.Application.Relationships;

namespace PlantProcess.Application.Definitions.Canvas;

/// <summary>
/// READS THE RELATIONSHIPS AN IMMUTABLE VERSION DECLARES. IT NEVER INFERS ONE.
///
/// A join and a relationship are different things, and conflating them was the trap
/// here. JoinSpec carries four fields - two tables and two columns - because that is
/// all a SELECT needs. RelationshipDeclaration additionally requires a code, a join
/// type, a cardinality and a grain on each side. None of those can be recovered from
/// a join, and guessing them would emit a relationship model the author never stated.
///
/// So the rule is: a version declares relationships only where it says so explicitly.
/// A graph with joins and no declaration block is declaration-free. It publishes, it
/// emits nothing, and it retires nothing. That is not a refusal - there is nothing to
/// refuse, because nothing was declared.
///
/// A declaration that IS present and is missing a required field is a different case
/// entirely, and it is refused by name rather than completed with a default.
/// </summary>
public static class CanvasRelationshipDeclarations
{
    public const string DeclarationNode = "relationships";

    public const string FieldMissingCode = "RELATIONSHIP_DECLARATION_FIELD_MISSING";
    public const string MemberMissingCode = "RELATIONSHIP_DECLARATION_MEMBER_MISSING";

    /// <summary>
    /// Returns the declared relationships, an empty list when the version declares
    /// none, or a refusal code when a declaration is present but incomplete.
    /// </summary>
    public static IReadOnlyList<RelationshipDeclaration> Read(string? graphJson, out string? refusal)
    {
        refusal = null;
        var none = new List<RelationshipDeclaration>();

        if (string.IsNullOrWhiteSpace(graphJson)) { return none; }

        JsonNode? root;
        try { root = JsonNode.Parse(graphJson); }
        catch (JsonException) { return none; }

        JsonArray? declared = root?[DeclarationNode] as JsonArray;
        if (declared is null || declared.Count == 0) { return none; }

        var result = new List<RelationshipDeclaration>();

        foreach (JsonNode? entry in declared)
        {
            JsonObject? o = entry as JsonObject;
            if (o is null) { refusal = FieldMissingCode; return none; }

            string? code = Text(o, "relationshipCode");
            string? left = Text(o, "leftEntity");
            string? right = Text(o, "rightEntity");
            string? joinType = Text(o, "joinType");
            string? cardinality = Text(o, "cardinality");
            string? grainLeft = Text(o, "grainLeft");
            string? grainRight = Text(o, "grainRight");

            if (code is null || left is null || right is null || joinType is null
                || cardinality is null || grainLeft is null || grainRight is null)
            {
                refusal = FieldMissingCode;
                return none;
            }

            JsonArray? members = o["members"] as JsonArray;
            if (members is null || members.Count == 0) { refusal = MemberMissingCode; return none; }

            var memberList = new List<RelationshipMemberDto>();
            short order = 1;
            foreach (JsonNode? m in members)
            {
                JsonObject? mo = m as JsonObject;
                if (mo is null) { refusal = MemberMissingCode; return none; }

                string? lc = Text(mo, "leftColumn");
                string? rc = Text(mo, "rightColumn");
                if (lc is null || rc is null) { refusal = MemberMissingCode; return none; }

                string comparison = Text(mo, "comparison") ?? "=";
                short declaredOrder = order;
                if (mo["memberOrder"] is JsonValue ov && ov.TryGetValue<int>(out int parsed))
                {
                    declaredOrder = (short)parsed;
                }

                memberList.Add(new RelationshipMemberDto(lc, rc, declaredOrder, comparison));
                order++;
            }

            result.Add(new RelationshipDeclaration(
                code, left, right, joinType, cardinality, grainLeft, grainRight,
                Text(o, "attributionRule"), Text(o, "attributionExpression"),
                o["isPreferredPath"] is JsonValue pv && pv.TryGetValue<bool>(out bool pref) && pref,
                memberList));
        }

        return result;
    }

    private static string? Text(JsonObject o, string key)
    {
        JsonNode? n = o[key];
        if (n is null) { return null; }
        string? s = n.GetValue<object>()?.ToString();
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}