using System.Collections.Generic;

namespace MafiaGame.Domain.Randomness
{
    /// <summary>
    /// Abstraction over randomness so domain rules stay deterministic and testable.
    /// Production code injects a seeded implementation; tests inject a fixed seed.
    /// Domain code must never use <c>UnityEngine.Random</c> or system-time seeding.
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>
        /// Returns an integer in the range [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>).
        /// </summary>
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>
        /// Shuffles <paramref name="list"/> in place using an unbiased Fisher–Yates shuffle.
        /// </summary>
        void Shuffle<T>(IList<T> list);
    }
}
