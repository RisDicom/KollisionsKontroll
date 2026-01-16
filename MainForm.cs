using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Collections;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using VMS.TPS.Common.Model.API;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows;
using System.Security.Cryptography;
using VMS.TPS.Common.Model.Types;
using System.Security.Policy;
using System.Windows.Documents;

namespace KollisionsKontroll
{
    public partial class MainForm : Form
    {
        private PlanSetup _plan;
        private Structure _bord;
        private Structure _body;
        private Structure _Target;
        private ElementPosition ipp0;
        private double rad = 390;

        public static void Main(PlanSetup plan)
        {
            System.Windows.Forms.Application.Run(new MainForm(plan));
        }

        public MainForm(PlanSetup plan)
        {
            InitializeComponent();
            _plan = plan;

            // Määritetään pöytä, keho (Body) ja kohdealue (PTV) suunnitelmasta
            _bord = _plan.StructureSet.Structures.FirstOrDefault(s => s.Id == "CouchSurface");
            _body = _plan.StructureSet.Structures.FirstOrDefault(s => s.DicomType == "EXTERNAL");
            _Target = _plan.StructureSet.Structures.FirstOrDefault(s => s.DicomType == "PTV");

            InitializeGUI();
            ipp0 = chKollision.ChartAreas[0].InnerPlotPosition; // Määritetään graafin sijainti
        }

        private void InitializeGUI()
        {
            // Haetaan kuvan koko x- ja y-suunnassa
            var xSize = Math.Round(_plan.StructureSet.Image.XSize / 100d, 0) * 100;
            var ySize = Math.Round(_plan.StructureSet.Image.YSize / 100d, 0) * 100;

            double padding = 300; // Puskuri kaavion koolle, koska gantryn kierto voi mennä kuvan ulkopuolelle
            var ca = chKollision.ChartAreas[0];
            ca.AxisX.Maximum = xSize / 2 + padding; // Määritetään pikselimatriisin x max
            ca.AxisY.Maximum = ySize / 2 + padding; // Määritetään pikselimatriisin y max
            ca.AxisX.Minimum = -xSize / 2 - padding; // Määritetään pikselimatriisin x min
            ca.AxisY.Minimum = -ySize / 2 - padding; // Määritetään pikselimatriisin y min
            ca.AxisY.IsReversed = true;

            ca.AxisX.MinorGrid.Enabled = true;
            ca.AxisX.MajorGrid.Enabled = false;
            ca.AxisX.MinorGrid.LineWidth = 1;
            ca.AxisX.MinorGrid.LineDashStyle = ChartDashStyle.Dot;
            ca.AxisX.MinorGrid.Interval = 50; // Gridin koko x

            ca.AxisY.MinorGrid.Enabled = true;
            ca.AxisY.MajorGrid.Enabled = false;
            ca.AxisY.MinorGrid.LineWidth = 1;
            ca.AxisY.MinorGrid.LineDashStyle = ChartDashStyle.Dot;
            ca.AxisY.MinorGrid.Interval = 50; // Gridin koko y

            ca.AxisX.Interval = 50;
            ca.AxisY.Interval = 50;

            // Muunnetaan isokeskuksen sijainti DICOM-koordinaateista Eclipse-koordinaateiksi
            VVector isoPos = _plan.StructureSet.Image.DicomToUser(_plan.Beams.First().IsocenterPosition, _plan);

            txtLatIso.Text = (isoPos.x / 10).ToString("0.00"); // Kirjoitetaan x-koordinaatti cm:nä (2 desimaalia) käyttöliittymään
            txtVRTiso.Text = (isoPos.y / 10).ToString("0.00"); // Kirjoitetaan y-koordinaatti cm:nä (2 desimaalia) käyttöliittymään

            // Lisätään sarjat (piirrokset) eri rakenteille
            chKollision.Series.Add("iso");
            chKollision.Series.Add("Höger Armbåge"); // Oikea kyynärpää
            chKollision.Series.Add("Vänster Armbåge"); // Vasen kyynärpää
            chKollision.Series.Add("Body");
            chKollision.Series.Add("PTV");
            chKollision.Series.Add("Gantry");
            chKollision.Series.Add("bord"); // Pöytä

            // Määritetään graafityypit
            chKollision.Series["iso"].ChartType = SeriesChartType.Bubble;
            chKollision.Series["Body"].ChartType = SeriesChartType.FastPoint;
            chKollision.Series["PTV"].ChartType = SeriesChartType.FastPoint;
            chKollision.Series["Gantry"].ChartType = SeriesChartType.Line;
            chKollision.Series["bord"].ChartType = SeriesChartType.FastPoint;

            // Määritetään värit
            chKollision.Series["iso"].Color = Color.Red;
            chKollision.Series["Body"].Color = Color.Orange;
            chKollision.Series["PTV"].Color = Color.Blue;
            chKollision.Series["Gantry"].Color = Color.Red;
            chKollision.Series["bord"].Color = Color.Fuchsia;

            chKollision.Series["iso"].Points.AddXY(isoPos.x, isoPos.y); // Lisätään isokeskuksen piste

            UpdateStructure(); // Piirretään kaikki rakenteet (pöytä, body, PTV) isokeskuksen sijainnin perusteella
        }

