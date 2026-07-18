# OWNER ACTION — ručni Unity Editor koraci

Ovaj fajl beleži korake koje moram da uradim ja (vlasnik) u Unity Editoru, jer se ne mogu
bezbedno izvesti iz koda. Kada završiš korak, može se obeležiti `[x]`.

---

## Milestone 2 — pokretanje lokalnog prototipa

Kod, asmdef-ovi i testovi su već napravljeni. PlayMode i EditMode testovi prolaze headless
(bez ovih koraka). Sledeći koraci su potrebni samo da bi se prototip **igrao** iz Editora.

### 1. Import TextMeshPro resursa (jednom po projektu)
- Otvori Unity, sačekaj da se projekat kompajlira (bez crvenih grešaka u Console).
- Meni: `Window > TextMeshPro > Import TMP Essential Resources`.
- Klikni `Import` u prozoru koji se otvori.
- Očekivano: pojavi se `Assets/TextMesh Pro/` folder. Bez ovoga TMP tekst se neće iscrtati.

### 2. Napravi scenu prototipa
- `File > New Scene` → izaberi prazan (Empty) ili Basic template.
- Sačuvaj kao: `Assets/MafiaGame/Content/Scenes/LocalPrototype.unity`.
  (Ako folder ne postoji, napravi ga u Project prozoru: `Assets/MafiaGame/Content/Scenes`.)
- U Hierarchy: `Right click > Create Empty`, preimenuj u `PrototypeBootstrap`.
- Sa selektovanim `PrototypeBootstrap`: u Inspectoru `Add Component` → otkucaj
  `PrototypeBootstrap` → dodaj komponentu `PrototypeBootstrap` (namespace `MafiaGame.Presentation.LocalPrototype`).
- Sačuvaj scenu (`Ctrl+S`). Ništa drugo ne treba ručno da se povezuje — Canvas, EventSystem
  i UI se prave iz koda pri pokretanju.

### 3. Dodaj scenu u Build Settings
- `File > Build Settings` (ili `Build Profiles` u Unity 6).
- `Add Open Scenes` da dodaš `LocalPrototype`.
- (Opciono) prevuci je na vrh liste. Ovo je potrebno ako kasnije budemo pokretali headless
  PlayMode test koji učitava scenu; trenutni PlayMode test ne zavisi od ovoga.

### 4. Igraj
- Otvori `LocalPrototype` scenu, pritisni `Play`.
- Očekivano: pojavi se ekran „MafiaGame — lokalni prototip (DEV)" sa 3 dugmeta postavki.
- Klikom kroz: postavka → redom otkrivanje uloga (pass-and-play) → noć (Mafija/Doktor/Detektiv)
  → jutro → diskusija → glasanje (svako redom) → ishod → sledeći krug ili „Kraj igre".

### Ako nešto ne radi
- Nema teksta na dugmadi: nisi uradio korak 1 (TMP Essentials).
- Klik na dugme ne reaguje: proveri da u sceni postoji `EventSystem` (pravi se automatski);
  Console poruka „Could not assign default UI input actions" znači da treba proveriti
  `Edit > Project Settings > Player > Active Input Handling` (očekivano: Input System Package).
- Bilo koja crvena Console greška: pošalji mi ceo tekst greške.

---
Napomena: prototip je DEV/lokalni test harness — namerno prikazuje sve uloge operateru na
jednom uređaju. Skrivanje informacija po igraču/klijentu dolazi tek sa mrežnim slojem
(Milestone 3+); ova arhitektura to ne blokira.
