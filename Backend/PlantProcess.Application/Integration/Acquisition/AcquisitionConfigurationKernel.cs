// Authored acquisition configuration: Time, Value change and Trigger/counter.
//
// The configuration is requested intent. It names stable field identities at an
// exact revision, an optional exact layout revision, the source mechanics it needs
// and the recording semantics it asks for. It never carries negotiated or measured
// runtime values, and it never carries credentials. Every refusal names the group
// or field that caused it.
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace PlantProcess.Application.Integration.Acquisition;

/// <summary>What the store currently knows about one governed field.</summary>
public sealed record AcquisitionFieldFact(Guid FieldId, int CurrentRevision, string DeclaredType, string LocatorKind);

/// <summary>What the store knows about one layout revision of the governed dataset.</summary>
public sealed record AcquisitionLayoutFact(int Revision, IReadOnlyCollection<Guid> FieldIds);

/// <summary>One referenced field at the exact revision the configuration was authored against.</summary>
public sealed record AcquisitionFieldReference(Guid FieldId, int Revision);

/// <summary>A validated configuration and the typed projections of the same content.</summary>
public sealed record NormalizedAcquisitionConfiguration(
    string ContentJson,
    Guid DatasetGovernanceId,
    string ProviderType,
    string ReadStrategy,
    IReadOnlyList<string> RequiredOperations,
    int? LayoutRevision,
    IReadOnlyList<AcquisitionFieldReference> FieldReferences,
    string FieldReferencesJson,
    string RecordingGroupsJson,
    string SourceRequirementsJson,
    string? SourceTimeReferenceJson,
    string StorageReferencesJson,
    string AcceptedRecordContractJson,
    IReadOnlyList<ParsedRecordingGroup> Groups);

/// <summary>The facts about one group that reference validation needs.</summary>
public sealed record ParsedRecordingGroup(
    string GroupKey,
    string Mode,
    IReadOnlyList<Guid> Members,
    IReadOnlyList<Guid> NumericBandFields,
    Guid? TriggerFieldId,
    string? TriggerCondition,
    Guid? EpochFieldId);

public static class AcquisitionConfigurationKernel
{
    public const string Periodic = "PERIODIC";
    public const string OnChange = "ON_CHANGE";
    public const string Triggered = "TRIGGERED";

    public static readonly IReadOnlyList<string> ReadStrategies = new[]
    {
        "BoundedRead", "Subscription", "FileArrival", "IncrementalCursor"
    };

    /// <summary>Weakest to strongest. A requirement may not exceed what the capture strategy can achieve.</summary>
    public static readonly IReadOnlyList<string> ConsistencyClasses = new[]
    {
        "IndependentObservations", "BoundedReadWindow", "TemporallyAligned", "SourceVersionVerified", "SourceLatchedRecord"
    };

    private const long MaxIntervalMs = 7L * 24 * 60 * 60 * 1000;

    private static readonly Regex GroupKeyPattern = new(@"^[A-Za-z][A-Za-z0-9_.\-]{0,99}$", RegexOptions.CultureInvariant);

    private static readonly string[] RootMembers =
        { "fields", "layoutRevision", "sourceTime", "acquire", "recordingGroups", "storage", "acceptedRecord" };

