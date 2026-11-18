using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Examenopname
{
    // Klein venster rechtsonder dat altijd zichtbaar blijft. Sluiten kan alleen met het leerkrachtpaswoord.
    class Venster : Form
    {
        readonly Instellingen inst;
        readonly Sessie sessie;
        readonly Opname opname;
        readonly Label stip, tijd, info, waarschuwing;
        readonly Timer klok = new Timer { Interval = 1000 };
        string controleMelding = "";
        int tik, schermen = 1, foutePogingen;
        DateTime geblokkeerdTot;
        bool gestopt;

        public Venster(Instellingen inst, Sessie sessie)
        {
            this.inst = inst;
            this.sessie = sessie;
            opname = new Opname(inst, sessie);

            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Examenopname";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9F);
            ClientSize = new Size(380, 130);

            string logo = Path.Combine(Program.AppMap, "logo.png");
            if (File.Exists(logo))
                Controls.Add(new PictureBox { Image = Image.FromFile(logo), SizeMode = PictureBoxSizeMode.Zoom, Bounds = new Rectangle(14, 16, 64, 64) });

            var donker = Color.FromArgb(40, 40, 40);
            stip = NieuwLabel("●", 90, 12, 24, 28, 14F, FontStyle.Bold, Color.Red);
            NieuwLabel("OPNAME LOOPT", 112, 12, 250, 28, 14F, FontStyle.Bold, donker);
            tijd = NieuwLabel("00:00:00", 92, 42, 200, 24, 12F, FontStyle.Regular, donker);
            info = NieuwLabel("", 92, 66, 280, 18, 8.5F, FontStyle.Regular, Color.Gray);
            waarschuwing = NieuwLabel("", 14, 100, 215, 20, 9F, FontStyle.Bold, Color.OrangeRed);

            var knop = new Button { Text = "Stoppen (leerkracht)", Bounds = new Rectangle(232, 94, 136, 28) };
            knop.Click += KnopStoppen;
            Controls.Add(knop);
            ResumeLayout();

            MouseDown += Slepen;
            klok.Tick += Tik;
        }

        Label NieuwLabel(string tekst, int x, int y, int b, int h, float grootte, FontStyle stijl, Color kleur)
        {
            var label = new Label { Text = tekst, Bounds = new Rectangle(x, y, b, h), ForeColor = kleur, Font = new Font("Segoe UI", grootte, stijl) };
            label.MouseDown += Slepen;
            Controls.Add(label);
            return label;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Rectangle scherm = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(scherm.Right - Width - 12, scherm.Bottom - Height - 12);

            NativeMethods.SetThreadExecutionState(NativeMethods.WakkerBlijven);
            SystemEvents.SessionSwitch += SessieGewisseld;
            SystemEvents.PowerModeChanged += StroomGewijzigd;

            Opname.StopAchtergeblevenOpnames();
            try
            {
                opname.Start();
            }
            catch (Exception ex)
            {
                sessie.Log("Opname kon niet starten: " + ex.Message); // de waakfunctie in Tik probeert opnieuw
            }
            Controle();
            klok.Start();
        }

        void Tik(object sender, EventArgs e)
        {
            tik++;
            string melding = opname.Bewaak();
            if (melding != null) sessie.Log(melding);

            stip.Visible = !opname.Loopt || tik % 2 == 0;
            stip.ForeColor = opname.Loopt ? Color.Red : Color.Orange;
            tijd.Text = (DateTime.Now - sessie.Start).ToString(@"hh\:mm\:ss");
            waarschuwing.Text = opname.Loopt ? controleMelding : "Opname wordt hersteld...";
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;

            if (tik % 30 == 0) Controle();
        }

        // Om de 30 seconden: teken van leven, schijfruimte, aantal schermen en maximale duur.
        void Controle()
        {
            sessie.TekenVanLeven();
            if ((DateTime.Now - sessie.Start).TotalHours >= inst.MaxUren)
            {
                Stop("Opname automatisch gestopt na " + inst.MaxUren + " uur.");
                return;
            }

            long vrij = Program.VrijeGB(inst);
            int aantal = Screen.AllScreens.Length;
            if (aantal != schermen)
            {
                sessie.Log("Aantal schermen veranderd van " + schermen + " naar " + aantal + ".");
                schermen = aantal;
            }

            string nieuw = aantal > 1 ? "Extra scherm aangesloten!" : vrij < inst.WaarschuwVrijGB ? "Bijna geen schijfruimte meer!" : "";
            if (nieuw != controleMelding && nieuw != "") sessie.Log(nieuw + " (" + vrij + " GB vrij)");
            controleMelding = nieuw;
            info.Text = Sessie.Gebruiker + "  ·  " + Sessie.Computer + "  ·  " + vrij + " GB vrij";
        }

        void KnopStoppen(object sender, EventArgs e)
        {
            if (DateTime.Now < geblokkeerdTot)
            {
                Program.Melding("Te veel foute pogingen. Probeer over een minuut opnieuw.");
                return;
            }

            string invoer = VraagPaswoord();
            if (invoer == null) return;
            if (inst.PaswoordKlopt(invoer))
            {
                Stop("Opname gestopt door de leerkracht.");
                return;
            }

            foutePogingen++;
            sessie.Log("Fout paswoord ingegeven (poging " + foutePogingen + ").");
            if (foutePogingen % 5 == 0) geblokkeerdTot = DateTime.Now.AddMinutes(1);
            Program.Melding("Dat paswoord klopt niet.");
        }

        void Stop(string reden)
        {
            gestopt = true;
            klok.Stop();
            opname.Stop();
            sessie.Afsluiten(reden + " Duur: " + tijd.Text + ".");
            MessageBox.Show(this, "De opname is gestopt en bewaard in:\n" + sessie.Map, "Examenopname",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }

        string VraagPaswoord()
        {
            using (var vraag = new Form())
            {
                vraag.AutoScaleDimensions = new SizeF(96F, 96F);
                vraag.AutoScaleMode = AutoScaleMode.Dpi;
                vraag.Text = "Opname stoppen";
                vraag.Font = Font;
                vraag.FormBorderStyle = FormBorderStyle.FixedDialog;
                vraag.MinimizeBox = vraag.MaximizeBox = vraag.ShowInTaskbar = false;
                vraag.StartPosition = FormStartPosition.CenterScreen;
                vraag.TopMost = true;
                vraag.ClientSize = new Size(300, 116);

                var vak = new TextBox { UseSystemPasswordChar = true, Bounds = new Rectangle(12, 36, 276, 24) };
                var ok = new Button { Text = "Stoppen", DialogResult = DialogResult.OK, Bounds = new Rectangle(112, 74, 84, 30) };
                var annuleren = new Button { Text = "Annuleren", DialogResult = DialogResult.Cancel, Bounds = new Rectangle(204, 74, 84, 30) };
                vraag.Controls.Add(new Label { Text = "Paswoord van de leerkracht:", Bounds = new Rectangle(12, 12, 276, 20) });
                vraag.Controls.AddRange(new Control[] { vak, ok, annuleren });
                vraag.AcceptButton = ok;
                vraag.CancelButton = annuleren;
                return vraag.ShowDialog(this) == DialogResult.OK ? vak.Text : null;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!gestopt)
            {
                if (e.CloseReason == CloseReason.WindowsShutDown)
                {
                    // Status blijft ACTIEF: na opnieuw aanmelden gaat de opname vanzelf verder.
                    klok.Stop();
                    opname.Stop();
                    sessie.Log("Windows werd afgesloten of de gebruiker meldde zich af tijdens de opname.");
                }
                else
                {
                    e.Cancel = true;
                }
            }
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            SystemEvents.SessionSwitch -= SessieGewisseld;
            SystemEvents.PowerModeChanged -= StroomGewijzigd;
            NativeMethods.SetThreadExecutionState(NativeMethods.Normaal);
            base.OnFormClosed(e);
        }

        void SessieGewisseld(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionLock) sessie.Log("Scherm vergrendeld.");
            if (e.Reason == SessionSwitchReason.SessionUnlock) sessie.Log("Scherm ontgrendeld.");
        }

        void StroomGewijzigd(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Suspend) sessie.Log("Laptop gaat in slaapstand.");
            if (e.Mode == PowerModes.Resume) sessie.Log("Laptop komt uit slaapstand.");
        }

        void Slepen(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); // WM_NCLBUTTONDOWN op de titelbalk
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var rand = new Pen(Color.Red, 3))
                e.Graphics.DrawRectangle(rand, 1, 1, ClientSize.Width - 3, ClientSize.Height - 3);
        }
    }
}
