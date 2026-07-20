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

        /// <summary>
        /// Command-line switch that gives this instance its own account, e.g. <c>-profile p3</c>.
        /// </summary>
        private const string ProfileArgument = "-profile";

        public static async Task InitializeAndSignInAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                ApplyProfile();
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        /// <summary>
        /// Puts this instance on its own authentication profile when one was requested.
        ///
        /// Several copies of the same build share one save folder, so they also share the cached
        /// anonymous account — every window signs in as the same player and the second one to join a
        /// lobby is rejected with "player is already a member of the lobby". A profile separates the
        /// cached accounts within one installation, which is what makes local multi-window testing
        /// possible. Without the switch nothing changes, so normal players are unaffected.
        /// </summary>
        private static void ApplyProfile()
        {
            string profile = ReadProfileArgument();
            if (string.IsNullOrEmpty(profile))
            {
                return;
            }

            try
            {
                AuthenticationService.Instance.SwitchProfile(profile);
            }
            catch (AuthenticationException exception)
            {
                // A rejected profile name must not stop the game from starting; the shared account
                // is still usable for a single instance.
                UnityEngine.Debug.LogWarning($"[GameServices] Profile '{profile}' refused: {exception.Message}");
            }
        }

        private static string ReadProfileArgument()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == ProfileArgument)
                {
                    return Sanitize(args[i + 1]);
                }
            }

            return string.Empty;
        }

        /// <summary>Profile names allow letters, digits, '-' and '_', up to 30 characters.</summary>
        private static string Sanitize(string value)
        {
            var builder = new System.Text.StringBuilder();
            foreach (char c in value)
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
                {
                    builder.Append(c);
                }

                if (builder.Length == 30)
                {
                    break;
                }
            }

            return builder.ToString();
        }
    }
}
