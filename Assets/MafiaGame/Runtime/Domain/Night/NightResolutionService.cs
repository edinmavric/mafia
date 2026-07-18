using System;
using System.Collections.Generic;
using MafiaGame.Domain.Matches;
using MafiaGame.Domain.Players;
using MafiaGame.Domain.Roles;

namespace MafiaGame.Domain.Night
{
    /// <summary>
    /// Resolves the night from submitted intents. Validates each intent against living-target
    /// rules, applies the Doctor's protection (self allowed; the same target may not be
    /// protected on two consecutive nights), computes the Detective's binary result, and
    /// determines whether the Mafia target dies.
    ///
    /// Pure: it does not eliminate anyone; the caller applies the returned kill so that
    /// mutation stays on one controlled path (the Match aggregate).
    /// </summary>
    public sealed class NightResolutionService
    {
        public NightResolution Resolve(Match match, NightActions actions, PlayerId? previousDoctorProtect)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            var rejections = new List<string>();

            PlayerId? appliedProtect = ResolveDoctorProtection(match, actions, previousDoctorProtect, rejections);
            DetectiveResult detectiveResult = ResolveInvestigation(match, actions, rejections);
            PlayerId? killed = ResolveKill(match, actions, appliedProtect, rejections);

            return new NightResolution(killed, appliedProtect, detectiveResult, rejections);
        }

        private static PlayerId? ResolveDoctorProtection(
            Match match, NightActions actions, PlayerId? previousDoctorProtect, ICollection<string> rejections)
        {
            if (!actions.DoctorProtect.HasValue)
            {
                return null;
            }

            PlayerId target = actions.DoctorProtect.Value;
            PlayerState player = match.Find(target);
            if (player == null || !player.IsAlive)
            {
                rejections.Add("Doctor protect target is not a living player.");
                return null;
            }

            if (previousDoctorProtect.HasValue && previousDoctorProtect.Value == target)
            {
                rejections.Add("Doctor cannot protect the same target on two consecutive nights.");
                return null;
            }

            return target;
        }

        private static DetectiveResult ResolveInvestigation(
            Match match, NightActions actions, ICollection<string> rejections)
        {
            if (!actions.DetectiveInvestigate.HasValue)
            {
                return null;
            }

            PlayerId target = actions.DetectiveInvestigate.Value;
            PlayerState player = match.Find(target);
            if (player == null || !player.IsAlive)
            {
                rejections.Add("Detective investigate target is not a living player.");
                return null;
            }

            bool isMafia = RoleFactions.FactionOf(player.Role) == Faction.Mafia;
            return new DetectiveResult(target, isMafia);
        }

        private static PlayerId? ResolveKill(
            Match match, NightActions actions, PlayerId? appliedProtect, ICollection<string> rejections)
        {
            if (!actions.MafiaTarget.HasValue)
            {
                return null;
            }

            PlayerId target = actions.MafiaTarget.Value;
            PlayerState player = match.Find(target);
            if (player == null || !player.IsAlive)
            {
                rejections.Add("Mafia target is not a living player.");
                return null;
            }

            if (appliedProtect.HasValue && appliedProtect.Value == target)
            {
                // Saved by the Doctor.
                return null;
            }

            return target;
        }
    }
}
