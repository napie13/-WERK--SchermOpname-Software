# Technische handleiding

Deze handleiding is voor wie Examenopname bouwt, uitrolt of onderhoudt. Waarom het programma zo in elkaar zit, staat in [Visie.md](Visie.md). De handleiding voor leerkrachten en leerlingen is [Handleiding.html](Handleiding.html) (openen in een browser en afdrukken).

## In het kort

1. Je dubbelklikt op `Maak.cmd`. Dat overloopt de instellingen, vraagt het leerkrachtpaswoord en bouwt een Intune-pakket (`.intunewin`).
2. Je zet dat pakket in Intune. Intune voert op elke laptop `install.cmd` uit.
3. Een leerling dubbelklikt op **Examenopname**, klikt op **Start opname** en het scherm wordt opgenomen met FFmpeg.
4. Alleen met het leerkrachtpaswoord stopt de opname. Valt ze weg, dan start ze vanzelf opnieuw.

## Mappenstructuur

```
Maak.cmd                    dubbelklik om het Intune-pakket te bouwen
instellingen.ini            de instellingen, Maak.cmd overloopt ze
programma/                  broncode van het programma (C#)
    Program.cs                startpunt, controles voor de start, opruimen
    Instellingen.cs           leest instellingen.ini, controleert het paswoord
    Sessie.cs                 één opname: map, logboek en status.txt
    Opname.cs                 start en bewaakt ffmpeg
    StartVenster.cs           eerste venster met uitleg en knop Handleiding
    OpnameVenster.cs          venster rechtsonder tijdens de opname
    logo.ico, logo.png
installatie/                wat Intune op de laptop uitvoert
    install.cmd
    uninstall.cmd
    waakhond.xml              geplande taak die de opname bewaakt
bouw/
    maak.ps1                  het eigenlijke bouwscript
    hulpmiddelen/             ffmpeg.exe en IntuneWinAppUtil.exe (worden gedownload)
docs/
    Visie.md
    Technische-handleiding.md
    Handleiding.html          voor leerkrachten en leerlingen, gaat ook mee in het pakket
uitvoer/                    wordt gemaakt door Maak.cmd (niet in git)
    Examenopname-1.0.0.intunewin
    pakket/                   de inhoud van het Intune-pakket, handig om te testen
```

## Een pakket bouwen

**Nodig:** een Windows 10- of 11-computer. De eerste keer ook internet, om FFmpeg en de Intune-tool te downloaden. Er wordt niets geïnstalleerd.

Heb je de map als zip gedownload, klik dan eerst met de rechtermuisknop op de zip, kies **Eigenschappen** en vink **Blokkering opheffen** aan. Pak de zip daarna pas uit.

1. Dubbelklik op `Maak.cmd`.
2. Het script toont elke instelling met de huidige waarde tussen haakjes. Druk op Enter om ze te behouden of typ een nieuwe waarde. Je antwoorden worden bewaard in `instellingen.ini`.
3. Typ twee keer het leerkrachtpaswoord (minstens 8 tekens). Het paswoord wordt niet bewaard: alleen een hash gaat mee in het pakket. Je moet het dus bij elke bouw opnieuw ingeven.
4. Het script downloadt zo nodig FFmpeg en de Intune-tool, compileert het programma en maakt het pakket.
5. Op het einde opent de map `uitvoer` met daarin `Examenopname-<versie>.intunewin`.

Loopt er iets fout, dan staat er een rode foutmelding en blijft het venster open.

**Zonder internet** zet je de hulpmiddelen zelf in `bouw\hulpmiddelen`:

| Bestand | Waar te halen |
|---|---|
| `ffmpeg.exe` | https://www.gyan.dev/ffmpeg/builds/ (release essentials, `bin\ffmpeg.exe` uit de zip) |
| `IntuneWinAppUtil.exe` | https://github.com/microsoft/Microsoft-Win32-Content-Prep-Tool |

**FFmpeg bijwerken:** verwijder `bouw\hulpmiddelen\ffmpeg.exe`. Bij de volgende bouw wordt de nieuwste versie gedownload en gecontroleerd met het controlegetal van de website.

## Instellingen

| Naam | Standaard | Betekenis |
|---|---|---|
| `Versie` | 1.0.0 | Versienummer. Verhoog dit bij elke nieuwe uitrol, Intune gebruikt het om de installatie te herkennen. |
| `OpnameMap` | `C:\Examenopnames` | Waar de opnames op de laptop komen. |
| `MinVrijGB` | 5 | Onder deze vrije ruimte start een opname niet. |
| `Fps` | 5 | Beelden per seconde (1 tot 30). |
| `Kwaliteit` | 30 | H.264 CRF. Lager is scherper en groter, 28 tot 32 is goed. |
| `MaxUren` | 6 | Een vergeten opname stopt na zoveel uur. |
| `BewaarDagen` | 60 | Oudere opnames worden bij het opstarten van de laptop verwijderd. |

In het pakket komt daar `PaswoordHash` bij (PBKDF2-SHA256, 100.000 rondes, willekeurige salt).

