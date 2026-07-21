# ADR-002: Voice chat provider

## Status
Proposed — decision deferred (2026-07-21)

## Context
Voice is a core product feature: this game is meant to feel like Mafia played around a table, and
that is carried by talking, not by UI. Milestone 5 needs a provider chosen before any package is
added (CLAUDE.md §13, §20).

The constraint that turned out to decide most of this: **development happens on Ubuntu 22.04**.
A provider that cannot run in a Linux Editor cannot be tested or iterated on at all, whatever its
price or feature list.

Requirements that matter for this game specifically:
- separate channels (day discussion, Mafia-only night, dead/ghost), because the rules depend on
  who can hear whom;
- mute and, eventually, reporting;
- 6–10 players per match, private lobbies;
- the existing stack is NGO + Unity Relay/Lobby/Authentication (ADR-001).

## Options considered

### Vivox (Unity first-party) — disqualified
- Free tier is the most generous of the three: **5000 PCU/month, on any plan including
  Pay-as-you-go**; beyond that, buckets of 5000 PCU at $2000/month and down.
- Best moderation story, and it is part of the same UGS stack we already use.
- **No Linux support at all.** The official supported-platforms list covers Windows, macOS
  (limited), Android, iOS, consoles and WebGL; Linux appears nowhere, and Unity Support states
  plainly that Vivox does not provide a Linux SDK. Linux x64 has appeared in the Vivox *Core* SDK
  changelog, but not in the Unity package.
- Consequence: it could not be run in the Editor on the development machine, let alone tested.
  Ruled out on that alone.

### Photon Voice 2
- Linux supported (Opus libraries re-added in 2.24.1).
- Free tier is **20 CCU and development-only**; a shipped game needs a paid plan — $95/year for
  100 CCU, or $95/month for 500 CCU, plus bandwidth beyond the included allowance.
- Runs on Photon Cloud: a **second vendor, second account, second usage meter** and second point
  of failure alongside Unity Relay.

### Dissonance (Placeholder Software, Asset Store)
- Linux supported.
- **€110.41 one-time**; the "Dissonance For Netcode For GameObjects" integration package is
  **free** and was last updated **March 2026** for Unity 6000.0.23+, so it is actively maintained
  against our engine version.
- No service of its own: voice rides the **existing NGO transport**, therefore the existing Relay
  allocation. No extra account, no per-user pricing.
- Hidden cost: voice consumes **Unity Relay bandwidth**, free up to 150 GiB/month, then
  $0.09/GiB (US/EU). Rough estimate at Opus ~24 kbps with the host forwarding to ~9 peers:
  **20–30 MB per 20-minute match**, i.e. thousands of matches per month inside the free tier.
  This is an estimate, not a measurement, and should be re-checked before any commercial launch.
- Weaknesses: paid up front before the feature can be evaluated; moderation is basic, so reporting
  and automatic moderation would have to be built; single-vendor asset.

### Writing it ourselves — rejected
Unity ships no Opus encoder, so raw PCM over NGO would be wasteful, and echo cancellation, noise
suppression and voice activity detection are each substantial work. Not a sensible use of the
budget for an MVP.

## Decision
**Deferred** (owner decision, 2026-07-21). Voice is postponed and Milestone 6 (the 3D vertical
slice) is taken first. No voice package is installed and no account is created.

The recommendation on the table when it is picked up again is **Dissonance**, because it is the
only option that both runs on the development machine and adds no second service; Vivox stays
ruled out for as long as the Unity package has no Linux support.

## Consequences
- No voice code, package or dependency enters the project yet; nothing has to be undone later.
- Behavioural decisions that voice needs — who hears whom at night, whether the dead hear the
  living, proximity versus global, push-to-talk — stay open and are **not** to be assumed by any
  code written in the meantime (see docs/game-rules.md, "Still open").
- Presentation work in Milestone 6 must not build in an assumption that speaking is silent: seats
  should be able to show a "talking" state later without being redesigned.
- Re-check before deciding: whether the Vivox Unity package has gained Linux support, and whether
  Photon's and Dissonance's prices still hold. The figures here are from July 2026.

## Sources
- Vivox pricing and billing FAQ (Unity Support), Vivox Unity SDK supported platforms (Unity docs),
  "Does Vivox offer Linux support?" (Unity Support).
- Photon Voice pricing page (photonengine.com), Photon Voice 2 documentation.
- Dissonance For Netcode For GameObjects (Unity Asset Store), Dissonance documentation.
- Unity Relay pricing (Unity Support, "How is the Relay Service Priced?").
