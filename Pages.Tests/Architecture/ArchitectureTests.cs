using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Extensions;
using ArchUnitNET.Loader;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ArchitectureModel = ArchUnitNET.Domain.Architecture;

namespace Pages.Tests.Architecture;

// Encodes the design invariants of the Pages library so drift trips the build, not code review.
// Best-of rules drawn from the plumber and pool architecture suites, scoped to a single library.
public sealed class ArchitectureTests
{
    // Loading and building the model is expensive; build it once and reuse it across every [Fact].
    private static readonly ArchitectureModel PagesArchitecture = new ArchLoader()
        .LoadAssemblies(typeof(MemoryStreamPage).Assembly)
        .Build();

    [Fact]
    public void AllTypesResideInPagesNamespaceTree() =>
        Verify(Types()
            .That()
            .DoNotHaveNameContaining("<") // exclude compiler-generated closures / async state machines
            .Should()
            .ResideInNamespaceMatching(@"^Pages(\..*)?$")
            .Because("Pages is intentionally a flat namespace; new top-level namespaces require explicit design review."));

    [Fact]
    public void ConcreteClassesAreSealed() =>
        Verify(Classes()
            .That()
            .AreNotAbstract() // C# 'static' compiles to 'abstract sealed' — this also excludes static helpers
            .And()
            .DoNotHaveNameContaining("<")
            .Should()
            .BeSealed()
            .Because("seal records and classes by default (enables devirtualization)."));

    [Fact]
    public void InstanceFieldsAreNotPublic() =>
        Verify(FieldMembers()
            .That()
            .AreNotStatic() // const / static readonly may be public; instance state must not be.
            .And()
            .DoNotHaveNameContaining("<") // exclude compiler-generated backing fields
            .And()
            .DoNotHaveName("value__") // exclude the implicit instance field every C# enum compiles to
            .Should()
            .NotBePublic()
            .Because("immutable-by-default; no public mutable instance state — expose readonly properties instead."));

    [Fact]
    public void PublicTypesAreNotNested() =>
        Verify(Types()
            .That()
            .AreNested()
            .And()
            .DoNotHaveNameContaining("<")
            .Should()
            .NotBePublic()
            .Because("the public API is intentionally flat and discoverable; a public nested type hides surface area inside another type.")
            .WithoutRequiringPositiveResults());

    [Fact]
    public void AsyncMethodsHaveAsyncSuffix() =>
        Verify(MethodMembers()
            .That()
            // A member's FullName begins with its return type; match Task / Task<T> / ValueTask / ValueTask<T>.
            .HaveFullNameMatching(@"^System\.Threading\.Tasks\.(Task|ValueTask)(`\d+)?(<.*>)? ")
            .And()
            .DoNotHaveNameContaining("<") // exclude compiler-generated async state-machine members
            .And()
            .DoNotHaveNameContaining("Invoke(") // exclude delegate Invoke/BeginInvoke/EndInvoke
            .Should()
            .HaveNameMatching(@"Async\(")
            .Because("the Async suffix is the only signal a call returns a Task that must be awaited.")
            .WithoutRequiringPositiveResults());

    [Fact]
    public void DoesNotDependOnAspNetCore() =>
        Verify(Types()
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Microsoft\.AspNetCore.*")
            .Because("Pages is a host-free storage library; pulling in ASP.NET Core would defeat its purpose."));

    [Fact]
    public void DoesNotDependOnHosting() =>
        Verify(Types()
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Microsoft\.Extensions\.Hosting.*")
            .Because("Pages targets host-free .NET; the consumer owns the host, not Pages."));

    [Fact]
    public void DoesNotDependOnConsole() =>
        Verify(Types()
            .Should()
            .NotDependOnAnyTypesThat()
            .HaveFullName("System.Console")
            .Because("Library code routes through its caller; direct Console writes leak into hosts that suppress stdout."));

    private static void Verify(IArchRule rule)
    {
        if (!rule.HasNoViolations(PagesArchitecture))
        {
            Assert.Fail(rule.Evaluate(PagesArchitecture).ToErrorMessage());
        }
    }
}
