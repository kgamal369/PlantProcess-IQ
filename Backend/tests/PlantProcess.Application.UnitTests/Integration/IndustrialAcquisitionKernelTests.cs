// Industrial acquisition kernels: locator grammar, field identity, raw layout,
// configuration policies and connector-truth consumption.
//
// Every test here is provider-neutral in the sense that matters: the same code
// accepts a file-column shape and an OPC node shape, and no test names a plant,
// a customer or an industry.
using System.Text.Json;
using PlantProcess.Application.Definitions;
using PlantProcess.Application.Integration.Acquisition;
using PlantProcess.Application.Jobs.Targeting;

namespace PlantProcess.Application.UnitTests.Integration;

public sealed class IndustrialAcquisitionKernelTests
{
    private static readonly Guid FieldA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FieldB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid FieldC = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Governance = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    private static FieldDeclarationRequest CsvField(
        string key = "temperature",
        string name = "Temperature",
        string type = "float64",
        string column = "temp_c",
        Guid? fieldId = null,
        string? reconcile = null) =>
        new(fieldId, key, name, type, null, "degC", new[] { "payload" },
            Json("{\"kind\":\"file_column\",\"column\":\"" + column + "\"}"), null, reconcile);

    private static FieldDeclarationRequest OpcField(
        string key = "pressure",
        string identifier = "4711",
        string type = "float32") =>
        new(null, key, "Pressure", type, null, null, new[] { "payload" },
            Json("{\"kind\":\"opc_node\",\"namespaceUri\":\"urn:example:source\",\"identifierType\":\"i\",\"identifier\":\"" + identifier + "\"}"),
            null, null);

    // ------------------------------------------------------------- locators

    [Fact]
    public void A_locator_identity_ignores_member_order_and_whitespace()
    {
        var first = SourceLocatorGrammar.Normalize("Csv", Json("{\"kind\":\"file_column\",\"column\":\"a\",\"sheet\":\"s\"}"));
        var second = SourceLocatorGrammar.Normalize("Csv", Json("{ \"sheet\" : \"s\" , \"column\":\"a\", \"kind\":\"file_column\" }"));

        Assert.True(first.IsAccepted, first.Refusal?.Detail);
        Assert.True(second.IsAccepted, second.Refusal?.Detail);
        Assert.Equal(first.Value!.Identity, second.Value!.Identity);
    }

    [Fact]
    public void An_opc_locator_keeps_namespace_uri_identity_and_defaults_the_attribute()
    {
        var accepted = SourceLocatorGrammar.Normalize("OpcUaHistorian",
            Json("{\"kind\":\"opc_node\",\"namespaceUri\":\"urn:example:source\",\"identifierType\":\"s\",\"identifier\":\"Line/Temp\"}"));

        Assert.True(accepted.IsAccepted, accepted.Refusal?.Detail);
        Assert.Contains("\"attribute\":\"Value\"", accepted.Value!.CanonicalJson);
        Assert.Contains("urn:example:source", accepted.Value.CanonicalJson);
    }

    [Fact]
    public void A_namespace_index_is_never_an_identity()
    {
        var indexMember = SourceLocatorGrammar.Normalize("OpcUaHistorian",
            Json("{\"kind\":\"opc_node\",\"namespaceIndex\":3,\"identifierType\":\"i\",\"identifier\":\"5\"}"));
        var indexAsUri = SourceLocatorGrammar.Normalize("OpcUaHistorian",
            Json("{\"kind\":\"opc_node\",\"namespaceUri\":\"3\",\"identifierType\":\"i\",\"identifier\":\"5\"}"));
        var prefixed = SourceLocatorGrammar.Normalize("OpcUaHistorian",
            Json("{\"kind\":\"opc_node\",\"namespaceUri\":\"ns=3\",\"identifierType\":\"i\",\"identifier\":\"5\"}"));

        Assert.False(indexMember.IsAccepted);
        Assert.False(indexAsUri.IsAccepted);
        Assert.False(prefixed.IsAccepted);
    }

