using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Examenopname
{
    // Het kleine venster rechtsonder dat zichtbaar blijft zolang de opname loopt.
    // Sluiten kan alleen met het leerkrachtpaswoord (of doordat Windows afsluit).
    class OpnameVenster : Form
    {
        readonly Instellingen inst;
        readonly Sessie sessie;
        readonly Opname opname;
        readonly Label bolletje, tijd, info, waarschuwing;
        readonly Timer klok = new Timer { Interval = 1000 };
        string controleMelding = "";
        int tikken, schermen = 1, foutePaswoorden;
        bool gestopt;

        public OpnameVenster(Instellingen inst, Sessie sessie)
        {
            this.inst = inst;
            this.sessie = sessie;
            opname = new Opname(inst, sessie);

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

            string logo = Path.Combine(Program.Map, "logo.png");
            if (File.Exists(logo))
                Controls.Add(new PictureBox { Image = Image.FromFile(logo), SizeMode = PictureBoxSizeMode.Zoom, Bounds = new Rectangle(14, 16, 64, 64) });

            Color donker = Color.FromArgb(40, 40, 40);
            bolletje = NieuwLabel("●", 90, 12, 24, 28, 14F, FontStyle.Bold, Color.Red);
            NieuwLabel("OPNAME LOOPT", 112, 12, 250, 28, 14F, FontStyle.Bold, donker);
            tijd = NieuwLabel("00:00:00", 92, 42, 200, 24, 12F, FontStyle.Regular, donker);
            info = NieuwLabel("", 92, 66, 280, 18, 8.5F, FontStyle.Regular, Color.Gray);
            waarschuwing = NieuwLabel("", 14, 100, 215, 20, 9F, FontStyle.Bold, Color.OrangeRed);

            var stopknop = new Button { Text = "Stoppen (leerkracht)", Bounds = new Rectangle(232, 94, 136, 28) };
            stopknop.Click += StopknopGeklikt;
            Controls.Add(stopknop);

            MouseDown += Verslepen;
            klok.Tick += ElkeSeconde;
        }

        Label NieuwLabel(string tekst, int x, int y, int breedte, int hoogte, float grootte, FontStyle stijl, Color kleur)
        {
            var label = new Label { Text = tekst, Bounds = new Rectangle(x, y, breedte, hoogte), ForeColor = kleur, Font = new Font("Segoe UI", grootte, stijl) };
            label.MouseDown += Verslepen;
            Controls.Add(label);
            return label;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Rectangle scherm = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(scherm.Right - Width - 12, scherm.Bottom - Height - 12);
            NativeMethods.SetThreadExecutionState(NativeMethods.WakkerBlijven); // geen slaapstand tijdens de opname

            Opname.StopAchtergeblevenOpnames();
            try
            {
                opname.Start();
            }
            catch (Exception ex)
            {
                sessie.Log("Opname kon niet starten: " + ex.Message); // Bewaak() probeert het zo meteen opnieuw
            }
            Controleer();
            klok.Start();
        }

        void ElkeSeconde(object sender, EventArgs e)
        {
            tikken++;
            string melding = opname.Bewaak();
            if (melding != null) sessie.Log(melding);

            bolletje.Visible = !opname.Loopt || tikken % 2 == 0; // knippert zolang alles goed gaat
            bolletje.ForeColor = opname.Loopt ? Color.Red : Color.Orange;
            tijd.Text = (DateTime.Now - sessie.Start).ToString(@"hh\:mm\:ss");
            waarschuwing.Text = opname.Loopt ? controleMelding : "Opname wordt hersteld...";
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;

            if (tikken % 30 == 0) Controleer();
        }

        // Om de 30 seconden: teken van leven, maximale duur, schijfruimte en aantal schermen.
        void Controleer()
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

            string nieuw = "";
            if (aantal > 1) nieuw = "Extra scherm aangesloten!";
            else if (vrij < 1) nieuw = "Bijna geen schijfruimte meer!";
            if (nieuw != "" && nieuw != controleMelding) sessie.Log(nieuw + " (" + vrij + " GB vrij)");
            controleMelding = nieuw;

            info.Text = Sessie.Gebruiker + "  ·  " + Sessie.Computer + "  ·  " + vrij + " GB vrij";
        }

        void StopknopGeklikt(object sender, EventArgs e)
        {
            string invoer = VraagPaswoord();
            if (invoer == null) return;

            if (inst.PaswoordKlopt(invoer))
            {
                Stop("Opname gestopt door de leerkracht.");
                return;
            }
            foutePaswoorden++;
            sessie.Log("Fout paswoord ingegeven (" + foutePaswoorden + "x).");
            Program.Melding("Dat paswoord klopt niet.");
        }

        void Stop(string reden)
        {
            gestopt = true;
            klok.Stop();
            opname.Stop();
            sessie.Afsluiten(reden + " Duur: " + tijd.Text + ".");
            NativeMethods.SetThreadExecutionState(NativeMethods.Normaal);
            MessageBox.Show(this, "De opname is gestopt en bewaard.", "Examenopname", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }

        string VraagPaswoord()
        {
            using (var venster = new Form())
            {
                venster.AutoScaleDimensions = new SizeF(96F, 96F);
                venster.AutoScaleMode = AutoScaleMode.Dpi;
                venster.Text = "Opname stoppen";
                venster.Font = Font;
                venster.FormBorderStyle = FormBorderStyle.FixedDialog;
                venster.MinimizeBox = venster.MaximizeBox = venster.ShowInTaskbar = false;
                venster.StartPosition = FormStartPosition.CenterScreen;
                venster.TopMost = true;
                venster.ClientSize = new Size(300, 116);

                var vak = new TextBox { UseSystemPasswordChar = true, Bounds = new Rectangle(12, 36, 276, 24) };
                var ok = new Button { Text = "Stoppen", DialogResult = DialogResult.OK, Bounds = new Rectangle(112, 74, 84, 30) };
                var annuleren = new Button { Text = "Annuleren", DialogResult = DialogResult.Cancel, Bounds = new Rectangle(204, 74, 84, 30) };
                venster.Controls.Add(new Label { Text = "Paswoord van de leerkracht:", Bounds = new Rectangle(12, 12, 276, 20) });
                venster.Controls.AddRange(new Control[] { vak, ok, annuleren });
                venster.AcceptButton = ok;
                venster.CancelButton = annuleren;
                return venster.ShowDialog(this) == DialogResult.OK ? vak.Text : null;
            }
        }

        // Alt+F4 of sluiten via de taakbalk doet niets. Alleen als Windows afsluit, laten we het venster gaan.
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!gestopt)
            {
                if (e.CloseReason == CloseReason.WindowsShutDown)
                {
                    klok.Stop();
                    opname.Stop();
                    sessie.Log("Windows werd afgesloten of de gebruiker meldde zich af tijdens de opname.");
                    // De status blijft ACTIEF: na opnieuw aanmelden hervat de waakhond de opname.
                }
                else
                {
                    e.Cancel = true;
                }
            }
            base.OnFormClosing(e);
        }

        void Verslepen(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); // doen alsof de titelbalk vastgenomen wordt
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var rand = new Pen(Color.Red, 3))
                e.Graphics.DrawRectangle(rand, 1, 1, ClientSize.Width - 3, ClientSize.Height - 3);
        }
    }
}