    public static AcquisitionOutcome<NormalizedAcquisitionConfiguration> Normalize(
        Guid datasetGovernanceId,
        string providerType,
        JsonElement? content)
    {
        if (datasetGovernanceId == Guid.Empty || string.IsNullOrWhiteSpace(providerType))
        {
            return Refuse("a configuration belongs to one governed dataset and its provider.");
        }

        if (content is null || content.Value.ValueKind != JsonValueKind.Object)
        {
            return Refuse("the configuration content must be a JSON object.");
        }

        var root = content.Value;
        if (AcquisitionCanonicalJson.TryFindSecretMaterial(root, out var secretPath))
        {
            return AcquisitionOutcome<NormalizedAcquisitionConfiguration>.Refuse(AcquisitionCodes.SecretMaterialRefused,
                "configuration content may not carry credential material (" + secretPath + ").");
        }

        var unknown = AcquisitionJson.UnknownMembers(root, RootMembers);
        if (unknown.Count > 0)
        {
            return Refuse("the configuration does not declare member(s): " + string.Join(", ", unknown) + ".");
        }

        // ---- fields
        if (!root.TryGetProperty("fields", out var fieldsElement) || fieldsElement.ValueKind != JsonValueKind.Array ||
            fieldsElement.GetArrayLength() == 0)
        {
            return Refuse("fields must list at least one field identity and revision.");
        }

        var references = new List<AcquisitionFieldReference>();
        var fieldSet = new HashSet<Guid>();
        foreach (var item in fieldsElement.EnumerateArray())
        {
            if (AcquisitionJson.UnknownMembers(item, new[] { "fieldId", "revision" }).Count > 0 ||
                !AcquisitionJson.TryGuid(item, "fieldId", out var fieldId) ||
                !AcquisitionJson.TryLong(item, "revision", out var revision) || revision < 1 || revision > int.MaxValue)
            {
                return Refuse("every field reference is {fieldId, revision} with a positive revision.");
            }

            if (!fieldSet.Add(fieldId))
            {
                return Refuse("field " + fieldId.ToString("D") + " is referenced twice.");
            }

            references.Add(new AcquisitionFieldReference(fieldId, (int)revision));
        }

        references.Sort((a, b) => a.FieldId.CompareTo(b.FieldId));

        // ---- layout revision
        int? layoutRevision = null;
        if (AcquisitionJson.Has(root, "layoutRevision"))
        {
            if (!AcquisitionJson.TryLong(root, "layoutRevision", out var layout) || layout < 1 || layout > int.MaxValue)
            {
                return Refuse("layoutRevision must be a positive integer.");
            }

            layoutRevision = (int)layout;
        }

        // ---- source time reference (the Source Time Authority owns its meaning)
        JsonObject? sourceTime = null;
        if (AcquisitionJson.Has(root, "sourceTime"))
        {
            var st = root.GetProperty("sourceTime");
            if (AcquisitionJson.UnknownMembers(st, new[] { "sourceKey", "signalKey" }).Count > 0 ||
                !AcquisitionJson.TryString(st, "sourceKey", out var sourceKey) || !AcquisitionJson.IsCleanText(sourceKey, 200) ||
                !AcquisitionJson.TryString(st, "signalKey", out var signalKey) || !AcquisitionJson.IsCleanText(signalKey, 200))
            {
                return Refuse("sourceTime is {sourceKey, signalKey}, each trimmed text of at most 200 characters.");
            }

            sourceTime = new JsonObject { ["signalKey"] = signalKey, ["sourceKey"] = sourceKey };
        }

        // ---- acquire (requested source mechanics)
        if (!root.TryGetProperty("acquire", out var acquire) || acquire.ValueKind != JsonValueKind.Object)
        {
            return Refuse("acquire must declare the requested read strategy.");
        }

        if (AcquisitionJson.UnknownMembers(acquire, new[] { "readStrategy", "requestedIntervalMs", "queueSize" }).Count > 0)
        {
            return Refuse("acquire declares only readStrategy, requestedIntervalMs and queueSize.");
        }

        if (!AcquisitionJson.TryString(acquire, "readStrategy", out var readStrategy) ||
            !ReadStrategies.Contains(readStrategy, StringComparer.Ordinal))
        {
            return Refuse("acquire.readStrategy must be one of " + string.Join(", ", ReadStrategies) + ".");
        }

        var acquireNode = new JsonObject { ["readStrategy"] = readStrategy };
        if (!OptionalPositive(acquire, "requestedIntervalMs", MaxIntervalMs, acquireNode, out var acquireRefusal) ||
            !OptionalPositive(acquire, "queueSize", 1_000_000, acquireNode, out acquireRefusal))
        {
            return Refuse("acquire: " + acquireRefusal);
        }

        var operations = new SortedSet<string>(StringComparer.Ordinal) { OperationOf(readStrategy) };

        // ---- recording groups
        if (!root.TryGetProperty("recordingGroups", out var groupsElement) || groupsElement.ValueKind != JsonValueKind.Array ||
            groupsElement.GetArrayLength() == 0)
        {
            return Refuse("recordingGroups must declare at least one group.");
        }

        var groupKeys = new HashSet<string>(StringComparer.Ordinal);
        var groupNodes = new List<(string Key, JsonObject Node)>();
        var parsedGroups = new List<ParsedRecordingGroup>();
        foreach (var group in groupsElement.EnumerateArray())
        {
            var parsed = ParseGroup(group, fieldSet, operations, out var node, out var refusal);
            if (parsed is null)
            {
                return Refuse(refusal!);
            }

            if (!groupKeys.Add(parsed.GroupKey))
            {
                return Refuse("group '" + parsed.GroupKey + "' is declared twice.");
            }

            groupNodes.Add((parsed.GroupKey, node!));
            parsedGroups.Add(parsed);
        }

        // ---- storage references (the retention/capacity authority owns their meaning)
        var storageNode = new JsonObject();
        if (AcquisitionJson.Has(root, "storage"))
        {
            var storage = root.GetProperty("storage");
            if (AcquisitionJson.UnknownMembers(storage, new[] { "retentionPolicyRef", "capacityProfileRef", "expectedRatePerSecond" }).Count > 0)
            {
                return Refuse("storage declares only retentionPolicyRef, capacityProfileRef and expectedRatePerSecond.");
            }

            foreach (var name in new[] { "retentionPolicyRef", "capacityProfileRef" })
            {
                if (!AcquisitionJson.Has(storage, name))
                {
                    storageNode[name] = null;
                    continue;
                }

                if (!AcquisitionJson.TryString(storage, name, out var reference) || !AcquisitionJson.IsCleanText(reference, 200))
                {
                    return Refuse("storage." + name + " must be trimmed text of at most 200 characters.");
                }

                storageNode[name] = reference;
            }

            if (storage.TryGetProperty("expectedRatePerSecond", out var rate) && rate.ValueKind != JsonValueKind.Null)
            {
                if (rate.ValueKind == JsonValueKind.String && rate.GetString() == "unknown")
                {
                    storageNode["expectedRatePerSecond"] = "unknown";
                }
                else if (rate.ValueKind == JsonValueKind.Number && rate.TryGetDecimal(out var value) && value >= 0)
                {
                    storageNode["expectedRatePerSecond"] = value;
                }
                else
                {
                    return Refuse("storage.expectedRatePerSecond must be a non-negative number or 'unknown'.");
                }
            }
        }

        if (!storageNode.ContainsKey("retentionPolicyRef")) storageNode["retentionPolicyRef"] = null;
        if (!storageNode.ContainsKey("capacityProfileRef")) storageNode["capacityProfileRef"] = null;

        // An unstated rate is unknown, never zero.
        if (!storageNode.ContainsKey("expectedRatePerSecond")) storageNode["expectedRatePerSecond"] = "unknown";

        // ---- accepted record contract
        var identityFields = new JsonArray();
        if (AcquisitionJson.Has(root, "acceptedRecord"))
        {
            var accepted = root.GetProperty("acceptedRecord");
            if (AcquisitionJson.UnknownMembers(accepted, new[] { "identityFieldIds" }).Count > 0)
            {
                return Refuse("acceptedRecord declares only identityFieldIds.");
            }

            if (AcquisitionJson.Has(accepted, "identityFieldIds"))
            {
                var ids = ReadGuidList(accepted, "identityFieldIds", out var idsRefusal);
                if (ids is null)
                {
                    return Refuse("acceptedRecord.identityFieldIds " + idsRefusal);
                }

                foreach (var id in ids)
                {
                    if (!fieldSet.Contains(id))
                    {
                        return Refuse("acceptedRecord identity field " + id.ToString("D") + " is not an acquired field.");
                    }

                    identityFields.Add(id.ToString("D"));
                }
            }
        }

        // ---- canonical content
        var fieldsNode = new JsonArray();
        foreach (var reference in references)
        {
            fieldsNode.Add(new JsonObject { ["fieldId"] = reference.FieldId.ToString("D"), ["revision"] = reference.Revision });
        }

        var groupsNode = new JsonArray();
        foreach (var group in groupNodes.OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            groupsNode.Add(group.Node);
        }

        var operationsNode = new JsonArray();
        foreach (var operation in operations) operationsNode.Add(operation);

        var requirementsNode = new JsonObject
        {
            ["acquire"] = acquireNode,
            ["requiredOperations"] = operationsNode
        };

        var acceptedNode = new JsonObject { ["identityFieldIds"] = identityFields };

        var contentNode = new JsonObject
        {
            ["acceptedRecord"] = acceptedNode,
            ["datasetGovernanceId"] = datasetGovernanceId.ToString("D"),
            ["fields"] = fieldsNode,
            ["layoutRevision"] = layoutRevision,
            ["providerType"] = providerType.Trim(),
            ["recordingGroups"] = groupsNode,
            ["sourceRequirements"] = requirementsNode,
            ["sourceTime"] = sourceTime,
            ["storage"] = storageNode,
        };

        string Canonical(JsonNode? node) =>
            node is null ? "null" : AcquisitionCanonicalJson.Canonicalize(node.ToJsonString());

        var contentJson = Canonical(contentNode);

        return AcquisitionOutcome<NormalizedAcquisitionConfiguration>.Accept(new NormalizedAcquisitionConfiguration(
            contentJson,
            datasetGovernanceId,
            providerType.Trim(),
            readStrategy,
            operations.ToList(),
            layoutRevision,
            references,
            Canonical(contentNode["fields"]),
            Canonical(contentNode["recordingGroups"]),
            Canonical(contentNode["sourceRequirements"]),
            sourceTime is null ? null : Canonical(contentNode["sourceTime"]),
            Canonical(contentNode["storage"]),
            Canonical(contentNode["acceptedRecord"]),
            parsedGroups));
    }

