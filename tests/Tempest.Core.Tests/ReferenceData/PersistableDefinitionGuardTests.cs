using System.Reflection;
using System.Text.Json;
using Tempest.Core.ReferenceData;
using Xunit;

namespace Tempest.Core.Tests.ReferenceData;

/// <summary>
/// One guard over every governed record type in the platform: the
/// serialiser must be able to construct it.
/// </summary>
/// <remarks>
/// <para>
/// This defect class has bitten twice. `P07` shipped a <c>Money</c> whose
/// get-only properties left the serialiser falling back to the implicit
/// struct constructor, so every persisted amount read back as
/// <c>0.00 (unspecified)</c>. `P03` shipped a <c>CostFigure</c> whose
/// private constructor left the serialiser with nothing to call at all,
/// so every persisted cost record threw the moment a catalogue read it.
/// Both were found by a hand-written round-trip test naming that one
/// type.
/// </para>
/// <para>
/// Hand-written tests only cover the types somebody remembered to list.
/// This one <b>discovers</b> the types instead: every
/// <see cref="ReferenceDataCatalog{TDefinition}"/> in the assembly is
/// found by reflection, and its definition type is checked. A new library
/// added in a year's time is covered the day it compiles.
/// </para>
/// <para>
/// The check distinguishes the two failures precisely.
/// <see cref="NotSupportedException"/> means the serialiser found no
/// constructor it could use — the defect. <see cref="JsonException"/>
/// means it found one and the empty document did not satisfy a required
/// member — entirely expected, and not a defect.
/// </para>
/// </remarks>
public sealed class PersistableDefinitionGuardTests
{
    public static TheoryData<Type> GovernedDefinitionTypes
    {
        get
        {
            var data = new TheoryData<Type>();

            foreach (var definition in typeof(ReferenceDataCatalog<>).Assembly
                         .GetTypes()
                         .Where(t => t is { IsAbstract: false, IsGenericTypeDefinition: false })
                         .Select(DefinitionTypeOf)
                         .OfType<Type>()
                         .Distinct()
                         .OrderBy(t => t.FullName, StringComparer.Ordinal))
                data.Add(definition);

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(GovernedDefinitionTypes))]
    public void Every_governed_definition_type_can_be_constructed_by_the_serialiser(Type definitionType)
    {
        var thrown = Record.Exception(() => JsonSerializer.Deserialize("{}", definitionType));

        Assert.False(
            thrown is NotSupportedException,
            $"{definitionType.FullName} is persisted by a ReferenceDataCatalog and System.Text.Json cannot construct "
            + "it. Every record of this type will throw the moment a catalogue reads it back. Annotate the "
            + $"constructor with [JsonConstructor], or give the type one the serialiser can use.{Environment.NewLine}"
            + $"{thrown?.Message}");
    }

    [Fact]
    public void The_guard_finds_the_libraries_it_is_meant_to_cover()
    {
        // A guard that silently discovers nothing passes forever. This
        // asserts it is actually looking at the platform's libraries, and
        // names a few from different programmes so a namespace rename
        // cannot quietly empty it.
        var covered = GovernedDefinitionTypes
            .Cast<object[]>()
            .Select(row => ((Type)row[0]).FullName ?? string.Empty)
            .ToList();

        Assert.InRange(covered.Count, 25, 200);

        Assert.Contains(covered, n => n.Contains("Materials", StringComparison.Ordinal));
        Assert.Contains(covered, n => n.Contains("CommercialIntelligence", StringComparison.Ordinal));
        Assert.Contains(covered, n => n.Contains("EngineeringAssets", StringComparison.Ordinal));
        Assert.Contains(covered, n => n.Contains("Knowledge", StringComparison.Ordinal));
        Assert.Contains(covered, n => n.Contains("BusinessOperations", StringComparison.Ordinal));
        Assert.Contains(covered, n => n.Contains("BusinessGovernance", StringComparison.Ordinal));
    }

    [Fact]
    public void The_guard_would_catch_the_defect_it_exists_for()
    {
        // The CostFigure defect, reproduced on a throwaway type: a
        // private constructor with no annotation. If this stops throwing
        // NotSupportedException the guard above has stopped meaning
        // anything, and this test says so.
        var thrown = Record.Exception(() => JsonSerializer.Deserialize<PrivatelyConstructed>("{}"));

        Assert.IsType<NotSupportedException>(thrown);

        // And the case that must not be mistaken for it: a type the
        // serialiser can construct, refusing an empty document because a
        // required member is missing.
        Assert.IsType<JsonException>(Record.Exception(() => JsonSerializer.Deserialize<RequiresAMember>("{}")));
    }

    private static Type? DefinitionTypeOf(Type candidate)
    {
        for (var type = candidate.BaseType; type is not null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ReferenceDataCatalog<>))
                return type.GetGenericArguments()[0];
        }

        return null;
    }

    private sealed record PrivatelyConstructed
    {
        private PrivatelyConstructed(int value) => Value = value;

        public int Value { get; }
    }

    private sealed record RequiresAMember
    {
        public required string Name { get; init; }
    }
}
