using System.Collections.Generic;
using System.Linq;
using MafiaGame.Application.Matches;
using MafiaGame.Domain.Matches;
using MafiaGame.Domain.Roles;
using NUnit.Framework;

namespace MafiaGame.Tests.EditMode
{
    /// <summary>
    /// Phase-timer tests. Time is fed in as a number, so a phase can expire instantly and the tests
    /// stay deterministic — no sleeping, no real clock. They assert both halves of the contract:
    /// what a timeout advances into, and that the timer never advances anything on its own beyond
    /// the single expiry it reports.
    /// </summary>
    public sealed class NetworkedMatchAuthorityTimerTests
    {
        private const int Seed = 4242;

        /// <summary>Short, unambiguous durations so each assertion names the phase it is testing.</summary>
        private static MatchTimings Timings() => MatchTimings.Create(
            roleRevealSeconds: 10d,
            nightSeconds: 20d,
            announcementSeconds: 5d,
            discussionSeconds: 30d,
            votingSeconds: 15d).Timings;

        private static (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) Start(
            int playerCount = 7)
        {
            // Special roles need a big enough lobby, so a small match runs with Citizens only.
            bool special = playerCount >= 7;
            MatchConfigurationResult config = MatchConfiguration.Create(
                playerCount,
                mafiaCount: 1,
                includeDoctor: special,
                includeDetective: special,
                revealRoleOnElimination: false);
            Assert.IsTrue(config.IsValid, config.Error);

            var authority = new NetworkedMatchAuthority();
            IReadOnlyList<PrivateRoleInfo> payloads =
                authority.StartMatch(playerCount, config.Configuration, Seed, Timings());
            return (authority, payloads);
        }

        /// <summary>Runs the reported advance so a test can walk the match forward by timeout only.</summary>
        private static void Apply(NetworkedMatchAuthority authority, PhaseAdvance advance)
        {
            switch (advance)
            {
                case PhaseAdvance.ConfirmRolesSeen: authority.ConfirmRolesSeen(); break;
                case PhaseAdvance.ResolveNight: authority.ResolveNight(); break;
                case PhaseAdvance.ContinueToDiscussion: authority.ContinueToDiscussion(); break;
                case PhaseAdvance.BeginVoting: authority.BeginVoting(); break;
                case PhaseAdvance.ResolveVoting: authority.ResolveVoting(); break;
            }
        }

        /// <summary>Expires the current phase and carries out whatever fell due.</summary>
        private static PhaseAdvance Expire(NetworkedMatchAuthority authority)
        {
            PhaseAdvance advance = authority.Tick(MatchTimings.MaxSeconds);
            Apply(authority, advance);
            return advance;
        }

        [Test]
        public void StartMatch_ArmsTheRoleRevealCountdown()
        {
            (NetworkedMatchAuthority authority, _) = Start();

            Assert.IsTrue(authority.HasDeadline);
            Assert.AreEqual(10d, authority.RemainingSeconds, 0.001d);
        }

        [Test]
        public void Tick_BeforeTheDeadline_AdvancesNothingButBurnsTime()
        {
            (NetworkedMatchAuthority authority, _) = Start();

            Assert.AreEqual(PhaseAdvance.None, authority.Tick(4d));
            Assert.AreEqual(6d, authority.RemainingSeconds, 0.001d);
            Assert.AreEqual(MatchPhase.RoleReveal, authority.CurrentPhase);
        }

        [Test]
        public void RoleRevealTimeout_EntersTheNightAndRearmsWithTheNightDuration()
        {
            (NetworkedMatchAuthority authority, _) = Start();

            Assert.AreEqual(PhaseAdvance.ConfirmRolesSeen, Expire(authority));

            Assert.AreEqual(MatchPhase.Night, authority.CurrentPhase);
            Assert.AreEqual(20d, authority.RemainingSeconds, 0.001d);
        }

