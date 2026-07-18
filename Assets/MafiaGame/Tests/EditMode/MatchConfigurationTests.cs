using MafiaGame.Domain.Matches;
using NUnit.Framework;

namespace MafiaGame.Tests.EditMode
{
    public sealed class MatchConfigurationTests
    {
        [Test]
        public void MinimalFourPlayerOneMafia_IsValid()
        {
            MatchConfigurationResult result = MatchConfiguration.Create(4, 1, false, false, false);

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(3, result.Configuration.CitizenCount);
        }

        [Test]
        public void FewerThanMinimumPlayers_IsInvalid()
        {
            Assert.IsFalse(MatchConfiguration.Create(3, 1, false, false, false).IsValid);
        }

        [Test]
        public void ZeroMafia_IsInvalid()
        {
            Assert.IsFalse(MatchConfiguration.Create(6, 0, false, false, false).IsValid);
        }

        [Test]
        public void MafiaAboveMaximumForSize_IsInvalid()
        {
            // 6 players allow at most 1 mafia.
            Assert.IsFalse(MatchConfiguration.Create(6, 2, false, false, false).IsValid);
        }

        [Test]
        public void TwoMafiaAllowedAtSevenPlayers()
        {
            Assert.IsTrue(MatchConfiguration.Create(7, 2, false, false, false).IsValid);
        }

        [Test]
        public void ThreeMafiaAllowedAtTenPlayers()
        {
            Assert.IsTrue(MatchConfiguration.Create(10, 3, false, false, false).IsValid);
        }

        [Test]
        public void SpecialRoleBelowFivePlayers_IsInvalid()
        {
            Assert.IsFalse(MatchConfiguration.Create(4, 1, true, false, false).IsValid);
        }

        [Test]
        public void OneSpecialRoleAtFivePlayers_IsValid()
        {
            Assert.IsTrue(MatchConfiguration.Create(5, 1, false, true, false).IsValid);
        }

        [Test]
        public void BothSpecialRolesBelowSevenPlayers_IsInvalid()
        {
            Assert.IsFalse(MatchConfiguration.Create(6, 1, true, true, false).IsValid);
        }

        [Test]
        public void BothSpecialRolesAtSevenPlayers_IsValid()
        {
            MatchConfigurationResult result = MatchConfiguration.Create(7, 1, true, true, false);

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(4, result.Configuration.CitizenCount);
        }

        [Test]
        public void RevealRoleOnElimination_IsCarriedThrough()
        {
            MatchConfigurationResult result = MatchConfiguration.Create(7, 1, true, true, true);

            Assert.IsTrue(result.IsValid);
            Assert.IsTrue(result.Configuration.RevealRoleOnElimination);
        }

        [Test]
        public void MaxMafiaFor_HasExpectedBoundaries()
        {
            Assert.AreEqual(1, MatchConfiguration.MaxMafiaFor(6));
            Assert.AreEqual(2, MatchConfiguration.MaxMafiaFor(7));
            Assert.AreEqual(2, MatchConfiguration.MaxMafiaFor(9));
            Assert.AreEqual(3, MatchConfiguration.MaxMafiaFor(10));
        }
    }
}
