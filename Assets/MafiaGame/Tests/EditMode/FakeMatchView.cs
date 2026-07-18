using MafiaGame.Presentation.LocalPrototype;

namespace MafiaGame.Tests.EditMode
{
    /// <summary>Records the last screen and lets tests drive the flow by pressing buttons.</summary>
    internal sealed class FakeMatchView : IMatchView
    {
        public ScreenModel Current { get; private set; }

        public void Show(ScreenModel screen) => Current = screen;

        public void PressFirst() => Current.Buttons[0].OnClick();

        public void Press(string label)
        {
            foreach (ButtonSpec button in Current.Buttons)
            {
                if (button.Label == label)
                {
                    button.OnClick();
                    return;
                }
            }

            throw new System.InvalidOperationException($"No button labelled '{label}' on screen '{Current.Title}'.");
        }
    }
}