    /// <summary>
    /// Proves the configuration still describes the governed dataset as it stands:
    /// every field exists at the referenced revision, typed rules fit the declared
    /// types, and raw members are covered by the referenced layout revision.
    /// </summary>
    public static AcquisitionRefusal? ValidateReferences(
        NormalizedAcquisitionConfiguration configuration,
        IReadOnlyDictionary<Guid, AcquisitionFieldFact> fields,
        IReadOnlyDictionary<int, AcquisitionLayoutFact> layouts)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(layouts);

        foreach (var reference in configuration.FieldReferences)
        {
            if (!fields.TryGetValue(reference.FieldId, out var fact))
            {
                return new AcquisitionRefusal(AcquisitionCodes.FieldReferenceUnknown,
                    "field " + reference.FieldId.ToString("D") + " is not governed by this dataset.");
            }

            if (fact.CurrentRevision != reference.Revision)
            {
                return new AcquisitionRefusal(AcquisitionCodes.FieldRevisionStale,
                    "field " + reference.FieldId.ToString("D") + " is at revision " +
                    fact.CurrentRevision.ToString(CultureInfo.InvariantCulture) + " but the configuration names revision " +
                    reference.Revision.ToString(CultureInfo.InvariantCulture) + "; the configuration must be revalidated and republished.");
            }
        }

