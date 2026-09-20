namespace PlantProcess.Application.Jobs.Execution.Transformations;

/// <summary>
/// THE STAGED READ PORT FOR GOVERNED TRANSFORMATION EXECUTION.
///
/// It executes only statements produced by the one transformation compiler, and it
/// answers one structural question about the staged relations: which of them expose
/// the governed provenance contract. It never infers identity from a name, a type, a
/// position or a value.
/// </summary>
public interface ITransformationSourceReader
{
    /// <summary>
    /// The relations, among those named, that expose both governed provenance columns,
    /// in ordinal order.
    /// </summary>
    Task<IReadOnlyList<string>> ProvenanceCapableRelationsAsync(
        string schema,
        IReadOnlyList<string> relations,
        CancellationToken cancellationToken);

    /// <summary>The measured row count of one staged relation.</summary>
    Task<long> CountRowsAsync(
        string schema,
        string relation,
        CancellationToken cancellationToken);

    /// <summary>
    /// Executes one compiled statement and returns its rows by ordinal. A statement that
    /// returns a different number of columns than the plan declares is refused.
    /// </summary>
    Task<IReadOnlyList<object?[]>> ReadAsync(
        string sql,
        IReadOnlyList<object> parameters,
        int expectedColumnCount,
        CancellationToken cancellationToken);
}

/// <summary>
/// The governed provenance contract a source-shaped relation exposes. These are the
/// provenance column names the product design declares for imported data, not a
/// customer vocabulary.
/// </summary>
public static class TransformationProvenanceColumns
{
    public const string SourceSystem = "source_system";

    public const string SourceRecordId = "source_record_id";
}
