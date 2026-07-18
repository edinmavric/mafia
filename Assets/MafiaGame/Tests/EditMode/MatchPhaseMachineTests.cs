using MafiaGame.Domain.Matches;
using NUnit.Framework;

namespace MafiaGame.Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for the authoritative phase state machine.
    /// These are pure C# tests: no GameObject, scene, network session,
    /// real clock, or nondeterministic randomness is required.
    /// </summary>
    public sealed class MatchPhaseMachineTests
    {
        [Test]
        public void NewMachine_DefaultsToLobby()
        {
            var machine = new MatchPhaseMachine();

            Assert.AreEqual(MatchPhase.Lobby, machine.CurrentPhase);
        }

        [Test]
        public void TryAdvanceTo_ValidTransition_AdvancesAndReportsAllowed()
        {
            var machine = new MatchPhaseMachine(MatchPhase.Lobby);

            PhaseTransitionResult result = machine.TryAdvanceTo(MatchPhase.RoleReveal);

            Assert.IsTrue(result.IsAllowed);
            Assert.AreEqual(MatchPhase.Lobby, result.FromPhase);
            Assert.AreEqual(MatchPhase.RoleReveal, result.ToPhase);
            Assert.IsNull(result.RejectionReason);
            Assert.AreEqual(MatchPhase.RoleReveal, machine.CurrentPhase);
        }

        [Test]
        public void TryAdvanceTo_InvalidTransition_IsRejectedAndPhaseUnchanged()
        {
            var machine = new MatchPhaseMachine(MatchPhase.Lobby);

            PhaseTransitionResult result = machine.TryAdvanceTo(MatchPhase.Night);

            Assert.IsFalse(result.IsAllowed);
            Assert.AreEqual(MatchPhase.Lobby, result.FromPhase);
            Assert.AreEqual(MatchPhase.Night, result.ToPhase);
            Assert.IsNotNull(result.RejectionReason);
            Assert.IsNotEmpty(result.RejectionReason);
            Assert.AreEqual(MatchPhase.Lobby, machine.CurrentPhase, "Phase must not change on a rejected transition.");
        }

        [Test]
        public void GameOver_IsTerminal_NoOutgoingTransitions()
        {
            var machine = new MatchPhaseMachine(MatchPhase.GameOver);

            PhaseTransitionResult result = machine.TryAdvanceTo(MatchPhase.Lobby);

            Assert.IsFalse(result.IsAllowed);
            Assert.AreEqual(MatchPhase.GameOver, machine.CurrentPhase);
        }

        [Test]
        public void VotingResolution_CanLoopBackToNight()
        {
            var machine = new MatchPhaseMachine(MatchPhase.VotingResolution);

            PhaseTransitionResult result = machine.TryAdvanceTo(MatchPhase.Night);

            Assert.IsTrue(result.IsAllowed);
            Assert.AreEqual(MatchPhase.Night, machine.CurrentPhase);
        }

        [Test]
        public void FullDayNightCycle_IsWalkableEndToEnd()
        {
            var machine = new MatchPhaseMachine(MatchPhase.Lobby);

            MatchPhase[] happyPath =
            {
                MatchPhase.RoleReveal,
                MatchPhase.Night,
                MatchPhase.NightResolution,
                MatchPhase.DayAnnouncement,
                MatchPhase.DayDiscussion,
                MatchPhase.Voting,
                MatchPhase.VotingResolution,
                MatchPhase.GameOver,
            };

            foreach (MatchPhase next in happyPath)
            {
                PhaseTransitionResult result = machine.TryAdvanceTo(next);
                Assert.IsTrue(result.IsAllowed, $"Expected transition to {next} to be allowed.");
                Assert.AreEqual(next, machine.CurrentPhase);
            }
        }

        [Test]
        public void CanTransition_IsPure_DoesNotRequireInstance()
        {
            Assert.IsTrue(MatchPhaseMachine.CanTransition(MatchPhase.Night, MatchPhase.NightResolution));
            Assert.IsFalse(MatchPhaseMachine.CanTransition(MatchPhase.Night, MatchPhase.Voting));
        }
    }
}