Vaste waarden in de code: videodelen van 10 minuten, maximaal 1920 pixels breed, een onderbroken opname wordt binnen 30 minuten nog hervat, waarschuwing onder 1 GB vrij.

## Uitrollen met Intune

In het Intune-beheercentrum: **Apps > Windows > Toevoegen > Windows-app (Win32)** en kies het `.intunewin`-bestand.

| Instelling | Waarde |
|---|---|
| Installatieopdracht | `install.cmd` |
| Verwijderopdracht | `uninstall.cmd` |
| Installatiegedrag | Systeem |
| Retourcodes | standaard laten (1618 = opnieuw proberen staat er al in) |
| Vereisten | 64-bit, Windows 10 of hoger |
| Detectieregel | Register, sleutel `HKEY_LOCAL_MACHINE\SOFTWARE\Examenopname`, waarde `Versie`, tekenreeksvergelijking, gelijk aan de versie (bv. `1.0.0`), 32-bit app op 64-bit: Nee |
| Toewijzing | Vereist, voor de apparaatgroep met leerlingenlaptops |

**Een nieuwe versie uitrollen:** geef bij `Maak.cmd` een hoger versienummer in. Open daarna de app in Intune, vervang het pakketbestand door het nieuwe `.intunewin` en zet het nieuwe versienummer in de detectieregel. Loopt er op een laptop net een opname, dan geeft `install.cmd` code 1618 en probeert Intune het later opnieuw. Een examen wordt dus nooit onderbroken door een update.

## Eerst testen op één laptop

Doe dit bij elke nieuwe versie voor je ze aan alle laptops toewijst.

1. Kopieer `uitvoer\pakket` naar een testlaptop, klik met de rechtermuisknop op `install.cmd` en kies **Als administrator uitvoeren**. Controleer `C:\ProgramData\Examenopname-installatie.log`: daar moet "Installatie gelukt" en "Afsluitcode: 0" staan.
2. Meld je aan als leerling. Start **Examenopname** via het bureaublad, open ook eens de handleiding en start de opname.
3. Probeer Alt+F4: het venster blijft staan.
4. Sluit **Examenopname** via Taakbeheer. Binnen de minuut is het venster terug.
5. Sluit **ffmpeg.exe** via Taakbeheer. Na enkele seconden loopt de opname weer.
6. Herstart de laptop tijdens een opname en meld opnieuw aan. De opname gaat verder.
7. Stop de opname, eerst met een fout paswoord en daarna met het juiste.
8. Bekijk `C:\Examenopnames\<gebruiker>\<datum_uur>`: de video's spelen af en `logboek.txt` toont alles wat je deed.
9. Probeer als leerling die map te verwijderen. Dat moet geweigerd worden.

## Wat er op de laptop gebeurt

`install.cmd` doet het volgende (alles wordt gelogd in `C:\ProgramData\Examenopname-installatie.log`):

1. Stopt met code 1618 als er een opname loopt.
2. Kopieert de map `programma` naar `C:\Program Files\Examenopname`.
3. Maakt de opnamemap aan en zet de rechten:
   - SYSTEM en Administrators: volledige controle.
   - Users: mogen alleen lezen en een eigen map aanmaken.
   - Wie een map aanmaakt, mag daarin lezen en schrijven maar niets verwijderen en geen rechten aanpassen. Leerlingen zien elkaars opnames niet.
4. Maakt twee geplande taken in de map **Examenopname** van Taakplanner:
   - **Waakhond**: bij elke aanmelding en daarna elke minuut, als de aangemelde gebruiker. Start `Examenopname.exe /waakhond`.
   - **Opruimen**: bij het opstarten van de laptop, als SYSTEM. Start `Examenopname.exe /opruimen`.
5. Zet de snelkoppeling **Examenopname** op het bureaublad van alle gebruikers, en **Examenopname** en **Examenopname handleiding** in het Startmenu.
6. Schrijft de versie naar `HKLM\SOFTWARE\Examenopname`.

`uninstall.cmd` haalt alles weer weg, behalve de opnames zelf.

## Hoe het programma werkt

**Starten.** De leerling ziet eerst het startvenster. Na **Start opname** controleert het programma of het volledig geïnstalleerd is, of er maar één scherm is en of er genoeg vrije ruimte is. Dan maakt het een map `C:\Examenopnames\<gebruiker>\<datum_uur>` en start het opnamevenster.

**Opnemen.** Het programma start `ffmpeg.exe` zonder zichtbaar venster. FFmpeg neemt het volledige hoofdscherm op met ddagrab (Desktop Duplication, zuinig en modern). Lukt dat niet meteen bij de start, dan schakelt het over op gdigrab (ouder, werkt overal). De video is H.264 in `.mkv`-bestanden van 10 minuten. De bestandsnaam bevat het uur waarop dat deel begint, bijvoorbeeld `deel_2026-11-18_10-30-00.mkv`.

