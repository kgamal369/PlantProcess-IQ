// =====================================================================================
// Comment-stripped access to pipeline definition files.
//
// A guard that can be satisfied by a comment is not a guard. Before this type existed,
// the Jenkinsfile's own header comment contained every token CiPipelineTruthGateTests
// and DeployRedPathProofTests asserted on - so deleting the test stages left both
// suites green. Everything read through PipelineSourceText has its comments removed
// first, so an assertion can only be satisfied by executable pipeline text.
//
// Stripping rules:
//   - Groovy block comments  /* ... */
//   - Line comments beginning with //, EXCEPT when preceded by ':' (protects http://)
//   - Shell comments: any line whose first non-whitespace character is '#'
// =====================================================================================
using System.Text.RegularExpressions;

namespace PlantProcess.Architecture.Tests;

internal static class PipelineSourceText
{
    private static readonly Regex BlockComment =
        new(@"/\*[\s\S]*?\*/", RegexOptions.Compiled);

    private static readonly Regex SlashComment =
        new(@"(?<!:)//[^\r\n]*", RegexOptions.Compiled);

    private static readonly Regex HashComment =
        new(@"^[ \t]*#[^\r\n]*", RegexOptions.Compiled | RegexOptions.Multiline);

    internal static string StripComments(string text)
    {
        var stripped = BlockComment.Replace(text, string.Empty);
        stripped = SlashComment.Replace(stripped, string.Empty);
        stripped = HashComment.Replace(stripped, string.Empty);
        return stripped;
    }

    internal static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Jenkinsfile")) &&
                Directory.Exists(Path.Combine(current.FullName, "Backend")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repo root with a Jenkinsfile could not be found.");
    }

    /// <summary>Reads a repo file and removes every comment before returning it.</summary>
    internal static string Read(params string[] relativeParts)
        => StripComments(ReadRaw(relativeParts));

    /// <summary>Reads a repo file verbatim. Use only where comments are the subject.</summary>
    internal static string ReadRaw(params string[] relativeParts)
    {
        var parts = new List<string> { RepoRoot() };
        parts.AddRange(relativeParts);
        var path = Path.Combine(parts.ToArray());
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Pipeline source file not found: {path}", path);
        }

        return File.ReadAllText(path);
    }

    /// <summary>
    /// Index of a needle, asserting presence. Kept here so every caller reports the same
    /// diagnostic and nobody reintroduces a raw IndexOf against uncommented text.
    /// </summary>
    internal static int RequiredIndexOf(string haystack, string needle, string diagnostic)
    {
        var index = haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            throw new InvalidOperationException(diagnostic);
        }

        return index;
    }
}