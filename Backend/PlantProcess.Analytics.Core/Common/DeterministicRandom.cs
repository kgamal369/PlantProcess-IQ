namespace PlantProcess.Analytics.Core.Common;

/// <summary>Deterministic, cross-machine reproducible PRNG (xorshift64*) used by bootstrap and tests.</summary>
public sealed class DeterministicRandom
{
    private ulong _state;
    public DeterministicRandom(ulong seed) => _state = seed == 0UL ? 0x9E3779B97F4A7C15UL : seed;

    public ulong NextUInt64()
    {
        _state ^= _state >> 12;
        _state ^= _state << 25;
        _state ^= _state >> 27;
        return _state * 0x2545F4914F6CDD1DUL;
    }

    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        return (int)(NextDouble() * maxExclusive);
    }

    public double NextUniform(double min, double max) => min + (max - min) * NextDouble();

    public double NextGaussian(double mean = 0.0, double stdDev = 1.0)
    {
        double u1 = 1.0 - NextDouble();
        double u2 = 1.0 - NextDouble();
        double mag = Math.Sqrt(-2.0 * Math.Log(u1));
        return mean + stdDev * (mag * Math.Cos(2.0 * Math.PI * u2));
    }
}