namespace MafiaGame.Application.Matches
{
    /// <summary>
    /// The Detective's private investigation result, delivered only to the Detective's seat over a
    /// targeted channel. Per the confirmed rules the answer is binary: Mafia or not-Mafia.
    /// </summary>
    public sealed class DetectivePrivateResult
    {
        public DetectivePrivateResult(int detectiveSeat, int targetSeat, bool isMafia)
        {
            DetectiveSeat = detectiveSeat;
            TargetSeat = targetSeat;
            IsMafia = isMafia;
        }

        /// <summary>The seat that receives this result (the investigating Detective).</summary>
        public int DetectiveSeat { get; }

        /// <summary>The investigated seat.</summary>
        public int TargetSeat { get; }

        /// <summary>True when the investigated seat belongs to the Mafia faction.</summary>
        public bool IsMafia { get; }
    }
}
