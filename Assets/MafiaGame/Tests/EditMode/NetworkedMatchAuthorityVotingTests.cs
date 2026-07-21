using System.Collections.Generic;
using System.Linq;
using MafiaGame.Application.Matches;
using MafiaGame.Domain.Matches;
using MafiaGame.Domain.Roles;
using MafiaGame.Domain.Voting;
using NUnit.Framework;

namespace MafiaGame.Tests.EditMode
{
    /// <summary>
    /// Day-vote tests for the host-authoritative brain. Like the night tests they drive the seat API
    /// directly and derive every seat from the returned private payloads, so they never depend on the
    /// shuffle for a given seed. Vote targets are Citizens on purpose: eliminating the Mafia would end
    /// the match and change the phase the test is asserting on.
    /// </summary>
    public sealed class NetworkedMatchAuthorityVotingTests
    {
        private const int Seed = 4242;

        private static (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) Start(
            int playerCount = 7, bool revealRole = false)
        {
            bool special = playerCount >= 7;
            MatchConfigurationResult config = MatchConfiguration.Create(
                playerCount,
                mafiaCount: 1,
                includeDoctor: special,
                includeDetective: special,
                revealRoleOnElimination: revealRole);
            Assert.IsTrue(config.IsValid, config.Error);

            var authority = new NetworkedMatchAuthority();
            IReadOnlyList<PrivateRoleInfo> payloads =
                authority.StartMatch(playerCount, config.Configuration, Seed);
            return (authority, payloads);
        }

        /// <summary>Plays a quiet night (nobody acts, nobody dies) and opens the voting round.</summary>
        private static void ToVoting(NetworkedMatchAuthority authority)
        {
            authority.ConfirmRolesSeen();
            authority.ResolveNight();
            authority.ContinueToDiscussion();
            authority.BeginVoting();
        }

        private static int SeatOf(IReadOnlyList<PrivateRoleInfo> payloads, Role role) =>
            payloads.First(p => p.Role == role).Seat;

        /// <summary>Citizen seats in ascending order — safe vote targets that never end the match.</summary>
        private static List<int> CitizenSeats(IReadOnlyList<PrivateRoleInfo> payloads) =>
            payloads.Where(p => p.Role == Role.Citizen).Select(p => p.Seat).OrderBy(seat => seat).ToList();

        private static List<int> SeatsExcept(IReadOnlyList<PrivateRoleInfo> payloads, params int[] excluded) =>
            payloads.Select(p => p.Seat).Where(seat => !excluded.Contains(seat)).OrderBy(seat => seat).ToList();

        [Test]
        public void SubmitVote_BeforeVotingPhase_IsRejectedAsWrongPhase()
        {
            var (authority, _) = Start();

            IntentResult result = authority.SubmitVote(senderSeat: 0, targetSeat: 1);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(IntentRejection.WrongPhase, result.Reason);
        }

        [Test]
        public void SubmitVote_FromLivingSeat_IsAccepted()
        {
            var (authority, payloads) = Start();
            ToVoting(authority);
            int target = CitizenSeats(payloads)[0];

            IntentResult result = authority.SubmitVote(SeatsExcept(payloads, target)[0], target);

            Assert.IsTrue(result.Accepted);
        }

        [Test]
        public void SubmitVote_ForSeatOutOfRange_IsRejectedAsInvalidTarget()
        {
            var (authority, _) = Start();
            ToVoting(authority);

            IntentResult result = authority.SubmitVote(senderSeat: 0, targetSeat: 99);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(IntentRejection.InvalidTarget, result.Reason);
        }

        [Test]
        public void SubmitVote_FromDeadSeat_IsRejectedAsNotAllowed()
        {
            var (authority, payloads) = Start();
            int mafiaSeat = SeatOf(payloads, Role.Mafia);
            int victimSeat = CitizenSeats(payloads)[0];

            authority.ConfirmRolesSeen();
            authority.SubmitMafiaTarget(mafiaSeat, victimSeat);
            authority.ResolveNight();
            authority.ContinueToDiscussion();
            authority.BeginVoting();

            IntentResult result = authority.SubmitVote(victimSeat, mafiaSeat);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(IntentRejection.NotAllowed, result.Reason);
        }

        [Test]
        public void SubmitVote_ForDeadTarget_IsRejectedAsInvalidTarget()
        {
            var (authority, payloads) = Start();
            int mafiaSeat = SeatOf(payloads, Role.Mafia);
            int victimSeat = CitizenSeats(payloads)[0];

            authority.ConfirmRolesSeen();
            authority.SubmitMafiaTarget(mafiaSeat, victimSeat);
            authority.ResolveNight();
            authority.ContinueToDiscussion();
            authority.BeginVoting();

            int voterSeat = SeatsExcept(payloads, mafiaSeat, victimSeat)[0];
            IntentResult result = authority.SubmitVote(voterSeat, victimSeat);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(IntentRejection.InvalidTarget, result.Reason);
        }

