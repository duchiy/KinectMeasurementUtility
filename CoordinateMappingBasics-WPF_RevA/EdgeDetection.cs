using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;

namespace WpfApplication1
{
    public class EdgeDetection
    {

        Bitmap _bitmap = null;
        PointF _startPt = new PointF();
        PointF _endPt = new PointF();
        PointF _vectorEndStart = new PointF();

        public Bitmap bitmap
        {
            get { return _bitmap; }
            set { _bitmap = value; }
        }

        public void CreateParametricLine(Point start, Point end)
        {
            _startPt.X = start.X;
            _startPt.Y = start.Y;

            _endPt.X = end.X;
            _endPt.Y = end.Y;

            _vectorEndStart.X = _endPt.X - _startPt.X;
            _vectorEndStart.Y = _endPt.Y - _startPt.Y;

        }
        private PointF GetPoint(double t)
        {
            PointF point = new PointF();

            point.X = _startPt.X + _vectorEndStart.X * (float)t;
            point.Y = _startPt.Y + _vectorEndStart.Y * (float)t;
            return point;
        }

        public void GetPixelsPoints(List<Color> colorList, List<PointF> points)
        {

            double distance = Math.Sqrt((_startPt.X - _endPt.X) * (_startPt.X - _endPt.X) + (_startPt.Y - _endPt.Y) * (_startPt.Y - _endPt.Y));
            double interval = 1.0 / distance;
            double interval1 = interval;
            int numberOfitems = (int)distance;
            Color tempPix = new Color();

            PointF point = new PointF();
            for (double i = 1; i <= numberOfitems; i++)
            {
                point = GetPoint(interval1);
                points.Add(point);
                interval1 = interval * i;
                tempPix = _bitmap.GetPixel((int)point.X, (int)point.Y);
                colorList.Add(tempPix);
            }

        }
        public List<double> GetGradientPoints(List<Color> colorList )
        {
            List<double> colorGradientList = new List<double>();
            double colorDifference = 0.0;
            double colormag0 = 0.0;
            double colormag1 = 0.0;
            for (int i = 0; i < colorList.Count-1; i++)
            {
                Color color0 = colorList[i];
                Color color1 = colorList[i+1];
                colormag0 = Math.Sqrt(color0.R * color0.R + color0.G * color0.G + color0.B * color0.B);
                colormag1 = Math.Sqrt(color1.R * color1.R + color1.G * color1.G + color1.B * color1.B);
                colorDifference = colormag0 - colormag1;
                colorGradientList.Add(colorDifference);


            }
            return colorGradientList;
        }

        public void OutputData(List<Color> colorList, List<double> colorGradientList, List<PointF> points, int indexMaxPoint, int indexMaxGradient)
        {
            using (System.IO.StreamWriter file = 
            new System.IO.StreamWriter(@"EdgeData.txt"))
            {

                double colormag0 = 0.0;
                string maxpoint = indexMaxPoint.ToString() + "," + indexMaxGradient.ToString();
                file.WriteLine(maxpoint);
                for (int i = 0; i < colorList.Count - 1; i++)
                {
                    Color color0 = colorList[i];
                    double gradient = colorGradientList[i];
                    colormag0 = Math.Sqrt(color0.R * color0.R + color0.G * color0.G + color0.B * color0.B);
                    
                    String linedata = colorList[i].R.ToString() + ", " +
                                      colorList[i].G.ToString() + ", " +
                                      colorList[i].B.ToString() + ", " +
                                      colormag0.ToString() + ", " +
                                      colorGradientList[i].ToString() + "," +
                                      points[i].X.ToString() + "," +
                                      points[i].Y.ToString();


                    file.WriteLine(linedata);

                }
            }
        }

        public int FindMaxPixelIntensity(List<Color> colorList)
        {
            int i = 0; int index = 0;
            double maxIntensity = Math.Sqrt(colorList[0].R * colorList[0].R + colorList[0].G * colorList[0].G + colorList[0].B * colorList[0].B); ;
            for (i = 1; i < colorList.Count; i++)
            {
                double listIntensity = Math.Sqrt(colorList[i].R * colorList[i].R + colorList[i].G * colorList[i].G + colorList[i].B * colorList[i].B);
                if (listIntensity > maxIntensity)
                {
                    maxIntensity = listIntensity;
                    index = i;
                }
            }
            return index;
        }
        public int FindMaxGradient(List<double> colorGradientList)
        {
            int i = 0; int index = 0;
            double maxGradient = Math.Abs(colorGradientList[0]);
            for (i = 1; i < colorGradientList.Count; i++)
            {

                if (Math.Abs(colorGradientList[i]) > maxGradient)
                {
                    maxGradient = Math.Abs(colorGradientList[i]);
                    index = i;
                }
            }
            return index-1;
        }
        private System.Drawing.Point FindEdge()
        {
            // This method will Find the edge and return a point on the edge
            Point myPoint = new Point();

            // Find the highest intensity pixel
            return myPoint;

        }


    }
}
