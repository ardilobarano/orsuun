using System.Security.Cryptography;
using Orsuun.Rules;

namespace Orsuun.Server.Game;

/// <summary>Production randomness: unpredictable, so no client can time a request to a known roll.</summary>
public sealed class CryptoRandom : IRandom
{
    public static readonly CryptoRandom Instance = new();

    public int NextInt(int maxExclusive) => RandomNumberGenerator.GetInt32(maxExclusive);
}
