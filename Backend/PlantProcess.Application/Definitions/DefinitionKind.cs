namespace PlantProcess.Application.Definitions;

/// <summary>
/// PPIQ T-039. WHAT KIND OF DEFINITION IS BEING VERSIONED.
///
/// Every member of this enum is declared NOW, in M1, including the ones M1 does
/// not yet store. That is the point of the task and it is not optimism: an
/// external contract that has to gain a member in M2a was never final, and
/// every caller that switched on it would have to be revisited. Adding storage
/// later is a change behind the interface; adding a member is a change to it.
///
/// The five authoring purposes of Chapter 4 section 5.2 come first, then the
/// five S2 sub-kinds the design also versions, then the bookmark a user saves.
/// </summary>
public enum DefinitionKind
{
    /// <summary>S1. Staged data mapped into the plant schema.</summary>
    Transformation = 1,

    /// <summary>S2. A page and its layout.</summary>
    Page = 2,

    /// <summary>S2. A widget and the dataset it displays.</summary>
    Widget = 3,

    /// <summary>S3. An analysis definition.</summary>
    Analysis = 4,

    /// <summary>S4. A model definition.</summary>
    Model = 5,

    /// <summary>S5. A rule emitting info, warning and error entries.</summary>
    LogRule = 6,

    /// <summary>A dimension published to the widget catalogue.</summary>
    MasterDimension = 7,

    /// <summary>A measure published to the widget catalogue.</summary>
    MasterMeasure = 8,

    /// <summary>A named filter reusable across pages.</summary>
    Filter = 9,

    /// <summary>A drill hierarchy over master dimensions.</summary>
    Hierarchy = 10,

    /// <summary>A saved selection state a user returns to.</summary>
    Bookmark = 11,
}