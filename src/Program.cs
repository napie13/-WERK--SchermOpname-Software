using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace Examenopname
{
    static class Program
    {
        public const string Versie = "1.0.0";
        public static readonly string AppMap = AppDomain.CurrentDomain.BaseDirectory;
        const string MutexNaam = @"Local\Examenopname";

        [STAThread]
        static int Main(string[] args)
        {
            string modus = args.Length > 0 ? args[0].ToLowerInvariant() : "";
            Instellingen inst;
            try
            {
                inst = Instellingen.Laad(Path.Combine(AppMap, "instellingen.ini"));
            }
            catch (Exception ex)
            {
                if (modus == "") Melding("De instellingen konden niet gelezen worden. Verwittig de ICT-dienst.\n\n" + ex.Message);
                return 1;
            }

            if (modus == "/waakhond") return Waakhond(inst);
            if (modus == "/opruimen") return Opruimen(inst);

            bool eerste;
            using (var mutex = new Mutex(true, MutexNaam, out eerste))
            {
                if (!eerste)
                {
                    Melding("De examenopname loopt al.");
                    return 0;
                }

                NativeMethods.SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Liep er nog een opname die onderbroken werd? Dan gaan we daarin verder.
                Sessie sessie = Sessie.ZoekActief(inst);
                if (sessie != null)
                    sessie.Log("Opname hervat. Laatste teken van leven was om " + sessie.LaatsteTeken.ToString("HH:mm:ss") + ".");
                else if (MagStarten(inst))
                    sessie = Sessie.Nieuw(inst);
                else
                    return 0;

                // Een onverwachte fout mag het venster nooit sluiten: noteren en verder opnemen.
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) => sessie.Log("Onverwachte fout: " + e.Exception.Message);
                Application.Run(new Venster(inst, sessie));
            }
            return 0;
        }

        static bool MagStarten(Instellingen inst)
        {
            string fout = null;
            if (!inst.PaswoordIngesteld)
                fout = "Er is geen leerkrachtpaswoord ingesteld. Verwittig de ICT-dienst.";
            else if (!File.Exists(Path.Combine(AppMap, "ffmpeg.exe")) || !Directory.Exists(inst.OpnameMap))
                fout = "Het programma is niet volledig geïnstalleerd. Verwittig de ICT-dienst.";
            else if (Screen.AllScreens.Length > 1)
                fout = "Er is meer dan één scherm aangesloten. Koppel het extra scherm los en probeer opnieuw.";
            else if (VrijeGB(inst) < inst.MinVrijGB)
                fout = string.Format("Er is te weinig vrije ruimte op de schijf ({0} GB vrij, minstens {1} GB nodig). Roep de toezichter.",
                    VrijeGB(inst), inst.MinVrijGB);

            if (fout != null)
            {
                Melding(fout);
                return false;
            }
            return MessageBox.Show("Je scherm wordt opgenomen tot de leerkracht de opname stopt.\n\nKlik op OK om te starten.",
                "Examenopname", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) == DialogResult.OK;
        }

        // Draait elke minuut via een geplande taak. Is de opname weggevallen terwijl ze nog
        // niet gestopt werd door een leerkracht, dan starten we ze opnieuw.
        static int Waakhond(Instellingen inst)
        {
            Mutex bestaand;
            if (Mutex.TryOpenExisting(MutexNaam, out bestaand))
            {
                bestaand.Dispose();
                return 0;
            }
            if (Sessie.ZoekActief(inst) != null)
                Process.Start(Application.ExecutablePath);
            return 0;
        }

        // Draait dagelijks als SYSTEM. Leerlingen kunnen zelf niets verwijderen.
        static int Opruimen(Instellingen inst)
        {
            if (!Directory.Exists(inst.OpnameMap)) return 0;
            DateTime grens = DateTime.Now.AddDays(-inst.BewaarDagen);
            foreach (string gebruiker in Directory.GetDirectories(inst.OpnameMap))
                foreach (string map in Directory.GetDirectories(gebruiker))
                {
                    try
                    {
                        if (Directory.GetLastWriteTime(map) < grens) Directory.Delete(map, true);
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            return 0;
        }

        public static long VrijeGB(Instellingen inst)
        {
            return new DriveInfo(Path.GetPathRoot(inst.OpnameMap)).AvailableFreeSpace / (1L << 30);
        }

        public static void Melding(string tekst)
        {
            MessageBox.Show(tekst, "Examenopname", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    class Instellingen
    {
        public string OpnameMap = @"C:\Examenopnames";
        public string PaswoordHash = "";
        public int MinVrijGB = 5, WaarschuwVrijGB = 1, Fps = 5, Kwaliteit = 30, MaxBreedte = 1920;
        public int DeelMinuten = 10, MaxUren = 6, HervatBinnenMinuten = 30, BewaarDagen = 60;

        public static Instellingen Laad(string pad)
        {
            var inst = new Instellingen();
            foreach (string regel in File.ReadAllLines(pad))
            {
                int pos = regel.IndexOf('=');
                if (pos < 1 || regel.TrimStart().StartsWith(";")) continue;
                var veld = typeof(Instellingen).GetField(regel.Substring(0, pos).Trim());
                if (veld == null) continue;
                string waarde = regel.Substring(pos + 1).Trim();
                veld.SetValue(inst, veld.FieldType == typeof(int) ? (object)int.Parse(waarde) : waarde);
            }
            inst.Fps = Math.Max(1, Math.Min(30, inst.Fps));
            return inst;
        }

        public bool PaswoordIngesteld
        {
            get { return PaswoordHash.StartsWith("pbkdf2-sha256$"); }
        }

        // Formaat: pbkdf2-sha256$iteraties$salt$hash (base64). Het paswoord zelf staat nergens op de laptop.
        public bool PaswoordKlopt(string invoer)
        {
            string[] d = PaswoordHash.Split('$');
            if (d.Length != 4 || d[0] != "pbkdf2-sha256") return false;
            byte[] salt = Convert.FromBase64String(d[2]);
            byte[] verwacht = Convert.FromBase64String(d[3]);
            byte[] berekend;
            using (var kdf = new Rfc2898DeriveBytes(invoer, salt, int.Parse(d[1]), HashAlgorithmName.SHA256))
                berekend = kdf.GetBytes(verwacht.Length);

            int verschil = 0;
            for (int i = 0; i < verwacht.Length; i++) verschil |= verwacht[i] ^ berekend[i];
            return verschil == 0;
        }
    }

    // Eén opname = één map met de videodelen en een logboek.
    // status.txt per gebruiker houdt bij of er een opname loopt: TOESTAND|map|start|laatste teken van leven
    class Sessie
    {
        public readonly string Map;
        public readonly DateTime Start;
        public DateTime LaatsteTeken;
        readonly string statusPad;

        public static readonly string Gebruiker = Veilig(Environment.UserName);
        public static readonly string Computer = Veilig(Environment.MachineName);

        Sessie(Instellingen inst, string map, DateTime start)
        {
            Map = map;
            Start = start;
            statusPad = StatusPad(inst);
        }

        static string Veilig(string tekst)
        {
            return Regex.Replace(tekst, "[^A-Za-z0-9._-]", "_");
        }

        static string StatusPad(Instellingen inst)
        {
            return Path.Combine(inst.OpnameMap, Gebruiker, "status.txt");
        }

        public static Sessie Nieuw(Instellingen inst)
        {
            DateTime nu = DateTime.Now;
            string map = Path.Combine(inst.OpnameMap, Gebruiker, nu.ToString("yyyy-MM-dd_HH-mm-ss"));
            Directory.CreateDirectory(map);
            var sessie = new Sessie(inst, map, nu);
            sessie.Log(string.Format("Opname gestart. Gebruiker: {0}, computer: {1}, versie {2}.",
                Environment.UserName, Environment.MachineName, Program.Versie));
            sessie.TekenVanLeven();
            return sessie;
        }

        public static Sessie ZoekActief(Instellingen inst)
        {
            string[] d;
            try
            {
                d = File.ReadAllText(StatusPad(inst)).Split('|');
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            if (d.Length != 4 || d[0] != "ACTIEF" || !Directory.Exists(d[1])) return null;

            var sessie = new Sessie(inst, d[1], DateTime.Parse(d[2], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
            sessie.LaatsteTeken = DateTime.Parse(d[3], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

            // Te lang geleden: niet meer hervatten, maar wel noteren dat niemand de opname stopte.
            if (DateTime.Now - sessie.LaatsteTeken > TimeSpan.FromMinutes(inst.HervatBinnenMinuten) ||
                DateTime.Now - sessie.Start > TimeSpan.FromHours(inst.MaxUren))
            {
                sessie.Log("Opname werd nooit gestopt door een leerkracht. Laatste teken van leven: " +
                    sessie.LaatsteTeken.ToString("dd-MM-yyyy HH:mm:ss") + ".");
                sessie.SchrijfStatus("AFGEBROKEN", sessie.LaatsteTeken);
                return null;
            }
            return sessie;
        }

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

        void SchrijfStatus(string toestand, DateTime teken)
        {
            try
            {
                File.WriteAllText(statusPad, string.Join("|", toestand, Map, Start.ToString("o"), teken.ToString("o")));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        public void Log(string tekst)
        {
            try
            {
                File.AppendAllText(Path.Combine(Map, "logboek.txt"),
                    DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss") + "  " + tekst + Environment.NewLine);
            }
            catch (IOException) { } // schijf vol: de opname zelf is dan belangrijker
            catch (UnauthorizedAccessException) { }
        }
    }

    static class NativeMethods
    {
        public const uint WakkerBlijven = 0x80000003; // ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED
        public const uint Normaal = 0x80000000;

        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);
    }
}
