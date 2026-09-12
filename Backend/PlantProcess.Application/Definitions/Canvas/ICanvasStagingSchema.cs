namespace PlantProcess.Application.Definitions.Canvas;

/// <summary>
/// PPIQ T-262. WHICH STAGED SCHEMA A GRAPH BINDING IS TYPED AGAINST.
///
/// The dataset catalogue already resolves this from configuration. The lifecycle needs
/// the same answer and must not go and read the key itself: a service that reaches for
/// configuration cannot be constructed in a test without a host, and it would also be a
/// second place the key is spelled - so the day it changes, one of them is wrong.
///
/// Composition reads the key once. Everything downstream receives the fact.
/// </summary>
public interface ICanvasStagingSchema
{
    /// <summary>The schema name, never empty.</summary>
    string Name { get; }
}

/// <summary>The resolved value. A blank answer falls back to the product default.</summary>
public sealed record CanvasStagingSchema : ICanvasStagingSchema
{
    public CanvasStagingSchema(string? name)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "ppiq_staging" : name!.Trim();
    }

    public string Name { get; }
}