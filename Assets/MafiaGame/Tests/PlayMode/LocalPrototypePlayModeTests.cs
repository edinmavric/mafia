using System.Collections;
using MafiaGame.Presentation.LocalPrototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MafiaGame.Tests.PlayMode
{
    public sealed class LocalPrototypePlayModeTests
    {
        [UnityTest]
        public IEnumerator Bootstrap_BuildsUiAndPlaysToGameOver()
        {
            var host = new GameObject("PrototypeBootstrapHost");
            PrototypeBootstrap bootstrap = host.AddComponent<PrototypeBootstrap>();

            // Let Start() run: builds the event system, canvas, view, presenter, and shows setup.
            yield return null;

            Assert.IsNotNull(bootstrap.View, "The bootstrap should have created the view.");
            Assert.IsNotNull(bootstrap.View.CurrentScreen, "The setup screen should be shown after Start.");

            int guard = 0;
            while (bootstrap.View.CurrentScreen.Title != "Kraj igre" && guard++ < 2000)
            {
                bootstrap.View.CurrentScreen.Buttons[0].OnClick();
            }

            Assert.AreEqual("Kraj igre", bootstrap.View.CurrentScreen.Title);

            Object.Destroy(host);
            yield return null;
        }
    }
}
