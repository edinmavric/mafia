using System.Collections.Generic;
using System.Text;
using MafiaGame.Application.Matches;
using MafiaGame.Domain.Matches;
using MafiaGame.Domain.Roles;
using MafiaGame.Infrastructure.Networking;
using MafiaGame.Presentation.LocalPrototype;
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

        private TextMeshProUGUI _phaseText;
        private TextMeshProUGUI _roleText;
        private TextMeshProUGUI _resultText;

        private Button _startButton;
        private Button _confirmButton;
        private Button _resolveButton;
        private Transform _nightRow;

        private Role _localRole = Role.Citizen;
        private bool _hasRole;

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
            _controller.DetectiveResultReceived -= OnDetectiveResult;
            _controller.IntentRejected -= OnIntentFeedback;
            _controller.HostNotice -= OnHostNotice;
        }

        private void OnReady() => RefreshHostControls();

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

            _roleText.text = builder.ToString();
        }

        private void OnPhaseChanged(MatchPhase phase)
        {
            _phaseText.text = "Faza: " + PhaseName(phase);
            RefreshHostControls();
            RebuildNightControls(phase);
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
        }

        private void RebuildNightControls(MatchPhase phase)
        {
            for (int i = _nightRow.childCount - 1; i >= 0; i--)
            {
                Destroy(_nightRow.GetChild(i).gameObject);
            }

            if (phase != MatchPhase.Night || !_hasRole || !HasNightAction(_localRole))
            {
                return;
            }

            int count = _controller.PlayerCount;
            int selfSeat = _controller.LocalSeat;
            for (int seat = 0; seat < count; seat++)
            {
                if (_localRole != Role.Doctor && seat == selfSeat)
                {
                    continue; // only the Doctor may target itself
                }

                int target = seat;
                Button button = UiFactory.CreateButton(_nightRow, $"Mesto {seat + 1}");
                button.onClick.AddListener(() => SubmitNightAction(target));
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
            MatchPhase.GameOver => "Kraj partije",
            _ => phase.ToString()
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

            _phaseText = UiFactory.CreateText(canvas.transform, "Phase", 30f, TextAlignmentOptions.Center);
            Anchor(_phaseText.rectTransform, new Vector2(0.05f, 0.80f), new Vector2(0.95f, 0.92f));
            _phaseText.text = "Faza: Lobi";

            _roleText = UiFactory.CreateText(canvas.transform, "Role", 26f, TextAlignmentOptions.Center);
            Anchor(_roleText.rectTransform, new Vector2(0.05f, 0.62f), new Vector2(0.95f, 0.78f));
            _roleText.text = string.Empty;

            _resultText = UiFactory.CreateText(canvas.transform, "Result", 24f, TextAlignmentOptions.Center);
            Anchor(_resultText.rectTransform, new Vector2(0.05f, 0.44f), new Vector2(0.95f, 0.60f));
            _resultText.text = string.Empty;

            Transform hostRow = CreateColumn(canvas.transform, "HostRow",
                new Vector2(0.05f, 0.06f), new Vector2(0.42f, 0.40f));
            _startButton = UiFactory.CreateButton(hostRow, "Počni partiju");
            _startButton.onClick.AddListener(() => _controller?.HostStartMatch());
            _confirmButton = UiFactory.CreateButton(hostRow, "Svi videli uloge → Noć");
            _confirmButton.onClick.AddListener(() => _controller?.HostConfirmRolesSeen());
            _resolveButton = UiFactory.CreateButton(hostRow, "Razreši noć");
            _resolveButton.onClick.AddListener(() => _controller?.HostResolveNight());

            _nightRow = CreateColumn(canvas.transform, "NightRow",
                new Vector2(0.58f, 0.06f), new Vector2(0.95f, 0.40f));

            RefreshHostControls();
        }

        private static Transform CreateColumn(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Anchor((RectTransform)go.transform, anchorMin, anchorMax);

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return go.transform;
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
