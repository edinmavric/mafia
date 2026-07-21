# MafiaGame — Game Rules & Deferred Decisions

Authoritative record of confirmed gameplay rules and explicitly deferred items.
Updated by owner decision. When code and this file conflict, ask the owner.

## Confirmed rules (owner-approved)

### Lobby & configuration (host-configurable per match)
- Minimum **4** players. Host sets the lobby size.
- **Mafia count** — host chooses within the maximum allowed by lobby size:
  - ≤ 6 players → max 1 Mafia
  - 7–9 players → max 2 Mafia
  - ≥ 10 players → max 3 Mafia
  - Minimum is always 1 Mafia.
- **Doctor** and **Detective** are optional; host enables/disables them before the game:
  - A special role requires at least **5** players.
  - Having **both** special roles requires at least **7** players (at 5–6, at most one).
- Remaining players are Citizens.
- Guard: Mafia must start with fewer players than the non-Mafia side.

### Roles (MVP only)
- Citizen, Mafia, Doctor, Detective. No other roles.
- Factions: Mafia → Mafia faction; Citizen/Doctor/Detective → Town faction.

### Night
- **Mafia** submit a single shared team target (one action for the team).
- **Doctor** may protect any living player, **including self**, but **may NOT protect the
  same target on two consecutive nights** (interpretation applied: same target, covers self).
- **Detective** investigates one living target; result is **binary: Mafia / Not-Mafia**
  (Doctor/Detective/Citizen all read as Not-Mafia).
- Night resolution: the mafia target dies unless the Doctor protected exactly that target.

### Day / voting
- **Abstention** is allowed; only cast votes count. Abstaining means letting the clock run out — a
  player who does not want to vote simply does not press anything.
- A phase **ends early the moment nobody is left to act**: the night resolves as soon as every living,
  connected night role has submitted, and the vote is tallied as soon as every living, connected
  player has voted (owner decision, 2026-07-20). Dead and disconnected players are never waited on.
  Consequence: a vote (or a night target) can be changed only until the **last** player acts, not
  until the host tallies — once the round is complete it resolves immediately.
- Highest vote count is eliminated.
- **Tie** for the highest → the tied players get a **30-second defense** (`TieBreaker` phase), then
  **one revote** restricted to the tied candidates; if still tied → **no elimination** that day.

### Elimination reveal
- Whether an eliminated player's role is publicly revealed is a **host setting chosen before
  the game starts** (`RevealRoleOnElimination`).

### Win conditions
- **Town wins** when 0 Mafia are alive.
- **Mafia wins** when living Mafia ≥ living non-Mafia.
- Otherwise the game continues.

## Deferred (recorded so nothing slips through)

- **30-second "tie-breaker" defense window** and the **`TieBreaker` phase**. **Status: DONE.**
  A tied vote no longer flows straight into the revote: the day moves to `MatchPhase.TieBreaker`,
  where the tied players get **30 seconds** (`MatchTimings.TieBreakerSeconds`) to defend themselves,
  and only then does the revote open on those same candidates. The flow is
  `Voting → TieBreaker → Voting`, enforced in `MatchPhaseMachine`; the defense cannot reach a result
  on its own, so a tie can never skip the revote. Only the *first* tie of a day reaches it — a tie in
  the revote ends the day with no elimination through `VotingResolution`, so the pair cannot loop.
  The defense is talking time, not voting time: votes submitted during it are rejected as
  `WrongPhase`, and the round starts with a clean tally.
  The duration is a constant rather than a lobby setting, like the role-reveal and announcement
  times: it is not replicated, so host and clients agree on it without another value on the wire.
  `TieBreaker` is **appended** to the `MatchPhase` enum rather than inserted where it belongs in the
  flow, because the numeric values are already replicated and shifting them would silently mean a
  different phase to anything holding an older value.
- **Phase-gating of commands** (an action is only legal in its correct phase): deferred to the
  Application/command layer (Milestone 4). Milestone 1 domain services are phase-agnostic and
  validate only the intrinsic rule (living target, correct actor, etc.).
  **Status: DONE** — implemented in `LocalMatchDriver` (guards every operation) and enforced
  again server-side in `NetworkedMatchAuthority`.

