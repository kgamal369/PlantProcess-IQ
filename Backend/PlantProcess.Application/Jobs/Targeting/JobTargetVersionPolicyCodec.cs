using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Jobs.Targeting;

/// <summary>
/// T-065. THE ONE TRANSLATION AUTHORITY FOR A TARGET VERSION POLICY.
///
/// Two stores persist this policy - job_definitions under T-064 and
/// inspection_jobs under the T-065 compatibility bridge - and before this codec
/// existed each carried its own mapping. The T-064 EF converter read anything
/// that was not the pinned literal as current-published, which is a guess
/// wearing the shape of an answer: a value written by a future migration, a
/// hand-edited row or a third store would have been silently reported as a
/// policy nobody chose.
///
/// UNKNOWN IS REFUSED. It is never defaulted, never parsed loosely, and never
/// matched without regard to case. Enum.TryParse is deliberately not used: it
/// accepts a numeric literal as a member, and a target policy that arrives as
/// the character 2 is not a policy anyone stated.
///
/// The stored vocabulary is the one script 824 and script 828 both constrain by
/// CHECK. The API vocabulary is the enum member name, because the wire contract
/// speaks the semantic value while the column speaks the stored one, and this
/// file is the only place the two meet.
/// </summary>
public static class JobTargetVersionPolicyCodec
{
    /// <summary>The stored spelling for <see cref="JobTargetVersionPolicy.CurrentPublished"/>.</summary>
    public const string CurrentPublishedStorage = "current_published";

    /// <summary>The stored spelling for <see cref="JobTargetVersionPolicy.Pinned"/>.</summary>
    public const string PinnedStorage = "pinned";

    /// <summary>The wire spelling for <see cref="JobTargetVersionPolicy.CurrentPublished"/>.</summary>
    public const string CurrentPublishedApi = "CurrentPublished";

    /// <summary>The wire spelling for <see cref="JobTargetVersionPolicy.Pinned"/>.</summary>
    public const string PinnedApi = "Pinned";

    /// <summary>The stored value for a policy. Total over the enum, so no caller has a default branch.</summary>
    public static string ToStorage(JobTargetVersionPolicy policy)
    {
        return policy switch
        {
            JobTargetVersionPolicy.CurrentPublished => CurrentPublishedStorage,
            JobTargetVersionPolicy.Pinned => PinnedStorage,
            _ => throw new ArgumentOutOfRangeException(
                nameof(policy), policy, UnknownPolicyMessage(policy.ToString()))
        };
    }

    /// <summary>The wire value for a policy.</summary>
    public static string ToApi(JobTargetVersionPolicy policy)
    {
        return policy switch
        {
            JobTargetVersionPolicy.CurrentPublished => CurrentPublishedApi,
            JobTargetVersionPolicy.Pinned => PinnedApi,
            _ => throw new ArgumentOutOfRangeException(
                nameof(policy), policy, UnknownPolicyMessage(policy.ToString()))
        };
    }

    /// <summary>Exact match against the stored vocabulary. False is a refusal, not an absence.</summary>
    public static bool TryFromStorage(string? stored, out JobTargetVersionPolicy policy)
    {
        switch (stored)
        {
            case CurrentPublishedStorage:
                policy = JobTargetVersionPolicy.CurrentPublished;
                return true;

            case PinnedStorage:
                policy = JobTargetVersionPolicy.Pinned;
                return true;

            default:
                policy = default;
                return false;
        }
    }

    /// <summary>Exact match against the wire vocabulary. False is a refusal, not an absence.</summary>
    public static bool TryFromApi(string? supplied, out JobTargetVersionPolicy policy)
    {
        switch (supplied)
        {
            case CurrentPublishedApi:
                policy = JobTargetVersionPolicy.CurrentPublished;
                return true;

            case PinnedApi:
                policy = JobTargetVersionPolicy.Pinned;
                return true;

            default:
                policy = default;
                return false;
        }
    }

    /// <summary>
    /// Decodes a stored value or throws. This is the form the EF converter uses:
    /// a row carrying a policy outside the vocabulary must stop the read rather
    /// than hand the application a value the database never held.
    /// </summary>
    public static JobTargetVersionPolicy FromStorage(string? stored)
    {
        if (!TryFromStorage(stored, out var policy))
        {
            throw new InvalidOperationException(UnknownPolicyMessage(stored));
        }

        return policy;
    }

    /// <summary>Decodes a wire value or throws.</summary>
    public static JobTargetVersionPolicy FromApi(string? supplied)
    {
        if (!TryFromApi(supplied, out var policy))
        {
            throw new InvalidOperationException(UnknownPolicyMessage(supplied));
        }

        return policy;
    }

    /// <summary>The sentence shown when a value is outside the closed vocabulary.</summary>
    public static string UnknownPolicyMessage(string? value)
    {
        var shown = string.IsNullOrWhiteSpace(value) ? "<none>" : value!;

        return "'" + shown + "' is not a target version policy. The two answers are "
             + CurrentPublishedApi + " and " + PinnedApi + ", stored as "
             + CurrentPublishedStorage + " and " + PinnedStorage + ". An unrecognised "
             + "value is refused rather than read as one of them, because a guessed "
             + "policy resolves a target nobody selected.";
    }
}