        // Piirtää kaikki rakenteet (pöytä, body, PTV) isokeskuksen sijainnin perusteella
        private void UpdateStructure()
        {
            double x, y, xIso, yIso;

            // Jos x ja y voidaan muuntaa luvuiksi, jatketaan
            if (CheckTextInput(txtLatIso.Text, out xIso) && CheckTextInput(txtVRTiso.Text, out yIso))
            {
                // Haetaan isokeskuksen sijainti syötteen perusteella + z suunnitelmasta
                VVector isoPos = new VVector(xIso, yIso, _plan.StructureSet.Image.DicomToUser(_plan.Beams.First().IsocenterPosition, _plan).z);

                if (_bord != null) // Tarkistetaan onko pöytä olemassa
                {
                    RitaStruktur(isoPos, _bord, "bord", true); // Piirretään pöytä
                }
                else
                    System.Windows.MessageBox.Show("Hittade ingen bordstruktur"); // Pöytärakennetta ei löytynyt

                if (_body != null && _Target != null) // Tarkistetaan onko Body ja Target olemassa
                {
                    RitaStruktur(isoPos, _body, "Body", false); // Piirretään Body
                    RitaStruktur(isoPos, _Target, "PTV", false); // Piirretään PTV
                }
                else
                    System.Windows.MessageBox.Show("Hittade ingen body eller PTV"); // Bodya tai PTV:tä ei löytynyt
            }
        }

        // Päivittää graafin kaikilla piirroksilla
        private void UpdateChart()
        {
            chKollision.Series["iso"].Points.Clear(); // Tyhjennetään aiempi isokeskuspiste
            UpdateStructure(); // Piirretään rakenteet uudelleen
            this.chKollision.Invalidate(); // Mitätöidään graafialue (pakottaa uudelleenpiirron)
            this.chKollision.Update(); // Päivitetään graafi
            RitaArmbågar("Höger Armbåge", txtArmLatDx.Text, txtArmVrtDx.Text, grpArmDx, lblArmDx); // Piirretään oikea kyynärpää
            RitaArmbågar("Vänster Armbåge", "-" + txtArmLatSin.Text, txtArmVrtSin.Text, grpArmSin, lblArmSin); // Piirretään vasen kyynärpää
        }

        // Piirtää kyynärpään graafiin syötteen perusteella
        private void RitaArmbågar(string ser, string lat, string vrt, GroupBox grpArm, Label label)
        {
            var s = chKollision.Series[ser]; // Valitaan sarja
            s.Points.Clear(); // Tyhjennetään sarja

            double x, y, xIso, yIso;

            if (CheckTextInput(txtLatIso.Text, out xIso) && CheckTextInput(txtVRTiso.Text, out yIso))
            {
                if (CheckTextInput(lat, out x) && CheckTextInput(vrt, out y))
                {
                    s.Points.AddXY(-x, -y); // Lisätään piste. Miinusmerkki, koska koordinaatit ovat muuten ylösalaisin.
                    s.ChartType = SeriesChartType.Point;
                    s.MarkerSize = 15;

                    double dist = GetDistance(-x, -y, xIso, yIso); // Lasketaan etäisyys gantryyn
                    CheckDist(grpArm, label, dist, "Armbågen är "); // Tarkistetaan onko etäisyys ok
                }
            }
        }

