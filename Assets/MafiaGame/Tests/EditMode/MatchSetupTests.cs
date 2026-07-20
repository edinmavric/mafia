using MafiaGame.Application.Matches;
using MafiaGame.Domain.Matches;
using NUnit.Framework;

namespace MafiaGame.Tests.EditMode
{
    /// <summary>
    /// Lobby-setup tests. The setup only carries the host's intent; the legality of a match is still
    /// decided by <see cref="MatchConfiguration"/>. These prove the carrying is lossless, that an
    /// illegal setup is reported rather than silently repaired, and that a duration can never be
    /// pushed outside the allowed range.
    /// </summary>
    public sealed class MatchSetupTests
    {
        [Test]
        public void Default_IsPlayableBySevenPlayers()
        {
            MatchConfigurationResult result = MatchSetup.Default.ToConfiguration(7);

            Assert.IsTrue(result.IsValid, result.Error);
            Assert.AreEqual(1, result.Configuration.MafiaCount);
            Assert.IsTrue(result.Configuration.IncludeDoctor);
            Assert.IsTrue(result.Configuration.IncludeDetective);
        }

        [Test]
        public void WithChanges_LeavesTheOriginalUntouched()
        {
            MatchSetup original = MatchSetup.Default;

            MatchSetup changed = original.WithMafiaCount(2).WithDoctor(false);

            Assert.AreEqual(1, original.MafiaCount, "The setup must be immutable.");
            Assert.IsTrue(original.IncludeDoctor);
            Assert.AreEqual(2, changed.MafiaCount);
            Assert.IsFalse(changed.IncludeDoctor);
        }

        [Test]
        public void BothSpecialRoles_InASmallLobby_AreReportedAsIllegal()
        {
            MatchSetup setup = MatchSetup.Default.WithDoctor(true).WithDetective(true);

            MatchConfigurationResult result = setup.ToConfiguration(5);

            Assert.IsFalse(result.IsValid);
            Assert.IsNotEmpty(result.Error);
        }

        [Test]
        public void TooManyMafia_ForTheLobbySize_AreReportedAsIllegal()
        {
            MatchSetup setup = MatchSetup.Default.WithMafiaCount(3).WithDoctor(false).WithDetective(false);

            MatchConfigurationResult result = setup.ToConfiguration(4);

            Assert.IsFalse(result.IsValid);
        }

        [Test]
        public void DurationBelowTheMinimum_IsRefusedAndKeepsThePreviousValue()
        {
            MatchSetup setup = MatchSetup.Default;

            MatchSetup unchanged = setup.WithNightSeconds(MatchTimings.MinSeconds - 1d);

            Assert.AreEqual(setup.Timings.NightSeconds, unchanged.Timings.NightSeconds, 0.001d);
        }

        [Test]
        public void DurationAboveTheMaximum_IsRefusedAndKeepsThePreviousValue()
        {
            MatchSetup setup = MatchSetup.Default;

            MatchSetup unchanged = setup.WithDiscussionSeconds(MatchTimings.MaxSeconds + 1d);

            Assert.AreEqual(setup.Timings.DiscussionSeconds, unchanged.Timings.DiscussionSeconds, 0.001d);
        }

        [Test]
        public void ChangingOneDuration_LeavesTheOthersAlone()
        {
            MatchSetup setup = MatchSetup.Default;

            MatchSetup changed = setup.WithVotingSeconds(30d);

            Assert.AreEqual(30d, changed.Timings.VotingSeconds, 0.001d);
            Assert.AreEqual(setup.Timings.NightSeconds, changed.Timings.NightSeconds, 0.001d);
            Assert.AreEqual(setup.Timings.DiscussionSeconds, changed.Timings.DiscussionSeconds, 0.001d);
            Assert.AreEqual(setup.Timings.RoleRevealSeconds, changed.Timings.RoleRevealSeconds, 0.001d);
        }

