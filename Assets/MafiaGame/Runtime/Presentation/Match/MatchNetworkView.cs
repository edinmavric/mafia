using System.Collections.Generic;
using System.Text;
using MafiaGame.Application.Matches;
using MafiaGame.Domain.Matches;
using MafiaGame.Domain.Roles;
using MafiaGame.Domain.Voting;
using MafiaGame.Infrastructure.Networking;
using MafiaGame.Presentation.LocalPrototype;
using MafiaGame.Presentation.Lobby;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaGame.Presentation.Match
{
    /// <summary>
    /// Thin networked-match view for the Milestone 4 vertical slice. It renders the public phase,
    /// this player's private role, night controls for the local role, and the night result — and it
    /// forwards intents to the <see cref="NetworkMatchController"/>. It holds no game rules: every
    /// decision stays on the host. The controller reference is assigned in the Inspector.
    /// </summary>
    public sealed class MatchNetworkView : MonoBehaviour
    {
        [SerializeField] private NetworkMatchController _controller;

        [Tooltip("Optional: the lobby bootstrap whose UI is hidden while a match is running.")]
        [SerializeField] private LobbyBootstrap _lobbyBootstrap;

        private TextMeshProUGUI _phaseText;
        private TextMeshProUGUI _roleText;
        private TextMeshProUGUI _resultText;

        private Button _startButton;
        private Button _confirmButton;
        private Button _resolveButton;
        private Button _discussButton;
        private Button _beginVoteButton;
        private Button _resolveVoteButton;
        private Transform _nightRow;

        private Role _localRole = Role.Citizen;
        private bool _hasRole;

        /// <summary>This player's private role line, kept so a re-render never loses it.</summary>
        private string _roleLine = string.Empty;

        private void Awake() => BuildUi();

        private void OnEnable()
        {
            if (_controller == null)
            {
                _resultText.text = "GREŠKA: NetworkMatchController nije povezan u Inspectoru.";
                return;
            }

            _controller.Ready += OnReady;
            _controller.RoleReceived += OnRoleReceived;
            _controller.PhaseChanged += OnPhaseChanged;
            _controller.NightResolved += OnNightResolved;
            _controller.VotingResolved += OnVotingResolved;
            _controller.ConnectedCountChanged += OnConnectedCountChanged;
            _controller.StateChanged += Render;
            _controller.DetectiveResultReceived += OnDetectiveResult;
            _controller.IntentRejected += OnIntentFeedback;
            _controller.HostNotice += OnHostNotice;
        }

        private void OnDisable()
        {
            if (_controller == null)
            {
                return;
            }

            _controller.Ready -= OnReady;
            _controller.RoleReceived -= OnRoleReceived;
            _controller.PhaseChanged -= OnPhaseChanged;
            _controller.NightResolved -= OnNightResolved;
            _controller.VotingResolved -= OnVotingResolved;
            _controller.ConnectedCountChanged -= OnConnectedCountChanged;
            _controller.StateChanged -= Render;
            _controller.DetectiveResultReceived -= OnDetectiveResult;
            _controller.IntentRejected -= OnIntentFeedback;
            _controller.HostNotice -= OnHostNotice;
        }

        private void OnReady() => Render();

        private void OnConnectedCountChanged(int count) => Render();

        /// <summary>
        /// The middle line: this player's private role once it is known, otherwise how many peers are
        /// really connected. The lobby player list counts everyone in the Relay session, which can be
        /// more — the connected count is what decides whether the match can start.
        /// </summary>
        private string InfoLine()
        {
            if (_hasRole)
            {
                return _roleLine;
            }

            int count = _controller.ConnectedCount;
            return $"Mrežno povezano: {count} igrača (za partiju treba najmanje {MatchConfiguration.MinPlayers})";
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

            MatchPhase phase = _controller.CurrentPhase;
            string header = "Faza: " + PhaseName(phase);
            if (phase == MatchPhase.GameOver)
            {
                header += " — " + OutcomeName(_controller.Outcome);
            }
            else if (phase == MatchPhase.Voting)
            {
                header += $" ({_controller.VotesCast}/{_controller.AliveCount} glasalo)";
            }

            _phaseText.text = header;
            _roleText.text = InfoLine();

            // Hide the lobby UI for the whole match so the two placeholder canvases never overlap.
            _lobbyBootstrap?.View?.SetVisible(phase == MatchPhase.Lobby);

            RefreshHostControls();
            RebuildActionControls();
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

                    _resultText.text = "Nerešeno (" + string.Join(", ", seats) + ") — ponovno glasanje.";
                    break;

                default:
                    _resultText.text = "Glasanje: niko nije eliminisan.";
                    break;
            }

            // A revote keeps the phase at Voting, so the phase event does not fire — re-render here.
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
            bool host = _controller != null && _controller.IsHostPeer;
            MatchPhase phase = _controller != null ? _controller.CurrentPhase : MatchPhase.Lobby;

            _startButton.gameObject.SetActive(host && phase == MatchPhase.Lobby);
            _confirmButton.gameObject.SetActive(host && phase == MatchPhase.RoleReveal);
            _resolveButton.gameObject.SetActive(host && phase == MatchPhase.Night);
            _discussButton.gameObject.SetActive(host && phase == MatchPhase.DayAnnouncement);
            _beginVoteButton.gameObject.SetActive(host && phase == MatchPhase.DayDiscussion);
            _resolveVoteButton.gameObject.SetActive(host && phase == MatchPhase.Voting);
        }

        /// <summary>
        /// Rebuilds the target buttons for the current phase: night actions for the local role, or
        /// vote targets during voting. Dead players get no buttons — the host rejects them anyway,
        /// this only avoids offering an action that cannot succeed.
        /// </summary>
        private void RebuildActionControls()
        {
            for (int i = _nightRow.childCount - 1; i >= 0; i--)
            {
                Destroy(_nightRow.GetChild(i).gameObject);
            }

            if (_controller == null || !_hasRole)
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
                Button button = UiFactory.CreateButton(_nightRow, $"Mesto {seat + 1}");
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
            MatchPhase.GameOver => "Kraj partije",
            _ => phase.ToString()
        };

        private static string OutcomeName(GameOutcome outcome) => outcome switch
        {
            GameOutcome.TownWins => "pobedio je grad",
            GameOutcome.MafiaWins => "pobedila je mafija",
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
            canvas.sortingOrder = 10; // draw above the lobby canvas

            // Dim the 3D scene behind the UI: white text over the bright skybox was unreadable.
            // Not a raycast target, so it never intercepts a click meant for a button.
            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(canvas.transform, false);
            Anchor((RectTransform)backdrop.transform, Vector2.zero, Vector2.one);
            Image backdropImage = backdrop.GetComponent<Image>();
            backdropImage.color = new Color(0.06f, 0.06f, 0.09f, 0.82f);
            backdropImage.raycastTarget = false;

            _phaseText = UiFactory.CreateText(canvas.transform, "Phase", 30f, TextAlignmentOptions.Center);
            Anchor(_phaseText.rectTransform, new Vector2(0.05f, 0.80f), new Vector2(0.95f, 0.92f));
            _phaseText.text = "Faza: Lobi";

            _roleText = UiFactory.CreateText(canvas.transform, "Role", 26f, TextAlignmentOptions.Center);
            Anchor(_roleText.rectTransform, new Vector2(0.05f, 0.62f), new Vector2(0.95f, 0.78f));
            _roleText.text = string.Empty;

            _resultText = UiFactory.CreateText(canvas.transform, "Result", 24f, TextAlignmentOptions.Center);
            Anchor(_resultText.rectTransform, new Vector2(0.05f, 0.44f), new Vector2(0.95f, 0.60f));
            _resultText.text = string.Empty;

            Transform hostRow = UiFactory.CreateScrollColumn(canvas.transform, "HostRow",
                new Vector2(0.05f, 0.04f), new Vector2(0.45f, 0.42f));
            _startButton = UiFactory.CreateButton(hostRow, "Počni partiju");
            _startButton.onClick.AddListener(() => _controller?.HostStartMatch());
            _confirmButton = UiFactory.CreateButton(hostRow, "Svi videli uloge → Noć");
            _confirmButton.onClick.AddListener(() => _controller?.HostConfirmRolesSeen());
            _resolveButton = UiFactory.CreateButton(hostRow, "Razreši noć");
            _resolveButton.onClick.AddListener(() => _controller?.HostResolveNight());
            _discussButton = UiFactory.CreateButton(hostRow, "Počni diskusiju");
            _discussButton.onClick.AddListener(() => _controller?.HostContinueToDiscussion());
            _beginVoteButton = UiFactory.CreateButton(hostRow, "Počni glasanje");
            _beginVoteButton.onClick.AddListener(() => _controller?.HostBeginVoting());
            _resolveVoteButton = UiFactory.CreateButton(hostRow, "Prebroj glasove");
            _resolveVoteButton.onClick.AddListener(() => _controller?.HostResolveVoting());

            _nightRow = UiFactory.CreateScrollColumn(canvas.transform, "NightRow",
                new Vector2(0.55f, 0.04f), new Vector2(0.95f, 0.42f));

            RefreshHostControls();
        }

        private static void Anchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
