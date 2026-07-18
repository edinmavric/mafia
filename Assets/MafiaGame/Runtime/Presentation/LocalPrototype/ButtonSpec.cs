using System;

namespace MafiaGame.Presentation.LocalPrototype
{
    /// <summary>A single button on a placeholder screen: its label and what it does when pressed.</summary>
    public sealed class ButtonSpec
    {
        public ButtonSpec(string label, Action onClick)
        {
            Label = label;
            OnClick = onClick;
        }

        public string Label { get; }

        public Action OnClick { get; }
    }
}
