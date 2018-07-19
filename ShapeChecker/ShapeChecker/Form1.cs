using AForge;
using AForge.Imaging;
using AForge.Imaging.Filters;
using AForge.Math.Geometry;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ShapeChecker
{
    public partial class MainForm : Form
    {
        private static double D254 = 2.54d;
        private float dpiX = 96;
        /// <summary>
        /// system dpiX setting, default is 96
        /// </summary>
        public float DpiX
        {
            get { return dpiX; }
            set { dpiX = value; }
        }
        private PointF pointF;
        private PointF redPointF;

        public double DigitalMagnification
        {
            //1600 -> image size is 1280*960
            get { return PixelToMillimeter(1600) / 4; }
        }

        public MainForm()
        {
            InitializeComponent();
            this.pictureBox.Paint += PictureBox_Paint;
        }

        private void PictureBox_Paint(object sender, PaintEventArgs e)
        {
            //using (Pen p = new Pen(Color.DarkGreen, 1.5f))
            //{
            //    if (!pointF.IsEmpty)
            //    {
            //        e.Graphics.DrawLine(p, pointF.X, pointF.Y - 10, pointF.X, pointF.Y + 10);
            //        e.Graphics.DrawLine(p, pointF.X - 10, pointF.Y, pointF.X + 10, pointF.Y);
            //    }
            //}
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            LoadDemo(string.Format("{0}\\{1}",  System.Environment.CurrentDirectory, "Images\\2018_07_19_09_59_54_901_290_650.png"));
        }

        // Load one of the embedded demo image
        private void LoadDemo(string embeddedFileName)
        {
            // load arrow bitmap
            ProcessImage(embeddedFileName);
        }

        private void tslblOpen_Click(object sender, EventArgs e)
        {
            openFileDialog.InitialDirectory = string.Format("{0}\\Images", System.Environment.CurrentDirectory);
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    ProcessImage(openFileDialog.FileName);
                }
                catch
                {
                    MessageBox.Show("加载所选图片失败。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // Process image
        private void ProcessImage(string fileName)
        {
            Bitmap bitmap = new Bitmap(fileName);
            // lock image
            BitmapData bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadWrite, bitmap.PixelFormat);

            // step 1 - turn background to black
            ColorFiltering colorFilter = new ColorFiltering();

            colorFilter.Red = new IntRange(0, 254);
            colorFilter.Green = new IntRange(0, 254);
            colorFilter.Blue = new IntRange(0, 254);
            colorFilter.FillOutsideRange = false;

            colorFilter.ApplyInPlace(bitmapData);

            // step 2 - locating objects
            BlobCounter blobCounter = new BlobCounter();

            blobCounter.FilterBlobs = true;
            blobCounter.MinHeight = 5;
            blobCounter.MinWidth = 5;

            blobCounter.ProcessImage(bitmapData);
            Blob[] blobs = blobCounter.GetObjectsInformation();
            bitmap.UnlockBits(bitmapData);

            // step 3 - check objects' type and highlight
            SimpleShapeChecker shapeChecker = new SimpleShapeChecker();

            Graphics g = Graphics.FromImage(bitmap);
            Pen yellowPen = new Pen(Color.Yellow, 2); // circles
            Pen redPen = new Pen(Color.Red, 2);       // quadrilateral
            Pen brownPen = new Pen(Color.Brown, 2);   // quadrilateral with known sub-type
            Pen greenPen = new Pen(Color.Green, 2);   // known triangle
            Pen bluePen = new Pen(Color.Blue, 2);     // triangle

            int count = 0;
            for (int i = 0, n = blobs.Length; i < n; i++)
            {
                List<IntPoint> edgePoints = blobCounter.GetBlobsEdgePoints(blobs[i]);

                DoublePoint center;
                double radius;

                // is circle ?
                if (shapeChecker.IsCircle(edgePoints, out center, out radius))
                {
                    g.DrawEllipse(yellowPen,
                        (float)(center.X - radius), (float)(center.Y - radius),
                        (float)(radius * 2), (float)(radius * 2));
                    redPointF = new PointF((float)center.X, (float)center.Y);
                    lblPoint.Text = string.Format("[{0}, {1}]", center.X, center.Y);
                    count++;
                }
                else
                {
                    List<IntPoint> corners;

                    // is triangle or quadrilateral
                    if (shapeChecker.IsConvexPolygon(edgePoints, out corners))
                    {
                        // get sub-type
                        PolygonSubType subType = shapeChecker.CheckPolygonSubType(corners);

                        Pen pen;

                        if (subType == PolygonSubType.Unknown)
                        {
                            pen = (corners.Count == 4) ? redPen : bluePen;
                        }
                        else
                        {
                            pen = (corners.Count == 4) ? brownPen : greenPen;
                        }

                        g.DrawPolygon(pen, ToPointsArray(corners));
                    }
                }
            }

            yellowPen.Dispose();
            redPen.Dispose();
            greenPen.Dispose();
            bluePen.Dispose();
            brownPen.Dispose();
            g.Dispose();

            // put new image to clipboard
            //Clipboard.SetDataObject(bitmap);
            // and to picture box
            pictureBox.Image = bitmap;
            if (count == 1)
            {
                string name = Path.GetFileNameWithoutExtension(fileName);
                var names = name.Split('_');
                if (names.Length == 9)
                {
                    var x = names[7];
                    var y = names[8];
                    txtXPoint.Text = x;
                    txtYPoint.Text = y;
                    btnCalculate_Click(null, null);
                }
                else
                {
                    txtXPoint.Text = "";
                    txtYPoint.Text = "";
                    this.lblDistance.Text = "";
                }
            }
            else
            {
                if(MessageBox.Show("光斑识别失败，请打开包含正常光斑的图片。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error) == DialogResult.OK)
                {
                    this.lblPoint.Text = "[0, 0]";
                    txtXPoint.Text = "";
                    txtYPoint.Text = "";
                    this.lblDistance.Text = "";
                    this.pictureBox.Image = null;
                }
            }
        }

        // Conver list of AForge.NET's points to array of .NET points
        private Point[] ToPointsArray(List<IntPoint> points)
        {
            Point[] array = new Point[points.Count];

            for (int i = 0, n = points.Count; i < n; i++)
            {
                array[i] = new Point(points[i].X, points[i].Y);
            }

            return array;
        }

        private void btnCalculate_Click(object sender, EventArgs e)
        {
            try
            {
                var px = double.Parse(txtXPoint.Text);
                var py = double.Parse(txtYPoint.Text);
                pointF = new PointF((float)px, (float)py);
                var x = PixelToMicroscope(System.Math.Abs(pointF.X - redPointF.X));
                var y = PixelToMicroscope(System.Math.Abs(pointF.Y - redPointF.Y));
                string cir = string.Format("{0:F2} {1}", Math.Sqrt(x * x + y * y), "um");
                this.lblDistance.Text = cir;
                this.pictureBox.Invalidate();
            }
            catch (Exception ex)
            {
            }
        }

        private double PixelToMicroscope(double pixel)
        {
            return PixelToMicron(pixel) / DigitalMagnification;
        }

        private double PixelToMicron(double pixel)
        {
            return PixelToMillimeter(pixel) * 1000;
        }

        private double PixelToCentimeter(double pixel)
        {
            return pixel * D254 / DpiX;
        }

        private double PixelToMillimeter(double pixel)
        {
            return PixelToCentimeter(pixel) * 10;
        }
    }
}
