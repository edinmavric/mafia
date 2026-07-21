using System.Collections.Generic;
using System.Text;
using MafiaGame.Application.Matches;
using MafiaGame.Domain.Matches;
using MafiaGame.Domain.Roles;
using MafiaGame.Domain.Voting;
using MafiaGame.Infrastructure.Networking;
using MafiaGame.Presentation.LocalPrototype;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaGame.Presentation.Match
{
    /// <summary>
    /// The screen shown while a match is being played: the public phase and its countdown, this
    /// player's private role, the result of the last resolution, the host's phase controls, and the
    /// buttons for whatever this player may do right now.
    ///
    /// It lives in the match scene, which is loaded when the match starts and unloaded when everyone
    /// returns to the lobby — so this screen exists exactly as long as a match does, and the lobby
    /// screen never has to hide it. It holds no game rules: every decision stays on the host.
    /// </summary>
    public sealed class MatchScreenView : MonoBehaviour
    {
        private NetworkMatchController _controller;

        private TextMeshProUGUI _phaseText;
        private TextMeshProUGUI _roleText;
        private TextMeshProUGUI _resultText;

        private Button _confirmButton;
        private Button _resolveButton;
        private Button _discussButton;
        private Button _beginVoteButton;
        private Button _resolveVoteButton;
        private Button _revoteButton;
        private Button _returnToLobbyButton;
        private Transform _actionRow;

        private Role _localRole = Role.Citizen;
        private bool _hasRole;

        /// <summary>This player's private role line, kept so a re-render never loses it.</summary>
        private string _roleLine = string.Empty;

        /// <summary>Countdown value currently on screen; -1 means "no timer shown".</summary>
        private int _shownSeconds = -1;

        private void Awake()
        {
            BuildUi();

            // The controller lives in the lobby scene, which is still loaded underneath this one, so
            // it cannot be wired through the Inspector — a serialized reference cannot cross scenes.
            // This is the one lookup this screen does, at its own startup; everything after it goes
            // through the reference.
            _controller = FindFirstObjectByType<NetworkMatchController>();
            if (_controller == null)
            {
                _resultText.text =
                    "GREŠKA: partija nije pronađena. Ova scena se učitava iz lobija i ne pokreće se sama.";
            }
        }

        private void OnEnable()
        {
            if (_controller == null)
            {
                return;
            }

            _controller.RoleReceived += OnRoleReceived;
            _controller.PhaseChanged += OnPhaseChanged;
            _controller.NightResolved += OnNightResolved;
            _controller.VotingResolved += OnVotingResolved;
            _controller.PlayerLeft += OnPlayerLeft;
            _controller.StateChanged += Render;
            _controller.DetectiveResultReceived += OnDetectiveResult;
            _controller.IntentRejected += OnIntentFeedback;
            _controller.HostNotice += OnHostNotice;

            // The match is already running by the time this scene finishes loading, and the role was
            // dealt before that. Ask for what is already known rather than waiting for a new event.
            AdoptCurrentRole();
            Render();
        }

        private void OnDisable()
        {
            if (_controller == null)
            {
                return;
            }

            _controller.RoleReceived -= OnRoleReceived;
            _controller.PhaseChanged -= OnPhaseChanged;
            _controller.NightResolved -= OnNightResolved;
            _controller.VotingResolved -= OnVotingResolved;
            _controller.PlayerLeft -= OnPlayerLeft;
            _controller.StateChanged -= Render;
            _controller.DetectiveResultReceived -= OnDetectiveResult;
            _controller.IntentRejected -= OnIntentFeedback;
            _controller.HostNotice -= OnHostNotice;
        }

        /// <summary>
        /// Picks up the role this client was already told about. The role arrives over the network
        /// the moment the host deals it, which is before this scene has finished loading, so waiting
        /// for the event alone would leave the player staring at a screen with no role on it.
        /// </summary>
        private void AdoptCurrentRole()
        {
            if (_controller.LocalRole != null)
            {
                OnRoleReceived(_controller.LocalRole);
            }
        }

        private void OnRoleReceived(PrivateRoleInfo info)
        {
            _localRole = info.Role;
            _hasRole = true;

            var builder = new StringBuilder();
            builder.Append($"Tvoje mesto: {info.Seat + 1}  |  Uloga: {RoleName(info.Role)}");
            if (info.Role == Role.Mafia && info.MafiaTeammateSeats.Count > 0)
            {
                builder.Append("\nSaigrači (Mafija): ");
                var seats = new List<string>();
                foreach (int seat in info.MafiaTeammateSeats)
                {
                    seats.Add("mesto " + (seat + 1));
                }

                builder.Append(string.Join(", ", seats));
            }

            _roleLine = builder.ToString();

            // The role can arrive after the phase change, so re-render to build this role's controls.
            Render();
        }

        private void OnPhaseChanged(MatchPhase phase) => Render();

        /// <summary>
        /// A seat gave up after being absent too long. No role is shown: dropping out must not
        /// reveal what somebody was.
        /// </summary>
        private void OnPlayerLeft(int seat)
        {
            _resultText.text = $"Mesto {seat + 1} je napustilo partiju (nije se vratilo na vreme).";
            Render();
        }

        /// <summary>
        /// Renders everything from the current replicated state. Called on every state change rather
        /// than only on the phase change, because a client can apply the phase before the masks and
        /// the outcome arrive — rendering once, on the phase, left clients with a stale screen.
        /// </summary>
        private void Render()
        {
            if (_controller == null)
            {
                return;
            }

            _phaseText.text = PhaseHeader();
            _roleText.text = _hasRole
                ? _roleLine
                : "Gledaš partiju — nemaš mesto u njoj.";

            RefreshHostControls();
            RebuildActionControls();
        }

        /// <summary>
        /// Repaints only the header so the countdown ticks. The rest of the screen is event-driven;
        /// redrawing the seat buttons every frame would rebuild them under the player's cursor.
        /// </summary>
        private void Update()
        {
            if (_controller == null)
            {
                return;
            }

            int seconds = RemainingWholeSeconds();
            if (seconds == _shownSeconds)
            {
                return;
            }

            _shownSeconds = seconds;
            _phaseText.text = PhaseHeader();
        }

        private int RemainingWholeSeconds() =>
            _controller.HasPhaseDeadline ? Mathf.CeilToInt((float)_controller.RemainingSeconds) : -1;

        /// <summary>
        /// Top line: the phase, its remaining time, and the phase-specific extra (the vote tally or
        /// the final outcome). Built in one place so the countdown and a state change never disagree.
        /// </summary>
        private string PhaseHeader()
        {
            MatchPhase phase = _controller.CurrentPhase;
            string header = "Faza: " + PhaseName(phase);

            int seconds = RemainingWholeSeconds();
            if (seconds >= 0)
            {
                header += $" — {seconds}s";
            }

            if (phase == MatchPhase.GameOver)
            {
                header += " — " + OutcomeName(_controller.Outcome);
            }
            else if (phase == MatchPhase.Voting)
            {
                header += $" ({_controller.VotesCast}/{_controller.AliveCount} glasalo)";
            }

            return header;
        }

        private void OnNightResolved(NightPublicResult result)
        {
            if (!result.SomeoneDied)
            {
                _resultText.text = "Noć je prošla — niko nije eliminisan.";
                return;
            }

            string message = $"Eliminisan: mesto {result.KilledSeat + 1}";
            if (result.RevealedRole.HasValue)
            {
                message += $" (uloga: {RoleName(result.RevealedRole.Value)})";
            }

            _resultText.text = message;
        }

        private void OnVotingResolved(VotingPublicResult result)
        {
            switch (result.Outcome)
            {
                case VoteOutcome.Eliminated:
                    string message = $"Glasanje: eliminisan je mesto {result.EliminatedSeat + 1}";
                    if (result.RevealedRole.HasValue)
                    {
                        message += $" (uloga: {RoleName(result.RevealedRole.Value)})";
                    }

                    _resultText.text = message;
                    break;

                case VoteOutcome.TieRequiresRevote:
                    var seats = new List<string>();
                    foreach (int seat in result.TiedSeats)
                    {
                        seats.Add("mesto " + (seat + 1));
                    }

                    _resultText.text = "Nerešeno (" + string.Join(", ", seats) +
                                       ") — imaju poslednju reč, pa ide ponovno glasanje.";
                    break;

                default:
                    _resultText.text = "Glasanje: niko nije eliminisan.";
                    break;
            }

            Render();
        }

        private void OnDetectiveResult(DetectivePrivateResult result)
        {
            string verdict = result.IsMafia ? "JESTE Mafija" : "NIJE Mafija";
            _resultText.text = $"Istraga: mesto {result.TargetSeat + 1} {verdict}.";
        }

        private void OnIntentFeedback(IntentRejection reason)
        {
            _resultText.text = reason == IntentRejection.None
                ? "Akcija prihvaćena."
                : "Odbijeno: " + RejectionText(reason);
        }

        private void OnHostNotice(string message) => _resultText.text = message;

        private void RefreshHostControls()
        {
            bool host = _controller.IsHostPeer;
            MatchPhase phase = _controller.CurrentPhase;

            _confirmButton.gameObject.SetActive(host && phase == MatchPhase.RoleReveal);
            _resolveButton.gameObject.SetActive(host && phase == MatchPhase.Night);
            _discussButton.gameObject.SetActive(host && phase == MatchPhase.DayAnnouncement);
            _beginVoteButton.gameObject.SetActive(host && phase == MatchPhase.DayDiscussion);
            _resolveVoteButton.gameObject.SetActive(host && phase == MatchPhase.Voting);
            _revoteButton.gameObject.SetActive(host && phase == MatchPhase.TieBreaker);
            _returnToLobbyButton.gameObject.SetActive(host && phase == MatchPhase.GameOver);
        }

        /// <summary>
        /// Rebuilds the target buttons for the current phase: night actions for the local role, or
        /// vote targets during voting. Dead players get no buttons — the host rejects them anyway,
        /// this only avoids offering an action that cannot succeed.
        /// </summary>
        private void RebuildActionControls()
        {
            for (int i = _actionRow.childCount - 1; i >= 0; i--)
            {
                Destroy(_actionRow.GetChild(i).gameObject);
            }

            if (!_hasRole)
            {
                return;
            }

            MatchPhase phase = _controller.CurrentPhase;
            int selfSeat = _controller.LocalSeat;
            if (selfSeat < 0 || !_controller.IsSeatAlive(selfSeat))
            {
                return;
            }

            if (phase == MatchPhase.Night)
            {
                if (!HasNightAction(_localRole))
                {
                    return;
                }

                BuildTargetButtons(
                    seat => _controller.IsSeatAlive(seat) &&
                            (_localRole == Role.Doctor || seat != selfSeat), // only the Doctor may self-target
                    SubmitNightAction);
                return;
            }

            if (phase == MatchPhase.Voting)
            {
                BuildTargetButtons(_controller.IsVoteCandidate, seat => _controller.SubmitVoteServerRpc(seat));
            }
        }

        private void BuildTargetButtons(System.Func<int, bool> isOffered, System.Action<int> onClick)
        {
            int count = _controller.PlayerCount;
            for (int seat = 0; seat < count; seat++)
            {
                if (!isOffered(seat))
                {
                    continue;
                }

                int target = seat;
                Button button = UiFactory.CreateButton(_actionRow, $"Mesto {seat + 1}");
                button.onClick.AddListener(() => onClick(target));
            }
        }

        private void SubmitNightAction(int targetSeat)
        {
            switch (_localRole)
            {
                case Role.Mafia:
                    _controller.SubmitMafiaTargetServerRpc(targetSeat);
                    break;
                case Role.Doctor:
                    _controller.SubmitDoctorProtectServerRpc(targetSeat);
                    break;
                case Role.Detective:
                    _controller.SubmitDetectiveInvestigateServerRpc(targetSeat);
                    break;
            }
        }

        private static bool HasNightAction(Role role) =>
            role == Role.Mafia || role == Role.Doctor || role == Role.Detective;

        private static string RoleName(Role role) => role switch
        {
            Role.Mafia => "Mafija",
            Role.Doctor => "Doktor",
            Role.Detective => "Detektiv",
            _ => "Građanin"
        };

        private static string PhaseName(MatchPhase phase) => phase switch
        {
            MatchPhase.RoleReveal => "Otkrivanje uloga",
            MatchPhase.Night => "Noć",
            MatchPhase.NightResolution => "Razrešenje noći",
            MatchPhase.DayAnnouncement => "Objava dana",
            MatchPhase.DayDiscussion => "Diskusija",
            MatchPhase.Voting => "Glasanje",
            MatchPhase.VotingResolution => "Razrešenje glasanja",
            MatchPhase.TieBreaker => "Odbrana pred ponovno glasanje",
            MatchPhase.GameOver => "Kraj partije",
            _ => phase.ToString()
        };

        private static string OutcomeName(GameOutcome outcome) => outcome switch
        {
            GameOutcome.TownWins => "pobedio je grad",
            GameOutcome.MafiaWins => "pobedila je mafija",
            GameOutcome.Abandoned => "partija je prekinuta (premalo igrača)",
            _ => "nerešeno"
        };

        private static string RejectionText(IntentRejection reason) => reason switch
        {
            IntentRejection.WrongPhase => "nije vreme za tu akciju.",
            IntentRejection.NotAllowed => "nije ti dozvoljena ta akcija.",
            IntentRejection.InvalidTarget => "neispravna meta.",
            _ => "nepoznat razlog."
        };

        private void BuildUi()
        {
            Canvas canvas = UiFactory.CreateCanvas("MatchCanvas");
            canvas.transform.SetParent(transform, false);
            canvas.sortingOrder = 20; // above the lobby canvas underneath

            // Dim the 3D scene behind the UI: white text over the bright skybox was unreadable.
            // Not a raycast target, so it never intercepts a click meant for a button.
            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(canvas.transform, false);
            UiFactory.Anchor((RectTransform)backdrop.transform, Vector2.zero, Vector2.one);
            Image backdropImage = backdrop.GetComponent<Image>();
            backdropImage.color = new Color(0.06f, 0.06f, 0.09f, 0.82f);
            backdropImage.raycastTarget = false;

            _phaseText = UiFactory.CreateText(canvas.transform, "Phase", 30f, TextAlignmentOptions.Center);
            UiFactory.Anchor(_phaseText.rectTransform, new Vector2(0.05f, 0.80f), new Vector2(0.95f, 0.92f));

            _roleText = UiFactory.CreateText(canvas.transform, "Role", 26f, TextAlignmentOptions.Center);
            UiFactory.Anchor(_roleText.rectTransform, new Vector2(0.05f, 0.62f), new Vector2(0.95f, 0.78f));
            _roleText.text = string.Empty;

            _resultText = UiFactory.CreateText(canvas.transform, "Result", 24f, TextAlignmentOptions.Center);
            UiFactory.Anchor(_resultText.rectTransform, new Vector2(0.05f, 0.44f), new Vector2(0.95f, 0.60f));
            _resultText.text = string.Empty;

            Transform hostRow = UiFactory.CreateScrollColumn(canvas.transform, "HostRow",
                new Vector2(0.05f, 0.04f), new Vector2(0.45f, 0.42f));
            _confirmButton = UiFactory.CreateButton(hostRow, "Preskoči → Noć");
            _confirmButton.onClick.AddListener(() => _controller?.HostConfirmRolesSeen());
            _resolveButton = UiFactory.CreateButton(hostRow, "Preskoči → Razreši noć");
            _resolveButton.onClick.AddListener(() => _controller?.HostResolveNight());
            _discussButton = UiFactory.CreateButton(hostRow, "Preskoči → Diskusija");
            _discussButton.onClick.AddListener(() => _controller?.HostContinueToDiscussion());
            _beginVoteButton = UiFactory.CreateButton(hostRow, "Preskoči → Glasanje");
            _beginVoteButton.onClick.AddListener(() => _controller?.HostBeginVoting());
            _resolveVoteButton = UiFactory.CreateButton(hostRow, "Prebroj glasove");
            _resolveVoteButton.onClick.AddListener(() => _controller?.HostResolveVoting());
            _revoteButton = UiFactory.CreateButton(hostRow, "Preskoči → Ponovno glasanje");
            _revoteButton.onClick.AddListener(() => _controller?.HostBeginRevote());
            _returnToLobbyButton = UiFactory.CreateButton(hostRow, "Nazad u lobi");
            _returnToLobbyButton.onClick.AddListener(() => _controller?.HostReturnToLobby());

            _actionRow = UiFactory.CreateScrollColumn(canvas.transform, "ActionRow",
                new Vector2(0.55f, 0.04f), new Vector2(0.95f, 0.42f));
        }
    }
}
