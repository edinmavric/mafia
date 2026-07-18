using MafiaGame.Domain.Matches;
using MafiaGame.Domain.Roles;
using NUnit.Framework;
using static MafiaGame.Tests.EditMode.TestFactory;

namespace MafiaGame.Tests.EditMode
{
    public sealed class WinConditionEvaluatorTests
    {
        private readonly WinConditionEvaluator _evaluator = new WinConditionEvaluator();

        [Test]
        public void AllMafiaEliminated_TownWins()
        {
            Match match = MatchWith((1, Role.Citizen), (2, Role.Doctor), (3, Role.Mafia));
            match.Eliminate(Id(3));

            Assert.AreEqual(GameOutcome.TownWins, _evaluator.Evaluate(match));
        }

        [Test]
        public void MafiaReachParityWithTown_MafiaWins()
        {
            Match match = MatchWith((1, Role.Mafia), (2, Role.Mafia), (3, Role.Citizen), (4, Role.Citizen));
            match.Eliminate(Id(3)); // 2 mafia vs 1 town

            Assert.AreEqual(GameOutcome.MafiaWins, _evaluator.Evaluate(match));
        }

        [Test]
        public void TownStillOutnumbersMafia_GameContinues()
        {
            Match match = MatchWith((1, Role.Mafia), (2, Role.Citizen), (3, Role.Citizen), (4, Role.Detective));

            Assert.AreEqual(GameOutcome.None, _evaluator.Evaluate(match));
        }
    }
}
