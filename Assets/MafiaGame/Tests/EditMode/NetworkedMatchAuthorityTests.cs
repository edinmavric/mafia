using System.Collections.Generic;
using System.Linq;
using MafiaGame.Application.Matches;
using MafiaGame.Domain.Matches;
using MafiaGame.Domain.Roles;
using NUnit.Framework;

namespace MafiaGame.Tests.EditMode
{
    /// <summary>
    /// Security-critical tests for the host-authoritative brain. They drive the authority purely
    /// through its seat API (no live network) and derive each seat's role from the returned private
    /// payloads, so they stay deterministic regardless of the shuffle for a given seed.
    /// </summary>
    public sealed class NetworkedMatchAuthorityTests
    {
        private const int Seed = 12345;

        private static MatchConfiguration Config(bool revealRole = false)
        {
            // 7 players allows 2 mafia plus both special roles (Doctor + Detective).
            MatchConfigurationResult result = MatchConfiguration.Create(
                playerCount: 7, mafiaCount: 2, includeDoctor: true, includeDetective: true,
                revealRoleOnElimination: revealRole);
            Assert.IsTrue(result.IsValid, result.Error);
            return result.Configuration;
        }

        private static (NetworkedMatchAuthority authority, IReadOnlyList<PrivateRoleInfo> payloads) Start(
            bool revealRole = false)
        {
            var authority = new NetworkedMatchAuthority();
            IReadOnlyList<PrivateRoleInfo> payloads = authority.StartMatch(7, Config(revealRole), Seed);
            return (authority, payloads);
        }

        private static int SeatOf(IReadOnlyList<PrivateRoleInfo> payloads, Role role) =>
            payloads.First(p => p.Role == role).Seat;

        private static List<int> SeatsOf(IReadOnlyList<PrivateRoleInfo> payloads, Role role) =>
            payloads.Where(p => p.Role == role).Select(p => p.Seat).ToList();

        [Test]
        public void StartMatch_AssignsRoleCountsFromConfiguration()
        {
            var (_, payloads) = Start();

            Assert.AreEqual(7, payloads.Count);
            Assert.AreEqual(2, payloads.Count(p => p.Role == Role.Mafia));
            Assert.AreEqual(1, payloads.Count(p => p.Role == Role.Doctor));
            Assert.AreEqual(1, payloads.Count(p => p.Role == Role.Detective));
            Assert.AreEqual(3, payloads.Count(p => p.Role == Role.Citizen));
        }

        [Test]
        public void StartMatch_EachSeatHasExactlyOnePayload()
        {
            var (_, payloads) = Start();

            CollectionAssert.AreEquivalent(Enumerable.Range(0, 7), payloads.Select(p => p.Seat));
        }

        [Test]
        public void StartMatch_OnlyMafiaLearnTeammates_AndNeverThemselves()
        {
            var (_, payloads) = Start();
            List<int> mafiaSeats = SeatsOf(payloads, Role.Mafia);

            foreach (PrivateRoleInfo info in payloads)
            {
                if (info.Role == Role.Mafia)
                {
                    IEnumerable<int> expected = mafiaSeats.Where(s => s != info.Seat);
                    CollectionAssert.AreEquivalent(expected, info.MafiaTeammateSeats);
                    CollectionAssert.DoesNotContain(info.MafiaTeammateSeats, info.Seat);
                }
                else
                {
                    CollectionAssert.IsEmpty(info.MafiaTeammateSeats);
                }
            }
        }

        [Test]
        public void SubmitMafiaTarget_BeforeNight_IsRejectedAsWrongPhase()
        {
            var (authority, payloads) = Start();
            int mafia = SeatsOf(payloads, Role.Mafia).First();
            int citizen = SeatOf(payloads, Role.Citizen);

            IntentResult result = authority.SubmitMafiaTarget(mafia, citizen);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(IntentRejection.WrongPhase, result.Reason);
        }

        [Test]
        public void SubmitMafiaTarget_FromMafiaAtNight_IsAccepted()
        {
            var (authority, payloads) = Start();
            authority.ConfirmRolesSeen();
            int mafia = SeatsOf(payloads, Role.Mafia).First();
            int citizen = SeatOf(payloads, Role.Citizen);

            IntentResult result = authority.SubmitMafiaTarget(mafia, citizen);

            Assert.IsTrue(result.Accepted);
        }

        [Test]
        public void SubmitMafiaTarget_FromNonMafia_IsRejectedAsNotAllowed()
        {
            var (authority, payloads) = Start();
            authority.ConfirmRolesSeen();
            int citizen = SeatOf(payloads, Role.Citizen);
            int otherCitizen = SeatsOf(payloads, Role.Citizen)[1];

            IntentResult result = authority.SubmitMafiaTarget(citizen, otherCitizen);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(IntentRejection.NotAllowed, result.Reason);
        }

        [Test]
        public void SubmitMafiaTarget_OnSelf_IsRejectedAsInvalidTarget()
        {
            var (authority, payloads) = Start();
            authority.ConfirmRolesSeen();
            int mafia = SeatsOf(payloads, Role.Mafia).First();

            IntentResult result = authority.SubmitMafiaTarget(mafia, mafia);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(IntentRejection.InvalidTarget, result.Reason);
        }

        [Test]
        public void SubmitMafiaTarget_OutOfRange_IsRejectedAsInvalidTarget()
        {
            var (authority, payloads) = Start();
            authority.ConfirmRolesSeen();
            int mafia = SeatsOf(payloads, Role.Mafia).First();

            IntentResult result = authority.SubmitMafiaTarget(mafia, 99);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(IntentRejection.InvalidTarget, result.Reason);
        }

