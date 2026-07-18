using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;

namespace MafiaGame.Infrastructure.Services
{
    /// <summary>
    /// Initializes Unity Gaming Services and signs the player in anonymously. Anonymous sign-in
    /// needs no identity provider; it is enough to identify a player within a session for the MVP.
    /// Idempotent: safe to call before every session action.
    /// </summary>
    public static class GameServices
    {
        public static bool IsSignedIn => AuthenticationService.Instance.IsSignedIn;

        public static string LocalPlayerId =>
            AuthenticationService.Instance.IsSignedIn ? AuthenticationService.Instance.PlayerId : string.Empty;

        public static async Task InitializeAndSignInAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }
    }
}
