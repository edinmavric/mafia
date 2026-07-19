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
- **Abstention** is allowed; only cast votes count.
- Highest vote count is eliminated.
- **Tie** for the highest → **one revote** restricted to the tied candidates; if still tied →
  **no elimination** that day.

### Elimination reveal
- Whether an eliminated player's role is publicly revealed is a **host setting chosen before
  the game starts** (`RevealRoleOnElimination`).

### Win conditions
- **Town wins** when 0 Mafia are alive.
- **Mafia wins** when living Mafia ≥ living non-Mafia.
- Otherwise the game continues.

## Deferred (recorded so nothing slips through)

- **30-second "tie-breaker" defense window** (tied players give a last defense before the
  revote): this is a timer/presentation concern. The domain must not use real time, so it is
  NOT modeled in Milestone 1. It arrives with the authority/timer layer (Milestone 4). The
  domain models the revote purely as a second scoped voting round.
- **A dedicated `TieBreaker` phase** in `MatchPhase`: deferred to avoid churning the already
  committed serialized enum; add it together with the timer/authority layer.
- **Phase-gating of commands** (an action is only legal in its correct phase): deferred to the
  Application/command layer (Milestone 4). Milestone 1 domain services are phase-agnostic and
  validate only the intrinsic rule (living target, correct actor, etc.).
  **Status: DONE** — implemented in `LocalMatchDriver` (guards every operation) and enforced
  again server-side in `NetworkedMatchAuthority`.

### Milestone 4 — networked match (deferred within the vertical slice)

The first networked slice covers **role distribution + one night + night resolution** only.
The following are intentionally deferred and recorded here so they are not lost:

- **Networked day / voting / win loop.** Only the night is wired over the network so far.
  Day announcement, discussion, voting, elimination, and win-condition broadcast still need a
  networked step (next M4 increment). The pure rules already exist in the domain/`LocalMatchDriver`.
- **Temporary auto-config (no lobby-settings UI yet).** `NetworkMatchController.HostStartMatch`
  currently derives the match config automatically: **1 Mafia**, Doctor only when ≥ 5 players,
  Detective only when ≥ 7 players, and **role reveal ON** (for easy testing). The host cannot yet
  choose Mafia count / special roles / reveal from UI. A lobby-settings screen that lets the host
  pick these (within the confirmed limits above) is deferred.
- **Match runs inside the `Lobby` scene.** No dedicated `Game` scene and no networked scene
  transition yet; the slice reuses the lobby scene. A proper scene flow is deferred.
- **Seed source.** The host picks a non-predictable seed (`Guid.NewGuid().GetHashCode()`) so
  clients cannot control or predict role assignment. This is NOT cryptographic; if stronger
  guarantees are needed later, revisit. Clients never receive or influence the seed.
- **Disconnect during a match.** Confirmed behavior (owner): a disconnected player stays in the
  roster but is treated as absent — a night action only they could supply is dropped so the night
  still resolves, and their vote is not counted. **Rejoin, grace period, and a host "end match if
  below minimum players" control are deferred.**

## Still open (not needed until later milestones)
- Whether dead players can speak / spectate (voice & presentation).
- Reconnect grace period, host migration, lobby code format (networking).
- Discussion / night / voting durations (timers).
