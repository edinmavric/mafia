using MafiaGame.Domain.Roles;
using NUnit.Framework;

namespace MafiaGame.Tests.EditMode
{
    public sealed class RoleFactionsTests
    {
        [Test]
        public void Mafia_IsMafiaFaction() =>
            Assert.AreEqual(Faction.Mafia, RoleFactions.FactionOf(Role.Mafia));

        [Test]
        public void Citizen_IsTown() =>
            Assert.AreEqual(Faction.Town, RoleFactions.FactionOf(Role.Citizen));

        [Test]
        public void Doctor_IsTown() =>
            Assert.AreEqual(Faction.Town, RoleFactions.FactionOf(Role.Doctor));

        [Test]
        public void Detective_IsTown() =>
            Assert.AreEqual(Faction.Town, RoleFactions.FactionOf(Role.Detective));
    }
}
