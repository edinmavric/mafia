namespace MafiaGame.Presentation.LocalPrototype
{
    /// <summary>Abstraction the presenter renders through. The real view is a MonoBehaviour; tests use a fake.</summary>
    public interface IMatchView
    {
        void Show(ScreenModel screen);
    }
}