        [Test]
        public void SubmitDoctorProtect_OnSelf_IsAllowed()
        {
            var (authority, payloads) = Start();
            authority.ConfirmRolesSeen();
            int doctor = SeatOf(payloads, Role.Doctor);

            IntentResult result = authority.SubmitDoctorProtect(doctor, doctor);

            Assert.IsTrue(result.Accepted);
        }

        [Test]
        public void ResolveNight_KillsMafiaTarget_WhenUnprotected()
        {
            var (authority, payloads) = Start();
            authority.ConfirmRolesSeen();
            int mafia = SeatsOf(payloads, Role.Mafia).First();
            int citizen = SeatOf(payloads, Role.Citizen);
            authority.SubmitMafiaTarget(mafia, citizen);

            NightOutcome outcome = authority.ResolveNight();

            Assert.IsTrue(outcome.Public.SomeoneDied);
            Assert.AreEqual(citizen, outcome.Public.KilledSeat);
        }

        [Test]
        public void ResolveNight_NoDeath_WhenDoctorProtectsTheTarget()
        {
            var (authority, payloads) = Start();
            authority.ConfirmRolesSeen();
            int mafia = SeatsOf(payloads, Role.Mafia).First();
            int doctor = SeatOf(payloads, Role.Doctor);
            int citizen = SeatOf(payloads, Role.Citizen);
            authority.SubmitMafiaTarget(mafia, citizen);
            authority.SubmitDoctorProtect(doctor, citizen);

            NightOutcome outcome = authority.ResolveNight();

            Assert.IsFalse(outcome.Public.SomeoneDied);
            Assert.AreEqual(-1, outcome.Public.KilledSeat);
        }

        [Test]
        public void ResolveNight_RevealsRole_OnlyWhenHostEnabledIt()
        {
            var (hidden, hiddenPayloads) = Start(revealRole: false);
            hidden.ConfirmRolesSeen();
            int mafiaH = SeatsOf(hiddenPayloads, Role.Mafia).First();
            int citizenH = SeatOf(hiddenPayloads, Role.Citizen);
            hidden.SubmitMafiaTarget(mafiaH, citizenH);
            NightOutcome hiddenOutcome = hidden.ResolveNight();
            Assert.IsNull(hiddenOutcome.Public.RevealedRole);

            var (shown, shownPayloads) = Start(revealRole: true);
            shown.ConfirmRolesSeen();
            int mafiaS = SeatsOf(shownPayloads, Role.Mafia).First();
            int citizenS = SeatOf(shownPayloads, Role.Citizen);
            shown.SubmitMafiaTarget(mafiaS, citizenS);
            NightOutcome shownOutcome = shown.ResolveNight();
            Assert.AreEqual(Role.Citizen, shownOutcome.Public.RevealedRole);
        }

        [Test]
        public void ResolveNight_DetectiveInvestigatingMafia_GetsPrivateMafiaResult()
        {
            var (authority, payloads) = Start();
            authority.ConfirmRolesSeen();
            int detective = SeatOf(payloads, Role.Detective);
            int mafia = SeatsOf(payloads, Role.Mafia).First();
            authority.SubmitDetectiveInvestigate(detective, mafia);

            NightOutcome outcome = authority.ResolveNight();

            Assert.IsNotNull(outcome.DetectivePrivate);
            Assert.AreEqual(detective, outcome.DetectivePrivate.DetectiveSeat);
            Assert.AreEqual(mafia, outcome.DetectivePrivate.TargetSeat);
            Assert.IsTrue(outcome.DetectivePrivate.IsMafia);
        }

        [Test]
        public void ResolveNight_DetectiveInvestigatingCitizen_GetsPrivateNotMafiaResult()
        {
            var (authority, payloads) = Start();
            authority.ConfirmRolesSeen();
            int detective = SeatOf(payloads, Role.Detective);
            int citizen = SeatOf(payloads, Role.Citizen);
            authority.SubmitDetectiveInvestigate(detective, citizen);

            NightOutcome outcome = authority.ResolveNight();

            Assert.IsNotNull(outcome.DetectivePrivate);
            Assert.IsFalse(outcome.DetectivePrivate.IsMafia);
        }

        [Test]
        public void ResolveNight_NoDetectivePrivate_WhenDetectiveDidNotAct()
        {
            var (authority, payloads) = Start();
            authority.ConfirmRolesSeen();
            int mafia = SeatsOf(payloads, Role.Mafia).First();
            int citizen = SeatOf(payloads, Role.Citizen);
            authority.SubmitMafiaTarget(mafia, citizen);

            NightOutcome outcome = authority.ResolveNight();

            Assert.IsNull(outcome.DetectivePrivate);
        }

        [Test]
        public void MarkDisconnected_DropsTheDetectiveIntent_SoNightStillResolves()
        {
            var (authority, payloads) = Start();
            authority.ConfirmRolesSeen();
            int detective = SeatOf(payloads, Role.Detective);
            int citizen = SeatOf(payloads, Role.Citizen);
            authority.SubmitDetectiveInvestigate(detective, citizen);

            authority.MarkDisconnected(detective);
            NightOutcome outcome = authority.ResolveNight();

            Assert.IsNull(outcome.DetectivePrivate);
        }

        [Test]
        public void SubmitFromDisconnectedSeat_IsRejectedAsNotAllowed()
        {
            var (authority, payloads) = Start();
            authority.ConfirmRolesSeen();
            int mafia = SeatsOf(payloads, Role.Mafia).First();
            int citizen = SeatOf(payloads, Role.Citizen);

            authority.MarkDisconnected(mafia);
            IntentResult result = authority.SubmitMafiaTarget(mafia, citizen);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(IntentRejection.NotAllowed, result.Reason);
        }
    }
}
