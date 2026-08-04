using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using PlantProcess.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Mapping;

// T-032. THE VISUAL MAPPER SESSION LIFECYCLE, END TO END OVER HTTP.
//
// WHY THIS TEST EXISTS. On 04-Aug the authoring shell returned 500 on
// POST /api/prep/visual-mapper/sessions, and a live schema check found the
// cause: the endpoint wrote a column named session_name that exists in no
// database and in no migration, while the table requires source_code and
// display_name. A count showed ZERO sessions had ever been created in either
// ppiq_app or ppiq_presentation - the path had never worked, and no test
// covered it.
//
// This test walks the four operations the authoring shell depends on, so the
// same drift cannot return silently: create, save graph, dry run, publish.
public sealed class VisualMapperSessionLifecycleTests : AuthenticatedApiTestBase
{
    public VisualMapperSessionLifecycleTests(WebApplicationFactory<Program> factory) : base(factory) { }

    [SkippableFact]
    public async Task Create_save_dryrun_and_publish_complete_the_session_lifecycle()
    {
        Skip.IfNot(IsIntegrationDbReachable(), "Integration Postgres not reachable on this machine; runs in CI.");
        var client = await CreateAuthenticatedClientAsync();

        // 1. CREATE. This is the call that returned 500 before the fix.
        var createRes = await client.PostAsJsonAsync(
            "/api/prep/visual-mapper/sessions", new { name = "T-032 lifecycle" });
        Assert.True(createRes.IsSuccessStatusCode,
            "create session failed: " + await createRes.Content.ReadAsStringAsync());
        var sessionId = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync())
            .RootElement.GetProperty("sessionId").GetGuid();
        Assert.NotEqual(Guid.Empty, sessionId);

        // 1a. TWO SESSIONS WITH THE SAME DISPLAY NAME MUST BOTH SUCCEED. The
        // unique constraint is on (tenant_id, source_code), and the shell sends
        // the same default definition name on every page load, so a per-name
        // source_code would fail on the second visit to the canvas.
        var second = await client.PostAsJsonAsync(
            "/api/prep/visual-mapper/sessions", new { name = "T-032 lifecycle" });
        Assert.True(second.IsSuccessStatusCode,
            "a second session with the same display name was refused: "
            + await second.Content.ReadAsStringAsync());

        // 2. A REAL STAGED TABLE, so the graph is not invented.
        var dsRes = await client.GetAsync("/api/prep/visual-mapper/datasets");
        Assert.True(dsRes.IsSuccessStatusCode,
            "datasets failed: " + await dsRes.Content.ReadAsStringAsync());
        var sets = JsonDocument.Parse(await dsRes.Content.ReadAsStringAsync()).RootElement;
        Skip.If(sets.GetArrayLength() == 0, "no staged datasets on this machine");
        var table = sets[0].GetProperty("table").GetString();

        // 3. SAVE THE GRAPH. This writes draft_definition, the column 541 adds.
        var graph = new
        {
            name = "T-032 lifecycle",
            targetEntity = "MaterialUnit",
            tables = new[] { table },
            joins = Array.Empty<object>()
        };
        var saveRes = await client.PostAsJsonAsync(
            "/api/prep/visual-mapper/sessions/" + sessionId + "/graph", graph);
        Assert.True(saveRes.IsSuccessStatusCode,
            "save graph failed: " + await saveRes.Content.ReadAsStringAsync());

        // 4. DRY RUN. It reads the graph back and records the run, which is the
        // second place the schema and the code had drifted apart.
        var dryRes = await client.PostAsJsonAsync(
            "/api/prep/visual-mapper/sessions/" + sessionId + "/dry-run", new { });
        Assert.True(dryRes.IsSuccessStatusCode,
            "dry run failed: " + await dryRes.Content.ReadAsStringAsync());
        var dry = JsonDocument.Parse(await dryRes.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("succeeded", dry.GetProperty("status").GetString());

        // 5. PUBLISH. An immutable version, numbered from one.
        var pubRes = await client.PostAsJsonAsync(
            "/api/prep/visual-mapper/sessions/" + sessionId + "/publish", new { });
        Assert.True(pubRes.IsSuccessStatusCode,
            "publish failed: " + await pubRes.Content.ReadAsStringAsync());
        var pub = JsonDocument.Parse(await pubRes.Content.ReadAsStringAsync()).RootElement;
        Assert.True(pub.GetProperty("versionNumber").GetInt32() >= 1,
            "publish returned no version number");
    }

    // T-032. THE THREE-NODE CHAIN, IN A DELIBERATELY AWKWARD ORDER.
    //
    // Two-table previews always worked, which is why the join planner shipped
    // broken: with A-B and B-C the emitter named t2 inside the ON clause for
    // t1, before t2 was in the query, and PostgreSQL refused with 42P01.
    //
    // This test submits the tables as [A, C, B] - connected, but NOT in
    // connectivity order - so it fails both against the original emitter and
    // against any fix that merely walks the list. And it asserts EXECUTION,
    // not the shape of the string: a generated statement that looks plausible
    // and will not run is exactly what this defect was.
    [SkippableFact]
    public async Task Three_table_chain_previews_regardless_of_the_order_tables_arrive_in()
    {
        Skip.IfNot(IsIntegrationDbReachable(), "Integration Postgres not reachable on this machine; runs in CI.");
        var client = await CreateAuthenticatedClientAsync();

        var dsRes = await client.GetAsync("/api/prep/visual-mapper/datasets");
        Assert.True(dsRes.IsSuccessStatusCode);
        var sets = JsonDocument.Parse(await dsRes.Content.ReadAsStringAsync()).RootElement;

        // Find any A-B-C chain from the live catalogue. No plant table or column
        // name appears in this file - the chain is discovered, never assumed.
        var tables = new List<(string Name, HashSet<string> Cols)>();
        foreach (var s in sets.EnumerateArray())
        {
            var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in s.GetProperty("columns").EnumerateArray())
            {
                cols.Add(col.GetProperty("name").GetString() ?? "");
            }
            tables.Add((s.GetProperty("table").GetString() ?? "", cols));
        }

