using MafiaGame.Application.Matches;
using MafiaGame.Domain.Matches;
using MafiaGame.Infrastructure.Networking;
using MafiaGame.Presentation.LocalPrototype;
using MafiaGame.Presentation.Lobby;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaGame.Presentation.Match
{
    /// <summary>
    /// The host's half of the lobby screen: the agreed match settings, the sheet for changing them,
    /// and the button that starts the match. It sits in the lobby scene alongside
    /// <see cref="LobbyBootstrap"/>, which owns joining and the player list.
    ///
    /// Everything that happens *during* a match belongs to <see cref="MatchScreenView"/>, which lives
    /// in the match scene and therefore only exists while a match is running. This screen simply
    /// steps aside for the duration instead of hiding a second set of controls.
    ///
    /// The class name is deliberately unchanged: the lobby scene references this component, and
    /// renaming a MonoBehaviour breaks that reference.
    /// </summary>
    public sealed class MatchNetworkView : MonoBehaviour
    {
        [SerializeField] private NetworkMatchController _controller;

        [Tooltip("Optional: the lobby bootstrap whose UI is hidden while a match is running.")]
        [SerializeField] private LobbyBootstrap _lobbyBootstrap;

        // Host-only lobby settings, shown as a sheet over the lobby.
        private Transform _setupRow;
        private GameObject _setupRoot;
        private Button _openSetupButton;
        private Button _startButton;
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

        private void Awake() => BuildUi();

        private void OnEnable()
        {
            if (_controller == null)
            {
                Debug.LogError("MatchNetworkView: NetworkMatchController nije povezan u Inspectoru.");
                return;
            }

            _controller.Ready += Render;
            _controller.PhaseChanged += OnPhaseChanged;
            _controller.ConnectedCountChanged += OnConnectedCountChanged;
            _controller.StateChanged += Render;
            _controller.HostNotice += OnHostNotice;
        }

        private void OnDisable()
        {
            if (_controller == null)
            {
                return;
            }

            _controller.Ready -= Render;
            _controller.PhaseChanged -= OnPhaseChanged;
            _controller.ConnectedCountChanged -= OnConnectedCountChanged;
            _controller.StateChanged -= Render;
            _controller.HostNotice -= OnHostNotice;
        }

        private void OnConnectedCountChanged(int count) => Render();

        private void OnPhaseChanged(MatchPhase phase) => Render();

        /// <summary>
        /// How many peers are really connected, plus the rules everyone is about to play under. The
        /// lobby player list counts everyone in the Relay session, which can be more — this count is
        /// what decides whether the match can start.
        /// </summary>
        private string SummaryLine()
        {
            int count = _controller.ConnectedCount;
            string line =
                $"Mrežno povezano: {count} igrača (za partiju treba najmanje {MatchConfiguration.MinPlayers})";

            // Everyone sees the agreed rules before the match — counts and durations only, no roles.
            return line + "\n" + _controller.Setup.Describe();
        }

        /// <summary>
        /// Shows the lobby summary and the host's controls, and gets out of the way entirely once a
        /// match is running — the match has its own screen, in its own scene.
        /// </summary>
        private void Render()
        {
            if (_controller == null)
            {
                return;
            }

            bool inLobby = _controller.CurrentPhase == MatchPhase.Lobby;
            _lobbyBootstrap?.View?.SetVisible(inLobby);
            if (inLobby)
            {
                _lobbyBootstrap?.View?.ShowMatchSummary(SummaryLine());
            }

            RefreshHostControls();
        }

        /// <summary>
        /// Shows a host-only message where the host is actually looking: on the lobby status line, and
        /// on the settings sheet when it is open and covering that line. Mid-match notices belong to
        /// the match screen, which shows them itself.
        /// </summary>
        private void OnHostNotice(string message)
        {
            if (_controller != null && _controller.CurrentPhase != MatchPhase.Lobby)
            {
                return;
            }

            _lobbyBootstrap?.View?.ShowStatus(message);
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
            UiFactory.Anchor((RectTransform)sheet.transform, Vector2.zero, Vector2.one);
            sheet.GetComponent<Image>().color = new Color(0.04f, 0.04f, 0.07f, 0.97f);
            _setupRoot = sheet;

            TextMeshProUGUI title = UiFactory.CreateText(
                sheet.transform, "Title", 30f, TextAlignmentOptions.Center);
            UiFactory.Anchor(title.rectTransform, new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.96f));
            title.text = "Podešavanja partije";

            _setupHintText = UiFactory.CreateText(sheet.transform, "Hint", 22f, TextAlignmentOptions.Center);
            UiFactory.Anchor(_setupHintText.rectTransform, new Vector2(0.05f, 0.76f), new Vector2(0.95f, 0.85f));
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

        /// <summary>
        /// Only two controls belong to this screen, and both only before the match starts: changing
        /// the rules mid-game would move the goalposts on players who already know their roles.
        /// Leaving the lobby closes the sheet, so a match can never start with it covering the screen.
        /// </summary>
        private void RefreshHostControls()
        {
            bool host = _controller != null && _controller.IsHostPeer;
            MatchPhase phase = _controller != null ? _controller.CurrentPhase : MatchPhase.Lobby;

            bool canConfigure = host && phase == MatchPhase.Lobby;
            _setupSheetOpen &= canConfigure;
            _setupRoot.SetActive(_setupSheetOpen);
            if (_setupSheetOpen)
            {
                RefreshSetupLabels();
            }

            _openSetupButton.gameObject.SetActive(canConfigure && !_setupSheetOpen);
            _startButton.gameObject.SetActive(canConfigure && !_setupSheetOpen);
        }

        private void BuildUi()
        {
            Canvas canvas = UiFactory.CreateCanvas("LobbyHostCanvas");
            canvas.transform.SetParent(transform, false);
            canvas.sortingOrder = 10; // draw above the lobby canvas

            Transform hostRow = UiFactory.CreateScrollColumn(canvas.transform, "HostRow",
                new Vector2(0.05f, 0.04f), new Vector2(0.45f, 0.42f));
            _startButton = UiFactory.CreateButton(hostRow, "Počni partiju");
            _startButton.onClick.AddListener(() => _controller?.HostStartMatch());
            _openSetupButton = UiFactory.CreateButton(hostRow, "Podešavanja partije");
            _openSetupButton.onClick.AddListener(() => SetSetupSheetOpen(true));

            BuildSetupSheet(canvas.transform);

            RefreshHostControls();
        }
    }
}
