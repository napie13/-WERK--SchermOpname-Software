using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Examenopname
{
    // Start en bewaakt ffmpeg.exe, dat het scherm opneemt.
    //
    // Er zijn twee opnamemethodes:
    //   ddagrab  modern en zuinig (Desktop Duplication), werkt op bijna alle laptops
    //   gdigrab  oud maar werkt overal, als reserve
    // Stopt ddagrab meteen bij de start, dan schakelen we over op gdigrab.
    class Opname
    {
        readonly Instellingen inst;
        readonly Sessie sessie;
        readonly Queue<string> laatsteFouten = new Queue<string>();
        Process ffmpeg;
        DateTime gestart, volgendePoging;
        bool reserveMethode, methodeWerkt;
        int mislukt;

        public Opname(Instellingen inst, Sessie sessie)
        {
            this.inst = inst;
            this.sessie = sessie;
        }

        public bool Loopt
        {
            get { return ffmpeg != null && !ffmpeg.HasExited; }
        }

        public void Start()
        {
            gestart = DateTime.Now;
            lock (laatsteFouten) laatsteFouten.Clear();

            var info = new ProcessStartInfo(Path.Combine(Program.Map, "ffmpeg.exe"), Argumenten());
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardInput = true;  // om netjes te stoppen met de toets q
            info.RedirectStandardError = true;  // foutmeldingen voor het logboek

            ffmpeg = new Process { StartInfo = info };
            ffmpeg.ErrorDataReceived += (s, e) =>
            {
                if (e.Data == null) return;
                lock (laatsteFouten)
                {
                    laatsteFouten.Enqueue(e.Data);
                    if (laatsteFouten.Count > 3) laatsteFouten.Dequeue();
                }
            };
            ffmpeg.Start();
            ffmpeg.BeginErrorReadLine();
        }

        // Wordt elke seconde aangeroepen. Herstart ffmpeg als het gestopt is.
        // Geeft een tekst voor het logboek terug als er iets gebeurde, anders null.
        public string Bewaak()
        {
            if (Loopt)
            {
                if ((DateTime.Now - gestart).TotalSeconds > 60) { methodeWerkt = true; mislukt = 0; }
                return null;
            }

            if (ffmpeg != null)
            {
                string melding = "Opname onderbroken (code " + ffmpeg.ExitCode + ").";
                lock (laatsteFouten)
                    if (laatsteFouten.Count > 0) melding += " " + string.Join(" / ", laatsteFouten.ToArray());

                if (!reserveMethode && !methodeWerkt && (DateTime.Now - gestart).TotalSeconds < 15)
                {
                    reserveMethode = true;
                    melding += " We schakelen over op de reservemethode (gdigrab).";
                }

                ffmpeg.Dispose();
                ffmpeg = null;
                mislukt++;
                volgendePoging = DateTime.Now.AddSeconds(Math.Min(30, 3 * mislukt)); // 3, 6, 9 ... tot 30 seconden
                return melding;
            }

            if (DateTime.Now < volgendePoging) return null;
            try
            {
                Start();
                return "Opname opnieuw gestart.";
            }
            catch (Exception ex)
            {
                mislukt++;
                volgendePoging = DateTime.Now.AddSeconds(30);
                return "Opname kon niet starten: " + ex.Message;
            }
        }

        public void Stop()
        {
            if (!Loopt) return;
            try
            {
                ffmpeg.StandardInput.Write('q'); // ffmpeg sluit dan het laatste videodeel netjes af
                ffmpeg.StandardInput.Flush();
            }
            catch (IOException) { }
            if (!ffmpeg.WaitForExit(10000)) ffmpeg.Kill();
        }

        // Werd het programma hard afgesloten, dan kan ffmpeg nog draaien. Dat ruimen we op voor we starten.
        public static void StopAchtergeblevenOpnames()
        {
            string pad = Path.Combine(Program.Map, "ffmpeg.exe");
            int sessieId = Process.GetCurrentProcess().SessionId;
            foreach (Process p in Process.GetProcessesByName("ffmpeg"))
            {
                try
                {
                    if (p.SessionId == sessieId && string.Equals(p.MainModule.FileName, pad, StringComparison.OrdinalIgnoreCase))
                        p.Kill();
                }
                catch (Exception) { } // al gestopt of geen toegang
                finally { p.Dispose(); }
            }
        }

        // Het volledige scherm, maximaal 1920 pixels breed, in H.264.
        // Elke 10 minuten begint een nieuw videodeel: valt de laptop uit, dan ben je hooguit dat deel kwijt.
        // De bestandsnaam bevat het uur waarop het deel begint.
        string Argumenten()
        {
            string schaal = "scale='min(1920,iw)':-2,format=yuv420p";
            string bron = reserveMethode
                ? "-f gdigrab -framerate " + inst.Fps + " -i desktop -vf \"" + schaal + "\""
                : "-filter_complex \"ddagrab=framerate=" + inst.Fps + ",hwdownload,format=bgra," + schaal + "\"";

            return "-hide_banner -loglevel error -y " + bron +
                   " -c:v libx264 -preset veryfast -crf " + inst.Kwaliteit + " -g " + (inst.Fps * 10) +
                   " -f segment -segment_time 600 -reset_timestamps 1 -strftime 1" +
                   " \"" + Path.Combine(sessie.Map, "deel_%Y-%m-%d_%H-%M-%S.mkv") + "\"";
        }
    }
}
