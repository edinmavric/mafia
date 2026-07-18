# ADR-001: Networking framework and authority model

## Status
Accepted (2026-07-18)

## Context
Milestone 3 needs multiplayer for a private-lobby, host-authoritative social-deduction game:
6–10 players, PC, low and bursty traffic (turn/phase based, not twitch), friends-only lobbies
joined by code. The domain and application layers are already engine-free and authority-friendly
(`LocalMatchDriver` centralizes authoritative state and phase transitions), so networking must
add transport, connection/lobby management, and authoritative command routing — not game rules.

A networking framework and an authority/hosting model must be chosen before any package is
installed (CLAUDE.md §12, §20).

## Decision
- Framework: **Unity Netcode for GameObjects (NGO)**.
- Session/connectivity: **Unity Relay** (NAT traversal via join code) and **Unity Lobby**,
  with **Unity Authentication** (anonymous) and **Services Core** — the Unity Gaming Services
  (UGS) stack.
- Authority/hosting model: **host-client + Relay**. One player hosts and is the authority;
  others join through Relay by code. No dedicated server for the MVP.
- Host migration is **deferred**; on host loss the match ends for the MVP.

## Consequences
- First-party stack with the best Unity 6 integration, official samples (Boss Room), and the
  easiest learning path for the owner.
- Free tier is sufficient for private MVP playtests; Relay/Lobby usage stays within free limits.
- Requires a **UGS account + linked project** (owner action; cannot be done from CLI).
- Relay/Lobby couple session management to UGS; NGO itself stays transport-agnostic, so the
  relay/session layer could be swapped later without rewriting gameplay.
- The existing authoritative boundary (`LocalMatchDriver`) maps onto server-authoritative NGO
  logic: clients send intents (commands), the host validates and owns truth. Hidden-role data is
  sent per-client, never broadcast.
- Host-client means the host has zero latency and is trusted as the authority; acceptable for a
  friends MVP. Revisit for public/ranked play.

## Alternatives considered
- **Fish-Net (OSS):** free, no vendor lock-in, capable; but internet play needs a separate relay
  provider (e.g. Edgegap) and more manual wiring. Viable fallback if we want to avoid UGS.
- **Photon Fusion 2:** mature hosted networking, easy relay/matchmaking; stronger vendor lock-in
  and CCU-based pricing beyond the free tier.
- **Dedicated server:** more robust (no host advantage, easier migration) but out of MVP scope
  due to setup, cost, and infrastructure.
