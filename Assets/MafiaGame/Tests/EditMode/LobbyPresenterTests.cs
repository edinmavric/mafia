using System;
using System.Threading.Tasks;
using MafiaGame.Presentation.Lobby;
using NUnit.Framework;

namespace MafiaGame.Tests.EditMode
{
    public sealed class LobbyPresenterTests
    {
        private static Task NoOpSignIn() => Task.CompletedTask;

        [Test]
        public void Begin_WithNoSession_ShowsDisconnected()
        {
            var view = new FakeLobbyView();
            using var presenter = new LobbyPresenter(view, new FakeMatchSession(), NoOpSignIn);

            presenter.Begin();

            Assert.IsFalse(view.IsConnectedShown);
        }

        [Test]
        public void Host_ShowsConnectedAsHostWithJoinCode()
        {
            var view = new FakeLobbyView();
            using var presenter = new LobbyPresenter(view, new FakeMatchSession(), NoOpSignIn);
            presenter.Begin();

            view.ClickHost();

            Assert.IsTrue(view.IsConnectedShown);
            Assert.IsTrue(view.ShownIsHost);
            Assert.AreEqual("ABC123", view.ShownJoinCode);
        }

        [Test]
        public void Join_WithEmptyCode_PromptsAndDoesNotConnect()
        {
            var view = new FakeLobbyView { JoinCodeInput = "  " };
            var session = new FakeMatchSession();
            using var presenter = new LobbyPresenter(view, session, NoOpSignIn);
            presenter.Begin();

            view.ClickJoin();

            Assert.IsFalse(view.IsConnectedShown);
            Assert.IsFalse(session.IsActive);
        }

        [Test]
        public void Join_WithCode_ConnectsAsClient()
        {
            var view = new FakeLobbyView { JoinCodeInput = "XYZ999" };
            using var presenter = new LobbyPresenter(view, new FakeMatchSession(), NoOpSignIn);
            presenter.Begin();

            view.ClickJoin();

            Assert.IsTrue(view.IsConnectedShown);
            Assert.IsFalse(view.ShownIsHost);
            Assert.AreEqual("XYZ999", view.ShownJoinCode);
        }

        [Test]
        public void Host_Failure_ShowsErrorAndStaysDisconnected()
        {
            var view = new FakeLobbyView();
            var session = new FakeMatchSession { FailWith = new Exception("relay unavailable") };
            using var presenter = new LobbyPresenter(view, session, NoOpSignIn);
            presenter.Begin();

            view.ClickHost();

            StringAssert.Contains("Greška", view.LastStatus);
            Assert.IsFalse(view.IsConnectedShown);
        }

        [Test]
        public void Leave_ReturnsToDisconnected()
        {
            var view = new FakeLobbyView();
            using var presenter = new LobbyPresenter(view, new FakeMatchSession(), NoOpSignIn);
            presenter.Begin();
            view.ClickHost();
            Assert.IsTrue(view.IsConnectedShown);

            view.ClickLeave();

            Assert.IsFalse(view.IsConnectedShown);
        }
    }
}