        // Piirtää rakenteen kaavioon (TÄSSÄ ON OLLUT ONGELMAKOHTA, NYT KORJATTU)
        private void RitaStruktur(VVector isoPos, Structure strukt, string serie, bool isBord)
        {
            double minDist = 999; // Alustetaan pienin etäisyys suureksi
            double tempDist = 0;

            VMS.TPS.Common.Model.API.Image img = _plan.StructureSet.Image; // Haetaan kuva
            var isoDCM = img.UserToDicom(isoPos, _plan); // Muunnetaan iso DICOM-koordinaatteihin

            // Tarkistetaan, että Z-koordinaatti on kuvan alueella
            if (isoDCM.z < img.Origin.z || isoDCM.z > (img.Origin.z + (img.ZSize * img.ZRes)))
            {
                return; // Isokeskus on kuva-alueen ulkopuolella (Z-suunnassa), ei voida piirtää
            }

            int imagePlane = Convert.ToInt32(Math.Round((isoDCM.z - img.Origin.z) / img.ZRes)); // Lasketaan leikkeen indeksi

            // KORJAUS: Haetaan ääriviivat ensin muuttujaan ilman .First() -kutsua
            var contours = strukt.GetContoursOnImagePlane(imagePlane);

            // Tarkistetaan, löytyikö tältä leikkeeltä yhtään ääriviivaa kyseiselle rakenteelle.
            // Jos rakennetta ei ole tällä leikkeellä (esim. pöytä poikki tai ollaan PTV:n ulkopuolella), hypätään yli.
            if (contours != null && contours.Length > 0)
            {
                var contourOnImagePlane = contours.First(); // Nyt tämä on turvallista suorittaa

                foreach (var point in contourOnImagePlane) // Käydään läpi jokainen piste
                {
                    VVector contourUser = img.DicomToUser(point, _plan); // Muunnetaan piste Eclipse-koordinaatteihin
                    chKollision.Series[serie].Points.AddXY(contourUser.x, contourUser.y); // Lisätään piste kaavioon

                    if (isBord) // Jos piirretään pöytää, lasketaan etäisyys gantryyn
                    {
                        tempDist = GetDistance(contourUser.x, contourUser.y, isoPos.x, isoPos.y);
                        if (tempDist < minDist)
                        {
                            minDist = tempDist;
                        }
                    }
                }
                if (isBord)
                    CheckDist(grpBord, lblBord, minDist, "Bordskanten är "); // Tarkistetaan etäisyys
            }
            else
            {
                // Tänne tullaan, jos rakenne puuttuu tältä leikkeeltä.
                // Ei kaaduta, vaan ilmoitetaan käyttöliittymään tarvittaessa.
                if (isBord)
                {
                    lblBord.Text = "Ei pöytää tällä leikkeellä";
                    grpBord.BackColor = Color.LightGray;
                }
            }
        }

        // Päivittää graafin kun GUI:ta piirretään (esim. ikkunan koon muutos)
        private void chKollision_PrePaint(object sender, ChartPaintEventArgs e)
        {
            RitaIsoAndGantry(e); // Piirretään iso ja gantryn ympyrä
        }

        // Piirtää isokeskuksen ja gantrya kuvaavan ympyrän
        private void RitaIsoAndGantry(ChartPaintEventArgs e)
        {
            double x, y;

            if (CheckTextInput(txtLatIso.Text, out x) && CheckTextInput(txtVRTiso.Text, out y))
            {
                chKollision.Series["iso"].Points.AddXY(x, y); // Lisätään iso-piste
                chKollision.Series["iso"].ChartType = SeriesChartType.FastPoint;

                ChartArea ca = chKollision.ChartAreas[0];
                Axis ax = ca.AxisX;
                Axis ay = ca.AxisY;

                var s = chKollision.Series["iso"];

                DataPoint dpCenter = s.Points[0];
                dpCenter.MarkerStyle = MarkerStyle.Circle; // Määritetään tyyli ympyräksi

                // Lasketaan säde pikseleinä
                float xRad = (float)(ax.ValueToPixelPosition(0) - ax.ValueToPixelPosition(rad));
                float yRad = (float)(ay.ValueToPixelPosition(0) - ay.ValueToPixelPosition(rad));

                float xc = (float)ax.ValueToPixelPosition(x);
                float yc = (float)ay.ValueToPixelPosition(y);

                Rectangle r = Rectangle.Round(new RectangleF(xc - xRad, yc - yRad, xRad * 2, yRad * 2));

                e.ChartGraphics.Graphics.DrawEllipse(Pens.Red, r); // Piirretään punainen ympyrä
            }
        }