### Milestone 4 — networked match (deferred within the vertical slice)

The first networked slice covers **role distribution + one night + night resolution** only.
The following are intentionally deferred and recorded here so they are not lost:

- **Networked day / voting / win loop.** **Status: DONE** — day announcement, discussion, voting
  (including the tie → revote round), elimination, and the win outcome are now host-authoritative
  and networked. Votes are seat-based intents validated server-side; a player may change their vote
  until the host tallies, and votes from disconnected seats are dropped. Public alive/dead status and
  the current vote candidates are replicated as bitmasks; no role data is broadcast.
  Still deferred here: **phase timers** (the host advances every phase by hand).
- **Phase timers.** **Status: DONE (authority side).** Role reveal, night, day announcement,
  discussion, and voting each run on a deadline owned by the host. The authority counts down through
  `Tick(deltaSeconds)` — elapsed time is passed in as a number, so the rules stay engine-free and the
  timeouts are unit-tested without waiting. On expiry the authority reports a `PhaseAdvance` and the
  transport carries it out; a client can neither run the clock nor influence it. Clients receive the
  **deadline** in NGO server time (one message per phase, not per frame) and count down locally for
  display only. Timeout semantics follow the confirmed rules: the night resolves with whatever
  intents arrived, and voting tallies whatever votes were cast (abstention is allowed). A phase also
  ends early once every living, connected player who owes an action has given it (see "Day / voting").
  The host buttons remain as manual "skip this phase" controls.
  Defaults are in `MatchTimings.Default`: reveal 10 s, night 45 s, announcement 8 s, discussion 90 s,
  voting 45 s. The host can change night / discussion / voting in the lobby (see "Lobby settings").
  The tie-breaker defense runs on the same clock at a fixed 30 s (see the tie-breaker item above).
- **Lobby settings.** **Status: DONE.** The host picks Mafia count, Doctor, Detective, role reveal,
  and the night / discussion / voting durations in the lobby; `MatchSetup` (engine-free, immutable)
  carries the choices and `MatchConfiguration` still decides whether they are legal. Options the
  lobby is too small for are **switched off automatically** (`MatchSetup.ClampTo`, owner decision
  2026-07-20) with a notice to the host — refusing each edit instead left a small lobby permanently
  stuck, because every individual change was still illegal on its own. The Detective is dropped
  before the Doctor, and the Mafia count only as far as it must be. The setup is re-clamped whenever
  the lobby changes size and once more just before roles are dealt. Settings can only change
  **before** the match starts. The agreed setup is replicated so every player sees the rules before they play —
  counts, flags and durations only, no role data.
  Not exposed in the UI (still the defaults): role-reveal duration 10 s and announcement duration 8 s.
- **Match scene.** **Status: DONE.** The match now happens in its own `Game` scene, which the host
  loads **additively** over the lobby through Netcode's scene manager the moment roles are dealt, and
  unloads on the way back. Additive rather than a full scene switch (owner decision 2026-07-21): the
  lobby scene is the network root — it holds the `NetworkManager` and the scene-placed
  `NetworkMatchController` — so replacing it would mean tearing the authority down and rebuilding it
  mid-session, for no gain the players can see. Netcode loads and unloads the scene on every peer at
  once, and a client that joins mid-match is synchronised into it automatically.
  The scene deliberately holds **no camera and no light** (the lobby scene has both) and no authored
  geometry: a single `MatchEnvironment` component builds a placeholder floor, table and seat ring
  from primitives at runtime, so the scene file stays trivial and cannot break a serialized
  reference. The real environment is Milestone 6. If the scene fails to load, the match plays on over
  the lobby background and only the host is told — the authority, not the scene, runs the game.
  Create it with **MafiaGame → Create Game scene**; the tool also registers it in Build Settings,
  which Netcode requires.
