# Examenopname

Neemt het scherm van een leerling op tijdens een digitaal examen, zodat er achteraf kan nagekeken worden of er gefraudeerd werd. Gratis, gebaseerd op FFmpeg, en uitgerold via Intune.

## Hoe het werkt

De leerling dubbelklikt op **Examenopname** op het bureaublad en klikt op OK. Rechtsonder verschijnt een klein venster met een rode rand, een knipperend opnamebolletje en de verstreken tijd. Zo zien leerling en leerkracht meteen dat de opname loopt.

Stoppen kan alleen via **Stoppen (leerkracht)** met het leerkrachtpaswoord. Het venster sluiten met Alt+F4 lukt niet. Wordt het programma toch hard afgesloten, dan start de waakhond het binnen de minuut opnieuw en komt de onderbreking in het logboek.

Voor de start wordt gecontroleerd of er genoeg vrije ruimte is en of er geen tweede scherm hangt. Tijdens de opname blijft de laptop wakker en verschijnt er een waarschuwing als de schijf bijna vol raakt of er alsnog een scherm bijkomt.

## Waar staan de opnames?

Op de laptop zelf, in `C:\Examenopnames\<gebruiker>\<datum_uur>\`:

- `deel_....mkv`: de video in stukken van 10 minuten (afspelen met VLC of Mediaspeler)
- `logboek.txt`: start, stop, onderbrekingen, foute paswoorden, slaapstand, vergrendelen, extra schermen

Leerlingen kunnen in die map schrijven maar niets verwijderen of bij een andere leerling kijken. Opnames ouder dan 60 dagen worden automatisch opgeruimd.

In beeld staan datum, uur, computernaam en gebruiker. Aan 5 beelden per seconde blijft tekst scherp en neemt een examen van een paar uur meestal enkele honderden MB in.

Een gezonde opname heeft in het logboek een start en een regel "Opname gestopt door de leerkracht". Ontbreekt die laatste regel, of staan er onderbrekingen in, dan is dat het bekijken waard.

## Pakket bouwen

Het Intune-pakket wordt automatisch gebouwd door GitHub bij elke wijziging.

1. Stel één keer het paswoord in: **Settings > Secrets and variables > Actions > New repository secret**, naam `LEERKRACHT_PASWOORD`, minstens 10 tekens. Het paswoord komt nergens leesbaar terecht, op de laptops staat enkel een hash.
2. Ga naar **Actions > Intune-pakket bouwen > Run workflow** (of wacht op de automatische build).
3. Download onderaan de build **Examenopname-intune**. Daarin zit `Examenopname.intunewin`.

Zet deze repository best op privé, want de builds bevatten de paswoordhash.

## Intune

Nieuwe app van het type **Windows app (Win32)** met `Examenopname.intunewin`:

| Instelling | Waarde |
|---|---|
| Installatieopdracht | `install.cmd` |
| Verwijderopdracht | `uninstall.cmd` |
| Installatiegedrag | Systeem |
| Detectieregel | Register: `HKLM\SOFTWARE\Examenopname`, waarde `Versie`, tekenreeks gelijk aan `1.0.0` |
| Toewijzing | Vereist, aan de apparaatgroep met leerlingenlaptops |

Loopt er een opname tijdens een update, dan geeft de installatie code 1618 en probeert Intune het later opnieuw. Een examen wordt dus nooit onderbroken door een update.

Wie een nieuwe versie maakt, verhoogt het versienummer in `src/Program.cs`, `pakket/install.ps1` en de detectieregel.

## Eerst testen

Test eerst op één laptop: download **Examenopname-testmap**, klik met de rechtermuisknop op `install.cmd` en kies **Als administrator uitvoeren**. Meld je daarna aan als leerling en probeer een opname, ook eens met de laptop in slaapstand of het venster via Taakbeheer afsluiten.

## Privacy

Schermopnames zijn persoonsgegevens. Vermeld de opname in het examenreglement, informeer leerlingen en ouders en beperk wie de opnames mag bekijken. Er wordt geen geluid of webcam opgenomen.
