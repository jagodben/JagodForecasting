namespace ElectionForecaster.Infrastructure.Forecasting;

/// <summary>
/// Shared statistical helpers for the forecasting model. Margins are expressed in
/// points of Democratic two-party lead (e.g. +3 = D+3); probabilities in [0,1].
/// Margin ↔ probability conversions use the same fat-tailed scaled Student-t the
/// Monte Carlo draws its errors from, so per-race probabilities and the chamber
/// simulation describe one distribution.
/// </summary>
public static class ForecastMath
{
    /// <summary>Degrees of freedom for the model's error distribution (shared with the simulator).</summary>
    public const int ErrorDegreesOfFreedom = 5;

    // A standard t(5) has variance 5/3; this rescales it to unit variance so "SD" keeps
    // meaning SD. Must match MonteCarloSimulator.SampleTError.
    private static readonly double TScale = Math.Sqrt((ErrorDegreesOfFreedom - 2.0) / ErrorDegreesOfFreedom);

    // t(5) pdf normalizing constant: Γ(3) / (√(5π)·Γ(5/2)).
    private const double TPdfCoefficient = 0.3796066898;

    /// <summary>CDF of the standard Student-t with 5 degrees of freedom (closed form).</summary>
    public static double TCdf(double x)
    {
        var theta = Math.Atan(x / Math.Sqrt(5));
        var cos = Math.Cos(theta);
        return 0.5 + (theta + Math.Sin(theta) * (cos + 2.0 / 3.0 * cos * cos * cos)) / Math.PI;
    }

    /// <summary>Inverse t(5) CDF via Newton's method (normal quantile as the initial guess).</summary>
    public static double TInverse(double p)
    {
        p = Math.Clamp(p, 1e-9, 1 - 1e-9);
        var x = NormalInverse(p);
        for (int i = 0; i < 20; i++)
        {
            var pdf = TPdfCoefficient * Math.Pow(1 + x * x / 5, -3);
            var step = (TCdf(x) - p) / pdf;
            x -= step;
            if (Math.Abs(step) < 1e-10) break;
        }
        return x;
    }
    /// <summary>Standard normal CDF (Abramowitz &amp; Stegun 7.1.26 approximation).</summary>
    public static double NormalCdf(double x)
    {
        const double a1 = 0.254829592, a2 = -0.284496736, a3 = 1.421413741;
        const double a4 = -1.453152027, a5 = 1.061405429, p = 0.3275911;

        int sign = x < 0 ? -1 : 1;
        double z = Math.Abs(x) / Math.Sqrt(2);
        double t = 1.0 / (1.0 + p * z);
        double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-z * z);
        return 0.5 * (1.0 + sign * y);
    }

    /// <summary>
    /// Inverse standard normal CDF / probit (Acklam's rational approximation).
    /// Maps a probability in (0,1) back to a z-score.
    /// </summary>
    public static double NormalInverse(double p)
    {
        p = Math.Clamp(p, 1e-6, 1 - 1e-6);

        double[] a = { -3.969683028665376e+01, 2.209460984245205e+02, -2.759285104469687e+02, 1.383577518672690e+02, -3.066479806614716e+01, 2.506628277459239e+00 };
        double[] b = { -5.447609879822406e+01, 1.615858368580409e+02, -1.556989798598866e+02, 6.680131188771972e+01, -1.328068155288572e+01 };
        double[] c = { -7.784894002430293e-03, -3.223964580411365e-01, -2.400758277161838e+00, -2.549732539343734e+00, 4.374664141464968e+00, 2.938163982698783e+00 };
        double[] d = { 7.784695709041462e-03, 3.224671290700398e-01, 2.445134137142996e+00, 3.754408661907416e+00 };

        const double pLow = 0.02425;
        const double pHigh = 1 - pLow;
        double q, r;

        if (p < pLow)
        {
            q = Math.Sqrt(-2 * Math.Log(p));
            return (((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) /
                   ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
        }
        if (p <= pHigh)
        {
            q = p - 0.5;
            r = q * q;
            return (((((a[0] * r + a[1]) * r + a[2]) * r + a[3]) * r + a[4]) * r + a[5]) * q /
                   (((((b[0] * r + b[1]) * r + b[2]) * r + b[3]) * r + b[4]) * r + 1);
        }
        q = Math.Sqrt(-2 * Math.Log(1 - p));
        return -(((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) /
               ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
    }

    /// <summary>Converts a Dem win probability into an implied Dem margin, given a margin SD.</summary>
    public static double ProbabilityToMargin(double demProbability, double marginStdDev)
        => TInverse(demProbability) * marginStdDev * TScale;

    /// <summary>Converts a Dem margin (points) into a Dem win probability, given a margin SD.</summary>
    public static double MarginToProbability(double demMargin, double marginStdDev)
        => TCdf(demMargin / (marginStdDev * TScale));
}