        foreach (var group in configuration.Groups)
        {
            foreach (var fieldId in group.NumericBandFields)
            {
                if (!FieldDeclarationKernel.IsNumeric(fields[fieldId].DeclaredType))
                {
                    return new AcquisitionRefusal(AcquisitionCodes.ConfigurationInvalid,
                        "group '" + group.GroupKey + "': a numeric band needs a numeric field, but " +
                        fieldId.ToString("D") + " is " + fields[fieldId].DeclaredType + ".");
                }
            }

            if (group.TriggerFieldId.HasValue)
            {
                var triggerType = fields[group.TriggerFieldId.Value].DeclaredType;
                var edge = group.TriggerCondition is "RisingEdge" or "FallingEdge";
                if (edge && triggerType != "boolean" && !FieldDeclarationKernel.IsInteger(triggerType))
                {
                    return new AcquisitionRefusal(AcquisitionCodes.ConfigurationInvalid,
                        "group '" + group.GroupKey + "': an edge trigger needs a boolean or integer field, not " + triggerType + ".");
                }

                if (group.TriggerCondition == "CounterAdvance" && !FieldDeclarationKernel.IsInteger(triggerType))
                {
                    return new AcquisitionRefusal(AcquisitionCodes.ConfigurationInvalid,
                        "group '" + group.GroupKey + "': a counter trigger needs an integer field, not " + triggerType + ".");
                }
            }

            if (group.EpochFieldId.HasValue && !FieldDeclarationKernel.IsInteger(fields[group.EpochFieldId.Value].DeclaredType))
            {
                return new AcquisitionRefusal(AcquisitionCodes.ConfigurationInvalid,
                    "group '" + group.GroupKey + "': a counter epoch needs an integer field.");
            }
        }

        var rawFields = configuration.FieldReferences
            .Where(r => fields[r.FieldId].LocatorKind == SourceLocatorGrammar.RawMember)
            .Select(r => r.FieldId)
            .ToList();

        if (configuration.LayoutRevision.HasValue)
        {
            if (!layouts.TryGetValue(configuration.LayoutRevision.Value, out var layout))
            {
                return new AcquisitionRefusal(AcquisitionCodes.LayoutReferenceInvalid,
                    "layout revision " + configuration.LayoutRevision.Value.ToString(CultureInfo.InvariantCulture) +
                    " does not exist for this dataset.");
            }

            foreach (var fieldId in rawFields)
            {
                if (!layout.FieldIds.Contains(fieldId))
                {
                    return new AcquisitionRefusal(AcquisitionCodes.LayoutReferenceInvalid,
                        "raw member " + fieldId.ToString("D") + " is not placed by layout revision " +
                        layout.Revision.ToString(CultureInfo.InvariantCulture) + ".");
                }
            }
        }
        else if (rawFields.Count > 0)
        {
            return new AcquisitionRefusal(AcquisitionCodes.LayoutReferenceInvalid,
                "raw member " + rawFields[0].ToString("D") + " needs an exact layout revision.");
        }

