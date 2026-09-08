using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace PlantProcess.Application.Dashboarding.Services.Dimensions;

/// <summary>
/// The ONE place a declared dimension becomes an executable expression.
///
/// The executor groups every dimension through one generic slot. For a declared
/// dimension that slot is DimensionText, and this class fills it by rewriting a
/// measure's own fact projection so that the slot is bound to
/// EF.Property&lt;string&gt;(source, declared.SourceField). EF translates that to the
/// mapped column, so PostgreSQL groups on the real column and no vocabulary is ever
/// spelled in C#.
///
/// A source projection of one canonical entity can only bind a dimension declared
/// against that same entity. A declaration against a DIFFERENT entity is reached
/// through the relationship authority - resolver, planner, executor - and never by
/// this class inspecting how two entities happen to be connected.
/// </summary>
public static class DeclaredDimensionProjection
{
    /// <summary>
    /// The execution-grammar code the executor groups on when the requested dimension
    /// is customer-declared. It is grammar, not vocabulary: it names a slot, never a
    /// concept, and it cannot be published by a customer because it is not a valid
    /// definition code.
    /// </summary>
    public const string SlotCode = "$declared";

    /// <summary>The fact member that carries a declared dimension's value.</summary>
    public const string SlotMember = "DimensionText";

    private static readonly MethodInfo EfPropertyOfString =
        typeof(EF).GetMethod(nameof(EF.Property), BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(typeof(string));

    private static readonly MethodInfo EfPropertyOfGuid =
        typeof(EF).GetMethod(nameof(EF.Property), BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(typeof(Guid));

    private static readonly MethodInfo EfPropertyOfNullableGuid =
        typeof(EF).GetMethod(nameof(EF.Property), BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(typeof(Guid?));

    public static bool IsSlot(string? dimensionCode) =>
        string.Equals(dimensionCode?.Trim(), SlotCode, StringComparison.Ordinal);

    /// <summary>
    /// Rewrite an object-initialiser projection so that its SlotMember is bound to the
    /// declared dimension's source field on the projection parameter. Every other
    /// binding is preserved untouched.
    /// </summary>
    public static Expression<Func<TSource, TFact>> WithDeclaredDimension<TSource, TFact>(
        Expression<Func<TSource, TFact>> projection,
        DeclaredDimension declared)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(declared);

        RequireBindable(declared, typeof(TSource));

        if (projection.Body is not MemberInitExpression init)
        {
            throw new InvalidOperationException(
                "Declared dimension binding requires an object-initialiser projection; " +
                "the executor's GroupBy translation depends on that shape.");
        }

        var slot = typeof(TFact).GetProperty(SlotMember, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                "Fact type " + typeof(TFact).Name + " does not expose the declared-dimension slot " + SlotMember + ".");

        var parameter = projection.Parameters[0];
        var value = Expression.Call(EfPropertyOfString, parameter, Expression.Constant(declared.SourceField, typeof(string)));

        var bindings = init.Bindings
            .Where(b => !string.Equals(b.Member.Name, SlotMember, StringComparison.Ordinal))
            .Append(Expression.Bind(slot, value))
            .ToArray();

        return Expression.Lambda<Func<TSource, TFact>>(Expression.MemberInit(init.NewExpression, bindings), parameter);
    }

    /// <summary>
    /// Restrict a canonical source population to rows whose declared dimension value
    /// equals the filter value. Same binding rule as grouping; same refusal when the
    /// declaration does not bind to this source.
    /// </summary>
    public static IQueryable<TSource> WhereDeclaredEquals<TSource>(
        IQueryable<TSource> source,
        DeclaredDimension declared,
        string value)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(declared);

        RequireBindable(declared, typeof(TSource));

        var parameter = Expression.Parameter(typeof(TSource), "x");
        var member = Expression.Call(EfPropertyOfString, parameter, Expression.Constant(declared.SourceField, typeof(string)));
        var body = Expression.Equal(member, Expression.Constant(value, typeof(string)));

        return source.Where(Expression.Lambda<Func<TSource, bool>>(body, parameter));
    }

    /// <summary>
    /// True when the declaration binds to the subject entity itself and can therefore
    /// restrict the subject population directly.
    /// </summary>
    public static bool BindsToSubject(DeclaredDimension declared, Type subjectEntityType) =>
        declared is not null &&
        declared.IsBindable &&
        declared.SourceEntityType is not null &&
        declared.SourceEntityType == subjectEntityType;

    /// <summary>
    /// The subject keys whose related row carries the declared value: restrict the
    /// related population by the declared field, then project the member the caller
    /// names. The caller is handed that member by a governed plan; this class never
    /// works out which member it should be. It spells no entity, no column and no
    /// concept.
    /// </summary>
    public static IQueryable<Guid> SubjectKeys<TSource>(
        IQueryable<TSource> source,
        DeclaredDimension declared,
        string linkField,
        bool optionalLink,
        string value)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(declared);
        if (string.IsNullOrWhiteSpace(linkField)) { throw new ArgumentException("A link field is required.", nameof(linkField)); }

        var restricted = WhereDeclaredEquals(source, declared, value ?? string.Empty);
        var parameter = Expression.Parameter(typeof(TSource), "x");

        if (!optionalLink)
        {
            var selector = Expression.Lambda<Func<TSource, Guid>>(
                Expression.Call(EfPropertyOfGuid, parameter, Expression.Constant(linkField, typeof(string))),
                parameter);

            return restricted.Select(selector).Distinct();
        }

        var access = Expression.Call(EfPropertyOfNullableGuid, parameter, Expression.Constant(linkField, typeof(string)));

        var present = Expression.Lambda<Func<TSource, bool>>(
            Expression.Property(access, "HasValue"), parameter);

        var carried = Expression.Lambda<Func<TSource, Guid>>(
            Expression.Property(access, "Value"), parameter);

        return restricted.Where(present).Select(carried).Distinct();
    }

    /// <summary>
    /// The publication half of RequireBindable: is this declaration executable at all,
    /// independently of which population is being restricted.
    /// </summary>
    public static void RequireBindableAnywhere(DeclaredDimension declared)
    {
        ArgumentNullException.ThrowIfNull(declared);

        if (!declared.IsBindable || declared.SourceEntityType is null)
        {
            throw new DimensionBindingRefusalException(
                declared.BindingRefusalCode ?? DimensionBindingRefusalCodes.Unbindable,
                declared.Code,
                "Declared dimension '" + declared.Code + "' is published but not executable: " +
                (declared.BindingRefusalReason ?? "its source binding could not be resolved against the canonical model."));
        }
    }

    public static void RequireBindable(DeclaredDimension declared, Type sourceEntityType)
    {
        ArgumentNullException.ThrowIfNull(declared);

        if (!declared.IsBindable || declared.SourceEntityType is null)
        {
            throw new DimensionBindingRefusalException(
                declared.BindingRefusalCode ?? DimensionBindingRefusalCodes.Unbindable,
                declared.Code,
                "Declared dimension '" + declared.Code + "' is published but not executable: " +
                (declared.BindingRefusalReason ?? "its source binding could not be resolved against the canonical model."));
        }

        if (declared.SourceEntityType != sourceEntityType)
        {
            throw new DimensionBindingRefusalException(
                DimensionBindingRefusalCodes.SourceMismatch,
                declared.Code,
                "Declared dimension '" + declared.Code + "' binds to " + declared.SourceEntityType.Name +
                " but this measure's population is " + sourceEntityType.Name +
                ". No join is inferred; relationship traversal is not part of this binding contract.");
        }
    }
}