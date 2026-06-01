using System.Collections.Generic;
using PlantProcess.Analytics.Core.Primitives;
namespace PlantProcess.Analytics.Core.Tests;
internal static class Ctx
{
public static AnalysisContext Make(string ds = "canon.demo", string? unit = "units") =>
new(ds, new List<string> { "tenant=default-demo" }, "last_30_days", new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero), unit);
public static double?[] N(params double[] values)
{
    var a = new double?[values.Length];
    for (int i = 0; i < values.Length; i++) a[i] = values[i];
    return a;
}
}