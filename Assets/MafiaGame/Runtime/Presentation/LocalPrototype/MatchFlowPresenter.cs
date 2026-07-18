using System;
using System.Collections.Generic;
using MafiaGame.Application.Matches;
using MafiaGame.Domain.Matches;
using MafiaGame.Domain.Night;
using MafiaGame.Domain.Players;
using MafiaGame.Domain.Roles;
using MafiaGame.Domain.Voting;

namespace MafiaGame.Presentation.LocalPrototype
{
    /// <summary>
    /// Engine-free pass-and-play flow controller. The app is the automatic moderator: it walks the
    /// device around the table (role reveal, then each acting role at night, then each voter by turn)
    /// and drives the authoritative <see cref="LocalMatchDriver"/>. All match rules live in the
    /// driver/domain; this class only sequences whose turn it is and what screen to show. Because it
    /// renders through <see cref="IMatchView"/>, the whole flow is testable against a fake view.
    /// </summary>
    public sealed class MatchFlowPresenter
    {
        private readonly LocalMatchDriver _driver;
        private readonly IMatchView _view;
        private readonly Dictionary<PlayerId, string> _names = new Dictionary<PlayerId, string>();

        private PlayerId? _mafiaTarget;
        private PlayerId? _doctorProtect;
        private PlayerId? _detectiveTarget;

        private int _revealIndex;

        private List<Vote> _votes = new List<Vote>();
        private List<PlayerId> _voteOrder = new List<PlayerId>();
        private int _voteIndex;
        private IReadOnlyList<PlayerId> _revoteCandidates;

        public MatchFlowPresenter(LocalMatchDriver driver, IMatchView view)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _view = view ?? throw new ArgumentNullException(nameof(view));
        }

        /// <summary>Entry point: shows the setup screen.</summary>
        public void Begin() => ShowSetup();

        // ----- Setup -----

        private void ShowSetup()
        {
            var buttons = new List<ButtonSpec>
            {
                new ButtonSpec("7 igrača · 1 Mafija · Doktor+Detektiv · otkrivanje uloga",
                    () => StartPreset(7, 1, true, true, true, 12345)),
                new ButtonSpec("8 igrača · 2 Mafije · Doktor+Detektiv · bez otkrivanja",
                    () => StartPreset(8, 2, true, true, false, 777)),
                new ButtonSpec("4 igrača · 1 Mafija · bez specijalnih · otkrivanje",
                    () => StartPreset(4, 1, false, false, true, 1)),
            };

            _view.Show(new ScreenModel(
                "MafiaGame — lokalni prototip (DEV)",
                "Izaberite postavku, pa predajte uređaj u krug.",
                buttons));
        }

        private void StartPreset(int players, int mafia, bool doctor, bool detective, bool reveal, int seed)
        {
            MatchConfigurationResult config = MatchConfiguration.Create(players, mafia, doctor, detective, reveal);
            if (!config.IsValid)
            {
                ShowInfo("Greška u postavci", config.Error, "Nazad", ShowSetup);
                return;
            }

            _names.Clear();
            var roster = new List<PlayerId>(players);
            for (int i = 1; i <= players; i++)
            {
                var id = new PlayerId(i);
                roster.Add(id);
                _names[id] = "Igrač " + i;
            }

            _driver.Start(config.Configuration, roster, seed);
            BeginRoleReveal();
        }

        // ----- Role reveal (pass-and-play) -----

        private void BeginRoleReveal()
        {
            _revealIndex = 0;
            ShowRevealPass();
        }

        private void ShowRevealPass()
        {
            IReadOnlyList<PlayerState> players = _driver.Players;
            if (_revealIndex >= players.Count)
            {
                _driver.ConfirmRolesSeen();
                BeginNight();
                return;
            }

            PlayerId id = players[_revealIndex].Id;
            ShowPass(Name(id), () => ShowRevealCard(id));
        }

        private void ShowRevealCard(PlayerId id)
        {
            Role role = _driver.RoleOf(id);
            string body = $"Tvoja uloga: {RoleName(role)}";
            if (role == Role.Mafia)
            {
                IReadOnlyList<PlayerId> mates = _driver.MafiaTeammates(id);
                body += mates.Count > 0
                    ? "\nSaigrači u Mafiji: " + JoinNames(mates)
                    : "\nNemaš saigrača u Mafiji.";
            }

            ShowInfo(Name(id), body, "Sakrij i predaj dalje", () =>
            {
                _revealIndex++;
                ShowRevealPass();
            });
        }

        // ----- Night (moderated, pass-and-play) -----

