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

## ✅ Milestone 3 — lobi scena i test host/join (ZAVRŠENO)

Cilj: napraviti scenu u kojoj jedan igrač pravi igru (dobije kod), a drugi se pridruži kodom.
U ovim koracima se **ništa ne preuzima** — svi paketi su već instalirani.

### [x] 1. Napravi novu scenu

1. Gore levo: klikni **File → New Scene**.
2. U prozoru izaberi **Basic (URP)** (ili **Empty**), pa klikni **Create**.
3. Klikni **File → Save As…**.
4. U prozoru za čuvanje otvori folder **Assets/MafiaGame/Content/Scenes**.
   - Ako taj folder ne postoji: u **Project** panelu desni klik na `MafiaGame` → **Create → Folder**,
     nazovi ga `Content`; uđi u njega, opet **Create → Folder**, nazovi `Scenes`.
5. Za ime ukucaj **Lobby**, klikni **Save**.

- Očekivano: gore na tabu scene piše `Lobby`.

### [x] 2. Dodaj NetworkManager (mrežni „mozak")

1. U **Hierarchy** panelu desni klik na prazno → **Create Empty**.
2. Preimenuj ga u **NetworkManager** (desni klik → **Rename**, ili pritisni `F2`).
3. Sa selektovanim `NetworkManager`, u **Inspector** panelu klikni dugme **Add Component**.
4. Ukucaj **Network Manager** i klikni na **Network Manager** iz liste.
5. Ponovo klikni **Add Component**, ukucaj **Unity Transport**, klikni **Unity Transport**.
6. U komponenti **Network Manager** nađi polje **Network Transport**. Ako je prazno, klikni
   mali kružić pored polja i izaberi **Unity Transport** (obično se poveže samo).

- Očekivano: `NetworkManager` ima dve komponente — **Network Manager** i **Unity Transport** —
  i polje **Network Transport** nije prazno.

### [x] 3. Dodaj LobbyBootstrap (pokreće lobi)

1. U **Hierarchy** desni klik na prazno → **Create Empty**.
2. Preimenuj ga u **LobbyBootstrap**.
3. U **Inspector** klikni **Add Component**, ukucaj **Lobby Bootstrap**, klikni ga.

- Očekivano: `LobbyBootstrap` ima komponentu **Lobby Bootstrap**.

### [x] 4. Sačuvaj i dodaj scenu u Build

1. Pritisni **Ctrl+S**.
2. Klikni **File → Build Settings** (ili **Build Profiles**).
3. Klikni **Add Open Scenes** (doda `Lobby` u listu).
4. Zatvori prozor.

### [x] 5. Test host/join sa 2 igrača

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

---

## ✅ Milestone 4 (presek) — mrežna partija: uloge + jedna noć (ZAVRŠENO)

Cilj: host podeli uloge (svako vidi SAMO svoju), odigra se jedna noć (napad/zaštita/istraga),
pa host razreši noć. Sve na host-u; klijenti samo šalju namere. U ovim koracima se **ništa ne
preuzima** — sve je već instalirano; samo dodajemo 2 objekta u postojeću `Lobby` scenu.

### [x] 1. Otvori Lobby scenu

1. U **Project** panelu otvori **Assets → MafiaGame → Content → Scenes**.
2. Dupli klik na **Lobby**.

- Očekivano: gore na tabu scene piše `Lobby`.

### [x] 2. Napravi objekat „MatchController" (mrežni mozak partije)

1. U **Hierarchy** panelu desni klik na prazno → **Create Empty**.
2. Preimenuj ga u **MatchController** (desni klik → **Rename**, ili `F2`).
3. Sa selektovanim `MatchController`, u **Inspector** klikni **Add Component**.
4. Ukucaj **Network Object** i klikni na **Network Object** iz liste.
5. Ponovo **Add Component**, ukucaj **Network Match Controller**, klikni ga.

- Očekivano: `MatchController` ima dve komponente — **Network Object** i **Network Match Controller**.

### [x] 3. Napravi objekat „MatchView" (ekran partije)

