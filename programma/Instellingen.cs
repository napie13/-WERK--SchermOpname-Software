using System;
using System.IO;
using System.Security.Cryptography;

namespace Examenopname
{
    // Leest instellingen.ini, dat naast het programma staat. Elke regel is Naam=Waarde, regels met ; zijn uitleg.
    class Instellingen
    {
        public string Versie = "?";
        public string OpnameMap = @"C:\Examenopnames";
        public int MinVrijGB = 5;
        public int Fps = 5;
        public int Kwaliteit = 30;
        public int MaxUren = 6;
        public int BewaarDagen = 60;
        public string PaswoordHash = "";

        public static Instellingen Laad()
        {
            var inst = new Instellingen();
            foreach (string regel in File.ReadAllLines(Path.Combine(Program.Map, "instellingen.ini")))
            {
                int pos = regel.IndexOf('=');
                if (regel.TrimStart().StartsWith(";") || pos < 1) continue;

                string waarde = regel.Substring(pos + 1).Trim();
                switch (regel.Substring(0, pos).Trim())
                {
                    case "Versie": inst.Versie = waarde; break;
                    case "OpnameMap": inst.OpnameMap = waarde; break;
                    case "MinVrijGB": inst.MinVrijGB = int.Parse(waarde); break;
                    case "Fps": inst.Fps = Math.Max(1, Math.Min(30, int.Parse(waarde))); break;
                    case "Kwaliteit": inst.Kwaliteit = int.Parse(waarde); break;
                    case "MaxUren": inst.MaxUren = int.Parse(waarde); break;
                    case "BewaarDagen": inst.BewaarDagen = int.Parse(waarde); break;
                    case "PaswoordHash": inst.PaswoordHash = waarde; break;
                }
            }
            return inst;
        }

        public bool PaswoordIngesteld
        {
            get { return PaswoordHash.StartsWith("pbkdf2-sha256$"); }
        }

        // Het paswoord zelf staat nergens op de laptop, alleen een hash.
        // Formaat: pbkdf2-sha256$iteraties$salt$hash (salt en hash in base64). Maak.cmd maakt die aan.
        public bool PaswoordKlopt(string invoer)
        {
            string[] delen = PaswoordHash.Split('$');
            if (delen.Length != 4 || delen[0] != "pbkdf2-sha256") return false;

            byte[] salt = Convert.FromBase64String(delen[2]);
            byte[] verwacht = Convert.FromBase64String(delen[3]);
            byte[] berekend;
            using (var kdf = new Rfc2898DeriveBytes(invoer, salt, int.Parse(delen[1]), HashAlgorithmName.SHA256))
                berekend = kdf.GetBytes(verwacht.Length);

            // Elke byte vergelijken, zodat de rekentijd niets verraadt.
            int verschil = 0;
            for (int i = 0; i < verwacht.Length; i++) verschil |= verwacht[i] ^ berekend[i];
            return verschil == 0;
        }
    }
}
