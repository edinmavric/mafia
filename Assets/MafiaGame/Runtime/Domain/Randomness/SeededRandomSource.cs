using System;
using System.Collections.Generic;

namespace MafiaGame.Domain.Randomness
{
    /// <summary>
    /// Deterministic <see cref="IRandomSource"/> backed by <see cref="System.Random"/> with an
    /// explicit seed. The same seed always produces the same sequence, which is what makes
    /// role assignment reproducible in tests. This is intentionally NOT cryptographic and NOT
    /// time-seeded; the authority owns the seed so clients cannot predict or control it.
    /// </summary>
    public sealed class SeededRandomSource : IRandomSource
    {
        private readonly Random _random;

        public SeededRandomSource(int seed)
        {
            _random = new Random(seed);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (minInclusive > maxExclusive)
            {
                throw new ArgumentException(
                    $"minInclusive ({minInclusive}) must not exceed maxExclusive ({maxExclusive}).");
            }

            return _random.Next(minInclusive, maxExclusive);
        }

        public void Shuffle<T>(IList<T> list)
        {
            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
