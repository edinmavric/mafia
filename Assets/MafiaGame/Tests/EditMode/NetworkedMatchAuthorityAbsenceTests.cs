using System.Collections.Generic;
using System.Linq;
using MafiaGame.Application.Matches;
using MafiaGame.Domain.Matches;
using MafiaGame.Domain.Roles;
using NUnit.Framework;

namespace MafiaGame.Tests.EditMode
{
    /// <summary>
    /// Disconnect and rejoin tests. Two separate rules are checked here and it matters that they are
    /// not confused: an absent player is skipped **immediately** (nobody ever waits on them), while
    /// the grace period only decides when they stop being a player at all.
    /// </summary>
    public sealed class NetworkedMatchAuthorityAbsenceTests
    {
        private const int Seed = 4242;

        private static (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) Start(
            int playerCount = 7)
        {
            bool special = playerCount >= 7;
            MatchConfigurationResult config = MatchConfiguration.Create(
                playerCount, 1, special, special, revealRoleOnElimination: false);
            Assert.IsTrue(config.IsValid, config.Error);

            var authority = new NetworkedMatchAuthority();
            IReadOnlyList<PrivateRoleInfo> payloads =
                authority.StartMatch(playerCount, config.Configuration, Seed);
            authority.ConfirmRolesSeen();
            return (authority, payloads);
        }

        private static int SeatOf(IReadOnlyList<PrivateRoleInfo> payloads, Role role) =>
            payloads.First(p => p.Role == role).Seat;

        private static List<int> CitizenSeats(IReadOnlyList<PrivateRoleInfo> payloads) =>
            payloads.Where(p => p.Role == Role.Citizen).Select(p => p.Seat).OrderBy(s => s).ToList();

        [Test]
        public void Disconnecting_StartsTheGracePeriod()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start();
            int seat = CitizenSeats(payloads)[0];

            authority.MarkDisconnected(seat);

            Assert.IsTrue(authority.IsDisconnected(seat));
            Assert.AreEqual(NetworkedMatchAuthority.AbandonAfterSeconds, authority.AbsenceRemaining(seat), 0.001d);
        }

        [Test]
        public void AnAbsentPlayer_IsSkippedImmediately_WithoutWaitingForTheGracePeriod()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start();
            int mafia = SeatOf(payloads, Role.Mafia);
            int doctor = SeatOf(payloads, Role.Doctor);
            int detective = SeatOf(payloads, Role.Detective);

            authority.MarkDisconnected(doctor);
            authority.MarkDisconnected(detective);
            Assert.IsTrue(authority.SubmitMafiaTarget(mafia, CitizenSeats(payloads)[0]).Accepted);

