using System.Collections.Generic;
using MafiaGame.Domain.Players;
using MafiaGame.Domain.Voting;
using NUnit.Framework;

namespace MafiaGame.Tests.EditMode
{
    public sealed class VotingServiceTests
    {
        private readonly VotingService _service = new VotingService();

        private static PlayerId P(int value) => new PlayerId(value);

        private static IReadOnlyCollection<PlayerId> Alive(params int[] ids)
        {
            var list = new List<PlayerId>(ids.Length);
            foreach (int id in ids)
            {
                list.Add(new PlayerId(id));
            }

            return list;
        }

        [Test]
        public void ClearMajority_EliminatesTarget()
        {
            var votes = new List<Vote> { new Vote(P(1), P(3)), new Vote(P(2), P(3)), new Vote(P(4), P(1)) };

            VotingResolution result = _service.Resolve(votes, Alive(1, 2, 3, 4));

            Assert.AreEqual(VoteOutcome.Eliminated, result.Outcome);
            Assert.AreEqual(P(3), result.EliminatedPlayer);
        }

        [Test]
        public void FirstRoundTie_RequestsRevoteWithCandidates()
        {
            var votes = new List<Vote> { new Vote(P(1), P(3)), new Vote(P(2), P(4)) };

            VotingResolution result = _service.Resolve(votes, Alive(1, 2, 3, 4));

            Assert.AreEqual(VoteOutcome.TieRequiresRevote, result.Outcome);
            CollectionAssert.AreEquivalent(new[] { P(3), P(4) }, result.TiedCandidates);
        }

        [Test]
        public void RevoteStillTied_NoElimination()
        {
            var votes = new List<Vote> { new Vote(P(1), P(3)), new Vote(P(2), P(4)) };
            var candidates = new List<PlayerId> { P(3), P(4) };

            VotingResolution result = _service.Resolve(votes, Alive(1, 2, 3, 4), candidates);

            Assert.AreEqual(VoteOutcome.NoElimination, result.Outcome);
        }

        [Test]
        public void RevoteResolved_EliminatesTarget()
        {
            var votes = new List<Vote> { new Vote(P(1), P(3)), new Vote(P(2), P(3)), new Vote(P(4), P(4)) };
            var candidates = new List<PlayerId> { P(3), P(4) };

            VotingResolution result = _service.Resolve(votes, Alive(1, 2, 3, 4), candidates);

            Assert.AreEqual(VoteOutcome.Eliminated, result.Outcome);
            Assert.AreEqual(P(3), result.EliminatedPlayer);
        }

        [Test]
        public void VoteFromDeadPlayer_IsIgnored()
        {
            // Player 5 is not alive.
            var votes = new List<Vote> { new Vote(P(5), P(3)), new Vote(P(1), P(2)) };

            VotingResolution result = _service.Resolve(votes, Alive(1, 2, 3, 4));

            Assert.AreEqual(VoteOutcome.Eliminated, result.Outcome);
            Assert.AreEqual(P(2), result.EliminatedPlayer);
            Assert.IsNotEmpty(result.Rejections);
        }

        [Test]
        public void DuplicateVoteFromSameVoter_OnlyFirstCounts()
        {
            var votes = new List<Vote> { new Vote(P(1), P(2)), new Vote(P(1), P(3)) };

            VotingResolution result = _service.Resolve(votes, Alive(1, 2, 3, 4));

            Assert.AreEqual(VoteOutcome.Eliminated, result.Outcome);
            Assert.AreEqual(P(2), result.EliminatedPlayer);
            Assert.IsNotEmpty(result.Rejections);
        }

        [Test]
        public void VoteForDeadTarget_IsIgnored()
        {
            var votes = new List<Vote> { new Vote(P(1), P(5)), new Vote(P(2), P(3)) };

            VotingResolution result = _service.Resolve(votes, Alive(1, 2, 3, 4));

            Assert.AreEqual(P(3), result.EliminatedPlayer);
            Assert.IsNotEmpty(result.Rejections);
        }

        [Test]
        public void AllAbstain_NoElimination()
        {
            VotingResolution result = _service.Resolve(new List<Vote>(), Alive(1, 2, 3, 4));

            Assert.AreEqual(VoteOutcome.NoElimination, result.Outcome);
        }
    }
}
