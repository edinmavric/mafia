using System.Collections.Generic;
using MafiaGame.Domain.Matches;
using MafiaGame.Domain.Players;
using MafiaGame.Domain.Randomness;
using MafiaGame.Domain.Roles;
using NUnit.Framework;

namespace MafiaGame.Tests.EditMode
{
    public sealed class RoleAssignmentServiceTests
    {
        private readonly RoleAssignmentService _service = new RoleAssignmentService();

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
        public void Assign_ProducesExactRoleCounts()
        {
            MatchConfiguration config = MatchConfiguration.Create(7, 2, true, true, false).Configuration;

            RoleAssignmentResult result = _service.Assign(Roster(7), config, new SeededRandomSource(1));

            Assert.IsTrue(result.IsSuccess);
            int mafia = 0, doctor = 0, detective = 0, citizen = 0;
            foreach (PlayerState player in result.Players)
            {
                switch (player.Role)
                {
                    case Role.Mafia: mafia++; break;
                    case Role.Doctor: doctor++; break;
                    case Role.Detective: detective++; break;
                    case Role.Citizen: citizen++; break;
                }
            }

            Assert.AreEqual(2, mafia);
            Assert.AreEqual(1, doctor);
            Assert.AreEqual(1, detective);
            Assert.AreEqual(3, citizen);
        }

        [Test]
        public void Assign_CoversEveryPlayerExactlyOnce()
        {
            MatchConfiguration config = MatchConfiguration.Create(6, 1, true, false, false).Configuration;

            RoleAssignmentResult result = _service.Assign(Roster(6), config, new SeededRandomSource(42));

            var ids = new HashSet<PlayerId>();
            foreach (PlayerState player in result.Players)
            {
                Assert.IsTrue(ids.Add(player.Id), "Each player id must appear exactly once.");
            }

            Assert.AreEqual(6, ids.Count);
        }

        [Test]
        public void Assign_IsDeterministicForSameSeed()
        {
            MatchConfiguration config = MatchConfiguration.Create(8, 2, true, true, false).Configuration;

            RoleAssignmentResult a = _service.Assign(Roster(8), config, new SeededRandomSource(555));
            RoleAssignmentResult b = _service.Assign(Roster(8), config, new SeededRandomSource(555));

            for (int i = 0; i < 8; i++)
            {
                Assert.AreEqual(a.Players[i].Id, b.Players[i].Id);
                Assert.AreEqual(a.Players[i].Role, b.Players[i].Role);
            }
        }

        [Test]
        public void Assign_WrongPlayerCount_Fails()
        {
            MatchConfiguration config = MatchConfiguration.Create(6, 1, false, false, false).Configuration;

            RoleAssignmentResult result = _service.Assign(Roster(5), config, new SeededRandomSource(1));

            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void Assign_DuplicatePlayerIds_Fails()
        {
            MatchConfiguration config = MatchConfiguration.Create(4, 1, false, false, false).Configuration;
            var roster = new List<PlayerId>
            {
                new PlayerId(1), new PlayerId(2), new PlayerId(2), new PlayerId(3)
            };

            RoleAssignmentResult result = _service.Assign(roster, config, new SeededRandomSource(1));

            Assert.IsFalse(result.IsSuccess);
        }
    }
}