    [Fact]
    public void A_provider_cannot_address_a_locator_family_it_has_no_access_to()
    {
        var wrong = SourceLocatorGrammar.Normalize("Csv",
            Json("{\"kind\":\"opc_node\",\"namespaceUri\":\"urn:x\",\"identifierType\":\"i\",\"identifier\":\"1\"}"));
        var unknownProvider = SourceLocatorGrammar.Normalize("NoSuchProvider", Json("{\"kind\":\"file_column\",\"column\":\"a\"}"));

        Assert.False(wrong.IsAccepted);
        Assert.False(unknownProvider.IsAccepted);
    }

    // --------------------------------------------------------------- fields

    [Fact]
    public void A_label_rename_changes_the_revision_content_but_not_the_locator_identity()
    {
        var before = FieldDeclarationKernel.Normalize("Csv", CsvField(name: "Temperature"));
        var after = FieldDeclarationKernel.Normalize("Csv", CsvField(name: "Inlet temperature"));

        Assert.True(before.IsAccepted, before.Refusal?.Detail);
        Assert.True(after.IsAccepted, after.Refusal?.Detail);
        Assert.Equal(before.Value!.Locator.Identity, after.Value!.Locator.Identity);
        Assert.NotEqual(before.Value.SemanticHash, after.Value.SemanticHash);
    }

    [Fact]
    public void An_identical_redeclaration_is_byte_identical()
    {
        var first = FieldDeclarationKernel.Normalize("Csv", CsvField());
        var second = FieldDeclarationKernel.Normalize("Csv", CsvField());

        Assert.Equal(first.Value!.SemanticHash, second.Value!.SemanticHash);
    }

    [Fact]
    public void A_reconciliation_must_name_the_identity_it_continues()
    {
        var orphan = FieldDeclarationKernel.Normalize("Csv", CsvField(reconcile: "same_field"));
        var named = FieldDeclarationKernel.Normalize("Csv", CsvField(fieldId: FieldA, reconcile: "same_field"));
        var invented = FieldDeclarationKernel.Normalize("Csv", CsvField(fieldId: FieldA, reconcile: "trust_me"));

        Assert.False(orphan.IsAccepted);
        Assert.True(named.IsAccepted, named.Refusal?.Detail);
        Assert.False(invented.IsAccepted);
    }

    [Fact]
    public void A_typed_source_item_is_never_bound_to_a_raw_layout()
    {
        var typedWithLayout = new FieldDeclarationRequest(
            null, "k", "K", "int32", null, null, new[] { "payload" },
            Json("{\"kind\":\"file_column\",\"column\":\"a\"}"), 1, null);

        Assert.False(FieldDeclarationKernel.Normalize("Csv", typedWithLayout).IsAccepted);
    }

    [Fact]
    public void A_field_declaration_refuses_an_unknown_type_role_or_key()
    {
        Assert.False(FieldDeclarationKernel.Normalize("Csv", CsvField(type: "real")).IsAccepted);
        Assert.False(FieldDeclarationKernel.Normalize("Csv", CsvField(key: "9bad")).IsAccepted);
        Assert.False(FieldDeclarationKernel.Normalize("Csv", new FieldDeclarationRequest(
            null, "k", "K", "int32", null, null, new[] { "operator" },
            Json("{\"kind\":\"file_column\",\"column\":\"a\"}"), null, null)).IsAccepted);
        Assert.False(FieldDeclarationKernel.Normalize("Csv", new FieldDeclarationRequest(
            null, "k", "K", "int32", null, null, Array.Empty<string>(),
            Json("{\"kind\":\"file_column\",\"column\":\"a\"}"), null, null)).IsAccepted);
    }