        string? a = null, b = null, c = null, keyAb = null, keyBc = null;
        foreach (var mid in tables)
        {
            foreach (var left in tables)
            {
                if (left.Name == mid.Name) { continue; }
                var k1 = left.Cols.FirstOrDefault(x => mid.Cols.Contains(x));
                if (k1 is null) { continue; }
                foreach (var right in tables)
                {
                    if (right.Name == mid.Name || right.Name == left.Name) { continue; }
                    var k2 = right.Cols.FirstOrDefault(x => mid.Cols.Contains(x));
                    if (k2 is null) { continue; }
                    a = left.Name; b = mid.Name; c = right.Name; keyAb = k1; keyBc = k2;
                    break;
                }
                if (a is not null) { break; }
            }
            if (a is not null) { break; }
        }
        Skip.If(a is null, "no three-table chain in the staged catalogue on this machine");

        var createRes = await client.PostAsJsonAsync(
            "/api/prep/visual-mapper/sessions", new { name = "T-032 three node chain" });
        Assert.True(createRes.IsSuccessStatusCode);
        var sessionId = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync())
            .RootElement.GetProperty("sessionId").GetGuid();

        // TABLES ARRIVE AS A, C, B. C is not reachable from A alone, so a
        // planner that walks the list refuses here; a frontier planner emits
        // B first and then C.
        var graph = new
        {
            name = "T-032 three node chain",
            targetEntity = "MaterialUnit",
            tables = new[] { a, c, b },
            joins = new object[]
            {
                new { leftTable = a, leftColumn = keyAb, rightTable = b, rightColumn = keyAb },
                new { leftTable = c, leftColumn = keyBc, rightTable = b, rightColumn = keyBc }
            }
        };
        var saveRes = await client.PostAsJsonAsync(
            "/api/prep/visual-mapper/sessions/" + sessionId + "/graph", graph);
        Assert.True(saveRes.IsSuccessStatusCode,
            "save graph failed: " + await saveRes.Content.ReadAsStringAsync());

        var dryRes = await client.PostAsJsonAsync(
            "/api/prep/visual-mapper/sessions/" + sessionId + "/dry-run", new { });
        Assert.True(dryRes.IsSuccessStatusCode,
            "dry run failed: " + await dryRes.Content.ReadAsStringAsync());
        var dry = JsonDocument.Parse(await dryRes.Content.ReadAsStringAsync()).RootElement;

        // THE ASSERTION IS EXECUTION. The message is carried through so a
        // failure names the PostgreSQL code rather than saying "expected true".
        var message = dry.TryGetProperty("message", out var m) ? m.GetString() : null;
        Assert.True(dry.GetProperty("status").GetString() == "succeeded",
            "three-table preview did not execute: " + (message ?? "no message"));

        // THE INVARIANT IS SCOPE, NOT ALIAS NUMBERING.
        //
        // Alias numbers come from the order tables appear in the graph; the
        // order JOINs are emitted comes from CONNECTIVITY. The planner fix
        // deliberately decouples the two, so a perfectly legal statement may
        // introduce t2 before t1 - and does, whenever the middle table is not
        // second in the list.
        //
        // An earlier version of this test asserted that " t1 ON " preceded
        // " t2 ON " and failed on correct SQL. That was a string-shape
        // assertion of exactly the kind this test exists to avoid.
        //
        // What must actually hold: every alias an ON clause references has
        // ALREADY been introduced, by the FROM clause or by an earlier JOIN.
        // That is the invariant whose breach produced 42P01, and it is checked
        // here by walking the statement rather than by matching a shape.
        var sql = dry.TryGetProperty("sql", out var s2) ? s2.GetString() ?? "" : "";
        var segments = sql.Split(new[] { " JOIN " }, StringSplitOptions.None);
        Assert.True(segments.Length == 3, "expected two JOINs in a three-table chain: " + sql);

        var introduced = new HashSet<string>(StringComparer.Ordinal);
        foreach (System.Text.RegularExpressions.Match alias  in
                 System.Text.RegularExpressions.Regex.Matches(segments[0], "\\bt[0-9]+\\b"))
        {
            introduced.Add(alias.Value);
        }

        for (var seg = 1; seg < segments.Length; seg++)
        {
            var onAt = segments[seg].IndexOf(" ON ", StringComparison.Ordinal);
            Assert.True(onAt > 0, "a JOIN without an ON clause: " + sql);

            // The alias this JOIN introduces is in scope for its own ON clause.
            var head = segments[seg].Substring(0, onAt);
            foreach (System.Text.RegularExpressions.Match alias  in
                     System.Text.RegularExpressions.Regex.Matches(head, "\\bt[0-9]+\\b"))
            {
                introduced.Add(alias.Value);
            }

            var on = segments[seg].Substring(onAt + 4);
            foreach (System.Text.RegularExpressions.Match alias in
                     System.Text.RegularExpressions.Regex.Matches(on, "\\bt[0-9]+\\b"))
            {
                Assert.True(introduced.Contains(alias.Value),
                    "the ON clause references " + alias.Value + " before it is introduced: " + sql);
            }
        }
    }
}