        [Test]
        public void ResolveVoting_WithMajority_EliminatesTargetAndReturnsToNight()
        {
            var (authority, payloads) = Start();
            ToVoting(authority);
            int target = CitizenSeats(payloads)[0];
            List<int> voters = SeatsExcept(payloads, target);

            authority.SubmitVote(voters[0], target);
            authority.SubmitVote(voters[1], target);

            VotingPublicResult result = authority.ResolveVoting();

            Assert.AreEqual(VoteOutcome.Eliminated, result.Outcome);
            Assert.AreEqual(target, result.EliminatedSeat);
            CollectionAssert.DoesNotContain(authority.AliveSeats(), target);
            Assert.AreEqual(MatchPhase.Night, authority.CurrentPhase);
        }

        [Test]
        public void ResolveVoting_RevealsRoleOnlyWhenHostEnabledIt()
        {
            var (hidden, hiddenPayloads) = Start(revealRole: false);
            ToVoting(hidden);
            int hiddenTarget = CitizenSeats(hiddenPayloads)[0];
            hidden.SubmitVote(SeatsExcept(hiddenPayloads, hiddenTarget)[0], hiddenTarget);
            Assert.IsFalse(hidden.ResolveVoting().RevealedRole.HasValue);

            var (shown, shownPayloads) = Start(revealRole: true);
            ToVoting(shown);
            int shownTarget = CitizenSeats(shownPayloads)[0];
            shown.SubmitVote(SeatsExcept(shownPayloads, shownTarget)[0], shownTarget);
            Assert.AreEqual(Role.Citizen, shown.ResolveVoting().RevealedRole);
        }

        [Test]
        public void ResolveVoting_WithNoVotes_EliminatesNobody()
        {
            var (authority, _) = Start();
            ToVoting(authority);

            VotingPublicResult result = authority.ResolveVoting();

            Assert.AreEqual(VoteOutcome.NoElimination, result.Outcome);
            Assert.IsFalse(result.SomeoneEliminated);
            Assert.AreEqual(MatchPhase.Night, authority.CurrentPhase);
        }

        [Test]
        public void ResolveVoting_LastVotePerSeatCounts()
        {
            var (authority, payloads) = Start();
            ToVoting(authority);
            List<int> citizens = CitizenSeats(payloads);
            int first = citizens[0];
            int second = citizens[1];
            int voter = SeatsExcept(payloads, first, second)[0];

            authority.SubmitVote(voter, first);
            authority.SubmitVote(voter, second); // the voter changes their mind

            VotingPublicResult result = authority.ResolveVoting();

            Assert.AreEqual(VoteOutcome.Eliminated, result.Outcome);
            Assert.AreEqual(second, result.EliminatedSeat);
        }

        [Test]
        public void ResolveVoting_Tie_EntersTheTieBreakerAndArmsRevoteOnTiedSeats()
        {
            var (authority, payloads) = Start();
            ToVoting(authority);
            List<int> citizens = CitizenSeats(payloads);
            int first = citizens[0];
            int second = citizens[1];
            List<int> voters = SeatsExcept(payloads, first, second);

            authority.SubmitVote(voters[0], first);
            authority.SubmitVote(voters[1], second);

            VotingPublicResult result = authority.ResolveVoting();

            Assert.AreEqual(VoteOutcome.TieRequiresRevote, result.Outcome);
            CollectionAssert.AreEquivalent(new[] { first, second }, result.TiedSeats);
            Assert.AreEqual(MatchPhase.TieBreaker, authority.CurrentPhase);
            Assert.IsTrue(authority.IsRevote);
            CollectionAssert.AreEquivalent(new[] { first, second }, authority.VoteCandidateSeats());
        }

        [Test]
        public void SubmitVote_DuringTheTieBreakerDefense_IsRejected()
        {
            var (authority, payloads) = Start();
            ToVoting(authority);
            List<int> citizens = CitizenSeats(payloads);
            List<int> voters = SeatsExcept(payloads, citizens[0], citizens[1]);
            authority.SubmitVote(voters[0], citizens[0]);
            authority.SubmitVote(voters[1], citizens[1]);
            authority.ResolveVoting();

            // The defense is talking time, not voting time: the revote has not opened yet.
            IntentResult result = authority.SubmitVote(voters[0], citizens[0]);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(IntentRejection.WrongPhase, result.Reason);
        }