    [Fact]
    public void One_request_cannot_declare_the_same_key_or_locator_twice()
    {
        var duplicateKey = FieldDeclarationKernel.NormalizeAll("Csv", new[] { CsvField(column: "a"), CsvField(column: "b") });
        var duplicateLocator = FieldDeclarationKernel.NormalizeAll("Csv", new[] { CsvField(key: "a"), CsvField(key: "b") });

        Assert.False(duplicateKey.IsAccepted);
        Assert.False(duplicateLocator.IsAccepted);
    }

    // -------------------------------------------------------------- layouts

    private static string RawLayout(string members, int region = 16) =>
        "{\"layoutKind\":\"raw_block\",\"regionBytes\":" + region + ",\"members\":[" + members + "]}";

    private static string Member(Guid id, string type, int byteOffset, string extra = "") =>
        "{\"fieldId\":\"" + id.ToString("D") + "\",\"type\":\"" + type + "\",\"byteOffset\":" + byteOffset + extra + "}";

    [Fact]
    public void A_bounded_layout_is_accepted_and_hashes_deterministically()
    {
        var text = RawLayout(
            Member(FieldA, "int32", 0, ",\"byteOrder\":\"big\"") + "," +
            Member(FieldB, "boolean", 4, ",\"bitOffset\":3") + "," +
            Member(FieldC, "string", 5, ",\"encoding\":\"ascii\",\"length\":8"));

        var first = RawLayoutKernel.Normalize(Json(text));
        var second = RawLayoutKernel.Normalize(Json(text.Replace("\"regionBytes\":16", "\"regionBytes\":  16")));

        Assert.True(first.IsAccepted, first.Refusal?.Detail);
        Assert.Equal(3, first.Value!.Members.Count);
        Assert.Equal(first.Value.SemanticHash, second.Value!.SemanticHash);
    }

    [Fact]
    public void A_layout_refuses_overlap_out_of_range_and_impossible_coordinates()
    {
        var overlap = RawLayoutKernel.Normalize(Json(RawLayout(
            Member(FieldA, "int32", 0, ",\"byteOrder\":\"big\"") + "," +
            Member(FieldB, "int16", 2, ",\"byteOrder\":\"big\""))));

        var outside = RawLayoutKernel.Normalize(Json(RawLayout(
            Member(FieldA, "int64", 12, ",\"byteOrder\":\"little\""))));

        var badBit = RawLayoutKernel.Normalize(Json(RawLayout(Member(FieldA, "boolean", 0, ",\"bitOffset\":9"))));
        var missingOrder = RawLayoutKernel.Normalize(Json(RawLayout(Member(FieldA, "int32", 0))));
        var orderOnByte = RawLayoutKernel.Normalize(Json(RawLayout(Member(FieldA, "int8", 0, ",\"byteOrder\":\"big\""))));
        var lengthOnNumber = RawLayoutKernel.Normalize(Json(RawLayout(Member(FieldA, "int16", 0, ",\"byteOrder\":\"big\",\"length\":2"))));
        var stringNoEncoding = RawLayoutKernel.Normalize(Json(RawLayout(Member(FieldA, "string", 0, ",\"length\":4"))));
        var unknownType = RawLayoutKernel.Normalize(Json(RawLayout(Member(FieldA, "decimal", 0))));
        var twice = RawLayoutKernel.Normalize(Json(RawLayout(
            Member(FieldA, "int8", 0) + "," + Member(FieldA, "int8", 1))));

        foreach (var refused in new[] { overlap, outside, badBit, missingOrder, orderOnByte, lengthOnNumber, stringNoEncoding, unknownType, twice })
        {
            Assert.False(refused.IsAccepted);
            Assert.Equal(AcquisitionCodes.LayoutInvalid, refused.Refusal!.Code);
        }
    }

