namespace Tempest.Calc.Engine;

/// <summary>
/// Every cap the engine enforces. Nothing driven by an expression or by
/// user data is unbounded: a parse fails before a deep tree is built, a
/// session refuses the step after the last, and an array refuses the
/// element after the last.
/// </summary>
public sealed record CalcLimits(
    int MaxExpressionLength = 4_000,
    int MaxTokens = 1_000,
    int MaxNestingDepth = 32,
    int MaxSymbolLength = 64,
    int MaxLiteralLength = 40,
    int MaxSteps = 5_000,
    int MaxVariables = 10_000,
    int MaxArrayElements = 1_000_000,
    int MaxMatrixOrder = 2_000,
    int MaxIterations = 100_000,
    int MaxFunctionArguments = 64,
    int MaxTextLength = 2_000)
{
    public static CalcLimits Default { get; } = new();

    /// <summary>A deliberately small configuration for tests that prove the caps bite.</summary>
    public static CalcLimits Tight { get; } = new(
        MaxExpressionLength: 200, MaxTokens: 60, MaxNestingDepth: 6, MaxSymbolLength: 16, MaxLiteralLength: 12,
        MaxSteps: 20, MaxVariables: 30, MaxArrayElements: 100, MaxMatrixOrder: 8, MaxIterations: 100, MaxFunctionArguments: 8, MaxTextLength: 100);

    public void Validate()
    {
        foreach (var (name, value) in new (string, int)[]
        {
            (nameof(MaxExpressionLength), MaxExpressionLength), (nameof(MaxTokens), MaxTokens), (nameof(MaxNestingDepth), MaxNestingDepth),
            (nameof(MaxSymbolLength), MaxSymbolLength), (nameof(MaxLiteralLength), MaxLiteralLength), (nameof(MaxSteps), MaxSteps),
            (nameof(MaxVariables), MaxVariables), (nameof(MaxArrayElements), MaxArrayElements), (nameof(MaxMatrixOrder), MaxMatrixOrder),
            (nameof(MaxIterations), MaxIterations), (nameof(MaxFunctionArguments), MaxFunctionArguments), (nameof(MaxTextLength), MaxTextLength),
        })
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(name, value, "Every limit must be positive.");
        }
    }
}
