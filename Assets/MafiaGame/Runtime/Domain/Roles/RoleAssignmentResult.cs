using System.Collections.Generic;
using MafiaGame.Domain.Players;

namespace MafiaGame.Domain.Roles
{
    /// <summary>
    /// Outcome of assigning roles to a roster. On success <see cref="Players"/> holds one
    /// <see cref="PlayerState"/> per input id; on failure <see cref="Error"/> explains why.
    /// </summary>
    public sealed class RoleAssignmentResult
    {
        private RoleAssignmentResult(bool isSuccess, IReadOnlyList<PlayerState> players, string error)
        {
            IsSuccess = isSuccess;
            Players = players;
            Error = error;
        }

        public bool IsSuccess { get; }

        public IReadOnlyList<PlayerState> Players { get; }

        public string Error { get; }

        internal static RoleAssignmentResult Success(IReadOnlyList<PlayerState> players) =>
            new RoleAssignmentResult(true, players, error: null);

        internal static RoleAssignmentResult Failure(string error) =>
            new RoleAssignmentResult(false, players: null, error);
    }
}
