using System;
using System.Collections.Generic;
using PlantProcess.Application.Jobs.Dependencies;
using Xunit;

namespace PlantProcess.Application.UnitTests.Jobs;

/// <summary>
/// T-106. The dependency algebra, positive and negative, with no database in
/// the way of falsifying it.
/// </summary>
public sealed class JobDependencyGraphTests
{
    private static readonly Guid JobA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid JobB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid JobC = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid JobD = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static JobDependencyEdge Edge(Guid dependent, Guid predecessor)
    {
        return new JobDependencyEdge(dependent, predecessor);
    }

    [Fact]
    public void A_chain_executes_predecessors_first()
    {
        var edges = new[] { Edge(JobC, JobB), Edge(JobB, JobA) };

        var result = JobDependencyGraph.TopologicalOrder(new[] { JobA, JobB, JobC }, edges);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { JobA, JobB, JobC }, result.Value!);
    }

    [Fact]
    public void Multiple_predecessors_both_precede_their_dependent()
    {
        var edges = new[] { Edge(JobC, JobA), Edge(JobC, JobB) };

        var result = JobDependencyGraph.TopologicalOrder(new[] { JobA, JobB, JobC }, edges);

        Assert.True(result.IsSuccess);
        var order = new List<Guid>(result.Value!);
        Assert.True(order.IndexOf(JobA) < order.IndexOf(JobC));
        Assert.True(order.IndexOf(JobB) < order.IndexOf(JobC));
    }

    [Fact]
    public void The_order_is_deterministic_and_not_merely_valid()
    {
        var forward = new[] { Edge(JobC, JobB), Edge(JobB, JobA), Edge(JobD, JobA) };
        var reversed = new[] { Edge(JobD, JobA), Edge(JobB, JobA), Edge(JobC, JobB) };

        var first = JobDependencyGraph.TopologicalOrder(new[] { JobA, JobB, JobC, JobD }, forward);
        var second = JobDependencyGraph.TopologicalOrder(new[] { JobD, JobC, JobB, JobA }, reversed);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value!, second.Value!);
    }

    [Fact]
    public void A_self_dependency_is_refused_with_JD01()
    {
        var result = JobDependencyGraph.ValidateNewEdge(Edge(JobA, JobA), Array.Empty<JobDependencyEdge>());

        Assert.True(result.IsFailure);
        Assert.Equal(JobDependencyErrorCodes.SelfDependency, result.Error!.Code);
    }

    [Fact]
    public void A_repeated_edge_is_refused_with_JD03()
    {
        var existing = new[] { Edge(JobB, JobA) };

        var result = JobDependencyGraph.ValidateNewEdge(Edge(JobB, JobA), existing);

        Assert.True(result.IsFailure);
        Assert.Equal(JobDependencyErrorCodes.DuplicateDependencyEdge, result.Error!.Code);
    }

    [Fact]
    public void A_two_job_cycle_is_refused_at_save_time_with_JD02()
    {
        var existing = new[] { Edge(JobB, JobA) };

        var result = JobDependencyGraph.ValidateNewEdge(Edge(JobA, JobB), existing);

        Assert.True(result.IsFailure);
        Assert.Equal(JobDependencyErrorCodes.DependencyCycle, result.Error!.Code);
    }

    [Fact]
    public void A_multi_job_cycle_is_refused_at_save_time_with_JD02()
    {
        // A -> B -> C already stored; closing C -> A would make the chain
        // non-terminating, and it is refused before it is written.
        var existing = new[] { Edge(JobB, JobA), Edge(JobC, JobB) };

        var result = JobDependencyGraph.ValidateNewEdge(Edge(JobA, JobC), existing);

        Assert.True(result.IsFailure);
        Assert.Equal(JobDependencyErrorCodes.DependencyCycle, result.Error!.Code);
    }

    [Fact]
    public void A_lawful_edge_is_accepted()
    {
        var existing = new[] { Edge(JobB, JobA) };

        var result = JobDependencyGraph.ValidateNewEdge(Edge(JobC, JobB), existing);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void A_cycle_that_reached_storage_is_still_refused_by_the_order()
    {
        var edges = new[] { Edge(JobB, JobA), Edge(JobC, JobB), Edge(JobA, JobC) };

        var result = JobDependencyGraph.TopologicalOrder(new[] { JobA, JobB, JobC }, edges);

        Assert.True(result.IsFailure);
        Assert.Equal(JobDependencyErrorCodes.DependencyCycle, result.Error!.Code);
    }

    [Fact]
    public void The_closure_carries_the_job_and_everything_beneath_it_and_nothing_else()
    {
        var edges = new[] { Edge(JobC, JobB), Edge(JobB, JobA), Edge(JobD, JobA) };

        var closure = JobDependencyGraph.DependencyClosure(JobC, edges);

        Assert.Contains(JobA, closure);
        Assert.Contains(JobB, closure);
        Assert.Contains(JobC, closure);
        Assert.DoesNotContain(JobD, closure);
    }
}