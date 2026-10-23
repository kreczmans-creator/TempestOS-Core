using Tempest.Calc.Engine.Parsing;

namespace Tempest.Calc.Engine;

/// <summary>The one implementation of <see cref="ICalcEngine"/>. Stateless; sessions carry the state.</summary>
public sealed class CalcEngine : ICalcEngine
{
    /// <summary>Bumped whenever evaluation semantics change; part of every issued sheet's footer.</summary>
    public const string Version = "calc-engine/1.0.0";

    public static CalcEngine Instance { get; } = new();

    public string EngineVersion => Version;

    public ICalcSession CreateSession(CalcLimits? limits = null, int significantFigures = 5) => new CalcSession(limits ?? CalcLimits.Default, significantFigures);

    public void Validate(string expression, CalcLimits? limits = null) => Parser.Parse(expression, limits ?? CalcLimits.Default);
}
