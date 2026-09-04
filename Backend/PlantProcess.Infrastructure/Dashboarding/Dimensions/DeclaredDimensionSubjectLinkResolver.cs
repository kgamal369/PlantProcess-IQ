using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Dashboarding.Services.Dimensions;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.Dashboarding.Dimensions;

/// <summary>
/// Reads the model to find how a declaration's entity reaches the subject entity of a
/// population, and executes the resulting projection.
///
/// Governance is structural, not lexical. The candidates are the mapped single-column
/// references from the declaration's entity to the subject entity, with shadow members
/// and non-identity members excluded. Which of them is the link is decided by the
/// Application contract, not here; this class supplies facts and runs the query.
/// </summary>
public sealed class DeclaredDimensionSubjectLinkResolver : IDeclaredDimensionSubjectLinkResolver
{
    private static readonly MethodInfo ReadKeysMethod =
        typeof(DeclaredDimensionSubjectLinkResolver)
            .GetMethod(nameof(ReadSubjectKeysAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly PlantProcessDbContext _db;

    public DeclaredDimensionSubjectLinkResolver(PlantProcessDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Guid>> SubjectKeysWhereDeclaredEqualsAsync(
        DeclaredDimension declared,
        Type subjectEntityType,
        string value,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(declared);
        ArgumentNullException.ThrowIfNull(subjectEntityType);

        DeclaredDimensionProjection.RequireBindableAnywhere(declared);

        var sourceEntityType = declared.SourceEntityType!;
        var entity = _db.Model.FindEntityType(sourceEntityType);

        if (entity is null)
        {
            throw new DimensionBindingRefusalException(
                DimensionBindingRefusalCodes.Unbindable,
                declared.Code,
                "Declared dimension '" + declared.Code + "' binds to " + sourceEntityType.Name +
                ", which is not a mapped canonical entity.");
        }

        var candidates = entity.GetForeignKeys()
            .Where(fk => fk.PrincipalEntityType.ClrType == subjectEntityType)
            .Where(fk => fk.Properties.Count == 1)
            .Select(fk => fk.Properties[0])
            .Where(p => !p.IsShadowProperty())
            .Where(p => p.ClrType == typeof(Guid) || p.ClrType == typeof(Guid?))
            .Select(p => p.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var linkField = DeclaredDimensionProjection.SelectSubjectLinkField(declared.Code, candidates);
        var optional = entity.FindProperty(linkField)!.ClrType == typeof(Guid?);

        var task = (Task<IReadOnlyList<Guid>>)ReadKeysMethod
            .MakeGenericMethod(sourceEntityType)
            .Invoke(this, new object[] { declared, linkField, optional, value ?? string.Empty, cancellationToken })!;

        return await task;
    }

    private async Task<IReadOnlyList<Guid>> ReadSubjectKeysAsync<TSource>(
        DeclaredDimension declared,
        string linkField,
        bool optionalLink,
        string value,
        CancellationToken cancellationToken)
        where TSource : class
    {
        var keys = DeclaredDimensionProjection.SubjectKeys(
            _db.Set<TSource>().AsNoTracking(),
            declared,
            linkField,
            optionalLink,
            value);

        return await keys.ToListAsync(cancellationToken);
    }
}