1. U **Hierarchy** desni klik na prazno → **Create Empty**.
2. Preimenuj ga u **MatchView**.
3. U **Inspector** klikni **Add Component**, ukucaj **Match Network View**, klikni ga.

- Očekivano: `MatchView` ima komponentu **Match Network View**, a na njoj polje **Controller** (prazno).

### [x] 4. Poveži MatchView sa MatchController

1. U **Hierarchy** klikni na **MatchView** (da ga selektuješ).
2. U **Inspector**, na komponenti **Match Network View**, nađi polje **Controller**.
3. Iz **Hierarchy** panela prevuci (drag) objekat **MatchController** i pusti ga na to polje **Controller**.

- Očekivano: u polju **Controller** sada piše `MatchController (Network Match Controller)` umesto `None`.

### [x] 5. Sačuvaj scenu

1. Pritisni **Ctrl+S**.

- Očekivano: nestane zvezdica `*` sa taba scene.

### [x] 6. Test mrežne partije sa 4 igrača

1. Gore klikni **Window → Multiplayer → Multiplayer Play Mode**.
2. U tom prozoru štikliraj **Player 2**, **Player 3** i **Player 4** (ukupno 4 igrača — to je minimum za partiju).
3. Klikni **Play** (▶). Otvoriće se 4 prozora.
4. U **Player 1** prozoru klikni **Napravi igru (Host)** → pojaviće se kod.
5. U **Player 2**, **Player 3** i **Player 4** ukucaj taj kod u polje pa klikni **Pridruži se kodom**.
   - Očekivano: svaki prozor pokaže **Igrači (4)**.
6. U **Player 1** klikni dugme **Počni partiju**.
   - Očekivano: u SVAKOM prozoru se pojavi „Tvoje mesto: N | Uloga: …". Svako vidi **samo svoju** ulogu.
     (Sa 4 igrača ima 1 Mafija + 3 Građanina; Doktor/Detektiv se pojavljuju tek na 5/7+ igrača.)
7. U **Player 1** klikni **Svi videli uloge → Noć**.
   - Očekivano: u svakom prozoru gore piše „Faza: Noć".
8. Nađi prozor u kom piše **Uloga: Mafija**. Tamo desno klikni jedno dugme **Mesto X** (koga napadaš).
   - Očekivano: dole se pojavi „Akcija prihvaćena." (ako klikneš svoje mesto: „Odbijeno: …").
9. U **Player 1** klikni **Razreši noć**.
   - Očekivano: u svim prozorima piše „Eliminisan: mesto X (uloga: …)" ili „niko nije eliminisan".

Ako se pojavi **crvena** greška u **Console** panelu, prekopiraj je i pošalji mi.

Napomena (pošteno): mrežni deo NISAM mogao da proverim automatski (samo kompilaciju i logiku).
Ovaj korak je prva živa provera mrežne partije — zato javi tačno šta vidiš ili koju grešku dobiješ.

---

## ✅ Milestone 4 (dan) — dan, glasanje i kraj partije (ZAVRŠENO)

Cilj: posle noći ide dan — diskusija, glasanje i eliminacija, pa se partija završi kad
mafija ili grad pobede. Dodato je i sakrivanje lobi ekrana da se tekstovi više ne preklapaju.
U ovim koracima se **ništa ne preuzima**; treba samo da povežeš **jedno novo polje**.

### [x] 1. Otvori Lobby scenu

1. U **Project** panelu otvori **Assets → MafiaGame → Content → Scenes**.
2. Dupli klik na **Lobby**.

- Očekivano: gore na tabu scene piše `Lobby`.

### [x] 2. Poveži MatchView sa LobbyBootstrap (da se lobi sakrije tokom partije)

1. U **Hierarchy** klikni na **MatchView**.
2. U **Inspector**, na komponenti **Match Network View**, sada postoji novo polje
   **Lobby Bootstrap** (ispod polja **Controller**).
3. Iz **Hierarchy** panela prevuci (drag) objekat **LobbyBootstrap** i pusti ga na to polje.

