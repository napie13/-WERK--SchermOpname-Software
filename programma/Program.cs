using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Examenopname
{
    // Startpunt. Het programma kent drie manieren om te starten:
    //   Examenopname.exe             leerling dubbelklikt: startvenster, daarna opname
    //   Examenopname.exe /waakhond   geplande taak, elke minuut: hervat een onderbroken opname
    //   Examenopname.exe /opruimen   geplande taak als SYSTEM: verwijdert oude opnames
    static class Program
    {
        public static readonly string Map = AppDomain.CurrentDomain.BaseDirectory;
        const string MutexNaam = @"Local\Examenopname"; // zorgt dat er maar één opname tegelijk loopt

        [STAThread]
        static void Main(string[] args)
        {
            string modus = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            Instellingen inst;
            try
            {
                inst = Instellingen.Laad();
            }
            catch (Exception ex)
            {
                if (modus == "") Melding("De instellingen konden niet gelezen worden. Verwittig de ICT-dienst.\n\n" + ex.Message);
                return;
            }

            if (modus == "/opruimen")
            {
                Opruimen(inst); // draait als SYSTEM, zonder vensters
                return;
            }

            // Een onverwachte fout mag de opname nooit afsluiten.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            NativeMethods.SetProcessDPIAware();

            try
            {
                StartOpname(inst, modus == "/waakhond");
            }
            catch (Exception ex)
            {
                // De waakhond toont nooit meldingen. Binnen de minuut probeert hij het opnieuw.
                if (modus == "") Melding("Examenopname kon niet starten. Verwittig de ICT-dienst.\n\n" + ex.Message);
            }
        }

        static void StartOpname(Instellingen inst, bool doorWaakhond)
        {
            bool eerste;
            using (new Mutex(true, MutexNaam, out eerste))
            {
                if (!eerste)
                {
                    if (!doorWaakhond) Melding("Examenopname is al geopend.");
                    return;
                }

                // Liep er een opname die nog niet door een leerkracht gestopt werd? Dan gaan we daarin verder.
                Sessie sessie = Sessie.ZoekLopende(inst);
                if (sessie != null)
                {
                    sessie.Log("Opname hervat na een onderbreking. Vorig teken van leven: " + sessie.LaatsteTeken.ToString("HH:mm:ss") + ".");
                }
                else if (doorWaakhond)
                {
                    return; // niets te hervatten
                }
                else
                {
                    using (var start = new StartVenster())
                        if (start.ShowDialog() != DialogResult.OK) return;
                    if (!KlaarOmTeStarten(inst)) return;
                    sessie = Sessie.Nieuw(inst);
                }

                Application.ThreadException += (s, e) => sessie.Log("Onverwachte fout: " + e.Exception.Message);
                Application.Run(new OpnameVenster(inst, sessie));
            }
        }

        static bool KlaarOmTeStarten(Instellingen inst)
        {
            string fout = null;
            if (!inst.PaswoordIngesteld || !File.Exists(Path.Combine(Map, "ffmpeg.exe")) || !Directory.Exists(inst.OpnameMap))
                fout = "Examenopname is niet volledig geïnstalleerd. Verwittig de ICT-dienst.";
            else if (Screen.AllScreens.Length > 1)
                fout = "Er is een extra scherm aangesloten. Koppel het los en probeer opnieuw.";
            else if (VrijeGB(inst) < inst.MinVrijGB)
                fout = "Er is te weinig vrije ruimte op de laptop (" + VrijeGB(inst) + " GB vrij, " + inst.MinVrijGB + " GB nodig). Roep de leerkracht.";

            if (fout != null) Melding(fout);
            return fout == null;
        }

        // Verwijdert opnamemappen die ouder zijn dan BewaarDagen. Draait als SYSTEM bij het opstarten van de laptop.
        static void Opruimen(Instellingen inst)
        {
            if (!Directory.Exists(inst.OpnameMap)) return;
            DateTime grens = DateTime.Now.AddDays(-inst.BewaarDagen);
            foreach (string gebruiker in Directory.GetDirectories(inst.OpnameMap))
                foreach (string opname in Directory.GetDirectories(gebruiker))
                {
                    try
                    {
                        if (Directory.GetLastWriteTime(opname) < grens) Directory.Delete(opname, true);
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
        }

        public static long VrijeGB(Instellingen inst)
        {
            return new DriveInfo(Path.GetPathRoot(inst.OpnameMap)).AvailableFreeSpace / (1024L * 1024 * 1024);
        }

        public static void Melding(string tekst)
        {
            MessageBox.Show(tekst, "Examenopname", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    static class NativeMethods
    {
        public const uint WakkerBlijven = 0x80000003; // ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED
        public const uint Normaal = 0x80000000;       // ES_CONTINUOUS

        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);
    }
}
