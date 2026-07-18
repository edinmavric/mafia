using MafiaGame.Application.Matches;
using MafiaGame.Presentation.LocalPrototype;
using NUnit.Framework;

namespace MafiaGame.Tests.EditMode
{
    public sealed class MatchFlowPresenterTests
    {
        [Test]
        public void Begin_ShowsSetupScreenWithPresets()
        {
            var view = new FakeMatchView();
            var presenter = new MatchFlowPresenter(new LocalMatchDriver(), view);

            presenter.Begin();

            Assert.IsNotNull(view.Current);
            Assert.Greater(view.Current.Buttons.Count, 0);
        }

        [Test]
        public void RoleReveal_HidesRoleBehindAPassScreen()
        {
            var view = new FakeMatchView();
            var presenter = new MatchFlowPresenter(new LocalMatchDriver(), view);
            presenter.Begin();

            view.PressFirst(); // choose the first preset → first "pass the device" screen

            StringAssert.Contains("Predaj uređaj", view.Current.Title);
        }

        [Test]
        public void PressingFirstButtonRepeatedly_PlaysAFullMatchToGameOver()
        {
            var view = new FakeMatchView();
            var presenter = new MatchFlowPresenter(new LocalMatchDriver(), view);
            presenter.Begin();

            int guard = 0;
            while (view.Current.Title != "Kraj igre" && guard++ < 2000)
            {
                view.PressFirst();
            }

            Assert.AreEqual("Kraj igre", view.Current.Title, "The pass-and-play flow should reach the game-over screen.");
        }
    }
}