    [Fact]
    public void An_array_member_must_fit_the_region()
    {
        var fits = RawLayoutKernel.Normalize(Json(RawLayout(
            Member(FieldA, "int16", 0, ",\"byteOrder\":\"big\",\"arrayCount\":8"))));
        var overruns = RawLayoutKernel.Normalize(Json(RawLayout(
            Member(FieldA, "int16", 0, ",\"byteOrder\":\"big\",\"arrayCount\":9"))));

        Assert.True(fits.IsAccepted, fits.Refusal?.Detail);
        Assert.False(overruns.IsAccepted);
    }

    // -------------------------------------------------------- configuration

    private static string PeriodicConfiguration(string fields, string members, string strategy = "BoundedRead") =>
        "{\"fields\":[" + fields + "],\"acquire\":{\"readStrategy\":\"" + strategy + "\"}," +
        "\"recordingGroups\":[{\"groupKey\":\"g1\",\"memberFieldIds\":[" + members + "]," +
        "\"requiredConsistency\":\"BoundedReadWindow\",\"policy\":{\"mode\":\"PERIODIC\",\"periodMs\":1000," +
        "\"phaseAnchorUtc\":\"2026-01-01T00:00:00Z\",\"capture\":\"FreshRead\",\"missedTick\":\"Gap\"}}]}";

    private static string Reference(Guid id, int revision = 1) =>
        "{\"fieldId\":\"" + id.ToString("D") + "\",\"revision\":" + revision + "}";

    private static string Quoted(Guid id) => "\"" + id.ToString("D") + "\"";

    private static IReadOnlyDictionary<Guid, AcquisitionFieldFact> Facts(
        string type = "float64", int revision = 1, string locatorKind = "file_column") =>
        new Dictionary<Guid, AcquisitionFieldFact>
        {
            [FieldA] = new(FieldA, revision, type, locatorKind),
            [FieldB] = new(FieldB, revision, "int32", locatorKind),
        };

    [Fact]
    public void A_time_policy_is_accepted_and_declares_the_operation_it_needs()
    {
        var accepted = AcquisitionConfigurationKernel.Normalize(Governance, "Csv",
            Json(PeriodicConfiguration(Reference(FieldA), Quoted(FieldA))));

        Assert.True(accepted.IsAccepted, accepted.Refusal?.Detail);
        Assert.Contains(AcquisitionCapabilityTruth.BoundedRead, accepted.Value!.RequiredOperations);
        Assert.Contains("\"expectedRatePerSecond\":\"unknown\"", accepted.Value.ContentJson);
    }

    [Fact]
    public void An_unstated_rate_stays_unknown_and_never_becomes_zero()
    {
        var accepted = AcquisitionConfigurationKernel.Normalize(Governance, "Csv",
            Json(PeriodicConfiguration(Reference(FieldA), Quoted(FieldA))));

        Assert.DoesNotContain("\"expectedRatePerSecond\":0", accepted.Value!.ContentJson);
    }

    [Fact]
    public void The_content_hash_follows_semantics_and_not_formatting()
    {
        var plain = AcquisitionConfigurationKernel.Normalize(Governance, "Csv",
            Json(PeriodicConfiguration(Reference(FieldA), Quoted(FieldA))));
        var reordered = AcquisitionConfigurationKernel.Normalize(Governance, "Csv",
            Json("{\"acquire\":{\"readStrategy\":\"BoundedRead\"},\"recordingGroups\":[{\"policy\":{\"capture\":\"FreshRead\"," +
                 "\"missedTick\":\"Gap\",\"mode\":\"PERIODIC\",\"periodMs\":1000,\"phaseAnchorUtc\":\"2026-01-01T00:00:00Z\"}," +
                 "\"requiredConsistency\":\"BoundedReadWindow\",\"memberFieldIds\":[" + Quoted(FieldA) + "],\"groupKey\":\"g1\"}]," +
                 "\"fields\":[" + Reference(FieldA) + "]}"));
        var changed = AcquisitionConfigurationKernel.Normalize(Governance, "Csv",
            Json(PeriodicConfiguration(Reference(FieldA), Quoted(FieldA)).Replace("\"periodMs\":1000", "\"periodMs\":2000")));

        Assert.Equal(plain.Value!.ContentJson, reordered.Value!.ContentJson);
        Assert.NotEqual(plain.Value.ContentJson, changed.Value!.ContentJson);
    }