        [Test]
        public void NightTimeout_ResolvesTheNightEvenWhenNobodyActed()
        {
            (NetworkedMatchAuthority authority, _) = Start();
            Expire(authority); // role reveal

            Assert.AreEqual(PhaseAdvance.ResolveNight, Expire(authority));

            Assert.AreEqual(MatchPhase.DayAnnouncement, authority.CurrentPhase);
            Assert.AreEqual(7, authority.AliveSeats().Count, "A quiet night must not kill anyone.");
        }

        [Test]
        public void NightTimeout_StillAppliesTheMafiaTargetThatWasSubmittedInTime()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start();
            Expire(authority); // role reveal

            int mafiaSeat = payloads.First(p => p.Role == Role.Mafia).Seat;
            int victimSeat = payloads.First(p => p.Role == Role.Citizen).Seat;
            Assert.IsTrue(authority.SubmitMafiaTarget(mafiaSeat, victimSeat).Accepted);

            Expire(authority);

            CollectionAssert.DoesNotContain(authority.AliveSeats(), victimSeat);
        }

        [Test]
        public void TimeoutChain_WalksAnnouncementIntoDiscussionIntoVoting()
        {
            (NetworkedMatchAuthority authority, _) = Start();
            Expire(authority); // role reveal
            Expire(authority); // night

            Assert.AreEqual(PhaseAdvance.ContinueToDiscussion, Expire(authority));
            Assert.AreEqual(MatchPhase.DayDiscussion, authority.CurrentPhase);
            Assert.AreEqual(30d, authority.RemainingSeconds, 0.001d);

            Assert.AreEqual(PhaseAdvance.BeginVoting, Expire(authority));
            Assert.AreEqual(MatchPhase.Voting, authority.CurrentPhase);
            Assert.AreEqual(15d, authority.RemainingSeconds, 0.001d);
        }

        [Test]
        public void VotingTimeout_TalliesTheVotesThatWereCast()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start();
            Expire(authority); // role reveal
            Expire(authority); // night
            Expire(authority); // announcement
            Expire(authority); // discussion → voting

            List<int> citizens = payloads
                .Where(p => p.Role == Role.Citizen).Select(p => p.Seat).OrderBy(seat => seat).ToList();
            int target = citizens[0];
            foreach (int voter in payloads.Select(p => p.Seat).Where(seat => seat != target).Take(3))
            {
                Assert.IsTrue(authority.SubmitVote(voter, target).Accepted);
            }

            Assert.AreEqual(PhaseAdvance.ResolveVoting, Expire(authority));

