namespace Tempest.Calc.Engine.Analysis;

/// <summary>The sampling distributions the shared sampler offers.</summary>
public enum DistributionKind
{
    Normal,
    Uniform,
    Triangular,
    TruncatedNormal,
}

/// <summary>
/// One input's distribution for a Monte Carlo run, in the input's own SI
/// value. Normal: mean, sigma. Uniform: lower, upper. Triangular: lower,
/// mode, upper. TruncatedNormal: mean, sigma, lower, upper (samples outside
/// the bounds are redrawn, at most <see cref="Sampler.MaxRedraws"/> times).
/// </summary>
public sealed record Distribution(DistributionKind Kind, double Mean = 0, double Sigma = 0, double Lower = 0, double Upper = 0, double Mode = 0)
{
    public static Distribution Normal(double mean, double sigma) => new(DistributionKind.Normal, Mean: mean, Sigma: sigma);

    public static Distribution Uniform(double lower, double upper) => new(DistributionKind.Uniform, Lower: lower, Upper: upper);

    public static Distribution Triangular(double lower, double mode, double upper) => new(DistributionKind.Triangular, Lower: lower, Upper: upper, Mode: mode);

    public static Distribution TruncatedNormal(double mean, double sigma, double lower, double upper) => new(DistributionKind.TruncatedNormal, Mean: mean, Sigma: sigma, Lower: lower, Upper: upper);

    public void Validate(string name)
    {
        static bool Finite(params double[] xs) => xs.All(double.IsFinite);

        switch (Kind)
        {
            case DistributionKind.Normal:
                if (!Finite(Mean, Sigma) || Sigma < 0)
                    throw new CalcEvaluationException($"{name}: a normal distribution needs a finite mean and a non-negative sigma.");
                break;
            case DistributionKind.Uniform:
                if (!Finite(Lower, Upper) || Lower > Upper)
                    throw new CalcEvaluationException($"{name}: a uniform distribution needs finite lower <= upper.");
                break;
            case DistributionKind.Triangular:
                if (!Finite(Lower, Mode, Upper) || Lower > Mode || Mode > Upper)
                    throw new CalcEvaluationException($"{name}: a triangular distribution needs finite lower <= mode <= upper.");
                break;
            case DistributionKind.TruncatedNormal:
                if (!Finite(Mean, Sigma, Lower, Upper) || Sigma < 0 || Lower > Upper)
                    throw new CalcEvaluationException($"{name}: a truncated normal needs a finite mean, non-negative sigma and lower <= upper.");
                break;
            default:
                throw new CalcEvaluationException($"{name}: unknown distribution.");
        }
    }
}
