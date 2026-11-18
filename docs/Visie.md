# Visie

## Waarom Examenopname?

Steeds meer examens gebeuren op de laptop. Dat is praktisch, maar het maakt toezicht moeilijker: een leerkracht ziet niet wat er op dertig schermen tegelijk gebeurt. Examenopname neemt tijdens een examen het scherm van de leerling op. Is er achteraf twijfel, dan kan de school kijken wat er echt gebeurd is.

Het doel is niet om leerlingen te wantrouwen. Het doel is dat elk examen eerlijk verloopt, en dat een leerling die eerlijk gewerkt heeft dat ook kan aantonen. Een opname beschermt leerlingen evengoed als dat ze fraude zichtbaar maakt.

## Uitgangspunten

**Eenvoud boven alles.** Het programma doet één ding: het scherm opnemen tot een leerkracht de opname stopt. Er zijn geen extra functies die kapot kunnen gaan. De volledige broncode en de scripts zijn op enkele minuten na te lezen. Wie twijfelt over een nieuwe functie, laat ze beter weg.

**Betrouwbaar, ook als er iets misloopt.** Een examen duurt maar één keer. Daarom herstelt het programma zichzelf. Stopt de opname, dan start ze binnen enkele seconden opnieuw. Wordt het programma afgesloten, dan start een waakhond het binnen de minuut opnieuw op. Valt de laptop uit, dan gaat de opname na het aanmelden verder. De video wordt opgeslagen in stukken van tien minuten, zodat een stroomonderbreking nooit de hele opname kost.

**Zichtbaar en eerlijk.** Een leerling weet altijd dat er opgenomen wordt. Er staat een duidelijk venster met een rode rand op het scherm, en het programma start pas als de leerling zelf op "Start opname" klikt. Er wordt geen geluid opgenomen en de webcam wordt niet gebruikt. Het is een hulpmiddel bij het toezicht, geen spionagesoftware.

**De leerkracht beslist.** Alleen een leerkracht kan een opname stoppen, met een paswoord. Dat paswoord staat nergens leesbaar op de laptop.

**Alles blijft op school.** De opnames blijven op de laptop zelf. Er gaat niets naar de cloud of naar een externe partij. Na een vast aantal dagen worden oude opnames automatisch verwijderd. Opnames worden alleen bekeken als er een concreet vermoeden van fraude is.

**Gratis en onafhankelijk.** Examenopname gebruikt FFmpeg, open-source software die wereldwijd gebruikt wordt om video op te nemen. Het programma wordt gebouwd met de compiler die al in Windows zit. Er zijn geen licenties, geen abonnementen en geen externe diensten nodig.

**Over te nemen door iemand anders.** Een school verandert, mensen veranderen van job. Daarom is alles beschreven en zo opgebouwd dat een andere ICT-medewerker het kan begrijpen, aanpassen en opnieuw bouwen, zonder de oorspronkelijke maker te moeten bellen.

## Wat Examenopname niet doet

Examenopname vergrendelt de laptop niet. Leerlingen kunnen nog steeds andere programma's of websites openen, maar dat staat dan wel op de opname. Wie internet of programma's wil blokkeren tijdens een examen, doet dat best met een aparte oplossing.

Er is geen live meekijken en geen automatische fraudedetectie. Een mens bekijkt de opname, en alleen als het nodig is.

Het programma is ook niet onkraakbaar. Iemand met beheerdersrechten of veel technische kennis kan een opname verstoren. Daarom houdt het programma een logboek bij: elke onderbreking, elke herstart en elk fout paswoord wordt genoteerd met datum en uur. Een opname die niet door een leerkracht gestopt werd, valt meteen op. Examenopname ondersteunt het toezicht in de klas, het vervangt het niet.

## Belangrijke keuzes

**Opslaan op de laptop in plaats van op een server.** Een netwerk kan wegvallen tijdens een examen, een lokale schijf niet. De meeste opnames worden nooit bekeken, dus het is niet nodig om ze allemaal te verzamelen. Is er een vermoeden van fraude, dan haalt de ICT-dienst die ene opname op.

**Een waakhond in plaats van Taakbeheer blokkeren.** Taakbeheer blokkeren is ingrijpend en hindert leerlingen ook buiten de examens. Een waakhond die het programma gewoon opnieuw start, en elke onderbreking noteert, is eenvoudiger en eerlijker.

**Vijf beelden per seconde.** Dat is genoeg om te zien welke programma's en websites gebruikt worden, terwijl een examen van een paar uur maar enkele honderden MB inneemt.

**Bouwen op de eigen computer.** Het pakket wordt gebouwd met één script op een gewone Windows-computer, zonder online bouwdiensten. Zo blijft de school zelf baas over wat er op de laptops terechtkomt.

## Naar de toekomst

Houd het klein. Elke uitbreiding maakt het programma moeilijker na te lezen en vergroot de kans op fouten tijdens een examen. Pas eerst de instellingen aan voor je code aanpast. Moet er toch iets veranderen, test het dan eerst op één laptop en werk de handleidingen bij.