    [Fact]
    public void A_stored_configuration_renormalises_to_itself()
    {
        var stored = AcquisitionConfigurationKernel.Normalize(Governance, "Csv",
            Json(PeriodicConfiguration(Reference(FieldA), Quoted(FieldA)))).Value!;

        var round = AcquisitionConfigurationKernel.FromStored(stored.ContentJson);

        Assert.True(round.IsAccepted, round.Refusal?.Detail);
        Assert.Equal(stored.ContentJson, round.Value!.ContentJson);
        Assert.Equal(Governance, round.Value.DatasetGovernanceId);
        Assert.Equal("Csv", round.Value.ProviderType);
    }

    [Fact]
    public void Configuration_content_never_carries_credential_material()
    {
        var withSecret = AcquisitionConfigurationKernel.Normalize(Governance, "Csv",
            Json(PeriodicConfiguration(Reference(FieldA), Quoted(FieldA))
                .Replace("\"acquire\":{\"readStrategy\":\"BoundedRead\"}", "\"acquire\":{\"readStrategy\":\"BoundedRead\",\"password\":\"x\"}")));

        Assert.False(withSecret.IsAccepted);
        Assert.Equal(AcquisitionCodes.SecretMaterialRefused, withSecret.Refusal!.Code);
    }

    [Fact]
    public void A_percent_band_needs_a_valid_range_and_a_numeric_field()
    {
        string OnChange(string band) =>
            "{\"fields\":[" + Reference(FieldA) + "],\"acquire\":{\"readStrategy\":\"Subscription\"}," +
            "\"recordingGroups\":[{\"groupKey\":\"g\",\"memberFieldIds\":[" + Quoted(FieldA) + "]," +
            "\"requiredConsistency\":\"IndependentObservations\",\"policy\":{\"mode\":\"ON_CHANGE\"," +
            "\"monitoredFieldIds\":[" + Quoted(FieldA) + "]," + band + ",\"comparison\":\"DurableRecordBaseline\"," +
            "\"qualityChange\":\"Record\",\"initialBaseline\":\"FirstObservation\"}}]}";

        var noRange = AcquisitionConfigurationKernel.Normalize(Governance, "OpcUaHistorian",
            Json(OnChange("\"band\":\"Percent\",\"bandValue\":2")));
        var withRange = AcquisitionConfigurationKernel.Normalize(Governance, "OpcUaHistorian",
            Json(OnChange("\"band\":\"Percent\",\"bandValue\":2,\"rangeLow\":0,\"rangeHigh\":100")));

        Assert.False(noRange.IsAccepted);
        Assert.True(withRange.IsAccepted, withRange.Refusal?.Detail);

        var onText = AcquisitionConfigurationKernel.ValidateReferences(
            withRange.Value!,
            new Dictionary<Guid, AcquisitionFieldFact> { [FieldA] = new(FieldA, 1, "string", "opc_node") },
            new Dictionary<int, AcquisitionLayoutFact>());

        Assert.NotNull(onText);
        Assert.Equal(AcquisitionCodes.ConfigurationInvalid, onText!.Code);
    }

