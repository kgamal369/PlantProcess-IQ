using PlantProcess.Domain.Common;

namespace PlantProcess.Application.Integration.Contracts.Mapping;

/// <summary>
/// PPIQ T-099. ONE ROW'S TYPED REFUSAL, CARRIED RATHER THAN THROWN.
///
/// Exceptions used to be the classification authority here: a missing field, an
/// unparseable number and an unresolvable reference all arrived as the same
/// InvalidOperationException and were distinguishable only by reading their
/// prose. That made the taxonomy unprovable and made every refusal look alike to
/// the batch, which is why one bad row could end a run.
///
/// A refusal is now a value. The row loop receives it, records it, and carries
/// on with the next row; nothing about a single invalid row reaches a catch
/// block or a transaction boundary.
/// </summary>
public sealed record RowValidationRefusal(
    ProjectionValidationCode Code,
    string Detail,
    string? OffendingValue)
{
    public string SuggestedCorrection => ProjectionValidationCorrection.For(Code);
}

/// <summary>
/// How a mapped field presented itself in the staged payload. FIVE ANSWERS, NOT
/// TWO: the old helpers returned null for every one of them, so an omitted
/// optional field and a malformed supplied number were indistinguishable and a
/// corrupt value could be written to canonical truth as a lawful null.
/// </summary>
public enum FieldReadState
{
    /// <summary>The mapping never declared this target field.</summary>
    NotMapped = 0,

    /// <summary>The mapping declared it; the staged payload does not carry it. PV01 when required.</summary>
    MappedButSourceAbsent = 1,

    /// <summary>Carried, but blank. Lawful for an optional field, PV03 for a required one.</summary>
    PresentEmpty = 2,

    /// <summary>Carried and usable.</summary>
    PresentValid = 3,

    /// <summary>Carried, but cannot become the target type. PV02, never a silent null.</summary>
    PresentInvalid = 4
}

/// <summary>
/// The outcome of reading one mapped field.
/// </summary>
public readonly struct FieldRead<T>
{
    private FieldRead(T? value, FieldReadState state, RowValidationRefusal? refusal)
    {
        Value = value;
        State = state;
        Refusal = refusal;
    }

    public T? Value { get; }

    public FieldReadState State { get; }

    public RowValidationRefusal? Refusal { get; }

    /// <summary>The mapping declared the field AND the staged payload carried it.</summary>
    public bool IsPresent => State is FieldReadState.PresentEmpty or FieldReadState.PresentValid or FieldReadState.PresentInvalid;

    /// <summary>The field was present but carried no value. PV03 territory when required.</summary>
    public bool IsEmpty => State == FieldReadState.PresentEmpty;

    public bool IsDeclared => State != FieldReadState.NotMapped;

    public bool IsRefused => Refusal is not null;

    public static FieldRead<T> Ok(T? value) => new(value, FieldReadState.PresentValid, null);

    /// <summary>The mapping does not declare this target field at all.</summary>
    public static FieldRead<T> NotMapped() => new(default, FieldReadState.NotMapped, null);

    /// <summary>Declared by the mapping, absent from the staged payload. PV01 when required.</summary>
    public static FieldRead<T> Absent() => new(default, FieldReadState.MappedButSourceAbsent, null);

    /// <summary>Present but blank.</summary>
    public static FieldRead<T> Empty() => new(default, FieldReadState.PresentEmpty, null);

    public static FieldRead<T> Refused(RowValidationRefusal refusal) => new(default, FieldReadState.PresentInvalid, refusal);
}