        [Test]
        public void ChosenTimings_ReachTheAuthorityAndDriveTheCountdown()
        {
            MatchSetup setup = MatchSetup.Default.WithNightSeconds(25d);
            MatchConfigurationResult config = setup.ToConfiguration(7);
            Assert.IsTrue(config.IsValid, config.Error);

            var authority = new NetworkedMatchAuthority();
            authority.StartMatch(7, config.Configuration, seed: 99, setup.Timings);
            authority.ConfirmRolesSeen();

            Assert.AreEqual(MatchPhase.Night, authority.CurrentPhase);
            Assert.AreEqual(25d, authority.RemainingSeconds, 0.001d);
        }

        [Test]
        public void ClampTo_SmallLobby_SwitchesBothSpecialRolesOff()
        {
            MatchSetup clamped = MatchSetup.Default.ClampTo(4);

            Assert.IsFalse(clamped.IncludeDoctor);
            Assert.IsFalse(clamped.IncludeDetective);
            Assert.IsTrue(clamped.ToConfiguration(4).IsValid);
        }

        [Test]
        public void ClampTo_MediumLobby_KeepsTheDoctorAndDropsTheDetective()
        {
            MatchSetup clamped = MatchSetup.Default.ClampTo(5);

            Assert.IsTrue(clamped.IncludeDoctor);
            Assert.IsFalse(clamped.IncludeDetective);
            Assert.IsTrue(clamped.ToConfiguration(5).IsValid);
        }

        [Test]
        public void ClampTo_LargeEnoughLobby_ChangesNothing()
        {
            MatchSetup setup = MatchSetup.Default;

            Assert.IsTrue(setup.ClampTo(7).SameAs(setup));
        }

        [Test]
        public void ClampTo_LowersTheMafiaCountUntilTheyAreOutnumbered()
        {
            MatchSetup setup = MatchSetup.Default.WithMafiaCount(3).WithDoctor(false).WithDetective(false);

            MatchSetup clamped = setup.ClampTo(4);

            Assert.Less(clamped.MafiaCount, 3);
            Assert.IsTrue(clamped.ToConfiguration(4).IsValid);
        }

        /// <summary>
        /// The bug this guards against: every single toggle was refused in a small lobby because the
        /// setup was illegal to begin with, so the host could never reach a startable match.
        /// </summary>
        [Test]
        public void ClampTo_AlwaysProducesAStartableSetup_ForEveryLobbySize()
        {
            foreach (bool doctor in new[] { true, false })
            {
                foreach (bool detective in new[] { true, false })
                {
                    for (int players = MatchConfiguration.MinPlayers; players <= 10; players++)
                    {
                        MatchSetup setup = MatchSetup.Default
                            .WithDoctor(doctor).WithDetective(detective).WithMafiaCount(3);

                        MatchConfigurationResult result = setup.ClampTo(players).ToConfiguration(players);

                        Assert.IsTrue(result.IsValid,
                            $"players={players} doctor={doctor} detective={detective}: {result.Error}");
                    }
                }
            }
        }

        [Test]
        public void ClampTo_KeepsTheSettingsThatLobbySizeDoesNotAffect()
        {
            MatchSetup setup = MatchSetup.Default.WithRoleReveal(false).WithNightSeconds(60d);

            MatchSetup clamped = setup.ClampTo(4);

            Assert.IsFalse(clamped.RevealRoleOnElimination);
            Assert.AreEqual(60d, clamped.Timings.NightSeconds, 0.001d);
        }

        [Test]
        public void Describe_ReportsTheAgreedRulesWithoutLeakingAnything()
        {
            string text = MatchSetup.Default.WithMafiaCount(2).WithDetective(false).Describe();

            StringAssert.Contains("Mafija: 2", text);
            StringAssert.Contains("Doktor", text);
            StringAssert.DoesNotContain("Detektiv", text);
            StringAssert.Contains("Noć", text);
        }
    }
}