- **Returning to the lobby after a match.** **Status: DONE** (owner decision 2026-07-21: on a
  button, not automatically). At `GameOver` the host gets **"Nazad u lobi"**, which unloads the match
  scene and puts everyone back on the lobby screen with the same join code, ready to play again. The
  reset replaces the whole `NetworkedMatchAuthority` with a fresh instance rather than clearing it
  field by field, so no leftover vote, night action or absence timer can survive into the next match;
  every replicated value is reset with it, and each client drops its seat and role on the phase
  returning to `Lobby`.
  Still deferred: the match UI itself still lives on the lobby scene's canvas (it is a screen-space
  overlay, so it draws over either scene). Splitting `MatchNetworkView` into a lobby part and a match
  part, and moving the match part into the `Game` scene, is a later cleanup.
- **Seed source.** The host picks a non-predictable seed (`Guid.NewGuid().GetHashCode()`) so
  clients cannot control or predict role assignment. This is NOT cryptographic; if stronger
  guarantees are needed later, revisit. Clients never receive or influence the seed.
- **Disconnect and rejoin during a match.** **Status: absence + forfeit DONE; rejoin NOT WORKING —
  deferred to the end (owner decision 2026-07-21).** Two separate rules, and it matters
  that they are not confused:
  1. *Absence* — from the moment the connection drops the player is skipped. Nobody ever waits on
     them: a night action only they could supply is dropped so the night still resolves, and their
     vote is not counted. This was already true before the grace period existed.
  2. *Forfeit* — if they have not returned within **30 seconds** (`AbandonAfterSeconds`, owner
     decision 2026-07-20) they are removed from the match for good. This is an **elimination**, so
     it can decide the game: a Mafia who drops and never returns hands the town the win 30 seconds
     later. The countdown is driven by `TickAbsence(deltaSeconds)` — engine-free and unit-tested
     without waiting.
  **TODO (deferred to the very end of the project, owner decision 2026-07-21).** Returning in time
  is *intended* to restore the same seat and the same role across a full application restart, but
  live testing on 2026-07-21 showed the Relay/Sessions layer still **rejects the reconnect**, so a
  returning player never gets back in and is forfeited at 30 s like anyone else. The code below is
  in place and harmless — the two rules above are unaffected — but it must not be described as
  working. When this is picked up again, start by capturing the exact SDK exception on the
  returning client; the "already a member" path may not even be the one being hit. The rest of the
  match loop continues without waiting on this. The intended design, for whoever resumes it:
  1. *Sessions layer.* `RelayMatchSession` always tries a normal `JoinSessionByCodeAsync` first, so
     a fresh join is never diverted. A dropped player is still a member of the Relay session for a
     short while, so their fresh join is rejected with "player is already a member of the lobby";
     only then does it reconnect — `GetJoinedSessionIdsAsync()` then `ReconnectToSessionAsync(id)`,
     skipping any dead sessions left over from earlier games. (Reconnecting eagerly on every join was
     tried first and dragged fresh joins into reconnecting to dead allocations — "Failed to join
     allocation"; the join-first order is deliberate.)
  2. *Game layer.* The seat is tied to the player's **Unity Authentication id** (stable per profile,
     survives a restart). Every client announces its id to the host on connect (`IdentifyServerRpc`);
     the host ties it to the seat when roles are dealt, and on return recognises the id and re-sends
     the role — privately, to that one client.
  Security (owner decision 2026-07-20, "identify by account"): the account id is a client-supplied,
  publicly-visible claim, so this is **not spoof-proof** — a lobby member could in theory claim a
  *disconnected* member's id to take their seat and role. Accepted for the private-friends MVP and
  contained two ways: a seat is handed over only while it is actually flagged disconnected, and never
  to a connection that already holds a seat. Hardening (a server-issued secret, or a trusted
  transport-level identity) is deferred.
  A forfeit is announced as a **seat number only**, never a role, and it never ends the match on the
  spot: declaring "town wins" the instant the last Mafia's connection drops would reveal what that
  player was. The win is evaluated at the next natural resolution instead, at most one phase later.
  Still deferred: surviving an application restart, and a host "end match if below minimum players"
  control.

## Still open (not needed until later milestones)
- Whether dead players can speak / spectate (voice & presentation).
- Host migration, lobby code format (networking).
- Role-reveal and announcement durations in the lobby UI (currently fixed at the defaults).
