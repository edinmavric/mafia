using System;
using System.Collections.Generic;
using System.Linq;
using MafiaGame.Application.Matches;
using MafiaGame.Domain.Matches;
using MafiaGame.Domain.Night;
using MafiaGame.Domain.Players;
using MafiaGame.Domain.Roles;
using MafiaGame.Domain.Voting;
using NUnit.Framework;

namespace MafiaGame.Tests.EditMode
{
    public sealed class LocalMatchDriverTests
    {
        private static PlayerId P(int value) => new PlayerId(value);

        private static IReadOnlyList<PlayerId> Roster(int count)
        {
            var list = new List<PlayerId>(count);
            for (int i = 1; i <= count; i++)
            {
                list.Add(new PlayerId(i));
            }

            return list;
        }

        [Test]
        public void ResolveNight_BeforeStart_ThrowsPhaseGate()
        {
            var driver = new LocalMatchDriver();

            Assert.Throws<InvalidOperationException>(
                () => driver.ResolveNight(new NightActions(null, null, null)));
        }

        [Test]
        public void BeginVoting_InWrongPhase_ThrowsPhaseGate()
        {
            var driver = new LocalMatchDriver();
            driver.Start(MatchConfiguration.Create(4, 1, false, false, false).Configuration, Roster(4), 1);

            // Still in RoleReveal, not DayDiscussion.
            Assert.Throws<InvalidOperationException>(() => driver.BeginVoting());
        }

        [Test]
        public void Start_EntersRoleRevealThenNight()
        {
            var driver = new LocalMatchDriver();
            driver.Start(MatchConfiguration.Create(7, 1, true, true, true).Configuration, Roster(7), 42);

            Assert.AreEqual(MatchPhase.RoleReveal, driver.CurrentPhase);
            driver.ConfirmRolesSeen();
            Assert.AreEqual(MatchPhase.Night, driver.CurrentPhase);
        }

        [Test]
        public void Voting_Tie_RequestsRevoteAndStaysInVotingPhase()
        {
            var driver = new LocalMatchDriver();
            driver.Start(MatchConfiguration.Create(4, 1, false, false, false).Configuration, Roster(4), 5);
            driver.ConfirmRolesSeen();
            driver.ResolveNight(new NightActions(null, null, null)); // no kill; all four alive
            driver.ContinueToDiscussion();
            driver.BeginVoting();

            var votes = new List<Vote> { new Vote(P(1), P(2)), new Vote(P(2), P(1)) };
            VotingResolution resolution = driver.ResolveVoting(votes);

            Assert.AreEqual(VoteOutcome.TieRequiresRevote, resolution.Outcome);
            Assert.AreEqual(MatchPhase.Voting, driver.CurrentPhase);
        }

        [Test]
        public void FullMatch_TerminatesInGameOverWithAWinner()
        {
            var driver = new LocalMatchDriver();
            driver.Start(MatchConfiguration.Create(7, 1, true, true, true).Configuration, Roster(7), 12345);
            driver.ConfirmRolesSeen();

            int guard = 0;
            while (driver.CurrentPhase != MatchPhase.GameOver && guard++ < 200)
            {
                // Night: mafia kills the first living non-mafia so the game makes progress.
                driver.ResolveNight(new NightActions(FirstLivingTown(driver), null, null));
                if (driver.CurrentPhase == MatchPhase.GameOver)
                {
                    break;
                }

                driver.ContinueToDiscussion();
                driver.BeginVoting();

                VotingResolution resolution = driver.ResolveVoting(VotesAgainstFirstLiving(driver, null));
                if (resolution.Outcome == VoteOutcome.TieRequiresRevote)
                {
                    driver.ResolveVoting(
                        VotesAgainstFirstLiving(driver, resolution.TiedCandidates), resolution.TiedCandidates);
                }
            }

            Assert.AreEqual(MatchPhase.GameOver, driver.CurrentPhase);
            Assert.AreNotEqual(GameOutcome.None, driver.Outcome);
        }

        private static PlayerId? FirstLivingTown(LocalMatchDriver driver)
        {
            foreach (PlayerState player in driver.AlivePlayers())
            {
                if (player.Role != Role.Mafia)
                {
                    return player.Id;
                }
            }

            return null;
        }

        private static List<Vote> VotesAgainstFirstLiving(
            LocalMatchDriver driver, IReadOnlyList<PlayerId> restriction)
        {
            var alive = new List<PlayerId>();
            foreach (PlayerState player in driver.AlivePlayers())
            {
                if (restriction == null || restriction.Contains(player.Id))
                {
                    alive.Add(player.Id);
                }
            }

            var votes = new List<Vote>();
            if (alive.Count == 0)
            {
                return votes;
            }

            PlayerId target = alive[0];
            foreach (PlayerState voter in driver.AlivePlayers())
            {
                PlayerId choice = voter.Id == target && alive.Count > 1 ? alive[1] : target;
                votes.Add(new Vote(voter.Id, choice));
            }

            return votes;
        }
    }
}
