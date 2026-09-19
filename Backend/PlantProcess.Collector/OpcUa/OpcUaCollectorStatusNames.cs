using System.Globalization;
using System.Reflection;
using Opc.Ua;

namespace PlantProcess.Collector.OpcUa;

/// <summary>Symbolic names for OPC UA status codes, read from the stack's own constants.</summary>
internal static class OpcUaCollectorStatusNames
{
    private static readonly Lazy<IReadOnlyDictionary<uint, string>> Names = new(Build);

    internal static string Describe(uint code)
    {
        string hex = "0x" + code.ToString("X8", CultureInfo.InvariantCulture);
        return Names.Value.TryGetValue(code, out string? name) ? name + " (" + hex + ")" : hex;
    }

    internal static string? NameOf(uint code)
        => Names.Value.TryGetValue(code, out string? name) ? name : null;

    private static IReadOnlyDictionary<uint, string> Build()
    {
        var map = new Dictionary<uint, string>();
        foreach (FieldInfo field in typeof(StatusCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.IsLiteral && field.FieldType == typeof(uint))
            {
                uint value = (uint)field.GetRawConstantValue()!;
                map.TryAdd(value, field.Name);
            }
        }

        return map;
    }
}