        [Test]
        public void BeginRevote_OpensVotingOnTheTiedSeatsOnly()
        {
            var (authority, payloads) = Start();
            ToVoting(authority);
            List<int> citizens = CitizenSeats(payloads);
            List<int> voters = SeatsExcept(payloads, citizens[0], citizens[1]);
            authority.SubmitVote(voters[0], citizens[0]);
            authority.SubmitVote(voters[1], citizens[1]);
            authority.ResolveVoting();

            authority.BeginRevote();

            Assert.AreEqual(MatchPhase.Voting, authority.CurrentPhase);
            Assert.AreEqual(0, authority.SubmittedVoteCount, "The defense must not carry votes over.");
            CollectionAssert.AreEquivalent(
                new[] { citizens[0], citizens[1] }, authority.VoteCandidateSeats());
        }

        [Test]
        public void SubmitVote_DuringRevote_ForNonCandidate_IsRejectedAsInvalidTarget()
        {
            var (authority, payloads) = Start();
            ToVoting(authority);
            List<int> citizens = CitizenSeats(payloads);
            List<int> voters = SeatsExcept(payloads, citizens[0], citizens[1]);
            authority.SubmitVote(voters[0], citizens[0]);
            authority.SubmitVote(voters[1], citizens[1]);
            authority.ResolveVoting();
            authority.BeginRevote();

            // citizens[2] is alive but was not part of the tie.
            IntentResult result = authority.SubmitVote(voters[0], citizens[2]);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(IntentRejection.InvalidTarget, result.Reason);
        }

        [Test]
        public void ResolveVoting_TieAgainInRevote_EliminatesNobody()
        {
            var (authority, payloads) = Start();
            ToVoting(authority);
            List<int> citizens = CitizenSeats(payloads);
            List<int> voters = SeatsExcept(payloads, citizens[0], citizens[1]);
            authority.SubmitVote(voters[0], citizens[0]);
            authority.SubmitVote(voters[1], citizens[1]);
            authority.ResolveVoting();
            authority.BeginRevote();

            authority.SubmitVote(voters[0], citizens[0]);
            authority.SubmitVote(voters[1], citizens[1]);
            VotingPublicResult revote = authority.ResolveVoting();

            Assert.AreEqual(VoteOutcome.NoElimination, revote.Outcome);
            Assert.IsFalse(revote.SomeoneEliminated);
            Assert.AreEqual(MatchPhase.Night, authority.CurrentPhase);
            Assert.IsFalse(authority.IsRevote);
        }

        [Test]
        public void ResolveVoting_IgnoresVotesFromDisconnectedSeats()
        {
            var (authority, payloads) = Start();
            ToVoting(authority);
            List<int> citizens = CitizenSeats(payloads);
            int ignoredTarget = citizens[0];
            int countedTarget = citizens[1];
            List<int> voters = SeatsExcept(payloads, ignoredTarget, countedTarget);

            authority.SubmitVote(voters[0], ignoredTarget);
            authority.SubmitVote(voters[1], ignoredTarget);
            authority.MarkDisconnected(voters[0]);
            authority.MarkDisconnected(voters[1]);
            authority.SubmitVote(voters[2], countedTarget);

            VotingPublicResult result = authority.ResolveVoting();

            Assert.AreEqual(VoteOutcome.Eliminated, result.Outcome);
            Assert.AreEqual(countedTarget, result.EliminatedSeat, "only the connected seat's vote counts");
        }

        [Test]
        public void SubmitVote_FromDisconnectedSeat_IsRejectedAsNotAllowed()
        {
            var (authority, payloads) = Start();
            ToVoting(authority);
            int target = CitizenSeats(payloads)[0];
            int voter = SeatsExcept(payloads, target)[0];
            authority.MarkDisconnected(voter);

            IntentResult result = authority.SubmitVote(voter, target);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(IntentRejection.NotAllowed, result.Reason);
        }

        [Test]
        public void ResolveVoting_EliminatingTheLastMafia_EndsTheMatchForTheTown()
        {
            var (authority, payloads) = Start(playerCount: 4);
            int mafiaSeat = SeatOf(payloads, Role.Mafia);
            int victimSeat = CitizenSeats(payloads)[0];

            authority.ConfirmRolesSeen();
            authority.SubmitMafiaTarget(mafiaSeat, victimSeat);
            authority.ResolveNight();
            authority.ContinueToDiscussion();
            authority.BeginVoting();

            foreach (int seat in authority.AliveSeats().Where(seat => seat != mafiaSeat))
            {
                authority.SubmitVote(seat, mafiaSeat);
            }

            VotingPublicResult result = authority.ResolveVoting();

            Assert.AreEqual(mafiaSeat, result.EliminatedSeat);
            Assert.AreEqual(MatchPhase.GameOver, authority.CurrentPhase);
            Assert.AreEqual(GameOutcome.TownWins, authority.Outcome);
        }
    }
}
