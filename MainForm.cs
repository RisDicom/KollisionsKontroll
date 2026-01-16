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
        // MUUTOS: Talletetaan PTV:t listaan, jotta voimme käsitellä useita PTV-rakenteita (esim. PTV_Lantio, PTV_Imusolmukkeet)
        private List<Structure> _Targets;
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

            // Haetaan rakenteet suunnitelmasta
            _bord = _plan.StructureSet.Structures.FirstOrDefault(s => s.Id == "CouchSurface");
            _body = _plan.StructureSet.Structures.FirstOrDefault(s => s.DicomType == "EXTERNAL");

            // MUUTOS: Haetaan kaikki rakenteet, joiden tyyppi on PTV, ja laitetaan ne listaan.
            // Näin varmistetaan, että oikea PTV piirtyy, vaikka listalla olisi muitakin PTV-rakenteita.
            _Targets = _plan.StructureSet.Structures.Where(s => s.DicomType == "PTV").ToList();

            InitializeGUI();
            ipp0 = chKollision.ChartAreas[0].InnerPlotPosition; // Otetaan talteen graafin alkuperäinen asettelu
        }

        private void InitializeGUI()
        {
            // Lasketaan kuvan koko senteissä ja pyöristetään sopivaksi
            var xSize = Math.Round(_plan.StructureSet.Image.XSize / 100d, 0) * 100;
            var ySize = Math.Round(_plan.StructureSet.Image.YSize / 100d, 0) * 100;

            double padding = 300; // Jätetään reilusti tilaa reunoille, ettei gantryn ympyrä leikkaudu pois
            var ca = chKollision.ChartAreas[0];

            // Asetetaan akselien rajat
            ca.AxisX.Maximum = xSize / 2 + padding;
            ca.AxisY.Maximum = ySize / 2 + padding;
            ca.AxisX.Minimum = -xSize / 2 - padding;
            ca.AxisY.Minimum = -ySize / 2 - padding;
            ca.AxisY.IsReversed = true; // Y-akseli käännetään, jotta kuva on oikein päin (kuten TPS:ssä)

            // Säädetään ristikon (gridin) ulkonäköä
            ca.AxisX.MinorGrid.Enabled = true;
            ca.AxisX.MajorGrid.Enabled = false;
            ca.AxisX.MinorGrid.LineWidth = 1;
            ca.AxisX.MinorGrid.LineDashStyle = ChartDashStyle.Dot;
            ca.AxisX.MinorGrid.Interval = 50;

            ca.AxisY.MinorGrid.Enabled = true;
            ca.AxisY.MajorGrid.Enabled = false;
            ca.AxisY.MinorGrid.LineWidth = 1;
            ca.AxisY.MinorGrid.LineDashStyle = ChartDashStyle.Dot;
            ca.AxisY.MinorGrid.Interval = 50;

            ca.AxisX.Interval = 50;
            ca.AxisY.Interval = 50;

            // Muunnetaan isosentrin sijainti DICOM-koordinaateista Eclipsen koordinaatistoon
            VVector isoPos = _plan.StructureSet.Image.DicomToUser(_plan.Beams.First().IsocenterPosition, _plan);

            // Täytetään tekstikentät oletusarvoilla (isosentrin sijainti cm)
            txtLatIso.Text = (isoPos.x / 10).ToString("0.00");
            txtVRTiso.Text = (isoPos.y / 10).ToString("0.00");

            // Luodaan graafiin tarvittavat sarjat (piirroskerrokset)
            chKollision.Series.Add("iso");
            chKollision.Series.Add("Höger Armbåge"); // Oikea kyynärpää
            chKollision.Series.Add("Vänster Armbåge"); // Vasen kyynärpää
            chKollision.Series.Add("Body");
            chKollision.Series.Add("PTV");
            chKollision.Series.Add("Gantry");
            chKollision.Series.Add("bord"); // Pöytä

            // Määritellään, miltä mikäkin sarja näyttää (pisteitä, viivaa, kuplaa...)
            chKollision.Series["iso"].ChartType = SeriesChartType.Bubble;
            chKollision.Series["Body"].ChartType = SeriesChartType.FastPoint;
            chKollision.Series["PTV"].ChartType = SeriesChartType.FastPoint;
            chKollision.Series["Gantry"].ChartType = SeriesChartType.Line;
            chKollision.Series["bord"].ChartType = SeriesChartType.FastPoint;

            // Asetetaan värit
            chKollision.Series["iso"].Color = Color.Red;
            chKollision.Series["Body"].Color = Color.Orange;
            chKollision.Series["PTV"].Color = Color.Blue;
            chKollision.Series["Gantry"].Color = Color.Red;
            chKollision.Series["bord"].Color = Color.Fuchsia;

            // Lisätään isosentri kuvaan
            chKollision.Series["iso"].Points.AddXY(isoPos.x, isoPos.y);

            // Piirretään loput rakenteet
            UpdateStructure();
        }

        // Tämä funktio hoitaa kaikkien rakenteiden piirtämisen graafiin
        private void UpdateStructure()
        {
            double x, y, xIso, yIso;

            // Tarkistetaan, että tekstikentissä on järkevät luvut
            if (CheckTextInput(txtLatIso.Text, out xIso) && CheckTextInput(txtVRTiso.Text, out yIso))
            {
                // Luodaan VVector isosentrin sijainnista (Z otetaan suoraan plannista)
                VVector isoPos = new VVector(xIso, yIso, _plan.StructureSet.Image.DicomToUser(_plan.Beams.First().IsocenterPosition, _plan).z);

                if (_bord != null)
                {
                    RitaStruktur(isoPos, _bord, "bord", true); // Piirretään pöytä
                }
                else
                    System.Windows.MessageBox.Show("Hittade ingen bordstruktur"); // Varoitus jos pöytää ei löydy

                if (_body != null)
                {
                    RitaStruktur(isoPos, _body, "Body", false); // Piirretään Body

                    // MUUTOS: Käydään läpi PTV-lista
                    if (_Targets != null && _Targets.Count > 0)
                    {
                        foreach (var ptv in _Targets)
                        {
                            // Yritetään piirtää jokainen PTV. Jos joku ei osu leikkeelle, RitaStruktur hoitaa sen kaatumatta.
                            RitaStruktur(isoPos, ptv, "PTV", false);
                        }
                    }
                    else
                    {
                        // Jos haluat varoituksen, kun yhtään PTV:tä ei löydy, voit lisätä sen tähän.
                        // System.Windows.MessageBox.Show("Ei löytynyt yhtään PTV-rakennetta.");
                    }
                }
                else
                    System.Windows.MessageBox.Show("Hittade ingen body eller PTV");
            }
        }

        // Päivittää koko graafin (tyhjentää vanhat ja piirtää uudet)
        private void UpdateChart()
        {
            chKollision.Series["iso"].Points.Clear(); // Poistetaan vanha isosentri
            UpdateStructure(); // Piirretään rakenteet
            this.chKollision.Invalidate(); // Pakotetaan graafin päivitys
            this.chKollision.Update();
            RitaArmbågar("Höger Armbåge", txtArmLatDx.Text, txtArmVrtDx.Text, grpArmDx, lblArmDx); // Piirretään kädet
            RitaArmbågar("Vänster Armbåge", "-" + txtArmLatSin.Text, txtArmVrtSin.Text, grpArmSin, lblArmSin);
        }

        // Hoitaa käsien (armbågar) piirtämisen ja etäisyyden tarkistamisen
        private void RitaArmbågar(string ser, string lat, string vrt, GroupBox grpArm, Label label)
        {
            var s = chKollision.Series[ser];
            s.Points.Clear();

            double x, y, xIso, yIso;

            if (CheckTextInput(txtLatIso.Text, out xIso) && CheckTextInput(txtVRTiso.Text, out yIso))
            {
                if (CheckTextInput(lat, out x) && CheckTextInput(vrt, out y))
                {
                    s.Points.AddXY(-x, -y); // Huom: miinusmerkki tarvitaan koordinaatiston kääntöön
                    s.ChartType = SeriesChartType.Point;
                    s.MarkerSize = 15;

                    double dist = GetDistance(-x, -y, xIso, yIso); // Lasketaan etäisyys
                    CheckDist(grpArm, label, dist, "Armbågen är "); // Tarkistetaan onko liian lähellä
                }
            }
        }

        // Piirtää yksittäisen rakenteen ääriviivat graafiin
        private void RitaStruktur(VVector isoPos, Structure strukt, string serie, bool isBord)
        {
            double minDist = 999;
            double tempDist = 0;

            VMS.TPS.Common.Model.API.Image img = _plan.StructureSet.Image;
            var isoDCM = img.UserToDicom(isoPos, _plan);

            // Varmistetaan, että ollaan ylipäätään kuvan sisällä Z-suunnassa
            if (isoDCM.z < img.Origin.z || isoDCM.z > (img.Origin.z + (img.ZSize * img.ZRes)))
            {
                return; // Isosentri on kuva-alueen ulkopuolella, lopetetaan tähän.
            }

            // Selvitetään, millä kuvaleikkeellä (slice number) isosentri sijaitsee
            int imagePlane = Convert.ToInt32(Math.Round((isoDCM.z - img.Origin.z) / img.ZRes));

            // --- TÄMÄ ON SE TÄRKEIN KORJAUS ---
            // Haetaan ääriviivat muuttujaan ilman .First() -kutsua, jotta ei tule crashia
            var contours = strukt.GetContoursOnImagePlane(imagePlane);

            // Tarkistetaan, onko tällä leikkeellä oikeasti piirrettyä viivaa
            if (contours != null && contours.Length > 0)
            {
                var contourOnImagePlane = contours.First(); // Nyt on turvallista ottaa viiva

                foreach (var point in contourOnImagePlane)
                {
                    VVector contourUser = img.DicomToUser(point, _plan); // Muunnetaan pisteet
                    chKollision.Series[serie].Points.AddXY(contourUser.x, contourUser.y);

                    if (isBord) // Jos piirretään pöytää, lasketaan samalla minimietäisyys gantryyn
                    {
                        tempDist = GetDistance(contourUser.x, contourUser.y, isoPos.x, isoPos.y);
                        if (tempDist < minDist)
                        {
                            minDist = tempDist;
                        }
                    }
                }
                if (isBord)
                    CheckDist(grpBord, lblBord, minDist, "Bordskanten är ");
            }
            else
            {
                // Jos tälle leikkeelle ei ole piirretty kyseistä rakennetta (esim. pöytä katkeaa tai PTV ei ylety tänne),
                // niin ei tehdä mitään. Ohjelma ei kaadu, vaan jatkaa seuraavaan rakenteeseen.
                if (isBord)
                {
                    lblBord.Text = "Ei pöytäkontuuria tällä leikkeellä";
                    grpBord.BackColor = Color.LightGray;
                }
            }
        }

        // Tapahtuu juuri ennen graafin piirtoa (esim. kun ikkunaa venytetään)
        private void chKollision_PrePaint(object sender, ChartPaintEventArgs e)
        {
            RitaIsoAndGantry(e); // Piirretään isosentri ja punainen gantry-rengas
        }

        // Piirtää isosentrin pisteen ja gantryn turvarajan (punainen rengas)
        private void RitaIsoAndGantry(ChartPaintEventArgs e)
        {
            double x, y;

            if (CheckTextInput(txtLatIso.Text, out x) && CheckTextInput(txtVRTiso.Text, out y))
            {
                chKollision.Series["iso"].Points.AddXY(x, y);
                chKollision.Series["iso"].ChartType = SeriesChartType.FastPoint;

                ChartArea ca = chKollision.ChartAreas[0];
                Axis ax = ca.AxisX;
                Axis ay = ca.AxisY;

                var s = chKollision.Series["iso"];

                DataPoint dpCenter = s.Points[0];
                dpCenter.MarkerStyle = MarkerStyle.Circle;

                // Lasketaan renkaan koko pikseleinä suhteessa akseliin
                float xRad = (float)(ax.ValueToPixelPosition(0) - ax.ValueToPixelPosition(rad));
                float yRad = (float)(ay.ValueToPixelPosition(0) - ay.ValueToPixelPosition(rad));

                float xc = (float)ax.ValueToPixelPosition(x);
                float yc = (float)ay.ValueToPixelPosition(y);

                // Piirretään rengas
                Rectangle r = Rectangle.Round(new RectangleF(xc - xRad, yc - yRad, xRad * 2, yRad * 2));
                e.ChartGraphics.Graphics.DrawEllipse(Pens.Red, r);
            }
        }

        // Apufunktio: Tarkistaa etäisyyden ja vaihtaa värin (vihreä/keltainen/punainen)
        private static void CheckDist(GroupBox grpArm, Label label, double dist, string descText)
        {
            if (dist >= 3) // Turvallinen
                grpArm.BackColor = Color.LightGreen;
            else if (dist < 3 && dist > 1.5) // Tarkkana
                grpArm.BackColor = Color.Yellow;
            else if (dist <= 1.5 && dist > 0) // Override tarvitaan
                grpArm.BackColor = Color.Orange;
            else // Törmäys!
                grpArm.BackColor = Color.Tomato;

            label.Text = descText + dist.ToString("0.00") + " cm från gantry";
        }

        // Apufunktio: Tarkistaa onko tekstikentän syöte numero
        private bool CheckTextInput(string str, out double value)
        {
            bool ok = double.TryParse(str, out value);
            value = value * 10; // Muunnetaan mm -> cm (tai skaalataan sopivaksi)
            return ok;
        }

        // Apufunktio: Laskee etäisyyden pisteestä gantryn kehälle
        private double GetDistance(double x, double y, double xIso, double yIso)
        {
            double dist = (rad - Math.Sqrt(Math.Pow((x - xIso), 2) + Math.Pow((y - yIso), 2))) / 10;
            return dist;
        }

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

        // Tämä pitää graafin neliön muotoisena, jotta ympyrä ei veny soikeaksi
        private void makeSquare(Chart chart)
        {
            ChartArea ca = chart.ChartAreas[0];

            ElementPosition cap = ca.Position;

            System.Drawing.Size CaSize = new System.Drawing.Size((int)(cap.Width * chart.ClientSize.Width / 100f),
                                    (int)(cap.Height * chart.ClientSize.Height / 100f));

            System.Drawing.Size IppSize = new System.Drawing.Size((int)(ipp0.Width * CaSize.Width / 100f),
                                    (int)(ipp0.Height * CaSize.Height / 100f));

            int ippNewSide = Math.Min(IppSize.Width, IppSize.Height);

            float px = ipp0.Width / IppSize.Width * ippNewSide;
            float py = ipp0.Height / IppSize.Height * ippNewSide;

            if (IppSize.Width < IppSize.Height)
                ca.InnerPlotPosition = new ElementPosition(ipp0.X, ipp0.Y, ipp0.Width, py);
            else
                ca.InnerPlotPosition = new ElementPosition(ipp0.X, ipp0.Y, px, ipp0.Height);
        }

        private void chKollision_Resize(object sender, EventArgs e)
        {
            makeSquare(chKollision);
        }

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