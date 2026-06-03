// PPIQ-GENERATED (T010)
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlantProcess.Application.Analytics.Engines;

public interface ICorrelationEngineRegistry
{
    ICorrelationEngine Default { get; }
    ICorrelationEngine Resolve(string? key);
    IReadOnlyCollection<string> Keys { get; }
}

/// <summary>Selector over all registered ICorrelationEngine strategies with an explicit default
/// (the canonical engine). Alternates ("managed","postgres",...) resolve by key; unknown -> default.</summary>
public sealed class CorrelationEngineRegistry : ICorrelationEngineRegistry
{
    private readonly Dictionary<string, ICorrelationEngine> _byKey;
    public CorrelationEngineRegistry(IEnumerable<ICorrelationEngine> engines)
    {
        _byKey = engines.ToDictionary(e => e.Key, StringComparer.OrdinalIgnoreCase);
        if (!_byKey.ContainsKey(CanonicalCorrelationEngine.CanonicalKey))
            throw new InvalidOperationException("Canonical correlation engine is not registered.");
    }
    public ICorrelationEngine Default => _byKey[CanonicalCorrelationEngine.CanonicalKey];
    public IReadOnlyCollection<string> Keys => _byKey.Keys;
    public ICorrelationEngine Resolve(string? key)
        => (!string.IsNullOrWhiteSpace(key) && _byKey.TryGetValue(key!, out var e)) ? e : Default;
}