using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace MafiaGame.Infrastructure.Networking
{
    /// <summary>
    /// Transport timeouts applied before a Relay-backed session starts. The defaults retry a failed
    /// connection for about a minute, which makes a broken Relay path look like a hang: the UI sits on
    /// "Pridruživanje…" and only then reports an error. Failing after a few seconds is far easier to
    /// work with — the player simply tries again.
    /// </summary>
    public static class TransportTuning
    {
        /// <summary>Connection attempts before the transport gives up (default is 60).</summary>
        private const int MaxConnectAttempts = 12;

        /// <summary>Milliseconds between connection attempts.</summary>
        private const int ConnectTimeoutMs = 1000;

        /// <summary>
        /// Applies the fast-failure timeouts to the active Unity Transport, if there is one. Safe to
        /// call repeatedly and a no-op when no NetworkManager or UTP transport exists yet.
        /// </summary>
        public static void ApplyFastFailure()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.NetworkConfig == null)
            {
                return;
            }

            if (manager.NetworkConfig.NetworkTransport is UnityTransport transport)
            {
                transport.MaxConnectAttempts = MaxConnectAttempts;
                transport.ConnectTimeoutMS = ConnectTimeoutMs;
            }
        }
    }
}
