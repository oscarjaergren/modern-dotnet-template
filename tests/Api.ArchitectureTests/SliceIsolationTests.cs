using ArchUnitNET.Domain;
using ArchUnitNET.Fluent.Slices;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;

namespace Api.ArchitectureTests;

/// <summary>
/// The layout is only a convention until something fails the build when it is violated.
/// This is that something: no slice under <c>Api.Features</c> may reference another slice.
/// </summary>
/// <remarks>
/// Slices are discovered from the assembly, so a newly added slice is covered automatically —
/// nobody has to remember to update this file.
///
/// If two slices genuinely need to share code, the answer is to move it out of both — into a
/// shared namespace outside <c>Api.Features</c> — not to relax this rule.
/// </remarks>
public class SliceIsolationTests
{
    private const string FeaturesRoot = "Api.Features";

    // The (*) capture is what makes a slice: everything under Api.Features.Orders, nested
    // namespaces included, is one slice named "Orders".
    private const string SlicePattern = $"{FeaturesRoot}.(*)";

    // By name, not via a type: every slice here is meant to be replaceable, and the sample ones
    // are meant to be deleted.
    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(System.Reflection.Assembly.Load("Api"))
        .Build();

    [Fact]
    public void There_are_at_least_two_slices_to_compare()
    {
        // Guards the guard: with fewer than two slices the rule below passes vacuously and the
        // isolation constraint silently stops protecting anything. Deleting the sample slices is
        // expected — see AGENTS.md — so this is the test that tells you the guard went with them.
        Assert.True(
            DiscoverSlices().Count >= 2,
            $"Expected at least two slices under {FeaturesRoot}, so that isolation is actually tested.");
    }

    // If this fails, the message names the slices that reached across. The fix is to move the
    // shared code out of Features/ entirely — never to relax the rule. SliceRule carries no
    // Because() overload, which is why that guidance is here rather than in the failure output.
    [Fact]
    public void Slices_do_not_depend_on_each_other() =>
        SliceRuleDefinition.Slices().Matching(SlicePattern)
            .Should().NotDependOnEachOther()
            .Check(Architecture);

    private static List<string> DiscoverSlices() =>
        [.. Architecture.Types
            .Select(t => t.Namespace.FullName)
            .Where(ns => ns.StartsWith(FeaturesRoot + ".", StringComparison.Ordinal))
            .Select(ns => ns[(FeaturesRoot.Length + 1)..].Split('.')[0])
            .Distinct(StringComparer.Ordinal)];
}
