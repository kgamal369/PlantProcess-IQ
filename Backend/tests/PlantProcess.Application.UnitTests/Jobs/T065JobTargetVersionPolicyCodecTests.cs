using PlantProcess.Application.Jobs.Targeting;
using PlantProcess.Domain.Enums.Integration;
using Xunit;

namespace PlantProcess.Application.UnitTests.Jobs;

/// <summary>
/// T-065. The one policy codec, and the behaviour that is the entire reason it
/// exists: an unrecognised value is refused instead of being read as the
/// current-published member.
/// </summary>
public sealed class T065JobTargetVersionPolicyCodecTests
{
    [Theory]
    [InlineData(JobTargetVersionPolicy.CurrentPublished, "current_published")]
    [InlineData(JobTargetVersionPolicy.Pinned, "pinned")]
    public void A_policy_stores_as_the_vocabulary_the_check_constraints_allow(
        JobTargetVersionPolicy policy, string expected)
    {
        Assert.Equal(expected, JobTargetVersionPolicyCodec.ToStorage(policy));
    }

    [Theory]
    [InlineData("current_published", JobTargetVersionPolicy.CurrentPublished)]
    [InlineData("pinned", JobTargetVersionPolicy.Pinned)]
    public void A_stored_value_decodes_to_its_exact_member(string stored, JobTargetVersionPolicy expected)
    {
        Assert.Equal(expected, JobTargetVersionPolicyCodec.FromStorage(stored));
    }

    [Theory]
    [InlineData(JobTargetVersionPolicy.CurrentPublished)]
    [InlineData(JobTargetVersionPolicy.Pinned)]
    public void Storage_encoding_round_trips(JobTargetVersionPolicy policy)
    {
        var stored = JobTargetVersionPolicyCodec.ToStorage(policy);
        Assert.Equal(policy, JobTargetVersionPolicyCodec.FromStorage(stored));
    }

    [Theory]
    [InlineData(JobTargetVersionPolicy.CurrentPublished)]
    [InlineData(JobTargetVersionPolicy.Pinned)]
    public void Wire_encoding_round_trips(JobTargetVersionPolicy policy)
    {
        var supplied = JobTargetVersionPolicyCodec.ToApi(policy);
        Assert.Equal(policy, JobTargetVersionPolicyCodec.FromApi(supplied));
    }

    /// <summary>
    /// The list is deliberately specific. Case variants and the enum member names
    /// are the values a loose parse would have accepted, and the numeric literal
    /// is the value Enum.TryParse would have accepted, and none of them is what
    /// the database holds.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Pinned")]
    [InlineData("PINNED")]
    [InlineData("Current_Published")]
    [InlineData("CurrentPublished")]
    [InlineData("currentpublished")]
    [InlineData(" pinned")]
    [InlineData("pinned ")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("latest")]
    public void An_unrecognised_stored_value_is_refused_and_never_read_as_current_published(string? stored)
    {
        Assert.False(JobTargetVersionPolicyCodec.TryFromStorage(stored, out _));

        var thrown = Assert.Throws<InvalidOperationException>(
            () => JobTargetVersionPolicyCodec.FromStorage(stored));

        Assert.Contains("not a target version policy", thrown.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("pinned")]
    [InlineData("current_published")]
    [InlineData("currentPublished")]
    [InlineData("PINNED")]
    [InlineData("2")]
    public void An_unrecognised_wire_value_is_refused(string? supplied)
    {
        Assert.False(JobTargetVersionPolicyCodec.TryFromApi(supplied, out _));
        Assert.Throws<InvalidOperationException>(() => JobTargetVersionPolicyCodec.FromApi(supplied));
    }

    /// <summary>
    /// The refusal has to be readable by whoever gets it, so it names both
    /// vocabularies rather than reporting that something unspecified was wrong.
    /// </summary>
    [Fact]
    public void The_refusal_names_both_vocabularies()
    {
        var message = JobTargetVersionPolicyCodec.UnknownPolicyMessage("latest");

        Assert.Contains("latest", message, StringComparison.Ordinal);
        Assert.Contains(JobTargetVersionPolicyCodec.CurrentPublishedApi, message, StringComparison.Ordinal);
        Assert.Contains(JobTargetVersionPolicyCodec.PinnedApi, message, StringComparison.Ordinal);
        Assert.Contains(JobTargetVersionPolicyCodec.CurrentPublishedStorage, message, StringComparison.Ordinal);
        Assert.Contains(JobTargetVersionPolicyCodec.PinnedStorage, message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The two stores must not drift apart. This asserts the constants against
    /// literals rather than against each other, so renaming one does not quietly
    /// rename the vocabulary the CHECK constraints in scripts 824 and 828 hold.
    /// </summary>
    [Fact]
    public void The_stored_vocabulary_is_exactly_what_the_database_constrains()
    {
        Assert.Equal("current_published", JobTargetVersionPolicyCodec.CurrentPublishedStorage);
        Assert.Equal("pinned", JobTargetVersionPolicyCodec.PinnedStorage);
        Assert.Equal("CurrentPublished", JobTargetVersionPolicyCodec.CurrentPublishedApi);
        Assert.Equal("Pinned", JobTargetVersionPolicyCodec.PinnedApi);
    }

    /// <summary>
    /// The enum has two members and the codec is total over both. A member added
    /// later without a codec branch fails here rather than at a customer.
    /// </summary>
    [Fact]
    public void The_codec_is_total_over_the_enum()
    {
        foreach (JobTargetVersionPolicy policy in Enum.GetValues<JobTargetVersionPolicy>())
        {
            var stored = JobTargetVersionPolicyCodec.ToStorage(policy);
            var api = JobTargetVersionPolicyCodec.ToApi(policy);

            Assert.Equal(policy, JobTargetVersionPolicyCodec.FromStorage(stored));
            Assert.Equal(policy, JobTargetVersionPolicyCodec.FromApi(api));
        }
    }
}