**Bewaken.** Elke seconde kijkt het programma of FFmpeg nog loopt. Zo niet, dan noteert het dat in het logboek en start het FFmpeg na enkele seconden opnieuw. Om de 30 seconden schrijft het een teken van leven naar `status.txt`, controleert het de schijfruimte en het aantal schermen, en houdt het de laptop uit slaapstand.

**Waakhond.** `status.txt` staat in de map van de gebruiker en bevat `TOESTAND|map|starttijd|laatste teken van leven`. Elke minuut start Taakplanner `Examenopname.exe /waakhond`. Loopt het programma al, dan stopt de waakhond meteen. Staat de status op ACTIEF en was het laatste teken van leven minder dan 30 minuten geleden, dan opent de waakhond het opnamevenster opnieuw en gaat de opname verder in dezelfde map. Is het langer geleden, dan noteert hij in het logboek dat de opname nooit gestopt werd en zet hij de status op AFGEBROKEN.

**Stoppen.** Alt+F4 en sluiten via de taakbalk doen niets. Na **Stoppen (leerkracht)** en het juiste paswoord stuurt het programma "q" naar FFmpeg, zodat het laatste videodeel netjes afgesloten wordt. De status wordt GESTOPT. Na `MaxUren` stopt een vergeten opname vanzelf. Sluit Windows af tijdens een opname, dan blijft de status ACTIEF en gaat de opname verder na het aanmelden.

**Opruimen.** Bij het opstarten van de laptop verwijdert de taak Opruimen opnamemappen die langer dan `BewaarDagen` niet meer gewijzigd zijn.

## Opnames ophalen en bekijken

Opnames staan op de laptop van de leerling in `C:\Examenopnames\<gebruiker>\<datum_uur>\`. Meld je aan met een beheerdersaccount en kopieer die map naar een veilige plek. Verkenner vraagt eerst toestemming, klik op **Doorgaan**.

De `.mkv`-bestanden spelen af met Mediaspeler of VLC. Het tijdstip in beeld vind je door het uur in de bestandsnaam op te tellen bij de positie in de video.

Zo ziet een logboek eruit:

```
18-11-2026 09:02:11  Opname gestart. Gebruiker: jan.peeters, computer: LT-0123, versie 1.0.0.
18-11-2026 10:15:40  Opname onderbroken (code 1).
18-11-2026 10:15:43  Opname opnieuw gestart.
18-11-2026 11:30:02  Opname gestopt door de leerkracht. Duur: 02:27:51.
```

Een normale opname begint met "Opname gestart" en eindigt met "Opname gestopt door de leerkracht". Onderbrekingen, "Opname hervat", "Fout paswoord ingegeven", "Aantal schermen veranderd" of een opname die nooit gestopt werd, zijn het bekijken waard.

## Problemen oplossen

| Probleem | Waar kijken / wat doen |
|---|---|
| Installatie mislukt in Intune | `C:\ProgramData\Examenopname-installatie.log` op de laptop. De laatste regels tonen welke stap faalde. |
| "Examenopname is niet volledig geïnstalleerd" | `ffmpeg.exe`, `instellingen.ini` met `PaswoordHash`, of de opnamemap ontbreekt. Installeer opnieuw. |
| "De instellingen konden niet gelezen worden" | Een waarde in `instellingen.ini` is geen getal. Bouw opnieuw met `Maak.cmd`. |
| Opname start telkens opnieuw | Bekijk `logboek.txt`. Daar staat de foutmelding van FFmpeg. |
| Paswoord vergeten | Bouw een nieuw pakket met een nieuw paswoord en een hoger versienummer. |
| Waakhond doet niets | Taakplanner, map Examenopname, taak Waakhond: kijk naar "Laatste resultaat". |
| Maak.cmd: download mislukt | Zet de hulpmiddelen zelf in `bouw\hulpmiddelen` (zie hoger). |

## Aanpassen

| Wat | Waar |
|---|---|
| Instellingen | `instellingen.ini`, of gewoon via `Maak.cmd` |
| Teksten in de vensters | `programma\StartVenster.cs` en `programma\OpnameVenster.cs` |
| Controles voor de start | `KlaarOmTeStarten` in `programma\Program.cs` |
| FFmpeg-opdracht | `Argumenten` in `programma\Opname.cs` |
| Hervat-regels | `programma\Sessie.cs` |
| Wat er op de laptop geïnstalleerd wordt | `installatie\install.cmd` |
| Handleiding voor leerkrachten en leerlingen | `docs\Handleiding.html` |
| Logo | `programma\logo.png` en `programma\logo.ico` (klassiek BMP-formaat, maximaal 64 pixels) |

De code is C# 5 voor .NET Framework 4.8. Er zijn geen externe bibliotheken. Na elke aanpassing: opnieuw `Maak.cmd`, testen op één laptop en de handleidingen bijwerken.

## Licenties

FFmpeg valt onder de GPL. De licentietekst gaat mee in het pakket als `ffmpeg-licentie.txt`. De broncode van FFmpeg staat op https://ffmpeg.org.
