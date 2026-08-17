using System;
using System.Text.Json;

namespace PlantProcess.Domain.Enums.Integration;

/// <summary>
/// T-064. WHICH VERSION OF ITS TARGET A JOB RUNS.
///
/// There are exactly two answers and no default. A job that has not stated one
/// has no target at all, which is a different fact from a job that follows the
/// published version, and the two must never collapse into each other.
///
/// T-089 and T-090 establish the canonical definition-store authority; T-106
/// owns the physical T-064 convergence onto definition_store(id). This enum is
/// the semantic half and does not change when that happens.
/// </summary>
public enum JobTargetVersionPolicy
{
    /// <summary>Run whichever version is published at the moment of the run.</summary>
    CurrentPublished = 1,

    /// <summary>Run one fixed version number, whatever is published later.</summary>
    Pinned = 2
}

/// <summary>
/// T-064. THE PARAMETERS A JOB PASSES TO ITS TARGET.
///
/// One implementation, used by the entity that stores them, the reference that
/// carries them and the run history that records what actually ran, so the three
/// cannot disagree about what a valid parameter payload is.
///
/// null and "{}" are different statements and this task keeps them different.
/// Absent means the job supplied nothing; an empty object means it deliberately
/// supplied an empty set. Collapsing them would make a configured job and an
/// unconfigured one indistinguishable a year later.
///
/// T-064 freezes the transport, not a vocabulary. What keys a given job class
/// accepts is that class's contract, and inventing a schema here would bind every
/// future job type to a shape nobody ratified.
/// </summary>
public static class JobTargetParameters
{
    /// <summary>
    /// Trims, and turns whitespace-only into genuine absence. Anything else is
    /// returned unchanged - the payload is stored as it was supplied, never
    /// re-serialised, because a reformatted payload is no longer the one the
    /// operator wrote.
    /// </summary>
    public static string? Normalise(string? parametersJson)
    {
        if (string.IsNullOrWhiteSpace(parametersJson))
        {
            return null;
        }

        return parametersJson.Trim();
    }

    /// <summary>
    /// True when the value is absent, or is syntactically valid JSON. Absence is
    /// valid; malformed text never is, and it is refused before persistence rather
    /// than discovered by whatever tries to read it later.
    /// </summary>
    public static bool IsValid(string? parametersJson)
    {
        string? normalised = Normalise(parametersJson);
        if (normalised is null)
        {
            return true;
        }

        try
        {
            using JsonDocument parsed = JsonDocument.Parse(normalised);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Normalises and refuses malformed JSON, naming the argument.</summary>
    public static string? Require(string? parametersJson, string argumentName)
    {
        string? normalised = Normalise(parametersJson);

        if (normalised is not null && !IsValid(normalised))
        {
            throw new ArgumentException(
                "Target parameters must be valid JSON. Absent parameters are expressed by "
                + "omitting them, never by supplying malformed text.", argumentName);
        }

        return normalised;
    }
}