        private void BeginNight()
        {
            _mafiaTarget = null;
            _doctorProtect = null;
            _detectiveTarget = null;
            NightMafia();
        }

        private void NightMafia()
        {
            if (!HasAliveRole(Role.Mafia))
            {
                NightDoctor();
                return;
            }

            ShowPass("Mafija", () => ShowSelection(
                "Mafija bira žrtvu",
                "Izaberite koga napadate ove noći.",
                AlivePlayersList(),
                allowSkip: true,
                chosen =>
                {
                    _mafiaTarget = chosen;
                    NightDoctor();
                }));
        }

        private void NightDoctor()
        {
            if (!HasAliveRole(Role.Doctor))
            {
                NightDetective();
                return;
            }

            ShowPass("Doktor", () => ShowSelection(
                "Doktor štiti",
                "Koga štitite? Možete i sebe, ali ne istog igrača dve noći zaredom.",
                AlivePlayersList(),
                allowSkip: true,
                chosen =>
                {
                    _doctorProtect = chosen;
                    NightDetective();
                }));
        }

        private void NightDetective()
        {
            if (!HasAliveRole(Role.Detective))
            {
                ResolveNightNow();
                return;
            }

            ShowPass("Detektiv", () => ShowSelection(
                "Detektiv istražuje",
                "Koga istražujete ove noći?",
                AlivePlayersList(),
                allowSkip: true,
                chosen =>
                {
                    _detectiveTarget = chosen;
                    ResolveNightNow();
                }));
        }

        private void ResolveNightNow()
        {
            NightResolution resolution =
                _driver.ResolveNight(new NightActions(_mafiaTarget, _doctorProtect, _detectiveTarget));

            if (_detectiveTarget.HasValue && resolution.DetectiveResult != null)
            {
                string targetName = Name(resolution.DetectiveResult.Target);
                string verdict = resolution.DetectiveResult.IsMafia ? "JESTE Mafija" : "NIJE Mafija";
                ShowPass("Detektiv (rezultat)", () => ShowInfo(
                    "Rezultat istrage",
                    $"{targetName}: {verdict}",
                    "Sakrij",
                    () => AfterNight(resolution)));
            }
            else
            {
                AfterNight(resolution);
            }
        }

        private void AfterNight(NightResolution resolution)
        {
            if (_driver.CurrentPhase == MatchPhase.GameOver)
            {
                ShowGameOver();
                return;
            }

            string body = resolution.KilledPlayer.HasValue
                ? $"{Name(resolution.KilledPlayer.Value)} je ubijen(a) tokom noći." + RevealSuffix(resolution.KilledPlayer.Value)
                : "Svanulo je. Ove noći niko nije stradao.";

            ShowInfo("Jutro", body, "Dalje", () =>
            {
                _driver.ContinueToDiscussion();
                ShowDiscussion();
            });
        }

        // ----- Day discussion -----

        private void ShowDiscussion()
        {
            ShowInfo(
                "Diskusija",
                "Razgovarajte, optužujte i branite se. Kada ste spremni, počnite glasanje.",
                "Počni glasanje",
                () =>
                {
                    _driver.BeginVoting();
                    BeginVotingRound(firstRound: true);
                });
        }

        // ----- Voting (pass-and-play, one voter at a time) -----

        private void BeginVotingRound(bool firstRound)
        {
            _votes = new List<Vote>();
            _voteOrder = AliveIds();
            _voteIndex = 0;
            if (firstRound)
            {
                _revoteCandidates = null;
            }

            PromptNextVoter();
        }

        private void PromptNextVoter()
        {
            if (_voteIndex >= _voteOrder.Count)
            {
                FinishVotingRound();
                return;
            }

            PlayerId voter = _voteOrder[_voteIndex];
            ShowPass(Name(voter), () => ShowVoteOptions(voter));
        }

        private void ShowVoteOptions(PlayerId voter)
        {
            var buttons = new List<ButtonSpec>();
            foreach (PlayerState player in _driver.AlivePlayers())
            {
                if (player.Id == voter)
                {
                    continue;
                }

                if (_revoteCandidates != null && !Contains(_revoteCandidates, player.Id))
                {
                    continue;
                }

                PlayerId target = player.Id;
                buttons.Add(new ButtonSpec("Glasaj: " + Name(target), () =>
                {
                    _votes.Add(new Vote(voter, target));
                    _voteIndex++;
                    PromptNextVoter();
                }));
            }

            buttons.Add(new ButtonSpec("Uzdržan", () =>
            {
                _voteIndex++;
                PromptNextVoter();
            }));

            string body = _revoteCandidates != null
                ? "Ponovljeno glasanje — biraj samo između izjednačenih."
                : "Koga grad izbacuje?";
            _view.Show(new ScreenModel($"Glasanje: {Name(voter)}", body, buttons));
        }