- Očekivano: u polju **Lobby Bootstrap** piše `LobbyBootstrap (Lobby Bootstrap)` umesto `None`.
- Napomena: ako ovo polje ostaviš prazno, sve i dalje radi — lobi se samo neće sakriti.

### [x] 3. Sačuvaj scenu

1. Pritisni **Ctrl+S**.

- Očekivano: nestane zvezdica `*` sa taba scene.

### [x] 4. Test cele partije sa 4 igrača

1. **Window → Multiplayer → Multiplayer Play Mode**, štikliraj **Player 2**, **Player 3**, **Player 4**.
2. Klikni **Play** (▶), pa u **Player 1** klikni **Napravi igru (Host)**, a u ostala tri
   ukucaj kod i klikni **Pridruži se kodom**.
3. U **Player 1** klikni **Počni partiju**.
   - Očekivano: lobi ekran (kod, lista igrača, dugme **Napusti**) **nestane** u svim prozorima,
     ostaje samo ekran partije. Svako vidi samo svoju ulogu.
4. **Svi videli uloge → Noć** → u prozoru gde piše **Uloga: Mafija** klikni jedno **Mesto X**
   → u **Player 1** klikni **Razreši noć**.
   - Očekivano: piše ko je eliminisan.
5. U **Player 1** klikni **Počni diskusiju**, pa **Počni glasanje**.
   - Očekivano: u svakom prozoru **živog** igrača se pojave dugmad **Mesto N** (mrtvi nemaju dugmad).
6. Neka bar dva živa igrača kliknu **isto** mesto, pa u **Player 1** klikni **Prebroj glasove**.
   - Očekivano: „Glasanje: eliminisan je mesto X (uloga: …)".
   - Ako je bilo nerešeno: piše „Nerešeno (…) — ponovno glasanje" i dugmad ostanu samo za ta mesta;
     glasajte ponovo pa opet **Prebroj glasove**.
7. Ponavljaj noć → dan → glasanje dok se partija ne završi.
   - Očekivano: gore piše „Faza: Kraj partije — pobedio je grad" (ili „pobedila je mafija").

Ako se pojavi **crvena** greška u **Console** panelu, prekopiraj je i pošalji mi.

---

## Milestone 4 (tajmeri) — faze same teku

Cilj: faze više ne čekaju host-a — svaka ima svoje vreme i sama prelazi u sledeću.
**Ovde ne treba ništa da povezuješ u Editoru** — nema novih objekata ni polja. Samo test.

Trajanja (za sada fiksna, host ih još ne bira):

- prikaz uloga: **10s**
- noć: **45s**
- objava ko je stradao: **8s**
- diskusija: **90s**
- glasanje: **45s**

### [x] 1. Test cele partije sa 4 igrača

1. **Window → Multiplayer → Multiplayer Play Mode**, štikliraj **Player 2**, **Player 3**, **Player 4**.
2. Klikni **Play** (▶), u **Player 1** klikni **Napravi igru (Host)**, u ostala tri ukucaj kod
   i klikni **Pridruži se kodom**.
3. U **Player 1** klikni **Počni partiju**.
   - Očekivano: gore u SVAKOM prozoru piše npr. „Faza: Prikaz uloga — 10s" i broj **opada**.
   - Očekivano: posle 10s SAM prelazi na „Faza: Noć — 45s". Niko ništa ne klikće.
4. U prozoru gde piše **Uloga: Mafija** klikni jedno **Mesto X**.
   - Očekivano: noć se razreši **odmah**, ne čeka se ostatak tajmera (sa 4 igrača mafija je
     jedina koja noću nešto radi). Kad dodamo Doktora i Detektiva, čekaće se i njih.
5. Pusti da diskusija istekne sama.
   - Očekivano: glasanje se samo otvori i živi igrači dobiju dugmad **Mesto N**.
6. Neka **svi živi** igrači glasaju.
   - Očekivano: čim glasa i poslednji, glasovi se **odmah** prebroje.
   - Ako neko ne glasa: čeka se da tajmer istekne, pa se broje samo dati glasovi.
7. Probaj i dugme **Preskoči → …** u **Player 1** usred neke faze.
   - Očekivano: faza se odmah promeni, a nova faza kreće od svog **punog** vremena.
