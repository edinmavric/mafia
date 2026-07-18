using MafiaGame.Domain.Players;

namespace MafiaGame.Domain.Night
{
    /// <summary>
    /// Private result delivered only to the Detective. Per the confirmed rules the result is
    /// binary: whether the investigated target belongs to the Mafia faction. The exact role is
    /// intentionally NOT exposed.
    /// </summary>
    public sealed class DetectiveResult
    {
        public DetectiveResult(PlayerId target, bool isMafia)
        {
            Target = target;
            IsMafia = isMafia;
        }

        public PlayerId Target { get; }

        public bool IsMafia { get; }
    }
}