        return null;
    }

    /// <summary>
    /// Turns stored canonical content back into the authored shape, so a stored
    /// version is re-validated by exactly the rules that accepted it. Re-normalising
    /// the result must reproduce the stored content byte for byte.
    /// </summary>
    public static AcquisitionOutcome<NormalizedAcquisitionConfiguration> FromStored(string storedContentJson)
    {
        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(storedContentJson);
        }
        catch (JsonException)
        {
            return Refuse("the stored configuration content is not valid JSON.");
        }

        if (parsed is not JsonObject stored ||
            stored["datasetGovernanceId"]?.GetValueKind() != JsonValueKind.String ||
            stored["providerType"]?.GetValueKind() != JsonValueKind.String ||
            stored["sourceRequirements"] is not JsonObject requirements)
        {
            return Refuse("the stored configuration content is not an acquisition configuration.");
        }

        if (!Guid.TryParseExact(stored["datasetGovernanceId"]!.GetValue<string>(), "D", out var governance))
        {
            return Refuse("the stored configuration names no governed dataset.");
        }

        var provider = stored["providerType"]!.GetValue<string>();
        var authored = new JsonObject
        {
            ["acceptedRecord"] = stored["acceptedRecord"]?.DeepClone(),
            ["acquire"] = requirements["acquire"]?.DeepClone(),
            ["fields"] = stored["fields"]?.DeepClone(),
            ["layoutRevision"] = stored["layoutRevision"]?.DeepClone(),
            ["recordingGroups"] = stored["recordingGroups"]?.DeepClone(),
            ["sourceTime"] = stored["sourceTime"]?.DeepClone(),
            ["storage"] = stored["storage"]?.DeepClone(),
        };

        using var document = JsonDocument.Parse(authored.ToJsonString());
        var normalized = Normalize(governance, provider, document.RootElement.Clone());
        if (!normalized.IsAccepted)
        {
            return normalized;
        }

        if (!string.Equals(normalized.Value!.ContentJson, AcquisitionCanonicalJson.Canonicalize(storedContentJson), StringComparison.Ordinal))
        {
            return Refuse("the stored configuration does not re-normalise to itself; it was not written by this contract.");
        }

        return normalized;
    }

    public static string OperationOf(string readStrategy) => readStrategy switch
    {
        "BoundedRead" => AcquisitionCapabilityTruth.BoundedRead,
        "Subscription" => AcquisitionCapabilityTruth.Subscription,
        "FileArrival" => AcquisitionCapabilityTruth.FileArrival,
        "IncrementalCursor" => AcquisitionCapabilityTruth.IncrementalImport,
        _ => throw new ArgumentOutOfRangeException(nameof(readStrategy), readStrategy, "Unknown read strategy.")
    };

    private static ParsedRecordingGroup? ParseGroup(
        JsonElement group,
        HashSet<Guid> acquired,
        SortedSet<string> operations,
        out JsonObject? node,
        out string? refusal)
    {
        node = null;
        refusal = null;
        if (group.ValueKind != JsonValueKind.Object)
        {
            refusal = "every recording group is an object.";
            return null;
        }

        var allowedGroup = new[] { "groupKey", "memberFieldIds", "requiredConsistency", "policy" };
        var unknown = AcquisitionJson.UnknownMembers(group, allowedGroup);
        if (unknown.Count > 0)
        {
            refusal = "a recording group does not declare member(s): " + string.Join(", ", unknown) + ".";
            return null;
        }

        if (!AcquisitionJson.TryString(group, "groupKey", out var key) || !GroupKeyPattern.IsMatch(key))
        {
            refusal = "groupKey must start with a letter and use letters, digits, '_', '.' or '-' (at most 100).";
            return null;
        }

        var at = "group '" + key + "': ";
        var members = ReadGuidList(group, "memberFieldIds", out var memberRefusal);
        if (members is null || members.Count == 0)
        {
            refusal = at + "memberFieldIds " + (memberRefusal ?? "must list at least one field.");
            return null;
        }

        foreach (var member in members)
        {
            if (!acquired.Contains(member))
            {
                refusal = at + "member " + member.ToString("D") + " is not an acquired field.";
                return null;
            }
        }

        if (!AcquisitionJson.TryString(group, "requiredConsistency", out var required) ||
            !ConsistencyClasses.Contains(required, StringComparer.Ordinal))
        {
            refusal = at + "requiredConsistency must be one of " + string.Join(", ", ConsistencyClasses) + ".";
            return null;
        }

        if (!group.TryGetProperty("policy", out var policy) || policy.ValueKind != JsonValueKind.Object ||
            !AcquisitionJson.TryString(policy, "mode", out var mode))
        {
            refusal = at + "policy must declare exactly one mode.";
            return null;
        }

        var policyNode = new JsonObject { ["mode"] = mode };
        string achievable;
        var bandFields = new List<Guid>();
        Guid? triggerField = null;
        string? condition = null;
        Guid? epochField = null;

        switch (mode)
        {
            case Periodic:
                if (!ParsePeriodic(policy, policyNode, operations, out achievable, out refusal))
                {
                    refusal = at + refusal;
                    return null;
                }

                break;

            case OnChange:
                if (!ParseOnChange(policy, members, policyNode, bandFields, out achievable, out refusal))
                {
                    refusal = at + refusal;
                    return null;
                }

                break;

            case Triggered:
                if (!ParseTriggered(policy, acquired, policyNode, operations, out achievable, out triggerField, out condition, out epochField, out refusal))
                {
                    refusal = at + refusal;
                    return null;
                }

                break;

            default:
                refusal = at + "policy mode must be one of " + Periodic + ", " + OnChange + ", " + Triggered + ".";
                return null;
        }

        if (Rank(required) > Rank(achievable))
        {
            refusal = at + "required consistency " + required + " is stronger than " + achievable +
                      ", the most this capture strategy can achieve.";
            return null;
        }

        var memberNode = new JsonArray();
        foreach (var member in members.OrderBy(m => m)) memberNode.Add(member.ToString("D"));

        node = new JsonObject
        {
            ["groupKey"] = key,
            ["memberFieldIds"] = memberNode,
            ["policy"] = policyNode,
            ["requiredConsistency"] = required,
        };

        return new ParsedRecordingGroup(key, mode, members, bandFields, triggerField, condition, epochField);
    }

    private static bool ParsePeriodic(
        JsonElement policy, JsonObject node, SortedSet<string> operations, out string achievable, out string? refusal)
    {
        achievable = ConsistencyClasses[0];
        refusal = null;
        var allowed = new[] { "mode", "periodMs", "phaseAnchorUtc", "capture", "maxCacheAgeMs", "missedTick" };
        var unknown = AcquisitionJson.UnknownMembers(policy, allowed);
        if (unknown.Count > 0)
        {
            refusal = "Time policy does not declare member(s): " + string.Join(", ", unknown) + ".";
            return false;
        }

        if (!AcquisitionJson.TryLong(policy, "periodMs", out var period) || period < 1 || period > MaxIntervalMs)
        {
            refusal = "periodMs must be a positive integer of at most seven days.";
            return false;
        }

        if (!AcquisitionJson.TryString(policy, "phaseAnchorUtc", out var anchorText) ||
            !DateTimeOffset.TryParseExact(anchorText, new[] { "yyyy-MM-ddTHH:mm:ssZ", "yyyy-MM-ddTHH:mm:ss.FFFFFFFZ" },
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var anchor))
        {
            refusal = "phaseAnchorUtc must be an explicit UTC instant ending in Z; the schedule is phase anchored.";
            return false;
        }

        if (!AcquisitionJson.TryString(policy, "capture", out var capture) || (capture != "FreshRead" && capture != "ValidatedCache"))
        {
            refusal = "capture must be FreshRead or ValidatedCache.";
            return false;
        }

        if (capture == "ValidatedCache")
        {
            if (!AcquisitionJson.TryLong(policy, "maxCacheAgeMs", out var age) || age < 1 || age > MaxIntervalMs)
            {
                refusal = "a validated-cache capture declares maxCacheAgeMs.";
                return false;
            }

            node["maxCacheAgeMs"] = age;
            achievable = "TemporallyAligned";
        }
        else
        {
            if (AcquisitionJson.Has(policy, "maxCacheAgeMs"))
            {
                refusal = "a fresh read does not declare a cache age.";
                return false;
            }

            operations.Add(AcquisitionCapabilityTruth.BoundedRead);
            achievable = "BoundedReadWindow";
        }

        if (!AcquisitionJson.TryString(policy, "missedTick", out var missed) || (missed != "Gap" && missed != "RecoverFromHistory"))
        {
            refusal = "missedTick must be Gap or RecoverFromHistory; a current value is never copied into missed ticks.";
            return false;
        }

        node["capture"] = capture;
        node["missedTick"] = missed;
        node["periodMs"] = period;
        node["phaseAnchorUtc"] = anchor.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
        return true;
    }

    private static bool ParseOnChange(
        JsonElement policy, IReadOnlyList<Guid> members, JsonObject node, List<Guid> bandFields,
        out string achievable, out string? refusal)
    {
        achievable = "IndependentObservations";
        refusal = null;
        var allowed = new[]
        {
            "mode", "monitoredFieldIds", "band", "bandValue", "rangeLow", "rangeHigh", "comparison",
            "maxRecordIntervalMs", "qualityChange", "initialBaseline"
        };
        var unknown = AcquisitionJson.UnknownMembers(policy, allowed);
        if (unknown.Count > 0)
        {
            refusal = "Value-change policy does not declare member(s): " + string.Join(", ", unknown) + ".";
            return false;
        }

        var monitored = ReadGuidList(policy, "monitoredFieldIds", out var listRefusal);
        if (monitored is null || monitored.Count == 0)
        {
            refusal = "monitoredFieldIds " + (listRefusal ?? "must list at least one member.");
            return false;
        }

        foreach (var id in monitored)
        {
            if (!members.Contains(id))
            {
                refusal = "monitored field " + id.ToString("D") + " is not a member of the group.";
                return false;
            }
        }

        if (!AcquisitionJson.TryString(policy, "band", out var band) || (band != "None" && band != "Absolute" && band != "Percent"))
        {
            refusal = "band must be None, Absolute or Percent.";
            return false;
        }

        if (band == "None")
        {
            if (AcquisitionJson.Has(policy, "bandValue") || AcquisitionJson.Has(policy, "rangeLow") || AcquisitionJson.Has(policy, "rangeHigh"))
            {
                refusal = "band None declares no band value or range.";
                return false;
            }
        }
        else
        {
            if (!AcquisitionJson.TryDecimal(policy, "bandValue", out var value) || value <= 0)
            {
                refusal = "band " + band + " declares a positive bandValue.";
                return false;
            }

            node["bandValue"] = value;
            bandFields.AddRange(monitored);

            if (band == "Percent")
            {
                if (value > 100)
                {
                    refusal = "a percent band is at most 100.";
                    return false;
                }

                if (!AcquisitionJson.TryDecimal(policy, "rangeLow", out var low) ||
                    !AcquisitionJson.TryDecimal(policy, "rangeHigh", out var high) || high <= low)
                {
                    refusal = "a percent band needs a valid engineering range rangeLow < rangeHigh.";
                    return false;
                }

                node["rangeHigh"] = high;
                node["rangeLow"] = low;
            }
            else if (AcquisitionJson.Has(policy, "rangeLow") || AcquisitionJson.Has(policy, "rangeHigh"))
            {
                refusal = "an absolute band declares no range.";
                return false;
            }
        }

        if (!AcquisitionJson.TryString(policy, "comparison", out var comparison) ||
            (comparison != "SourceNotification" && comparison != "DurableRecordBaseline"))
        {
            refusal = "comparison must be SourceNotification or DurableRecordBaseline.";
            return false;
        }

        if (AcquisitionJson.Has(policy, "maxRecordIntervalMs"))
        {
            if (!AcquisitionJson.TryLong(policy, "maxRecordIntervalMs", out var maxInterval) || maxInterval < 1 || maxInterval > MaxIntervalMs)
            {
                refusal = "maxRecordIntervalMs must be a positive integer of at most seven days.";
                return false;
            }

            node["maxRecordIntervalMs"] = maxInterval;
        }

        if (!AcquisitionJson.TryString(policy, "qualityChange", out var quality) || (quality != "Record" && quality != "Ignore"))
        {
            refusal = "qualityChange must be Record or Ignore.";
            return false;
        }

        if (!AcquisitionJson.TryString(policy, "initialBaseline", out var baseline) ||
            (baseline != "FirstObservation" && baseline != "RecordFirstObservation"))
        {
            refusal = "initialBaseline must be FirstObservation or RecordFirstObservation.";
            return false;
        }

        var monitoredNode = new JsonArray();
        foreach (var id in monitored.OrderBy(m => m)) monitoredNode.Add(id.ToString("D"));
        node["band"] = band;
        node["comparison"] = comparison;
        node["initialBaseline"] = baseline;
        node["monitoredFieldIds"] = monitoredNode;
        node["qualityChange"] = quality;
        return true;
    }

    private static bool ParseTriggered(
        JsonElement policy, HashSet<Guid> acquired, JsonObject node, SortedSet<string> operations,
        out string achievable, out Guid? triggerField, out string? condition, out Guid? epochField, out string? refusal)
    {
        achievable = ConsistencyClasses[0];
        triggerField = null;
        condition = null;
        epochField = null;
        refusal = null;
        var allowed = new[]
        {
            "mode", "triggerFieldId", "condition", "equalityValue", "captureStrategy", "debounceMs",
            "holdoffMs", "startup", "counter"
        };
        var unknown = AcquisitionJson.UnknownMembers(policy, allowed);
        if (unknown.Count > 0)
        {
            refusal = "Trigger policy does not declare member(s): " + string.Join(", ", unknown) + ".";
            return false;
        }

        if (!AcquisitionJson.TryGuid(policy, "triggerFieldId", out var trigger))
        {
            refusal = "triggerFieldId is required.";
            return false;
        }

        if (!acquired.Contains(trigger))
        {
            refusal = "trigger field " + trigger.ToString("D") + " is excluded from acquisition.";
            return false;
        }

        var conditions = new[] { "Change", "RisingEdge", "FallingEdge", "Equality", "CounterAdvance" };
        if (!AcquisitionJson.TryString(policy, "condition", out var cond) || !conditions.Contains(cond, StringComparer.Ordinal))
        {
            refusal = "condition must be one of " + string.Join(", ", conditions) + ".";
            return false;
        }

        if (cond == "Equality")
        {
            if (!policy.TryGetProperty("equalityValue", out var equality) ||
                (equality.ValueKind != JsonValueKind.Number && equality.ValueKind != JsonValueKind.String &&
                 equality.ValueKind != JsonValueKind.True && equality.ValueKind != JsonValueKind.False))
            {
                refusal = "an equality trigger declares a scalar equalityValue.";
                return false;
            }

            node["equalityValue"] = JsonNode.Parse(AcquisitionCanonicalJson.Canonicalize(equality));
        }
        else if (AcquisitionJson.Has(policy, "equalityValue"))
        {
            refusal = "only an equality trigger declares equalityValue.";
            return false;
        }

        var strategies = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ReadAfterTrigger"] = "BoundedReadWindow",
            ["ValidatedCacheAtTrigger"] = "TemporallyAligned",
            ["SourceVersionVerified"] = "SourceVersionVerified",
            ["SourceLatchedRecord"] = "SourceLatchedRecord",
        };

        if (!AcquisitionJson.TryString(policy, "captureStrategy", out var strategy) || !strategies.ContainsKey(strategy))
        {
            refusal = "captureStrategy must be one of " + string.Join(", ", strategies.Keys) + ".";
            return false;
        }

        achievable = strategies[strategy];
        switch (strategy)
        {
            case "ReadAfterTrigger":
                operations.Add(AcquisitionCapabilityTruth.BoundedRead);
                break;
            case "SourceVersionVerified":
                operations.Add(AcquisitionCapabilityTruth.SourceVersionRecord);
                break;
            case "SourceLatchedRecord":
                operations.Add(AcquisitionCapabilityTruth.SourceLatchedRecord);
                break;
        }

        foreach (var name in new[] { "debounceMs", "holdoffMs" })
        {
            if (!AcquisitionJson.Has(policy, name))
            {
                continue;
            }

            if (!AcquisitionJson.TryLong(policy, name, out var ms) || ms < 0 || ms > MaxIntervalMs)
            {
                refusal = name + " must be a non-negative integer of at most seven days.";
                return false;
            }

            node[name] = ms;
        }

        if (!AcquisitionJson.TryString(policy, "startup", out var startup) || (startup != "BaselineOnly" && startup != "FireIfActive"))
        {
            refusal = "startup must be BaselineOnly or FireIfActive; a restart never invents an edge by default.";
            return false;
        }

        if (cond == "CounterAdvance")
        {
            if (!policy.TryGetProperty("counter", out var counter) || counter.ValueKind != JsonValueKind.Object)
            {
                refusal = "a counter trigger declares width, increment, reset, wrap and plausible advance.";
                return false;
            }

            var counterAllowed = new[] { "widthBits", "increment", "reset", "wrap", "epochFieldId", "maxPlausibleAdvance" };
            var counterUnknown = AcquisitionJson.UnknownMembers(counter, counterAllowed);
            if (counterUnknown.Count > 0)
            {
                refusal = "counter does not declare member(s): " + string.Join(", ", counterUnknown) + ".";
                return false;
            }

            if (!AcquisitionJson.TryLong(counter, "widthBits", out var width) || (width != 8 && width != 16 && width != 32 && width != 64))
            {
                refusal = "counter.widthBits must be 8, 16, 32 or 64.";
                return false;
            }

            if (!AcquisitionJson.TryLong(counter, "increment", out var increment) || increment < 1)
            {
                refusal = "counter.increment must be a positive integer.";
                return false;
            }

            if (!AcquisitionJson.TryString(counter, "reset", out var reset) ||
                (reset != "None" && reset != "ToZero" && reset != "EpochField"))
            {
                refusal = "counter.reset must be None, ToZero or EpochField; reset semantics are never implied.";
                return false;
            }

            if (!AcquisitionJson.TryBool(counter, "wrap", out var wrap))
            {
                refusal = "counter.wrap must be declared true or false.";
                return false;
            }

            if (!AcquisitionJson.TryLong(counter, "maxPlausibleAdvance", out var plausible) || plausible < increment)
            {
                refusal = "counter.maxPlausibleAdvance must be at least the increment.";
                return false;
            }

            var counterNode = new JsonObject
            {
                ["increment"] = increment,
                ["maxPlausibleAdvance"] = plausible,
                ["reset"] = reset,
                ["widthBits"] = width,
                ["wrap"] = wrap,
            };

            if (reset == "EpochField")
            {
                if (!AcquisitionJson.TryGuid(counter, "epochFieldId", out var epoch) || !acquired.Contains(epoch))
                {
                    refusal = "an epoch reset names an acquired epochFieldId.";
                    return false;
                }

                counterNode["epochFieldId"] = epoch.ToString("D");
                epochField = epoch;
            }
            else if (AcquisitionJson.Has(counter, "epochFieldId"))
            {
                refusal = "only an epoch reset names an epoch field.";
                return false;
            }

            node["counter"] = counterNode;
        }
        else if (AcquisitionJson.Has(policy, "counter"))
        {
            refusal = "only a counter trigger declares counter semantics.";
            return false;
        }

        node["captureStrategy"] = strategy;
        node["condition"] = cond;
        node["startup"] = startup;
        node["triggerFieldId"] = trigger.ToString("D");
        triggerField = trigger;
        condition = cond;
        return true;
    }

    private static List<Guid>? ReadGuidList(JsonElement owner, string name, out string? refusal)
    {
        refusal = null;
        if (!owner.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Array)
        {
            refusal = "must be an array of field identities.";
            return null;
        }

        var result = new List<Guid>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || !Guid.TryParseExact(item.GetString(), "D", out var id) || id == Guid.Empty)
            {
                refusal = "must contain canonical field identities only.";
                return null;
            }

            if (result.Contains(id))
            {
                refusal = "lists " + id.ToString("D") + " twice.";
                return null;
            }

            result.Add(id);
        }

        return result;
    }

    private static bool OptionalPositive(JsonElement owner, string name, long max, JsonObject node, out string? refusal)
    {
        refusal = null;
        if (!AcquisitionJson.Has(owner, name))
        {
            return true;
        }

        if (!AcquisitionJson.TryLong(owner, name, out var value) || value < 1 || value > max)
        {
            refusal = name + " must be a positive integer of at most " + max.ToString(CultureInfo.InvariantCulture) + ".";
            return false;
        }

        node[name] = value;
        return true;
    }

    private static int Rank(string consistency)
    {
        for (var i = 0; i < ConsistencyClasses.Count; i++)
        {
            if (string.Equals(ConsistencyClasses[i], consistency, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static AcquisitionOutcome<NormalizedAcquisitionConfiguration> Refuse(string detail) =>
        AcquisitionOutcome<NormalizedAcquisitionConfiguration>.Refuse(AcquisitionCodes.ConfigurationInvalid, detail);
}