8. Pusti partiju do kraja.
   - Očekivano: na „Faza: Kraj partije" **nema** odbrojavanja.

Ako se pojavi **crvena** greška u **Console** panelu, prekopiraj je i pošalji mi.

Napomena (pošteno): pravila tajmera su pokrivena automatskim testovima (13 novih), ali kako se
odbrojavanje ponaša **uživo preko mreže** NISAM mogao da proverim — to je ovaj tvoj test.

---

## Milestone 4 (podešavanja) — host bira pravila u lobiju

Cilj: pre početka partije host bira broj mafija, da li igraju Doktor i Detektiv, da li se
otkriva uloga eliminisanog, i trajanja faza. **Ništa se ne povezuje u Editoru** — samo test.

Kako izgleda: kad si **host** i još niste počeli, desno se pojavi kolona dugmadi. Svako dugme
je jedno podešavanje i **klik ga menja u sledeću vrednost** (kad dođe do kraja, vrati se na
početak). Nema kucanja.

- **Mafija: 1** → klik povećava, do maksimuma za trenutan broj igrača, pa nazad na 1.
- **Doktor: DA/NE**, **Detektiv: DA/NE**, **Otkrij ulogu eliminisanog: DA/NE** → klik prebacuje.
- **Noć: 45s** (korak 15s), **Diskusija: 90s** (korak 30s), **Glasanje: 45s** (korak 15s).

Podsetnik na pravila: specijalna uloga traži bar **5** igrača, a **obe** traže bar **7**.

### [ ] 1. Test podešavanja sa 4 igrača

1. **Window → Multiplayer → Multiplayer Play Mode**, štikliraj **Player 2**, **Player 3**, **Player 4**.
2. Klikni **Play** (▶), u **Player 1** klikni **Napravi igru (Host)**, u ostala tri ukucaj kod
   i klikni **Pridruži se kodom**.
