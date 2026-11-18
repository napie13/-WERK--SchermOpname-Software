using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Examenopname
{
    // Eén sessie is één examenopname: een map met de videodelen en logboek.txt.
    //
    // Per gebruiker houdt status.txt bij of er een opname loopt, in de vorm
    //   TOESTAND|map|starttijd|laatste teken van leven
    // TOESTAND is ACTIEF, GESTOPT of AFGEBROKEN. De waakhond hervat alleen een ACTIEVE sessie.
    class Sessie
    {
        const int HervatBinnenMinuten = 30; // langer onderbroken? dan niet meer hervatten (bv. de volgende dag thuis)

        public static readonly string Gebruiker = Veilig(Environment.UserName);
        public static readonly string Computer = Veilig(Environment.MachineName);

        public readonly string Map;
        public readonly DateTime Start;
        public DateTime LaatsteTeken;
        readonly string statusBestand;

        Sessie(Instellingen inst, string map, DateTime start)
        {
            Map = map;
            Start = start;
            statusBestand = Path.Combine(inst.OpnameMap, Gebruiker, "status.txt");
        }

        public static Sessie Nieuw(Instellingen inst)
        {
            DateTime nu = DateTime.Now;
            string map = Path.Combine(inst.OpnameMap, Gebruiker, nu.ToString("yyyy-MM-dd_HH-mm-ss"));
            Directory.CreateDirectory(map);

            var sessie = new Sessie(inst, map, nu);
            sessie.Log("Opname gestart. Gebruiker: " + Environment.UserName + ", computer: " + Environment.MachineName + ", versie " + inst.Versie + ".");
            sessie.TekenVanLeven();
            return sessie;
        }

        public static Sessie ZoekLopende(Instellingen inst)
        {
            Sessie sessie;
            try
            {
                string[] delen = File.ReadAllText(Path.Combine(inst.OpnameMap, Gebruiker, "status.txt")).Split('|');
                if (delen.Length != 4 || delen[0] != "ACTIEF" || !Directory.Exists(delen[1])) return null;
                sessie = new Sessie(inst, delen[1], LeesTijd(delen[2]));
                sessie.LaatsteTeken = LeesTijd(delen[3]);
            }
            catch (Exception)
            {
                return null; // geen of onleesbaar statusbestand: er loopt geen opname
            }

            bool teLangOnderbroken = DateTime.Now - sessie.LaatsteTeken > TimeSpan.FromMinutes(HervatBinnenMinuten);
            bool teLangBezig = DateTime.Now - sessie.Start > TimeSpan.FromHours(inst.MaxUren);
            if (teLangOnderbroken || teLangBezig)
            {
                sessie.Log("Opname werd nooit gestopt door een leerkracht. Laatste teken van leven: " + sessie.LaatsteTeken.ToString("dd-MM-yyyy HH:mm:ss") + ".");
                sessie.SchrijfStatus("AFGEBROKEN", sessie.LaatsteTeken);
                return null;
            }
            return sessie;
        }

        // Om de 30 seconden, zodat de waakhond weet dat de opname recent nog liep.
        public void TekenVanLeven()
        {
            LaatsteTeken = DateTime.Now;
            SchrijfStatus("ACTIEF", LaatsteTeken);
        }

        public void Afsluiten(string reden)
        {
            Log(reden);
            SchrijfStatus("GESTOPT", DateTime.Now);
        }

        public void Log(string tekst)
        {
            try
            {
                File.AppendAllText(Path.Combine(Map, "logboek.txt"), DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss") + "  " + tekst + Environment.NewLine);
            }
            catch (IOException) { }                 // bv. schijf vol: de opname zelf gaat voor
            catch (UnauthorizedAccessException) { }
        }

        void SchrijfStatus(string toestand, DateTime teken)
        {
            try
            {
                File.WriteAllText(statusBestand, toestand + "|" + Map + "|" + Start.ToString("o") + "|" + teken.ToString("o"));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        static DateTime LeesTijd(string tekst)
        {
            return DateTime.Parse(tekst, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        // Alleen letters, cijfers, punt, streepje en underscore, zodat de naam altijd een geldige mapnaam is.
        static string Veilig(string tekst)
        {
            return Regex.Replace(tekst, "[^A-Za-z0-9._-]", "_");
        }
    }
}