    [Fact]
    public void A_trigger_field_cannot_be_excluded_from_acquisition_and_a_counter_states_its_semantics()
    {
        string Triggered(string trigger, string counter) =>
            "{\"fields\":[" + Reference(FieldA) + "," + Reference(FieldB) + "],\"acquire\":{\"readStrategy\":\"Subscription\"}," +
            "\"recordingGroups\":[{\"groupKey\":\"g\",\"memberFieldIds\":[" + Quoted(FieldA) + "]," +
            "\"requiredConsistency\":\"BoundedReadWindow\",\"policy\":{\"mode\":\"TRIGGERED\",\"triggerFieldId\":" + trigger +
            ",\"condition\":\"CounterAdvance\",\"captureStrategy\":\"ReadAfterTrigger\",\"startup\":\"BaselineOnly\"" + counter + "}}]}";

        var full = ",\"counter\":{\"widthBits\":16,\"increment\":1,\"reset\":\"ToZero\",\"wrap\":true,\"maxPlausibleAdvance\":50}";

        var excluded = AcquisitionConfigurationKernel.Normalize(Governance, "OpcUaHistorian",
            Json(Triggered(Quoted(FieldC), full)));
        var missingCounter = AcquisitionConfigurationKernel.Normalize(Governance, "OpcUaHistorian",
            Json(Triggered(Quoted(FieldB), string.Empty)));
        var noWrap = AcquisitionConfigurationKernel.Normalize(Governance, "OpcUaHistorian",
            Json(Triggered(Quoted(FieldB), ",\"counter\":{\"widthBits\":16,\"increment\":1,\"reset\":\"ToZero\",\"maxPlausibleAdvance\":50}")));
        var accepted = AcquisitionConfigurationKernel.Normalize(Governance, "OpcUaHistorian",
            Json(Triggered(Quoted(FieldB), full)));

        Assert.False(excluded.IsAccepted);
        Assert.False(missingCounter.IsAccepted);
        Assert.False(noWrap.IsAccepted);
        Assert.True(accepted.IsAccepted, accepted.Refusal?.Detail);

        var onFloat = AcquisitionConfigurationKernel.ValidateReferences(
            accepted.Value!,
            new Dictionary<Guid, AcquisitionFieldFact>
            {
                [FieldA] = new(FieldA, 1, "float64", "opc_node"),
                [FieldB] = new(FieldB, 1, "float64", "opc_node"),
            },
            new Dictionary<int, AcquisitionLayoutFact>());

        Assert.NotNull(onFloat);
    }

