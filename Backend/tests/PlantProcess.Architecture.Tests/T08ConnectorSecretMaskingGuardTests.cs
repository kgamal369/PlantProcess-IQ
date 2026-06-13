// PPIQ-T08 static guard. Scans connector read-back DTO sources and FAILS if a response-shaped
// type (Dto/Response/Result) declares a plaintext secret property with no masking evidence in
// the file. Write-side types (Request/Command/Input) legitimately carry the secret on the way in.
using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

[Trait("Task", "T08")]
public sealed class T08ConnectorSecretMaskingGuardTests
{
    private static readonly Regex SecretProp =
        new(@"public\s+(?:required\s+)?string\??\s+(\w*(?:Password|Secret|Credential|ApiKey|PrivateKey)\w*)\s*\{",
            RegexOptions.IgnoreCase);

    private static string BackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Backend");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Backend directory could not be located.");
    }

    private static bool IsReadBackFile(string name) =>
        name.Contains("Connector", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("ConnectionProfile", StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void Connector_readback_dtos_never_expose_an_unmasked_secret()
    {
        var root = BackendRoot();
        var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f =>
            {
                var n = Path.GetFileName(f);
                return IsReadBackFile(n) && (n.Contains("Dto") || n.Contains("Contract") || n.Contains("Response"));
            })
            .ToList();

        Assert.True(files.Count > 0, "PPIQ-T08: no connector DTO/contract files were found to scan.");

        var violations = new List<string>();
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            var masked = text.Contains("****") ||
                         text.Contains("Masked", StringComparison.OrdinalIgnoreCase) ||
                         text.Contains("[JsonIgnore]");

            foreach (Match m in SecretProp.Matches(text))
            {
                // Heuristic: only response/read shapes matter. Write shapes carry the secret inbound.
                var window = text.Substring(Math.Max(0, m.Index - 400), Math.Min(400, m.Index));
                var isWriteShape = Regex.IsMatch(window, @"(Request|Command|Input|Create|Update)\b");
                if (isWriteShape) continue;

                if (!masked)
                    violations.Add($"{Path.GetFileName(file)} :: property '{m.Groups[1].Value}' (no masking marker in file)");
            }
        }

        Assert.True(violations.Count == 0,
            "PPIQ-T08: connector read-back DTO(s) expose an unmasked secret. Mask on read-back (e.g. \"****\") " +
            "or [JsonIgnore] the property:\n  " + string.Join("\n  ", violations));
    }
}
