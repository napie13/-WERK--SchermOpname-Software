using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Examenopname
{
    // Stuurt ffmpeg aan. Er zijn vier manieren om op te nemen, van best naar meest compatibel:
    // 0 = Desktop Duplication met tijdstempel, 1 = zonder tijdstempel, 2 = gdigrab met tijdstempel, 3 = zonder.
    // Faalt een manier meteen bij de start, dan schuiven we door naar de volgende.
    class Opname
    {
        readonly Instellingen inst;
        readonly Sessie sessie;
        readonly Queue<string> laatsteFouten = new Queue<string>();
        Process proces;
        DateTime gestart, herstartOm;
        int modus, mislukt;
        bool modusWerkt;

        public Opname(Instellingen inst, Sessie sessie)
        {
            this.inst = inst;
            this.sessie = sessie;
        }

        public bool Loopt
        {
            get { return proces != null && !proces.HasExited; }
        }

        public void Start()
        {
            gestart = DateTime.Now;
            lock (laatsteFouten) laatsteFouten.Clear();

            var info = new ProcessStartInfo(Path.Combine(Program.AppMap, "ffmpeg.exe"), Argumenten());
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardInput = true;  // om netjes te stoppen met "q"
            info.RedirectStandardError = true;

            proces = new Process { StartInfo = info };
            proces.ErrorDataReceived += (s, e) =>
            {
                if (e.Data == null) return;
                lock (laatsteFouten)
                {
                    laatsteFouten.Enqueue(e.Data);
                    while (laatsteFouten.Count > 3) laatsteFouten.Dequeue();
                }
            };
            proces.Start();
            proces.BeginErrorReadLine();
        }

        // Elke seconde aangeroepen. Geeft een tekst voor het logboek terug als er iets gebeurde.
        public string Bewaak()
        {
            if (Loopt)
            {
                if ((DateTime.Now - gestart).TotalSeconds > 60) { modusWerkt = true; mislukt = 0; }
                return null;
            }

            if (proces != null)
            {
                string melding = "Opname onderbroken (code " + proces.ExitCode + ").";
                lock (laatsteFouten)
                    if (laatsteFouten.Count > 0) melding += " " + string.Join(" / ", laatsteFouten.ToArray());

                if (!modusWerkt && modus < 3 && (DateTime.Now - gestart).TotalSeconds < 15)
                {
                    modus++;
                    melding += " Volgende poging met opnamemethode " + modus + ".";
                }
                proces.Dispose();
                proces = null;
                mislukt++;
                herstartOm = DateTime.Now.AddSeconds(Math.Min(30, 3 * mislukt));
                return melding;
            }

            if (DateTime.Now < herstartOm) return null;
            try
            {
                Start();
                return "Opname opnieuw gestart.";
            }
            catch (Exception ex)
            {
                mislukt++;
                herstartOm = DateTime.Now.AddSeconds(30);
                return "Opname kon niet starten: " + ex.Message;
            }
        }

        public void Stop()
        {
            if (!Loopt) return;
            try
            {
                proces.StandardInput.Write('q');
                proces.StandardInput.Flush();
            }
            catch (IOException) { }
            if (!proces.WaitForExit(10000)) proces.Kill();
        }

        // Werd het venster eerder hard afgesloten, dan kan ffmpeg nog draaien. Dat ruimen we op voor we starten.
        public static void StopAchtergeblevenOpnames()
        {
            string pad = Path.Combine(Program.AppMap, "ffmpeg.exe");
            int sessieId = Process.GetCurrentProcess().SessionId;
            foreach (Process p in Process.GetProcessesByName("ffmpeg"))
            {
                try
                {
                    if (p.SessionId == sessieId && string.Equals(p.MainModule.FileName, pad, StringComparison.OrdinalIgnoreCase))
                        p.Kill();
                }
                catch (Exception) { } // geen toegang of al gestopt
                finally { p.Dispose(); }
            }
        }

        string Argumenten()
        {
            bool dda = modus < 2, tijdstempel = modus % 2 == 0;
            string filters = "scale='min(" + inst.MaxBreedte + ",iw)':-2," + (tijdstempel ? Tijdstempel() : "") + "format=yuv420p";
            string bron = dda
                ? "-filter_complex \"ddagrab=framerate=" + inst.Fps + ":draw_mouse=1,hwdownload,format=bgra," + filters + "\""
                : "-f gdigrab -framerate " + inst.Fps + " -draw_mouse 1 -i desktop -vf \"" + filters + "\"";

            // Delen van x minuten: valt de laptop uit, dan ben je hooguit één deel kwijt.
            return "-hide_banner -loglevel error -y " + bron +
                " -c:v libx264 -preset veryfast -crf " + inst.Kwaliteit + " -g " + (inst.Fps * 10) +
                " -f segment -segment_time " + (inst.DeelMinuten * 60) + " -reset_timestamps 1 -strftime 1 \"" +
                Path.Combine(sessie.Map, "deel_%Y-%m-%d_%H-%M-%S.mkv") + "\"";
        }

        // Datum, uur, computer en gebruiker staan in beeld, zodat elk fragment te herleiden is.
        static string Tijdstempel()
        {
            return "drawtext=fontfile='C\\:/Windows/Fonts/arial.ttf':text='%{localtime\\:%d-%m-%Y %T}   " +
                Sessie.Computer + "   " + Sessie.Gebruiker +
                "':fontsize=20:fontcolor=white:box=1:boxcolor=black@0.6:boxborderw=6:x=10:y=h-th-10,";
        }
    }
}