        private void FinishVotingRound()
        {
            VotingResolution resolution = _driver.ResolveVoting(_votes, _revoteCandidates);

            if (resolution.Outcome == VoteOutcome.TieRequiresRevote)
            {
                _revoteCandidates = resolution.TiedCandidates;
                ShowInfo(
                    "Nerešeno",
                    "Izjednačeno: " + JoinNames(resolution.TiedCandidates) + ". Sledi ponovljeno glasanje samo za njih.",
                    "Ponovi glasanje",
                    () => BeginVotingRound(firstRound: false));
                return;
            }

            string body = resolution.Outcome == VoteOutcome.Eliminated && resolution.EliminatedPlayer.HasValue
                ? $"{Name(resolution.EliminatedPlayer.Value)} je izglasan(a) i eliminisan(a)." + RevealSuffix(resolution.EliminatedPlayer.Value)
                : "Grad nije doneo odluku. Niko nije eliminisan.";

            if (_driver.CurrentPhase == MatchPhase.GameOver)
            {
                ShowInfo("Ishod glasanja", body, "Dalje", ShowGameOver);
            }
            else
            {
                ShowInfo("Ishod glasanja", body, "Dalje", BeginNight);
            }
        }

        // ----- Game over -----

        private void ShowGameOver()
        {
            string text = _driver.Outcome == GameOutcome.TownWins
                ? "Grad je pobedio! Sva Mafija je eliminisana."
                : _driver.Outcome == GameOutcome.MafiaWins
                    ? "Mafija je pobedila!"
                    : "Igra je završena.";

            ShowInfo("Kraj igre", text, "Nova igra", ShowSetup);
        }

        // ----- View helpers -----

        private void ShowPass(string who, Action onReady)
        {
            _view.Show(new ScreenModel(
                $"Predaj uređaj: {who}",
                "Neka ostali ne gledaju u ekran.",
                new[] { new ButtonSpec("Spremni", onReady) }));
        }

        private void ShowInfo(string title, string body, string buttonLabel, Action onContinue)
        {
            _view.Show(new ScreenModel(title, body, new[] { new ButtonSpec(buttonLabel, onContinue) }));
        }

        private void ShowSelection(
            string title, string body, IReadOnlyList<PlayerState> options, bool allowSkip, Action<PlayerId?> onChosen)
        {
            var buttons = new List<ButtonSpec>();
            foreach (PlayerState player in options)
            {
                PlayerId id = player.Id;
                buttons.Add(new ButtonSpec(Name(id), () => onChosen(id)));
            }

            if (allowSkip)
            {
                buttons.Add(new ButtonSpec("Preskoči", () => onChosen(null)));
            }

            _view.Show(new ScreenModel(title, body, buttons));
        }

        // ----- Small queries -----

        private bool HasAliveRole(Role role)
        {
            foreach (PlayerState player in _driver.AlivePlayers())
            {
                if (player.Role == role)
                {
                    return true;
                }
            }

            return false;
        }

        private List<PlayerState> AlivePlayersList()
        {
            var list = new List<PlayerState>();
            foreach (PlayerState player in _driver.AlivePlayers())
            {
                list.Add(player);
            }

            return list;
        }

        private List<PlayerId> AliveIds()
        {
            var list = new List<PlayerId>();
            foreach (PlayerState player in _driver.AlivePlayers())
            {
                list.Add(player.Id);
            }

            return list;
        }

        private static bool Contains(IReadOnlyList<PlayerId> ids, PlayerId id)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id)
                {
                    return true;
                }
            }

            return false;
        }

        private string RevealSuffix(PlayerId id) =>
            _driver.RevealRoleOnElimination ? $" (Uloga: {RoleName(_driver.RoleOf(id))})" : string.Empty;

        private string JoinNames(IReadOnlyList<PlayerId> ids)
        {
            var parts = new List<string>(ids.Count);
            for (int i = 0; i < ids.Count; i++)
            {
                parts.Add(Name(ids[i]));
            }

            return string.Join(", ", parts);
        }

        private string Name(PlayerId id) =>
            _names.TryGetValue(id, out string name) ? name : id.ToString();

        private static string RoleName(Role role)
        {
            switch (role)
            {
                case Role.Citizen: return "Građanin";
                case Role.Mafia: return "Mafija";
                case Role.Doctor: return "Doktor";
                case Role.Detective: return "Detektiv";
                default: return role.ToString();
            }
        }
    }
}
