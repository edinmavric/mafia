using System;
using System.Collections.Generic;
using MafiaGame.Domain.Players;

namespace MafiaGame.Domain.Voting
{
    /// <summary>
    /// Tallies day votes and decides the outcome. Invalid votes (from the dead, for the dead,
    /// duplicates, or — during a revote — for a non-candidate) are ignored with a recorded
    /// reason; the first valid vote per voter is the one that counts.
    ///
    /// Pass <paramref name="candidateRestriction"/> to run a revote limited to the previously
    /// tied candidates. In a revote a renewed tie resolves to no elimination; in the first round
    /// a tie asks for a revote.
    /// </summary>
    public sealed class VotingService
    {
        public VotingResolution Resolve(
            IReadOnlyCollection<Vote> votes,
            IReadOnlyCollection<PlayerId> alivePlayers,
            IReadOnlyCollection<PlayerId> candidateRestriction = null)
        {
            if (votes == null)
            {
                throw new ArgumentNullException(nameof(votes));
            }

            if (alivePlayers == null)
            {
                throw new ArgumentNullException(nameof(alivePlayers));
            }

            var alive = new HashSet<PlayerId>(alivePlayers);
            HashSet<PlayerId> candidates =
                candidateRestriction != null ? new HashSet<PlayerId>(candidateRestriction) : null;

            var rejections = new List<string>();
            var alreadyVoted = new HashSet<PlayerId>();
            var tally = new Dictionary<PlayerId, int>();

            foreach (Vote vote in votes)
            {
                if (!alive.Contains(vote.Voter))
                {
                    rejections.Add($"Vote from non-living player {vote.Voter} ignored.");
                    continue;
                }

                if (!alive.Contains(vote.Target))
                {
                    rejections.Add($"Vote for non-living target {vote.Target} ignored.");
                    continue;
                }

                if (candidates != null && !candidates.Contains(vote.Target))
                {
                    rejections.Add($"Vote for non-candidate {vote.Target} ignored.");
                    continue;
                }

                if (!alreadyVoted.Add(vote.Voter))
                {
                    rejections.Add($"Duplicate vote from {vote.Voter} ignored.");
                    continue;
                }

                tally.TryGetValue(vote.Target, out int current);
                tally[vote.Target] = current + 1;
            }

            if (tally.Count == 0)
            {
                return new VotingResolution(VoteOutcome.NoElimination, null, Array.Empty<PlayerId>(), rejections);
            }

            int max = 0;
            foreach (KeyValuePair<PlayerId, int> entry in tally)
            {
                if (entry.Value > max)
                {
                    max = entry.Value;
                }
            }

            var top = new List<PlayerId>();
            foreach (KeyValuePair<PlayerId, int> entry in tally)
            {
                if (entry.Value == max)
                {
                    top.Add(entry.Key);
                }
            }

            if (top.Count == 1)
            {
                return new VotingResolution(VoteOutcome.Eliminated, top[0], Array.Empty<PlayerId>(), rejections);
            }

            bool isRevote = candidateRestriction != null;
            VoteOutcome tieOutcome = isRevote ? VoteOutcome.NoElimination : VoteOutcome.TieRequiresRevote;
            return new VotingResolution(tieOutcome, null, top, rejections);
        }
    }
}