3. Pogledaj **Player 2** (običan igrač).
   - Očekivano: vidi red sa pravilima („Mafija: 1 | … | Noć 45s | Diskusija 90s | Glasanje 45s"),
     ali **nema** dugmad za podešavanja — njih ima samo host.
4. U **Player 1** klikni nekoliko puta **Noć: 45s**.
   - Očekivano: broj se menja (60s, 75s, …), i **istovremeno se menja** i kod ostalih igrača.
5. U **Player 1** klikni **Doktor: DA** i **Detektiv: DA** dok oba ne budu **DA**.
   - Očekivano (sa samo 4 igrača): dole se pojavi poruka „Ne mogu tako: …" jer specijalne uloge
     traže više igrača. To je ispravno ponašanje — podešavanje se ne primeni.
6. Postavi **Doktor: NE**, **Detektiv: NE**, **Mafija: 1**, pa klikni **Počni partiju**.
   - Očekivano: partija kreće i noć traje onoliko koliko si postavio.
7. Kad partija počne, pogledaj desnu kolonu.
   - Očekivano: dugmad za podešavanja su **nestala** (pravila se ne menjaju usred partije).

Ako se pojavi **crvena** greška u **Console** panelu, prekopiraj je i pošalji mi.

Napomena: sa **7+ igrača** možeš uključiti i Doktora i Detektiva — tada noć čeka sve tri uloge
pre nego što se razreši.

---

## Milestone 4 (popravke podešavanja) — test

Šta je popravljeno:

1. **Dugmad za vreme se nisu videla** — kolona sa podešavanjima je bila preniska, pa su
   „Noć / Diskusija / Glasanje" ostajali ispod ivice. Sada je kolona viša.
2. **Nije moglo da se promeni Doktor/Detektiv/Otkrij ulogu** — sa 4 igrača su i Doktor i
   Detektiv bili DA, što je nedozvoljeno, a **svaka** pojedinačna promena je i dalje bila
   nedozvoljena, pa je sve odbijano. Sada se nedozvoljena opcija **sama isključi na NE**,
   uz poruku zašto.
3. **„Ne mogu da počnem"** — posledica istog problema; sada se podešavanje uskladi i pre
   samog starta, pa partija kreće.

### [x] 1. Test sa 4 igrača

1. **Window → Multiplayer → Multiplayer Play Mode**, štikliraj **Player 2**, **3**, **4**, pa **Play** (▶).
2. U **Player 1**: **Napravi igru (Host)**, u ostalima ukucaj kod i **Pridruži se kodom**.
   - Očekivano: čim se skupi 4 igrača, u redu sa pravilima piše **bez specijalnih uloga**
     (Doktor i Detektiv su se sami isključili jer traže 5, odnosno 7 igrača).
3. U **Player 1** klikni **Podešavanja partije**.
   - Očekivano: otvori se poseban ekran preko lobija sa **svih 7** podešavanja, uključujući
     **Noć: 45s**, **Diskusija: 90s**, **Glasanje: 45s**, i dugme **Sačuvaj i zatvori**.
4. Klikni **Doktor: NE**.
   - Očekivano: vrati se na **NE** i iznad dugmadi piše „Doktor je isključen: specijalna uloga traži bar 5 igrača."
5. Klikni **Otkrij ulogu eliminisanog: DA**.
   - Očekivano: prebaci se na **NE** (ovo podešavanje ne zavisi od broja igrača, mora da radi uvek).
6. Klikni nekoliko puta **Noć: 45s** i **Glasanje: 45s**.
   - Očekivano: brojevi se menjaju i **odmah se vide i kod ostalih igrača**.
7. Klikni **Sačuvaj i zatvori**, pa **Počni partiju**.
   - Očekivano: partija kreće (nema više „Ne mogu da počnem"), i noć traje koliko si postavio.

---

## Milestone 4 (build) — test sa 5+ igrača

Zašto: **Multiplayer Play Mode ide najviše do 4 igrača**, a Doktor traži bar **5**, a Detektiv
(uz Doktora) bar **7**. Zato pravimo običan program (build) i pokrećemo ga više puta. Standalone
prozor nema Editor u sebi, pa je znatno lakši za laptop od MPPM virtuelnog igrača.

Napravio sam ti dugme u meniju da ne moraš svaki put da biraš folder.

### [x] 1. Build jednim klikom (uvek u isti folder)

1. U Unity-ju gore u meniju klikni **MafiaGame → Build dev player (Linux)**.
2. Sačekaj (prvi put ume da traje par minuta). Dole u **Console** panelu se pojavi
   `[DevBuild] Build ready: /home/<ti>/MafiaBuild/Mafia.x86_64`.

- Uvek ide u **isti** folder `~/MafiaBuild` i **prepisuje** stari build. Nema dijaloga,
  nema biranja, nema gomilanja starih verzija.
- Build je **van** projekta, pa ne može slučajno da uđe u git.
- Posle svake promene koda samo ponovo klikni isto dugme.

### [x] 2. Pokreni onoliko igrača koliko ti treba

U meniju **MafiaGame** imaš:

- **Launch 4 players**, **Launch 5 players**, **Launch 6 players**, **Launch 7 players**,
  **Launch 8 players** — pokreće toliko prozora od **poslednjeg** build-a (bez ponovnog build-a).
- **Build and launch 6 players** — build pa odmah 6 prozora.

Koliko igrača za šta:

| Igrača | Šta dobijaš |
|---|---|
| 4 | 1 Mafija + Građani (bez specijalnih uloga) |
| 5–6 | + **Doktor** |
| 7–8 | + **Detektiv** (obe specijalne uloge) |

Svaki prozor dobija **svoj nalog** (`-profile p1`, `p2`, …). Bez toga svi prozori dele isti
keširani anonimni nalog, pa se prijave kao **isti igrač** — odatle greška
„player is already a member of the lobby".

Ako pokrećeš iz Terminala, moraš i ti da dodaš profil:

```bash
cd ~/MafiaBuild
for i in 1 2 3 4 5 6 7; do ./Mafia.x86_64 -screen-width 640 -screen-height 480 -screen-fullscreen 0 -profile p$i & sleep 2; done
```

Da zatvoriš sve odjednom: `pkill -f Mafia.x86_64`

### [x] 3. Odigraj partiju sa 7 igrača (Doktor + Detektiv)

1. Pokreni **MafiaGame → Launch 7 players**.
2. U **jednom** prozoru klikni **Napravi igru (Host)** → zapamti kod.
3. U ostalih 6 ukucaj kod i klikni **Pridruži se kodom**.
   - Očekivano: svuda piše **Igrači (7)** i **Mrežno povezano: 7 igrača**.
   - Očekivano: **nema** greške „player is already a member of the lobby".
   - Očekivano: u pravilima stoje i **Doktor** i **Detektiv**.
   - Očekivano: lobi ekran je čist — pravila partije se ispisuju **unutar** lobi panela,
     ništa se ne preklapa.
4. U host prozoru klikni **Podešavanja partije** pa proveri vrednosti, zatim **Sačuvaj i zatvori**.
5. Klikni **Počni partiju**.
   - Očekivano: jedan igrač je **Mafija**, jedan **Doktor**, jedan **Detektiv**, ostali **Građani**.
6. U noći: Mafija klikne metu, **Doktor** koga štiti (može i sebe), **Detektiv** koga istražuje.
   - Očekivano: noć se razreši tek **kad su odigrala sva tri** — ne čeka se ostatak tajmera.
   - Očekivano: ako je Doktor zaštitio baš metu mafije, niko ne umire.
   - Očekivano: Detektiv dobija odgovor **samo u svom prozoru** (Mafija / nije Mafija).
7. Odigraj do kraja partije.

Napomena: build namerno uzima **samo `Lobby` scenu**. Ranije je build pokretao
`LocalPrototype` (pisalo je „MafiaGame — lokalni prototip (DEV)") jer je taj prototip prvi
u listi scena u Build Settings — sada to više ne može da se desi.

Ako se pojavi crvena greška u **Console** panelu ili prozor pukne, javi mi šta piše.

## Milestone 4 (prekid veze i ponovno priključivanje)

Nema ničega da se klikće u Editoru — sve je u kodu. Ovo je samo test.

### [x] 1. Test: igrač se vrati na vreme — ZA SADA NE RADI (odloženo)

Povratak u partiju posle gašenja prozora **trenutno ne uspeva** (Relay/Sessions odbija
ponovno povezivanje). Ovo je svesno odloženo za kraj — vidi TODO u `docs/game-rules.md`.

Ono što **radi** i što je važno za igru: partija se nikada ne zaledi zbog igrača koji je
otišao — niko ga ne čeka, a posle 30 sekundi ispada iz partije (test 2 ispod).

### [x] 2. Test: igrač se NE vrati

1. Isto kao gore, ali zatvoreni prozor **ne vraćaj**.
2. Broji do 30.
   - Očekivano: u ostalim prozorima se pojavi poruka
     **„Mesto N je napustilo partiju (nije se vratilo na vreme)."**
   - Očekivano: piše **samo broj mesta**, nikada uloga. Ispadanje ne sme da oda ko je šta bio.
   - Očekivano: taj igrač više ne može da se glasa za njega, niti on glasa.
3. Ako je taj igrač bio **Mafija**: partija se **ne završava odmah** — završiće se na
   sledećem razrešenju (kraj noći ili kraj glasanja) sa pobedom grada.
   - Ovo je namerno: da se objavi „grad je pobedio" u trenutku kad neko ispadne,
     to bi svima reklo da je taj igrač bio Mafija.

Ako se pojavi crvena greška u **Console** panelu, javi mi šta piše.

## Milestone 4 (Game scena) — partija se seli u svoju scenu

Ovde **ima** posla u Editoru, ali je jedan klik. Idi redom.

### [x] 1. Napravi Game scenu

1. Otvori Unity i sačekaj da završi kompajliranje (donji desni ugao, kružić prestane).
2. U gornjem meniju klikni **MafiaGame** → **Create Game scene**.
   - Očekivano: u **Console** panelu se pojavi zelena/bela poruka
     `[GameSceneBuilder] Created Assets/MafiaGame/Content/Scenes/Game.unity and added it to Build Settings.`
3. Provera u **Project** panelu: otvori folder
   `Assets` → `MafiaGame` → `Content` → `Scenes`.
   - Očekivano: pored `Lobby` i `LocalPrototype` sada stoji i **`Game`**.
4. Provera u Build Settings: meni **File** → **Build Profiles** (ili **Build Settings**).
   - Očekivano: u listi scena postoji red
     `Assets/MafiaGame/Content/Scenes/Game.unity` i **kvadratić levo od njega je čekiran**.
   - Zatvori taj prozor (X gore desno).

Ako Console pokaže crvenu grešku, javi mi tekst i stani ovde.

### [x] 2. Napravi novi build

1. Meni **MafiaGame** → **Build dev player (Linux)**.
2. Sačekaj (traje minut-dva). Očekivano u Console: `[DevBuild] Build ready: ...`
   - Ako umesto toga vidiš žuto upozorenje `Game scene not found`, znači da korak 1
     nije uspeo — vrati se na njega.

### [x] 3. Test: partija menja scenu

1. Meni **MafiaGame** → **Launch 5 players**.
2. U jednom prozoru **Napravi igru**, ostala četiri se pridruže kodom.
3. Klikni **Počni partiju**.
   - Očekivano: pozadina se promeni — pojavi se **sivi pod, smeđi okrugli sto i
     10 svetlih blokova u krug** oko njega. To je privremeno okruženje (pravo 3D
     okruženje dolazi u Milestone 6).
   - Očekivano: UI partije (faza, uloga, dugmad) radi potpuno isto kao pre.
4. Odigraj partiju do kraja (do poruke **„Faza: Kraj partije"**).
5. Kod **hosta** se sada vidi dugme **„Nazad u lobi"**. Klikni ga.
   - Očekivano: kod **svih** igrača nestane 3D okruženje i svi se vrate na lobi ekran.
   - Očekivano: kod je **isti**, niko se ne mora ponovo pridruživati.
   - Očekivano: kod hosta se opet vide **„Podešavanja partije"** i **„Počni partiju"**.
6. Klikni **Počni partiju** ponovo.
   - Očekivano: nova partija počinje normalno, uloge se **ponovo dele** (mogu biti druge).

Ako nešto od ovoga ne odgovara, javi mi šta si video i šta piše u prozoru.

## Milestone 4 (čišćenje) — ekran partije se seli u Game scenu

Spolja se ništa ne menja. Ekran koji si do sada gledao je bio jedan veliki fajl koji je
držao i lobi i partiju; sada je podeljen na dva, po sceni u kojoj se prikazuje.

### [ ] 1. Dopuni Game scenu

1. Otvori Unity i sačekaj da završi kompajliranje.
2. Meni **MafiaGame** → **Create Game scene**.
   - Ovo NE pravi scenu iznova — samo dopuni ono što fali. Sada dodaje objekat
     **MatchScreen** (ekran partije).
   - Očekivano u Console: `[GameSceneBuilder] ... is ready and registered in Build Settings.`
3. Provera: u **Project** panelu dupli klik na `Assets/MafiaGame/Content/Scenes/Game`.
   - Očekivano: u **Hierarchy** panelu vidiš **MatchEnvironment** i **MatchScreen**.
   - Vrati se na `Lobby` scenu (dupli klik na `Lobby`) da ne bi slučajno radio u Game sceni.

### [ ] 2. Build i test

1. Meni **MafiaGame** → **Build dev player (Linux)**.
2. **MafiaGame** → **Launch 5 players**, napravi igru, ostali se pridruže.
   - Očekivano u lobiju: i dalje vidiš **Podešavanja partije** i **Počni partiju** (host),
     i red „Mrežno povezano: N igrača…".
3. Klikni **Počni partiju** i odigraj partiju do kraja.
   - Očekivano: faza, uloga, rezultat i dugmad rade **potpuno isto** kao pre.
   - Očekivano: uloga se vidi ODMAH kad partija počne (ne prazan red).
4. Host klikne **Nazad u lobi**.
   - Očekivano: svi se vrate na lobi ekran, kod isti, može nova partija.
