using System.Collections.Generic;

namespace MafiaGame.Presentation.LocalPrototype
{
    /// <summary>
    /// A view-agnostic description of one placeholder screen. The presenter builds these; the view
    /// only renders title, body, and a vertical list of buttons. Keeping the view this dumb makes
    /// the whole pass-and-play flow testable against a fake view without any Unity objects.
    /// </summary>
    public sealed class ScreenModel
    {
        public ScreenModel(string title, string body, IReadOnlyList<ButtonSpec> buttons)
        {
            Title = title;
            Body = body;
            Buttons = buttons;
        }

        public string Title { get; }

        public string Body { get; }

        public IReadOnlyList<ButtonSpec> Buttons { get; }
    }
}
