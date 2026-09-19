using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using PlantProcess.Collector.Bootstrap;
using PlantProcess.Collector.Host;
using PlantProcess.Collector.OpcUa;

namespace PlantProcess.Collector;

/// <summary>
/// The collector executable boundary. It runs inside the customer network, reads one
/// published source profile version from a collector-local file, resolves the source
/// credential from the collector-local secret path, establishes or refuses an OPC UA
/// session, and shuts down cleanly.
///
/// Exit codes: 0 session established, 3 session refused, 4 configuration error.
/// </summary>
public static class Program
{
    private static readonly JsonSerializerOptions ReceiptJson = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<int> Main(string[] args)
    {
        using var stopping = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            stopping.Cancel();
        };

        ITelemetryContext telemetry = DefaultTelemetry.Create(builder =>
            builder.AddProvider(new ConsoleCollectorLoggerProvider(LogLevel.Information)));

        string? profileFile = Argument(args, "--profile-file");
        string? profileId = Argument(args, "--profile-id");
        string? profileVersion = Argument(args, "--profile-version");
        string? pkiRoot = Argument(args, "--pki-root");
        string applicationName = Argument(args, "--application-name") ?? "PlantProcess IQ Collector";
        string applicationUri = Argument(args, "--application-uri") ?? "urn:plantprocessiq:collector";
        string productUri = Argument(args, "--product-uri") ?? "uri:souindustrial.com:plantprocessiq:collector";
        string subject = Argument(args, "--certificate-subject") ?? "CN=PlantProcess IQ Collector, O=SOU Industrial Software";
        bool probeOnly = args.Contains("--probe", StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(profileFile) ||
            string.IsNullOrWhiteSpace(profileId) ||
            string.IsNullOrWhiteSpace(profileVersion) ||
            string.IsNullOrWhiteSpace(pkiRoot))
        {
            Console.WriteLine(
                "Usage: PlantProcess.Collector --profile-file <path> --profile-id <id> " +
                "--profile-version <version> --pki-root <path> [--probe]");
            return 4;
        }

        var options = new OpcUaCollectorApplicationOptions(
            applicationName,
            applicationUri,
            productUri,
            Path.GetFullPath(pkiRoot),
            subject);

        await using OpcUaCollectorHost host = await OpcUaCollectorHost
            .CreateAsync(
                options,
                new FileSourceProfileProvider(Path.GetFullPath(profileFile)),
                new EnvironmentSecretResolver(),
                profileId,
                profileVersion,
                telemetry,
                stopping.Token)
            .ConfigureAwait(false);

        OpcUaCollectorHostRunResult result = await host.StartAsync(stopping.Token).ConfigureAwait(false);
        Console.WriteLine(JsonSerializer.Serialize(result.Receipt, ReceiptJson));

        if (!result.SessionEstablished)
        {
            await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
            return 3;
        }

        if (!probeOnly)
        {
            await host.RunUntilCancelledAsync(stopping.Token).ConfigureAwait(false);
        }

        await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
        return 0;
    }

    private static string? Argument(string[] args, string name)
    {
        for (int index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.Ordinal))
            {
                return args[index + 1];
            }
        }

        return null;
    }
}