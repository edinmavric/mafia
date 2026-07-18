# OWNER ACTION — ručni Unity koraci

Koraci koje radiš ti u Unity Editoru. Pisani su što prostije: tačan meni, tačan panel,
tačno dugme, i šta treba da se desi posle. Kad završiš korak, stavi `[x]`.

> Pravilo (uvek): koraci su korak-po-korak, za početnika — gde se klikće, šta se preuzima,
> i šta se očekuje posle svakog koraka.

Paneli u Unity-ju (da znamo o čemu pričamo):
- **Hierarchy** — lista objekata u sceni (levo).
- **Inspector** — detalji selektovanog objekta (desno).
- **Project** — fajlovi projekta (dole).

---

## ✅ Milestone 2 — prototip (ZAVRŠENO)
- [x] TextMeshPro importovan.
- [x] Scena `Assets/MafiaGame/Content/Scenes/LocalPrototype.unity` napravljena.
- [x] Dodata u Build Settings i pokrenuta.

## ✅ Milestone 3 — UGS povezivanje (ZAVRŠENO)
- [x] Projekat povezan (Project ID `a1657a4a-a0aa-4da0-97af-0e1a57b50557`).
- [x] Authentication, Relay, Lobby uključeni.

---

## ⬜ Milestone 3 — lobi scena i test host/join

Cilj: napraviti scenu u kojoj jedan igrač pravi igru (dobije kod), a drugi se pridruži kodom.
U ovim koracima se **ništa ne preuzima** — svi paketi su već instalirani.

### [ ] 1. Napravi novu scenu
1. Gore levo: klikni **File → New Scene**.
2. U prozoru izaberi **Basic (URP)** (ili **Empty**), pa klikni **Create**.
3. Klikni **File → Save As…**.
4. U prozoru za čuvanje otvori folder **Assets/MafiaGame/Content/Scenes**.
   - Ako taj folder ne postoji: u **Project** panelu desni klik na `MafiaGame` → **Create → Folder**,
     nazovi ga `Content`; uđi u njega, opet **Create → Folder**, nazovi `Scenes`.
5. Za ime ukucaj **Lobby**, klikni **Save**.
- Očekivano: gore na tabu scene piše `Lobby`.

### [ ] 2. Dodaj NetworkManager (mrežni „mozak")
1. U **Hierarchy** panelu desni klik na prazno → **Create Empty**.
2. Preimenuj ga u **NetworkManager** (desni klik → **Rename**, ili pritisni `F2`).
3. Sa selektovanim `NetworkManager`, u **Inspector** panelu klikni dugme **Add Component**.
4. Ukucaj **Network Manager** i klikni na **Network Manager** iz liste.
5. Ponovo klikni **Add Component**, ukucaj **Unity Transport**, klikni **Unity Transport**.
6. U komponenti **Network Manager** nađi polje **Network Transport**. Ako je prazno, klikni
   mali kružić pored polja i izaberi **Unity Transport** (obično se poveže samo).
- Očekivano: `NetworkManager` ima dve komponente — **Network Manager** i **Unity Transport** —
  i polje **Network Transport** nije prazno.

### [ ] 3. Dodaj LobbyBootstrap (pokreće lobi)
1. U **Hierarchy** desni klik na prazno → **Create Empty**.
2. Preimenuj ga u **LobbyBootstrap**.
3. U **Inspector** klikni **Add Component**, ukucaj **Lobby Bootstrap**, klikni ga.
- Očekivano: `LobbyBootstrap` ima komponentu **Lobby Bootstrap**.

### [ ] 4. Sačuvaj i dodaj scenu u Build
1. Pritisni **Ctrl+S**.
2. Klikni **File → Build Settings** (ili **Build Profiles**).
3. Klikni **Add Open Scenes** (doda `Lobby` u listu).
4. Zatvori prozor.

### [ ] 5. Test host/join sa 2 igrača
1. Gore klikni **Window → Multiplayer → Multiplayer Play Mode**.
2. U tom prozoru štikliraj **Player 2** (da imaš 2 virtuelna igrača).
3. Klikni **Play** (dugme ▶ na vrhu ekrana).
4. Otvoriće se 2 prikaza. U prvom klikni **Napravi igru (Host)** → pojaviće se kod (npr. `ABCD`).
5. U drugom prikazu ukucaj taj kod u polje **Unesi kod…**, pa klikni **Pridruži se kodom**.
- Očekivano: oba prikaza pokažu listu **Igrači (2)**.
- Ako se pojavi crvena greška u **Console** panelu, prekopiraj je i pošalji mi.

Napomena: ovo je mrežni temelj (prijava + lobi). Sinhronizacija same partije (tajne uloge,
komande, faze) je Milestone 4.

---

Napomena: prototip je DEV/lokalni test harness — namerno prikazuje sve uloge na jednom uređaju.
Skrivanje po igraču/klijentu dolazi sa mrežnom partijom (Milestone 4).