            // One frame, far short of the grace period.
            Assert.AreEqual(PhaseAdvance.ResolveNight, authority.Tick(0.02d));
            Assert.AreEqual(NetworkedMatchAuthority.AbandonAfterSeconds, authority.AbsenceRemaining(doctor), 0.1d);
        }

        [Test]
        public void TickAbsence_BeforeTheGracePeriodEnds_RemovesNobody()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start();
            int seat = CitizenSeats(payloads)[0];
            authority.MarkDisconnected(seat);

            IReadOnlyList<int> forfeited = authority.TickAbsence(NetworkedMatchAuthority.AbandonAfterSeconds - 1d);

            CollectionAssert.IsEmpty(forfeited);
            CollectionAssert.Contains(authority.AliveSeats(), seat);
        }

        [Test]
        public void TickAbsence_AfterTheGracePeriod_RemovesThePlayerFromTheMatch()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start();
            int seat = CitizenSeats(payloads)[0];
            authority.MarkDisconnected(seat);

            IReadOnlyList<int> forfeited = authority.TickAbsence(NetworkedMatchAuthority.AbandonAfterSeconds);

            CollectionAssert.AreEqual(new[] { seat }, forfeited);
            CollectionAssert.DoesNotContain(authority.AliveSeats(), seat);
        }

        [Test]
        public void AForfeitedSeat_IsReportedOnlyOnce()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start();
            int seat = CitizenSeats(payloads)[0];
            authority.MarkDisconnected(seat);
            authority.TickAbsence(NetworkedMatchAuthority.AbandonAfterSeconds);

            CollectionAssert.IsEmpty(authority.TickAbsence(NetworkedMatchAuthority.AbandonAfterSeconds));
        }

        [Test]
        public void Reconnecting_InTime_KeepsThePlayerInTheMatch()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start();
            int seat = CitizenSeats(payloads)[0];
            authority.MarkDisconnected(seat);
            authority.TickAbsence(NetworkedMatchAuthority.AbandonAfterSeconds - 5d);

            authority.MarkReconnected(seat);
            authority.TickAbsence(NetworkedMatchAuthority.AbandonAfterSeconds * 2d);

            Assert.IsFalse(authority.IsDisconnected(seat));
            CollectionAssert.Contains(authority.AliveSeats(), seat);
        }

        [Test]
        public void AReturningPlayer_MayActAgain()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start();
            int mafia = SeatOf(payloads, Role.Mafia);
            int target = CitizenSeats(payloads)[0];

            authority.MarkDisconnected(mafia);
            Assert.IsFalse(authority.SubmitMafiaTarget(mafia, target).Accepted,
                "An absent player must not be able to act.");

            authority.MarkReconnected(mafia);

            Assert.IsTrue(authority.SubmitMafiaTarget(mafia, target).Accepted);
        }

        [Test]
        public void AReturningPlayer_GetsTheSameSeatAndRoleBack()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start();
            int seat = SeatOf(payloads, Role.Detective);

            authority.MarkDisconnected(seat);
            authority.MarkReconnected(seat);

            PrivateRoleInfo restored = authority.RoleInfoFor(seat);
            Assert.AreEqual(seat, restored.Seat);
            Assert.AreEqual(Role.Detective, restored.Role);
        }

        [Test]
        public void ARestoredMafiaPayload_StillListsTheirTeammates()
        {
            MatchConfigurationResult config = MatchConfiguration.Create(
                8, mafiaCount: 2, includeDoctor: true, includeDetective: true, revealRoleOnElimination: false);
            Assert.IsTrue(config.IsValid, config.Error);
            var authority = new NetworkedMatchAuthority();
            IReadOnlyList<PrivateRoleInfo> payloads = authority.StartMatch(8, config.Configuration, Seed);

            PrivateRoleInfo original = payloads.First(p => p.Role == Role.Mafia);
            PrivateRoleInfo restored = authority.RoleInfoFor(original.Seat);

            CollectionAssert.AreEqual(original.MafiaTeammateSeats, restored.MafiaTeammateSeats);
            Assert.IsNotEmpty(restored.MafiaTeammateSeats);
        }

        [Test]
        public void ANonMafiaPayload_NeverCarriesTeammates()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start();

            foreach (PrivateRoleInfo payload in payloads.Where(p => p.Role != Role.Mafia))
            {
                CollectionAssert.IsEmpty(authority.RoleInfoFor(payload.Seat).MafiaTeammateSeats,
                    $"Seat {payload.Seat} ({payload.Role}) must learn nothing about the Mafia.");
            }
        }

        [Test]
        public void ForfeitingTheLastMafia_DoesNotEndTheMatchOnTheSpot()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start();
            int mafia = SeatOf(payloads, Role.Mafia);

            authority.MarkDisconnected(mafia);
            authority.TickAbsence(NetworkedMatchAuthority.AbandonAfterSeconds);

            // Announcing the win here would tell everyone the player who dropped was the Mafia.
            Assert.AreEqual(MatchPhase.Night, authority.CurrentPhase);
            Assert.AreEqual(GameOutcome.None, authority.Outcome);
        }

        [Test]
        public void TheWinIsDeclared_AtTheNextResolution_AfterTheLastMafiaLeaves()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start();
            int mafia = SeatOf(payloads, Role.Mafia);

            authority.MarkDisconnected(mafia);
            authority.TickAbsence(NetworkedMatchAuthority.AbandonAfterSeconds);
            authority.ResolveNight();

            Assert.AreEqual(MatchPhase.GameOver, authority.CurrentPhase);
            Assert.AreEqual(GameOutcome.TownWins, authority.Outcome);
        }

        [Test]
        public void AForfeitedPlayer_NoLongerHoldsUpTheNight()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start();
            int mafia = SeatOf(payloads, Role.Mafia);
            int doctor = SeatOf(payloads, Role.Doctor);
            int detective = SeatOf(payloads, Role.Detective);

            Assert.IsTrue(authority.SubmitMafiaTarget(mafia, CitizenSeats(payloads)[0]).Accepted);
            Assert.IsTrue(authority.SubmitDoctorProtect(doctor, doctor).Accepted);
            Assert.AreEqual(PhaseAdvance.None, authority.Tick(0.02d), "The Detective still owes an action.");

            authority.MarkDisconnected(detective);
            authority.TickAbsence(NetworkedMatchAuthority.AbandonAfterSeconds);

            Assert.AreEqual(PhaseAdvance.ResolveNight, authority.Tick(0.02d));
        }

        [Test]
        public void AForfeitedPlayer_CannotBeVotedForOrVote()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start();
            authority.ResolveNight();
            authority.ContinueToDiscussion();
            authority.BeginVoting();

            List<int> citizens = CitizenSeats(payloads);
            int gone = citizens[0];
            authority.MarkDisconnected(gone);
            authority.TickAbsence(NetworkedMatchAuthority.AbandonAfterSeconds);

            Assert.AreEqual(IntentRejection.NotAllowed, authority.SubmitVote(gone, citizens[1]).Reason);
            Assert.AreEqual(IntentRejection.InvalidTarget, authority.SubmitVote(citizens[1], gone).Reason);
            CollectionAssert.DoesNotContain(authority.VoteCandidateSeats(), gone);
        }

        [Test]
        public void AForfeitedSeat_IsNoLongerReportedAsDisconnected()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start();
            int seat = CitizenSeats(payloads)[0];
            authority.MarkDisconnected(seat);
            authority.TickAbsence(NetworkedMatchAuthority.AbandonAfterSeconds);

            // "Disconnected" means temporarily away and rejoinable. A forfeited seat is gone for
            // good, so a returning player must not be able to reclaim their eliminated seat.
            Assert.IsFalse(authority.IsDisconnected(seat));
        }

        [Test]
        public void ForfeitDoesNothing_OnceTheMatchIsOver()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start(4);
            int mafia = SeatOf(payloads, Role.Mafia);
            authority.MarkDisconnected(mafia);
            authority.TickAbsence(NetworkedMatchAuthority.AbandonAfterSeconds);
            authority.ResolveNight();
            Assert.AreEqual(MatchPhase.GameOver, authority.CurrentPhase);

            int other = payloads.First(p => p.Seat != mafia).Seat;
            authority.MarkDisconnected(other);

            CollectionAssert.IsEmpty(authority.TickAbsence(NetworkedMatchAuthority.AbandonAfterSeconds));
        }

        [Test]
        public void TheMatchEndsAtTwoPlayers_WithTheWinnerTheRulesName()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start();
            int mafia = SeatOf(payloads, Role.Mafia);

            // Everyone leaves except the Mafia and one villager: parity, so the match is settled.
            int survivor = payloads.First(p => p.Seat != mafia).Seat;
            foreach (int seat in payloads.Select(p => p.Seat).Where(s => s != mafia && s != survivor))
            {
                authority.MarkDisconnected(seat);
            }

            authority.TickAbsence(NetworkedMatchAuthority.AbandonAfterSeconds);

            Assert.AreEqual(2, authority.AliveSeats().Count);
            Assert.AreEqual(MatchPhase.GameOver, authority.CurrentPhase);

            // Stopping early must not rob the Mafia of the win they already had.
            Assert.AreEqual(GameOutcome.MafiaWins, authority.Outcome);
            Assert.IsFalse(authority.HasDeadline, "A finished match has no clock left to run.");
        }

        [Test]
        public void TwoVillagersLeft_EndsTheMatchAsATownWin()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start();
            List<int> citizens = CitizenSeats(payloads);

            // The Mafia is among those who walk out, so no Mafia are left at all.
            foreach (int seat in payloads.Select(p => p.Seat)
                         .Where(s => s != citizens[0] && s != citizens[1]))
            {
                authority.MarkDisconnected(seat);
            }

            authority.TickAbsence(NetworkedMatchAuthority.AbandonAfterSeconds);

            Assert.AreEqual(MatchPhase.GameOver, authority.CurrentPhase);
            Assert.AreEqual(GameOutcome.TownWins, authority.Outcome);
        }

        [Test]
        public void AnEmptyTable_IsRecordedAsAbandonedRatherThanATownWin()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start();
            foreach (int seat in payloads.Select(p => p.Seat))
            {
                authority.MarkDisconnected(seat);
            }

            authority.TickAbsence(NetworkedMatchAuthority.AbandonAfterSeconds);

            // With nobody alive the win rule would still say "no Mafia remain, Town wins". That is
            // an artefact, not a result: nobody was there to win it.
            Assert.AreEqual(MatchPhase.GameOver, authority.CurrentPhase);
            Assert.AreEqual(GameOutcome.Abandoned, authority.Outcome);
        }

        [Test]
        public void ThreePlayersLeft_IsStillAPlayableMatch()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start();
            int mafia = SeatOf(payloads, Role.Mafia);
            List<int> citizens = CitizenSeats(payloads);

            // Four of seven leave; the Mafia and two villagers play on.
            foreach (int seat in payloads.Select(p => p.Seat)
                         .Where(s => s != mafia && s != citizens[0] && s != citizens[1]))
            {
                authority.MarkDisconnected(seat);
            }

            authority.TickAbsence(NetworkedMatchAuthority.AbandonAfterSeconds);

            Assert.AreEqual(3, authority.AliveSeats().Count);
            Assert.AreEqual(MatchPhase.Night, authority.CurrentPhase);
            Assert.AreEqual(GameOutcome.None, authority.Outcome);
        }

        [Test]
        public void ABlinkedConnection_DoesNotEndTheMatch()
        {
            (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) = Start();
            foreach (int seat in payloads.Select(p => p.Seat).Skip(1))
            {
                authority.MarkDisconnected(seat);
            }

            // Still inside the grace period: they are absent, not gone.
            authority.TickAbsence(NetworkedMatchAuthority.AbandonAfterSeconds - 1d);

            Assert.AreEqual(MatchPhase.Night, authority.CurrentPhase);
            Assert.AreEqual(GameOutcome.None, authority.Outcome);
        }
    }
}
