// Static authority gates for the published raw_block v1 decoder.
//
// These support the behavioural tests; they do not replace them. What they protect
// is that the decoder stays a consumer: one grammar authority, no storage, no
// schedule, no customer code, and no identity minted from the clock.
using System.Text.RegularExpressions;
using PlantProcess.Application.Integration.Acquisition.Decoding;
using Xunit;

namespace PlantProcess.Architecture.Tests;

[Trait("BacklogTask", "T-269")]
public sealed class IndustrialDecoderContractTests
{
    private const string DecodingDirectory = "Backend/PlantProcess.Application/Integration/Acquisition/Decoding";
    private const string SeamPath = "Backend/PlantProcess.Infrastructure/Integration/Acquisition/LayoutRevisionDecodeService.cs";

    [Fact]
    [Trait("Gate", "DECODER_SINGLE_GRAMMAR_AUTHORITY")]
    public void The_decoder_validates_through_the_layout_authority_and_declares_no_grammar_of_its_own()
    {
        var decoder = StripComments(Read(DecodingDirectory + "/RawBlockDecoder.cs"));

        Assert.Contains("RawLayoutKernel.Normalize(layoutDocument)", decoder, StringComparison.Ordinal);
        Assert.Contains("RawLayoutKernel.RawBlock", decoder, StringComparison.Ordinal);

        // A second grammar would be a second set of rules that can disagree with the
        // one T-268 froze.
        Assert.DoesNotContain("layoutKind\":\"", decoder, StringComparison.Ordinal);
        foreach (var invented in new[] { "relativeOffset", "structure", "nested", "v2" })
        {
            Assert.DoesNotContain("\"" + invented + "\"", decoder, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Gate", "DECODER_IS_PURE")]
    public void Decoding_carries_no_storage_no_schedule_and_no_customer_code()
    {
        var scanned = 0;
        var lines = 0;

        foreach (var file in Directory.EnumerateFiles(
            Path.Combine(Root(), DecodingDirectory.Replace('/', Path.DirectorySeparatorChar)), "*.cs"))
        {
            var text = File.ReadAllText(file);
            var code = StripComments(text);
            scanned++;
            lines += text.Count(c => c == '\n');

            foreach (var forbidden in new[]
            {
                "Npgsql", "DbContext", "HttpClient", "Timer", "Task.Delay", "IJobExecutor",
                "Reflection.Emit", "CSharpScript", "Assembly.Load", "Activator.CreateInstance", "Process.Start"
            })
            {
                Assert.DoesNotContain(forbidden, code, StringComparison.Ordinal);
            }

            // Identity must never be minted from the clock or from randomness.
            foreach (var forbidden in new[] { "DateTime.UtcNow", "DateTimeOffset.UtcNow", "Guid.NewGuid", "Random" })
            {
                Assert.DoesNotContain(forbidden, code, StringComparison.Ordinal);
            }
        }

        Assert.True(scanned >= 2, "only " + scanned + " decoding file(s) were opened; the scan is not covering the slice");
        Assert.True(lines > 400, "only " + lines + " lines were read; the scan is vacuous");

        // NEGATIVE CONTROL: the same comparison finds the forbidden construct when it
        // is really there, and ignores it when it is only described in a comment.
        Assert.Contains("Guid.NewGuid", StripComments("var id = Guid.NewGuid();"), StringComparison.Ordinal);
        Assert.DoesNotContain("Guid.NewGuid", StripComments("// never Guid.NewGuid here\nvar x = 1;"), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Gate", "DECODER_SEAM_EXACT_REVISION")]
    public void The_seam_resolves_one_exact_revision_inside_the_caller_scope()
    {
        var seam = StripComments(Read(SeamPath));

        Assert.Contains("ResolveGovernanceAsync(tenantId, datasetId", seam, StringComparison.Ordinal);
        Assert.Contains("ListLayoutsAsync(", seam, StringComparison.Ordinal);
        Assert.Contains("LayoutRevisionMismatch", seam, StringComparison.Ordinal);
        Assert.Contains("TypedValueGuard.Check", seam, StringComparison.Ordinal);

        // No fallback to whatever happens to be newest, and no write path.
        foreach (var forbidden in new[] { "OrderByDescending", "Last()", "INSERT", "UPDATE", "DELETE" })
        {
            Assert.DoesNotContain(forbidden, seam, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Gate", "DECODER_ATOMIC_REFUSAL")]
    public void A_refused_block_returns_no_values()
    {
        var decoder = StripComments(Read(DecodingDirectory + "/RawBlockDecoder.cs"));
        Assert.Contains("Array.Empty<DecodedValue>()", decoder, StringComparison.Ordinal);

        // Proven behaviourally as well, here on the shortest possible refusal.
        var result = RawBlockDecoder.Decode(null, 1, ReadOnlySpan<byte>.Empty);
        Assert.False(result.Accepted);
        Assert.Empty(result.Values);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(string.Empty, result.ContentDigest);
    }

    private static string Read(string relativePath)
    {
        var full = Path.Combine(Root(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(full), "Expected " + relativePath + " to exist. Without it this gate proves nothing.");
        var text = File.ReadAllText(full);
        Assert.False(string.IsNullOrWhiteSpace(text), relativePath + " is empty; the scan would be vacuous.");
        return text;
    }

    private static string StripComments(string source)
    {
        var withoutBlocks = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return string.Join('\n', withoutBlocks.Split('\n').Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
