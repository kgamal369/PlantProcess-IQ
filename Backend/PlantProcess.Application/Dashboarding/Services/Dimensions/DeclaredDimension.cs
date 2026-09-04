using System;

namespace PlantProcess.Application.Dashboarding.Services.Dimensions;

/// <summary>
/// A customer-declared dimension as published in the canonical definition store,
/// resolved against the EF model so the executor can bind it without knowing its name.
///
/// The product never learns what a declared dimension is CALLED. It learns only that a
/// published definition exists, which governed canonical entity it binds to, and which
/// mapped scalar member of that entity carries its value. Everything else - the code,
/// the label, the vocabulary - is the customer's, arrives as data, and changes without a
/// build.
///
/// IsBindable is decided once, at catalogue load, from the EF model: the entity must be
/// a mapped canonical entity, the member must be a mapped non-key string scalar. A
/// declaration that fails that check is still reported (so metadata can say it exists)
/// but is never executed - BindingRefusalCode says why.
/// </summary>
public sealed record DeclaredDimension(
    string Code,
    string Label,
    string DataType,
    string GrainCode,
    string SourceCatalog,
    string SourceField,
    Type? SourceEntityType,
    bool IsBindable,
    string? BindingRefusalCode,
    string? BindingRefusalReason,
    Guid DefinitionId,
    int DefinitionVersion);