    [Fact]
    public void A_requirement_stronger_than_the_capture_strategy_is_refused()
    {
        var text = PeriodicConfiguration(Reference(FieldA), Quoted(FieldA))
            .Replace("\"requiredConsistency\":\"BoundedReadWindow\"", "\"requiredConsistency\":\"SourceLatchedRecord\"");

        var refused = AcquisitionConfigurationKernel.Normalize(Governance, "Csv", Json(text));

        Assert.False(refused.IsAccepted);
        Assert.Contains("stronger", refused.Refusal!.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_stale_field_revision_demands_revalidation()
    {
        var configuration = AcquisitionConfigurationKernel.Normalize(Governance, "Csv",
            Json(PeriodicConfiguration(Reference(FieldA), Quoted(FieldA)))).Value!;

        var current = AcquisitionConfigurationKernel.ValidateReferences(configuration, Facts(), new Dictionary<int, AcquisitionLayoutFact>());
        var stale = AcquisitionConfigurationKernel.ValidateReferences(configuration, Facts(revision: 2), new Dictionary<int, AcquisitionLayoutFact>());
        var missing = AcquisitionConfigurationKernel.ValidateReferences(
            configuration, new Dictionary<Guid, AcquisitionFieldFact>(), new Dictionary<int, AcquisitionLayoutFact>());

        Assert.Null(current);
        Assert.Equal(AcquisitionCodes.FieldRevisionStale, stale!.Code);
        Assert.Equal(AcquisitionCodes.FieldReferenceUnknown, missing!.Code);
    }

    [Fact]
    public void A_raw_member_needs_the_exact_layout_revision_that_places_it()
    {
        var configuration = AcquisitionConfigurationKernel.Normalize(Governance, "OpcUaHistorian",
            Json(PeriodicConfiguration(Reference(FieldA), Quoted(FieldA), "Subscription"))).Value!;

        var withLayout = AcquisitionConfigurationKernel.Normalize(Governance, "OpcUaHistorian",
            Json(PeriodicConfiguration(Reference(FieldA), Quoted(FieldA), "Subscription")
                .Replace("{\"fields\"", "{\"layoutRevision\":1,\"fields\""))).Value!;

        var raw = new Dictionary<Guid, AcquisitionFieldFact> { [FieldA] = new(FieldA, 1, "float64", "raw_member") };

        var noLayout = AcquisitionConfigurationKernel.ValidateReferences(configuration, raw, new Dictionary<int, AcquisitionLayoutFact>());
        var wrongLayout = AcquisitionConfigurationKernel.ValidateReferences(
            withLayout, raw, new Dictionary<int, AcquisitionLayoutFact> { [1] = new(1, new[] { FieldB }) });
        var placed = AcquisitionConfigurationKernel.ValidateReferences(
            withLayout, raw, new Dictionary<int, AcquisitionLayoutFact> { [1] = new(1, new[] { FieldA }) });

        Assert.Equal(AcquisitionCodes.LayoutReferenceInvalid, noLayout!.Code);
        Assert.Equal(AcquisitionCodes.LayoutReferenceInvalid, wrongLayout!.Code);
        Assert.Null(placed);
    }

    [Fact]
    public void The_same_implementation_accepts_two_materially_different_source_shapes()
    {
        var file = FieldDeclarationKernel.Normalize("Csv", CsvField());
        var opc = FieldDeclarationKernel.Normalize("OpcUaHistorian", OpcField());

        var fileConfiguration = AcquisitionConfigurationKernel.Normalize(Governance, "Csv",
            Json(PeriodicConfiguration(Reference(FieldA), Quoted(FieldA))));
        var opcConfiguration = AcquisitionConfigurationKernel.Normalize(Governance, "OpcUaHistorian",
            Json(PeriodicConfiguration(Reference(FieldA), Quoted(FieldA), "Subscription")));

        Assert.True(file.IsAccepted, file.Refusal?.Detail);
        Assert.True(opc.IsAccepted, opc.Refusal?.Detail);
        Assert.NotEqual(file.Value!.Locator.Identity, opc.Value!.Locator.Identity);
        Assert.True(fileConfiguration.IsAccepted, fileConfiguration.Refusal?.Detail);
        Assert.True(opcConfiguration.IsAccepted, opcConfiguration.Refusal?.Detail);
        Assert.Contains(AcquisitionCapabilityTruth.Subscription, opcConfiguration.Value!.RequiredOperations);
    }

    // ---------------------------------------------------------- capability

    [Fact]
    public void Connector_truth_is_consumed_and_never_restated()
    {
        var opcSubscription = AcquisitionCapabilityTruth.Evaluate("OpcUaHistorian", AcquisitionCapabilityTruth.Subscription);
        var opcBoundedRead = AcquisitionCapabilityTruth.Evaluate("OpcUaHistorian", AcquisitionCapabilityTruth.BoundedRead);
        var csvBoundedRead = AcquisitionCapabilityTruth.Evaluate("Csv", AcquisitionCapabilityTruth.BoundedRead);
        var csvFileArrival = AcquisitionCapabilityTruth.Evaluate("Csv", AcquisitionCapabilityTruth.FileArrival);
        var csvLatched = AcquisitionCapabilityTruth.Evaluate("Csv", AcquisitionCapabilityTruth.SourceLatchedRecord);

        Assert.Equal(
            PlantProcess.Application.Integration.Connectors.HistorianCapabilityRegistry.IsExecutable(
                PlantProcess.Application.Integration.Connectors.HistorianCapabilityRegistry.Subscription),
            opcSubscription.DeclaredExecutable);
        Assert.Equal(
            PlantProcess.Application.Integration.Connectors.ProviderAvailability.IsAvailableNow(
                AcquisitionCapabilityTruth.HistorianProviderType),
            opcSubscription.EnvironmentAvailable);
        var unknown = AcquisitionCapabilityTruth.Evaluate("NoSuchProvider", AcquisitionCapabilityTruth.BoundedRead);

        // The adapter is exactly the conjunction of the two authorities it reads: the
        // historian registry for the operation and provider availability for this
        // environment. Asserting the conjunction rather than a captured value means a
        // later commissioning commit flips this test's expectation with it, and the
        // adapter can never be more permissive than either authority.
        Assert.Equal(RegistryAndAvailability(
            PlantProcess.Application.Integration.Connectors.HistorianCapabilityRegistry.Subscription),
            opcSubscription.Executable);
        Assert.Equal(RegistryAndAvailability(
            PlantProcess.Application.Integration.Connectors.HistorianCapabilityRegistry.BoundedRead),
            opcBoundedRead.Executable);

        // NEVER MORE PERMISSIVE THAN EITHER AUTHORITY. The inverse is not a rule and
        // must not be asserted: an operation the registry declares executable is still
        // refused where the provider is not certified in this environment, and that
        // refusal is the honest answer rather than a defect.
        foreach (var answer in new[] { opcSubscription, opcBoundedRead })
        {
            if (!answer.Executable)
            {
                continue;
            }

            Assert.True(
                PlantProcess.Application.Integration.Connectors.HistorianCapabilityRegistry.IsExecutable(
                    answer.Operation == AcquisitionCapabilityTruth.Subscription
                        ? PlantProcess.Application.Integration.Connectors.HistorianCapabilityRegistry.Subscription
                        : PlantProcess.Application.Integration.Connectors.HistorianCapabilityRegistry.BoundedRead),
                "The adapter advertised an operation the registry does not declare executable.");
            Assert.True(
                PlantProcess.Application.Integration.Connectors.ProviderAvailability.IsAvailableNow(
                    AcquisitionCapabilityTruth.HistorianProviderType),
                "The adapter advertised an operation for a provider this environment has not certified.");
        }

        // A refusal says WHICH authority refused, because "nobody implements this" and
        // "this environment has not certified the provider" call for different actions.
        if (!opcSubscription.Executable)
        {
            Assert.NotEmpty(opcSubscription.Evidence);
        }

        // Independent of any commissioning: no provider's truth declares a latched
        // source record, so a configuration needing one refuses in every environment.
        Assert.False(AcquisitionCapabilityTruth.Evaluate(
            AcquisitionCapabilityTruth.HistorianProviderType, AcquisitionCapabilityTruth.SourceLatchedRecord).Executable);

        Assert.True(csvBoundedRead.Executable);
        Assert.False(csvFileArrival.Executable);
        Assert.False(csvLatched.Executable);
        Assert.False(unknown.Executable);
        Assert.NotEmpty(csvFileArrival.Evidence);
    }


    private static bool RegistryAndAvailability(string registryCapability) =>
        PlantProcess.Application.Integration.Connectors.HistorianCapabilityRegistry.IsExecutable(registryCapability) &&
        PlantProcess.Application.Integration.Connectors.ProviderAvailability.IsAvailableNow(
            AcquisitionCapabilityTruth.HistorianProviderType);

    // ------------------------------------------------- definition authority

    [Fact]
    public void The_acquisition_configuration_kind_is_additive_and_structurally_bindable()
    {
        Assert.Equal(17, (int)DefinitionKind.AcquisitionConfiguration);

        var pinned = JobTargetReference.Pinned(DefinitionKind.AcquisitionConfiguration, FieldA, 3);
        var incoherent = JobTargetReference.Pinned(DefinitionKind.AcquisitionConfiguration, FieldA, 0);

        Assert.Null(pinned.Validate());
        Assert.Equal(DefinitionKind.AcquisitionConfiguration, pinned.Kind);
        Assert.Equal(3, pinned.PinnedVersion);
        Assert.NotNull(incoherent.Validate());
    }
}
