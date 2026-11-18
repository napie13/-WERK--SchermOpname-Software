using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Examenopname
{
    // Eerste venster na het dubbelklikken: korte uitleg, de handleiding en de knop om te starten.
    class StartVenster : Form
    {
        public StartVenster()
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Examenopname";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Font = new Font("Segoe UI", 10F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            BackColor = Color.White;
            ClientSize = new Size(440, 236);

            string logo = Path.Combine(Program.Map, "logo.png");
            if (File.Exists(logo))
                Controls.Add(new PictureBox { Image = Image.FromFile(logo), SizeMode = PictureBoxSizeMode.Zoom, Bounds = new Rectangle(20, 20, 56, 56) });

            Controls.Add(new Label { Text = "Examenopname", Font = new Font("Segoe UI", 15F, FontStyle.Bold), Bounds = new Rectangle(90, 20, 330, 30) });
            Controls.Add(new Label { Text = "Je scherm wordt opgenomen tijdens het examen.", ForeColor = Color.DimGray, Bounds = new Rectangle(92, 52, 330, 22) });
            Controls.Add(new Label
            {
                Text = "•  Laat het opnamevenster rechtsonder openstaan.\n" +
                       "•  Alleen de leerkracht kan de opname stoppen.\n" +
                       "•  Sluit geen extra scherm aan.",
                Bounds = new Rectangle(20, 96, 400, 72)
            });

            var handleiding = new Button { Text = "Handleiding", Bounds = new Rectangle(20, 186, 110, 32) };
            var annuleren = new Button { Text = "Annuleren", DialogResult = DialogResult.Cancel, Bounds = new Rectangle(204, 186, 100, 32) };
            var starten = new Button { Text = "Start opname", DialogResult = DialogResult.OK, Bounds = new Rectangle(312, 186, 108, 32) };
            handleiding.Click += OpenHandleiding;
            Controls.AddRange(new Control[] { handleiding, annuleren, starten });
            AcceptButton = starten;
            CancelButton = annuleren;
            ActiveControl = starten; // Enter = starten
        }

        void OpenHandleiding(object sender, EventArgs e)
        {
            try
            {
                Process.Start(Path.Combine(Program.Map, "Handleiding.html")); // opent in de standaardbrowser
            }
            catch (Exception)
            {
                Program.Melding("De handleiding kon niet geopend worden.");
            }
        }
    }
}
