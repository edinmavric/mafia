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

        private GameObject _backdrop;
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

        // Host-only lobby settings, shown as a sheet over the lobby.
        private Transform _setupRow;
        private GameObject _setupRoot;
        private Button _openSetupButton;
        private TextMeshProUGUI _setupHintText;
        private bool _setupSheetOpen;

        /// <summary>Last host-only message, so the sheet can show it instead of the generic hint.</summary>
        private string _setupNotice = string.Empty;

        private Button _mafiaCountButton;
        private Button _doctorButton;
        private Button _detectiveButton;
        private Button _revealButton;
        private Button _nightSecondsButton;
        private Button _discussionSecondsButton;
        private Button _votingSecondsButton;

        // Ranges the lobby buttons cycle through. Kept here because they are a UI convenience;
        // the authoritative bounds live in MatchTimings.
        private const double MinNight = 15d;
        private const double MaxNight = 120d;
        private const double NightStep = 15d;
        private const double MinDiscussion = 30d;
        private const double MaxDiscussion = 300d;
        private const double DiscussionStep = 30d;
        private const double MinVoting = 15d;
        private const double MaxVoting = 120d;
        private const double VotingStep = 15d;

        private Role _localRole = Role.Citizen;
        private bool _hasRole;

        /// <summary>This player's private role line, kept so a re-render never loses it.</summary>
        private string _roleLine = string.Empty;

        /// <summary>Countdown value currently on screen; -1 means "no timer shown".</summary>
        private int _shownSeconds = -1;

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
            string line =
                $"Mrežno povezano: {count} igrača (za partiju treba najmanje {MatchConfiguration.MinPlayers})";

            // Everyone sees the agreed rules before the match — counts and durations only, no roles.
            return line + "\n" + _controller.Setup.Describe();
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
            bool inLobby = phase == MatchPhase.Lobby;

            // One screen owns the lobby. While it is up this view draws nothing of its own except
            // the host's controls: its dimmed backdrop and its texts sat on top of the lobby canvas,
            // and the two overlapped into an unreadable mess.
            _lobbyBootstrap?.View?.SetVisible(inLobby);
            _backdrop.SetActive(!inLobby);
            _phaseText.gameObject.SetActive(!inLobby);
            _roleText.gameObject.SetActive(!inLobby);
            _resultText.gameObject.SetActive(!inLobby);

            if (inLobby)
            {
                _lobbyBootstrap?.View?.ShowMatchSummary(InfoLine());
            }
            else
            {
                _phaseText.text = PhaseHeader();
                _roleText.text = InfoLine();
            }

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

        /// <summary>
        /// Shows a host-only message. It also goes to the settings sheet, which covers the normal
        /// result line — a rejected setting has to be visible where the host is looking.
        /// </summary>
        private void OnHostNotice(string message)
        {
            _resultText.text = message;

            // In the lobby this view's result line is hidden, so the message goes where the host is
            // actually looking — otherwise "I cannot start" would vanish silently.
            if (_controller != null && _controller.CurrentPhase == MatchPhase.Lobby)
            {
                _lobbyBootstrap?.View?.ShowStatus(message);
            }

            _setupNotice = message;
            if (_setupSheetOpen)
            {
                RefreshSetupLabels();
            }
        }

        /// <summary>
        /// Builds the settings sheet: a panel that covers the lobby while it is open. Its background
        /// is a raycast target on purpose, so a click meant for a setting can never fall through to
        /// the lobby underneath. Created last so it draws above everything else on this canvas.
        /// </summary>
        private void BuildSetupSheet(Transform parent)
        {
            var sheet = new GameObject("SetupSheet", typeof(RectTransform), typeof(Image));
            sheet.transform.SetParent(parent, false);
            Anchor((RectTransform)sheet.transform, Vector2.zero, Vector2.one);
            sheet.GetComponent<Image>().color = new Color(0.04f, 0.04f, 0.07f, 0.97f);
            _setupRoot = sheet;

            TextMeshProUGUI title = UiFactory.CreateText(
                sheet.transform, "Title", 30f, TextAlignmentOptions.Center);
            Anchor(title.rectTransform, new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.96f));
            title.text = "Podešavanja partije";

            _setupHintText = UiFactory.CreateText(sheet.transform, "Hint", 22f, TextAlignmentOptions.Center);
            Anchor(_setupHintText.rectTransform, new Vector2(0.05f, 0.76f), new Vector2(0.95f, 0.85f));
            _setupHintText.text = string.Empty;

            _setupRow = UiFactory.CreateScrollColumn(sheet.transform, "SetupRow",
                new Vector2(0.15f, 0.16f), new Vector2(0.85f, 0.74f));
            BuildSetupControls();

            Transform footer = UiFactory.CreateScrollColumn(sheet.transform, "SetupFooter",
                new Vector2(0.15f, 0.04f), new Vector2(0.85f, 0.14f));
            Button close = UiFactory.CreateButton(footer, "Sačuvaj i zatvori");
            close.onClick.AddListener(() => SetSetupSheetOpen(false));
        }

        /// <summary>
        /// Opens or closes the settings sheet. Every change is already sent to the host as it is
        /// made, so closing only hides the sheet — there is nothing left to save or to discard.
        /// </summary>
        private void SetSetupSheetOpen(bool open)
        {
            _setupSheetOpen = open;
            if (open)
            {
                _setupNotice = string.Empty;
            }

            Render();
        }

        /// <summary>
        /// Builds the host's lobby settings. Each control is one button that cycles through its
        /// allowed values and wraps around — a placeholder that cannot produce an illegal setup and
        /// needs no typing. The host still owns the truth; these only send the change.
        /// </summary>
        private void BuildSetupControls()
        {
            _mafiaCountButton = CreateSetupButton(() => _controller?.HostChangeSetup(NextMafiaCount));
            _doctorButton = CreateSetupButton(() =>
                _controller?.HostChangeSetup(setup => setup.WithDoctor(!setup.IncludeDoctor)));
            _detectiveButton = CreateSetupButton(() =>
                _controller?.HostChangeSetup(setup => setup.WithDetective(!setup.IncludeDetective)));
            _revealButton = CreateSetupButton(() =>
                _controller?.HostChangeSetup(setup => setup.WithRoleReveal(!setup.RevealRoleOnElimination)));

            _nightSecondsButton = CreateSetupButton(() => _controller?.HostChangeSetup(setup =>
                setup.WithNightSeconds(Cycle(setup.Timings.NightSeconds, MinNight, MaxNight, NightStep))));
            _discussionSecondsButton = CreateSetupButton(() => _controller?.HostChangeSetup(setup =>
                setup.WithDiscussionSeconds(
                    Cycle(setup.Timings.DiscussionSeconds, MinDiscussion, MaxDiscussion, DiscussionStep))));
            _votingSecondsButton = CreateSetupButton(() => _controller?.HostChangeSetup(setup =>
                setup.WithVotingSeconds(Cycle(setup.Timings.VotingSeconds, MinVoting, MaxVoting, VotingStep))));
        }

        private Button CreateSetupButton(UnityEngine.Events.UnityAction onClick)
        {
            Button button = UiFactory.CreateButton(_setupRow, string.Empty);
            button.onClick.AddListener(() =>
            {
                // Drop the previous message first; the host applies the change synchronously, so a
                // new notice lands right after this and a stale one never lingers.
                _setupNotice = string.Empty;
                onClick();
                RefreshSetupLabels();
            });

            return button;
        }

        /// <summary>Steps a duration up, wrapping back to the minimum once past the maximum.</summary>
        private static double Cycle(double current, double min, double max, double step)
        {
            double next = current + step;
            return next > max ? min : next;
        }

        /// <summary>
        /// Steps the Mafia count up, wrapping at whatever the current lobby size allows. With too few
        /// players connected to know the limit yet, it wraps at the smallest maximum.
        /// </summary>
        private MatchSetup NextMafiaCount(MatchSetup setup)
        {
            int players = _controller != null ? _controller.ConnectedCount : 0;
            int max = MatchConfiguration.MaxMafiaFor(
                players >= MatchConfiguration.MinPlayers ? players : MatchConfiguration.MinPlayers);
            int next = setup.MafiaCount + 1;
            return setup.WithMafiaCount(next > max ? 1 : next);
        }

        private static string YesNo(bool value) => value ? "DA" : "NE";

        private void RefreshSetupLabels()
        {
            if (_controller == null)
            {
                return;
            }

            MatchSetup setup = _controller.Setup;
            _setupHintText.text = string.IsNullOrEmpty(_setupNotice)
                ? $"Povezano: {_controller.ConnectedCount} igrača.  " +
                  $"Specijalna uloga traži bar {MatchConfiguration.MinPlayersForSpecialRole}, " +
                  $"obe bar {MatchConfiguration.MinPlayersForBothSpecialRoles} igrača.\n" +
                  "Klik na dugme menja vrednost."
                : _setupNotice;

            SetLabel(_mafiaCountButton, $"Mafija: {setup.MafiaCount}");
            SetLabel(_doctorButton, $"Doktor: {YesNo(setup.IncludeDoctor)}");
            SetLabel(_detectiveButton, $"Detektiv: {YesNo(setup.IncludeDetective)}");
            SetLabel(_revealButton, $"Otkrij ulogu eliminisanog: {YesNo(setup.RevealRoleOnElimination)}");
            SetLabel(_nightSecondsButton, $"Noć: {setup.Timings.NightSeconds:0}s");
            SetLabel(_discussionSecondsButton, $"Diskusija: {setup.Timings.DiscussionSeconds:0}s");
            SetLabel(_votingSecondsButton, $"Glasanje: {setup.Timings.VotingSeconds:0}s");
        }

        private static void SetLabel(Button button, string text)
        {
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = text;
            }
        }

        private void RefreshHostControls()
        {
            bool host = _controller != null && _controller.IsHostPeer;
            MatchPhase phase = _controller != null ? _controller.CurrentPhase : MatchPhase.Lobby;

            // Settings are the host's, and only before the match: changing the rules mid-game would
            // move the goalposts on players who already know their roles. Leaving the lobby closes
            // the sheet, so a match can never start with it covering the screen.
            bool canConfigure = host && phase == MatchPhase.Lobby;
            _setupSheetOpen &= canConfigure;
            _setupRoot.SetActive(_setupSheetOpen);
            if (_setupSheetOpen)
            {
                RefreshSetupLabels();
            }

            _openSetupButton.gameObject.SetActive(canConfigure && !_setupSheetOpen);
            _startButton.gameObject.SetActive(canConfigure && !_setupSheetOpen);
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
            _backdrop = backdrop;

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
            _openSetupButton = UiFactory.CreateButton(hostRow, "Podešavanja partije");
            _openSetupButton.onClick.AddListener(() => SetSetupSheetOpen(true));
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

            _nightRow = UiFactory.CreateScrollColumn(canvas.transform, "NightRow",
                new Vector2(0.55f, 0.04f), new Vector2(0.95f, 0.42f));

            BuildSetupSheet(canvas.transform);

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
