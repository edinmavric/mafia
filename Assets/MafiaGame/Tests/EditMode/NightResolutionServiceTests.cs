using MafiaGame.Domain.Matches;
using MafiaGame.Domain.Night;
using MafiaGame.Domain.Roles;
using NUnit.Framework;
using static MafiaGame.Tests.EditMode.TestFactory;

namespace MafiaGame.Tests.EditMode
{
    public sealed class NightResolutionServiceTests
    {
        private readonly NightResolutionService _service = new NightResolutionService();

        // 1=Mafia, 2=Doctor, 3=Detective, 4=Citizen, 5=Citizen
        private static Match SampleMatch() => MatchWith(
            (1, Role.Mafia), (2, Role.Doctor), (3, Role.Detective), (4, Role.Citizen), (5, Role.Citizen));

        [Test]
        public void Mafia_KillsUnprotectedLivingTarget()
        {
            NightResolution result = _service.Resolve(SampleMatch(), new NightActions(Id(4), null, null), null);

            Assert.AreEqual(Id(4), result.KilledPlayer);
        }

        [Test]
        public void Doctor_SavesTheMafiaTarget()
        {
            NightResolution result = _service.Resolve(SampleMatch(), new NightActions(Id(4), Id(4), null), null);

            Assert.IsFalse(result.KilledPlayer.HasValue);
            Assert.AreEqual(Id(4), result.ProtectedTarget);
        }

        [Test]
        public void Doctor_MayProtectSelf()
        {
            // Mafia targets the doctor (2); the doctor self-protects.
            NightResolution result = _service.Resolve(SampleMatch(), new NightActions(Id(2), Id(2), null), null);

            Assert.IsFalse(result.KilledPlayer.HasValue);
            Assert.AreEqual(Id(2), result.ProtectedTarget);
        }

        [Test]
        public void Doctor_CannotProtectSameTargetTwoNightsInARow()
        {
            NightResolution result = _service.Resolve(
                SampleMatch(), new NightActions(Id(4), Id(4), null), previousDoctorProtect: Id(4));

            Assert.IsFalse(result.ProtectedTarget.HasValue);
            Assert.AreEqual(Id(4), result.KilledPlayer, "Rejected protection means the target dies.");
            Assert.IsNotEmpty(result.Rejections);
        }

        [Test]
        public void Detective_OnMafia_ReportsMafia()
        {
            NightResolution result = _service.Resolve(SampleMatch(), new NightActions(null, null, Id(1)), null);

            Assert.IsNotNull(result.DetectiveResult);
            Assert.IsTrue(result.DetectiveResult.IsMafia);
        }

        [Test]
        public void Detective_OnTownRole_ReportsNotMafia()
        {
            NightResolution result = _service.Resolve(SampleMatch(), new NightActions(null, null, Id(2)), null);

            Assert.IsNotNull(result.DetectiveResult);
            Assert.IsFalse(result.DetectiveResult.IsMafia);
        }

        [Test]
        public void DeadTarget_IsRejectedAndNoOneDies()
        {
            Match match = SampleMatch();
            match.Eliminate(Id(4));

            NightResolution result = _service.Resolve(match, new NightActions(Id(4), null, null), null);

            Assert.IsFalse(result.KilledPlayer.HasValue);
            Assert.IsNotEmpty(result.Rejections);
        }

        [Test]
        public void NoMafiaTarget_ProducesNoKill()
        {
            NightResolution result = _service.Resolve(SampleMatch(), new NightActions(null, null, null), null);

            Assert.IsFalse(result.KilledPlayer.HasValue);
        }
    }
}