        // Tarkistaa etäisyyden ja muuttaa taustaväriä sen mukaan
        private static void CheckDist(GroupBox grpArm, Label label, double dist, string descText)
        {
            if (dist >= 3) // Yli 3 cm: ei ongelmaa
                grpArm.BackColor = Color.LightGreen;
            else if (dist < 3 && dist > 1.5) // 1.5 - 3 cm: gantry pyörii hitaasti
                grpArm.BackColor = Color.Yellow;
            else if (dist <= 1.5 && dist > 0) // 0 - 1.5 cm: tarvitaan override
                grpArm.BackColor = Color.Orange;
            else // Alle 0 cm: törmäys
                grpArm.BackColor = Color.Tomato;

            label.Text = descText + dist.ToString("0.00") + " cm från gantry"; // Tulostetaan etäisyys
        }

        // Tarkistaa onko syöte luku
        private bool CheckTextInput(string str, out double value)
        {
            bool ok = double.TryParse(str, out value);
            value = value * 10;
            return ok;
        }

        // Laskee etäisyyden pisteen ja gantryn kehän välillä
        private double GetDistance(double x, double y, double xIso, double yIso)
        {
            // Lasketaan etäisyys ympyrän kehälle
            double dist = (rad - Math.Sqrt(Math.Pow((x - xIso), 2) + Math.Pow((y - yIso), 2))) / 10;
            return dist;
        }

        // Palauttaa graafin alkutilaan
        private void btnReset_Click(object sender, EventArgs e)
        {
            chKollision.Series.Clear();
            InitializeGUI();
            UpdateChart();
        }

        private void btnExecute_Click(object sender, EventArgs e)
        {
            UpdateChart();
        }

        private void txtLatIso_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                UpdateChart();
            }
        }

        private void txtVRTiso_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                UpdateChart();
            }
        }

        private void txtArmLatDx_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                UpdateChart();
        }

        private void txtArmLatSin_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                UpdateChart();
        }

        private void txtArmVrtDx_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                UpdateChart();
        }

        private void txtArmVrtSin_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                UpdateChart();
        }

        // Pitää kuvasuhteen neliönä (jotta ympyrä on pyöreä eikä soikea)
        private void makeSquare(Chart chart)
        {
            ChartArea ca = chart.ChartAreas[0];

            ElementPosition cap = ca.Position;

            // Lasketaan koot pikseleinä
            System.Drawing.Size CaSize = new System.Drawing.Size((int)(cap.Width * chart.ClientSize.Width / 100f),
                                    (int)(cap.Height * chart.ClientSize.Height / 100f));

            System.Drawing.Size IppSize = new System.Drawing.Size((int)(ipp0.Width * CaSize.Width / 100f),
                                    (int)(ipp0.Height * CaSize.Height / 100f));

            // Käytetään pienempää sivua
            int ippNewSide = Math.Min(IppSize.Width, IppSize.Height);

            // Lasketaan skaalauskertoimet
            float px = ipp0.Width / IppSize.Width * ippNewSide;
            float py = ipp0.Height / IppSize.Height * ippNewSide;

            // Käytetään jompaa kumpaa
            if (IppSize.Width < IppSize.Height)
                ca.InnerPlotPosition = new ElementPosition(ipp0.X, ipp0.Y, ipp0.Width, py);
            else
                ca.InnerPlotPosition = new ElementPosition(ipp0.X, ipp0.Y, px, ipp0.Height);
        }

        private void chKollision_Resize(object sender, EventArgs e)
        {
            makeSquare(chKollision);
        }

        // Klikkaus graafissa päivittää koordinaatit tekstikenttiin
        private void chKollision_MouseClick(object sender, MouseEventArgs e)
        {
            var xv = chKollision.ChartAreas[0].AxisX.PixelPositionToValue(e.X) / 10;
            var yv = chKollision.ChartAreas[0].AxisY.PixelPositionToValue(e.Y) / 10;
            txtLatIso.Text = xv.ToString("0.00");
            txtVRTiso.Text = yv.ToString("0.00");
            UpdateChart();
        }
    }
}