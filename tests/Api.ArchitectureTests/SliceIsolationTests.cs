using System.Text.RegularExpressions;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

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

    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(typeof(Features.Greetings.Greeter).Assembly)
        .Build();

    public static TheoryData<string, string> SlicePairs
    {
        get
        {
            var slices = DiscoverSlices();
            var data = new TheoryData<string, string>();

            foreach (var from in slices)
            {
                foreach (var to in slices.Where(s => !string.Equals(s, from, StringComparison.Ordinal)))
                {
                    data.Add(from, to);
                }
            }

            return data;
        }
    }

    [Fact]
    public void There_are_at_least_two_slices_to_compare()
    {
        // Guards the guard: if slice discovery silently returned nothing, the theory below
        // would vacuously pass and the isolation rule would stop protecting anything.
        Assert.True(DiscoverSlices().Count >= 2, "Expected at least two slices under " + FeaturesRoot);
    }

    [Theory]
    [MemberData(nameof(SlicePairs))]
    public void Slice_does_not_depend_on_another_slice(string from, string to)
    {
        // Matching is by regex so that nested namespaces inside a slice
        // (Api.Features.Orders.Internal) are covered too, not just the slice root.
        var rule = Types().That().ResideInNamespaceMatching(NamespacePattern(from))
            .Should().NotDependOnAny(
                Types().That().ResideInNamespaceMatching(NamespacePattern(to)))
            .Because($"'{from}' and '{to}' are independent slices; sharing code between them "
                   + "means the code belongs outside Features/.");

        rule.Check(Architecture);
    }

    private static string NamespacePattern(string slice) =>
        $@"^{Regex.Escape(FeaturesRoot)}\.{Regex.Escape(slice)}($|\.)";

    private static List<string> DiscoverSlices() =>
        [.. Architecture.Types
            .Select(t => t.Namespace.FullName)
            .Where(ns => ns.StartsWith(FeaturesRoot + ".", StringComparison.Ordinal))
            .Select(ns => ns[(FeaturesRoot.Length + 1)..].Split('.')[0])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
}
