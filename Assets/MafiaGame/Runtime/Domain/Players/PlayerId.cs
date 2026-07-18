using System;

namespace MafiaGame.Domain.Players
{
    /// <summary>
    /// Stable, engine-agnostic identifier for a player within a match.
    /// This is intentionally decoupled from Unity object ids and from any
    /// networking connection id, so domain rules can be tested in isolation.
    /// It is an immutable value type with value equality, making it safe to
    /// use as a dictionary key.
    /// </summary>
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        /// <summary>The underlying positive identifier.</summary>
        public int Value { get; }

        /// <summary>
        /// Creates a player identifier.
        /// </summary>
        /// <param name="value">A positive integer. Zero and negatives are invalid.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is not positive.</exception>
        public PlayerId(int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), value, "PlayerId must be a positive integer.");
            }

            Value = value;
        }

        public bool Equals(PlayerId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is PlayerId other && Equals(other);

        public override int GetHashCode() => Value;

        public override string ToString() => $"Player({Value})";

        public static bool operator ==(PlayerId left, PlayerId right) => left.Equals(right);

        public static bool operator !=(PlayerId left, PlayerId right) => !left.Equals(right);
    }
}
