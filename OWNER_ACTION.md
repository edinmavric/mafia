# OWNER ACTION — ručni Unity Editor koraci

Ovaj fajl beleži korake koje moram da uradim ja (vlasnik) u Unity Editoru, jer se ne mogu
bezbedno izvesti iz koda. Kada završiš korak, može se obeležiti `[x]`.

---

## Milestone 2 — pokretanje lokalnog prototipa

Kod, asmdef-ovi i testovi su već napravljeni. PlayMode i EditMode testovi prolaze headless
(bez ovih koraka). Sledeći koraci su potrebni samo da bi se prototip **igrao** iz Editora.

### [x] 1. Import TextMeshPro resursa (jednom po projektu)
- Otvori Unity, sačekaj da se projekat kompajlira (bez crvenih grešaka u Console).
- Meni: `Window > TextMeshPro > Import TMP Essential Resources`.
- Klikni `Import` u prozoru koji se otvori.
- Očekivano: pojavi se `Assets/TextMesh Pro/` folder. Bez ovoga TMP tekst se neće iscrtati.

### [x] 2. Napravi scenu prototipa
- `File > New Scene` → izaberi prazan (Empty) ili Basic template.
- Sačuvaj kao: `Assets/MafiaGame/Content/Scenes/LocalPrototype.unity`.
  (Ako folder ne postoji, napravi ga u Project prozoru: `Assets/MafiaGame/Content/Scenes`.)
- U Hierarchy: `Right click > Create Empty`, preimenuj u `PrototypeBootstrap`.
- Sa selektovanim `PrototypeBootstrap`: u Inspectoru `Add Component` → otkucaj
  `PrototypeBootstrap` → dodaj komponentu `PrototypeBootstrap` (namespace `MafiaGame.Presentation.LocalPrototype`).
- Sačuvaj scenu (`Ctrl+S`). Ništa drugo ne treba ručno da se povezuje — Canvas, EventSystem
  i UI se prave iz koda pri pokretanju.

### [x] 3. Dodaj scenu u Build Settings
- `File > Build Settings` (ili `Build Profiles` u Unity 6).
- `Add Open Scenes` da dodaš `LocalPrototype`.
- (Opciono) prevuci je na vrh liste. Ovo je potrebno ako kasnije budemo pokretali headless
  PlayMode test koji učitava scenu; trenutni PlayMode test ne zavisi od ovoga.

### [x] 4. Igraj
- Otvori `LocalPrototype` scenu, pritisni `Play`.
- Očekivano: pojavi se ekran „MafiaGame — lokalni prototip (DEV)" sa 3 dugmeta postavki.
- Klikom kroz: postavka → redom otkrivanje uloga (pass-and-play) → noć (Mafija/Doktor/Detektiv)
  → jutro → diskusija → glasanje (svako redom) → ishod → sledeći krug ili „Kraj igre".

### [x] Ako nešto ne radi
- Nema teksta na dugmadi: nisi uradio korak 1 (TMP Essentials).
- Klik na dugme ne reaguje: proveri da u sceni postoji `EventSystem` (pravi se automatski);
  Console poruka „Could not assign default UI input actions" znači da treba proveriti
  `Edit > Project Settings > Player > Active Input Handling` (očekivano: Input System Package).
- Bilo koja crvena Console greška: pošalji mi ceo tekst greške.

---

## Milestone 3 — UGS (Unity Gaming Services) setup

Mrežni paketi (NGO + Relay + Lobby + Authentication + Core) su instalirani i projekat se
kompajlira. Pre nego što Relay/Lobby kod može da radi, potrebno je povezati UGS projekat.
Ovo se NE može uraditi iz CLI-ja — traži Unity nalog i Dashboard.

### [x] 1. Prijava i povezivanje projekta
- Otvori Unity, gore desno se prijavi na svoj Unity nalog (ako nisi).
- `Edit > Project Settings > Services` → izaberi organizaciju i `Create`/`Link` UGS projekat.
- Očekivano: prikaže se Project ID (i Environment `production`).

### [x] 2. Uključi servise na Dashboard-u
- Idi na https://dashboard.unity3d.com → izaberi ovaj projekat.
- Uključi: **Authentication** (Anonymous sign-in), **Relay**, **Lobby**.
- Free tier je dovoljan za privatne testove.

### [x] 3. Potvrda
- U `Project Settings > Services` proveri da je projekat „Linked".
- Odgovori `done` ili mi pošalji tekst greške ako se pojavi.

Napomena: dok ovo ne bude povezano, mrežni kod pišem i unit-testiram bez pozivanja Relay/Lobby-ja
(kroz apstrakcije); pravi host-join test radimo tek posle povezivanja.

---

## Milestone 3 — lobi scena i test host/join

Mrežni sloj (auth + Relay/Lobby sesija + lobi UI) je napisan i unit-testiran. Za pravi
host/join test treba scena sa NGO `NetworkManager`-om (Sessions API preko njega diže Relay).

### [ ] 1. Napravi lobi scenu
- `File > New Scene` (Empty) → sačuvaj kao `Assets/MafiaGame/Content/Scenes/Lobby.unity`.

### [ ] 2. Dodaj NetworkManager sa transportom
- Hierarchy: `Create Empty`, preimenuj u `NetworkManager`.
- `Add Component` → `Network Manager` (Netcode for GameObjects).
- Na isti GameObject: `Add Component` → `Unity Transport`.
- U `Network Manager` inspektoru, pod `Network Transport`, dodeli tu `Unity Transport`
  komponentu (obično se sama poveže; ako ne, prevuci je u polje).

### [ ] 3. Dodaj lobi bootstrap
- `Create Empty`, preimenuj u `LobbyBootstrap`.
- `Add Component` → `LobbyBootstrap` (namespace `MafiaGame.Presentation.Lobby`).
- Sačuvaj scenu.

### [ ] 4. Dodaj `Lobby` scenu u Build Settings.

### [ ] 5. Test host/join (dva učesnika)
- Preporuka: `Window > Multiplayer > Multiplayer Play Mode` → uključi 1 dodatnog virtuelnog
  igrača (ukupno 2), pa `Play`.
- U jednom prozoru klikni `Napravi igru (Host)` → prikaže se kod.
- U drugom prozoru upiši taj kod i klikni `Pridruži se kodom`.
- Očekivano: oba prozora pokažu listu igrača (2). Ako se pojavi greška, pošalji mi je.

Napomena: ovo je MREŽNI TEMELJ (prijava + sesija + lobi). Sinhronizacija same partije preko
mreže (tajne uloge po klijentu, autoritativne komande/RPC, faze) je Milestone 4.

---
Napomena: prototip je DEV/lokalni test harness — namerno prikazuje sve uloge operateru na
jednom uređaju. Skrivanje informacija po igraču/klijentu dolazi tek sa mrežnim slojem
(Milestone 3+); ova arhitektura to ne blokira.