            CollectionAssert.DoesNotContain(authority.AliveSeats(), target);
            Assert.AreEqual(MatchPhase.Night, authority.CurrentPhase);
        }

        [Test]
        public void VotingTimeout_WithNoVotes_EliminatesNobodyAndStillOpensTheNight()
        {
            (NetworkedMatchAuthority authority, _) = Start();
            Expire(authority); // role reveal
            Expire(authority); // night
            Expire(authority); // announcement
            Expire(authority); // discussion → voting

            Expire(authority); // voting

            Assert.AreEqual(7, authority.AliveSeats().Count);
            Assert.AreEqual(MatchPhase.Night, authority.CurrentPhase);
        }

        [Test]
        public void TiedVotingTimeout_RestartsTheClockForTheRevote()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start();
            Expire(authority); // role reveal
            Expire(authority); // night
            Expire(authority); // announcement
            Expire(authority); // discussion → voting

            List<int> citizens = payloads
                .Where(p => p.Role == Role.Citizen).Select(p => p.Seat).OrderBy(seat => seat).ToList();
            List<int> voters = payloads.Select(p => p.Seat)
                .Where(seat => seat != citizens[0] && seat != citizens[1]).OrderBy(seat => seat).ToList();
            Assert.IsTrue(authority.SubmitVote(voters[0], citizens[0]).Accepted);
            Assert.IsTrue(authority.SubmitVote(voters[1], citizens[1]).Accepted);

            Expire(authority);

            Assert.AreEqual(MatchPhase.Voting, authority.CurrentPhase);
            Assert.IsTrue(authority.IsRevote);
            Assert.AreEqual(15d, authority.RemainingSeconds, 0.001d, "The revote gets a fresh clock.");
        }

        [Test]
        public void Tick_AfterTheMatchEnds_ReportsNothingAndStopsCountingDown()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start(4);
            Expire(authority); // role reveal
            Expire(authority); // night
            Expire(authority); // announcement
            Expire(authority); // discussion → voting

            int mafiaSeat = payloads.First(p => p.Role == Role.Mafia).Seat;
            foreach (int voter in payloads.Select(p => p.Seat).Where(seat => seat != mafiaSeat))
            {
                Assert.IsTrue(authority.SubmitVote(voter, mafiaSeat).Accepted);
            }

            Expire(authority);

            Assert.AreEqual(MatchPhase.GameOver, authority.CurrentPhase);
            Assert.IsFalse(authority.HasDeadline, "A finished match must not keep a countdown.");
            Assert.AreEqual(PhaseAdvance.None, authority.Tick(MatchTimings.MaxSeconds));
        }

        [Test]
        public void Tick_FiresOnlyOncePerExpiry()
        {
            (NetworkedMatchAuthority authority, _) = Start();

            Assert.AreEqual(PhaseAdvance.ConfirmRolesSeen, authority.Tick(MatchTimings.MaxSeconds));
            Assert.AreEqual(PhaseAdvance.None, authority.Tick(MatchTimings.MaxSeconds),
                "A single expiry must not advance the match twice.");
        }

        [Test]
        public void ManualAdvance_RearmsTheClockForTheNewPhase()
        {
            (NetworkedMatchAuthority authority, _) = Start();

            // The host skipped the reveal early; the night must still get its full duration.
            authority.ConfirmRolesSeen();

            Assert.AreEqual(MatchPhase.Night, authority.CurrentPhase);
            Assert.AreEqual(20d, authority.RemainingSeconds, 0.001d);
        }

        /// <summary>One frame's worth of time — enough to notice an early finish, nowhere near a timeout.</summary>
        private static PhaseAdvance Frame(NetworkedMatchAuthority authority) => authority.Tick(0.02d);

        [Test]
        public void Night_EndsAsSoonAsEveryLivingNightRoleHasActed()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start();
            Expire(authority); // role reveal

            int mafia = payloads.First(p => p.Role == Role.Mafia).Seat;
            int doctor = payloads.First(p => p.Role == Role.Doctor).Seat;
            int detective = payloads.First(p => p.Role == Role.Detective).Seat;
            int victim = payloads.First(p => p.Role == Role.Citizen).Seat;

            Assert.IsTrue(authority.SubmitMafiaTarget(mafia, victim).Accepted);
            Assert.AreEqual(PhaseAdvance.None, Frame(authority), "The Doctor and Detective still owe an action.");

            Assert.IsTrue(authority.SubmitDoctorProtect(doctor, doctor).Accepted);
            Assert.AreEqual(PhaseAdvance.None, Frame(authority), "The Detective still owes an action.");

            Assert.IsTrue(authority.SubmitDetectiveInvestigate(detective, mafia).Accepted);
            Assert.AreEqual(PhaseAdvance.ResolveNight, Frame(authority));
        }

        [Test]
        public void Night_WithoutSpecialRoles_EndsOnTheMafiaTargetAlone()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start(4);
            Expire(authority); // role reveal

            int mafia = payloads.First(p => p.Role == Role.Mafia).Seat;
            int victim = payloads.First(p => p.Role == Role.Citizen).Seat;

            Assert.AreEqual(PhaseAdvance.None, Frame(authority));
            Assert.IsTrue(authority.SubmitMafiaTarget(mafia, victim).Accepted);

            Assert.AreEqual(PhaseAdvance.ResolveNight, Frame(authority));
        }

        [Test]
        public void Night_DoesNotWaitForADisconnectedNightRole()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start();
            Expire(authority); // role reveal

            int mafia = payloads.First(p => p.Role == Role.Mafia).Seat;
            int doctor = payloads.First(p => p.Role == Role.Doctor).Seat;
            int detective = payloads.First(p => p.Role == Role.Detective).Seat;

            authority.MarkDisconnected(doctor);
            authority.MarkDisconnected(detective);
            Assert.IsTrue(authority.SubmitMafiaTarget(mafia, payloads.First(p => p.Role == Role.Citizen).Seat).Accepted);

            Assert.AreEqual(PhaseAdvance.ResolveNight, Frame(authority));
        }

        [Test]
        public void Voting_EndsAsSoonAsEveryLivingSeatHasVoted()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start(4);
            Expire(authority); // role reveal
            Expire(authority); // night
            Expire(authority); // announcement
            Expire(authority); // discussion → voting

            List<int> seats = payloads.Select(p => p.Seat).OrderBy(seat => seat).ToList();
            int target = payloads.First(p => p.Role == Role.Citizen).Seat;

            for (int i = 0; i < seats.Count; i++)
            {
                Assert.IsTrue(authority.SubmitVote(seats[i], target).Accepted);
                bool last = i == seats.Count - 1;
                Assert.AreEqual(
                    last ? PhaseAdvance.ResolveVoting : PhaseAdvance.None,
                    Frame(authority),
                    last ? "The last vote must end the round." : "A missing vote must keep the round open.");
            }
        }

        [Test]
        public void Voting_DoesNotWaitForDeadOrDisconnectedSeats()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start();
            Expire(authority); // role reveal

            // Kill one seat during the night so the day only waits on the survivors.
            int mafia = payloads.First(p => p.Role == Role.Mafia).Seat;
            List<int> citizens = payloads
                .Where(p => p.Role == Role.Citizen).Select(p => p.Seat).OrderBy(seat => seat).ToList();
            Assert.IsTrue(authority.SubmitMafiaTarget(mafia, citizens[0]).Accepted);
            Expire(authority); // night
            Expire(authority); // announcement
            Expire(authority); // discussion → voting

            int absent = citizens[1];
            authority.MarkDisconnected(absent);

            List<int> voters = authority.AliveSeats().Where(seat => seat != absent).ToList();
            for (int i = 0; i < voters.Count; i++)
            {
                Assert.IsTrue(authority.SubmitVote(voters[i], citizens[2]).Accepted);
            }

            Assert.AreEqual(PhaseAdvance.ResolveVoting, Frame(authority),
                "The dead seat and the disconnected seat must not hold the round open.");
        }

        [Test]
        public void Voting_StaysOpenWhileALivingSeatHasNotVoted()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start();
            Expire(authority); // role reveal
            Expire(authority); // night
            Expire(authority); // announcement
            Expire(authority); // discussion → voting

            int target = payloads.First(p => p.Role == Role.Citizen).Seat;
            foreach (int voter in authority.AliveSeats().Take(authority.AliveSeats().Count - 1))
            {
                Assert.IsTrue(authority.SubmitVote(voter, target).Accepted);
            }

            Assert.AreEqual(PhaseAdvance.None, Frame(authority));
            Assert.IsTrue(authority.HasDeadline, "The clock must keep running for the last voter.");
        }

        [Test]
        public void Create_RejectsDurationsOutsideTheAllowedRange()
        {
            MatchTimingsResult tooShort = MatchTimings.Create(10d, 1d, 5d, 30d, 15d);
            MatchTimingsResult tooLong = MatchTimings.Create(10d, 20d, 5d, 30d, MatchTimings.MaxSeconds + 1d);

            Assert.IsFalse(tooShort.IsValid);
            Assert.IsFalse(tooLong.IsValid);
            Assert.IsTrue(MatchTimings.Create(10d, 20d, 5d, 30d, 15d).IsValid);
        }
    }
}
