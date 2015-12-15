using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.Cuda;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;

namespace SJTU.IOTLab.ManTracking.ImageProcess
{
    class EdgeDetection
    {
        public static byte[] Canny(IntPtr buffer, int width, int height, bool smooth = false)
        {
            unsafe
            {
                Mat source = new Mat(height, width, DepthType.Cv8U, 1, buffer, width);
                Mat blurred = new Mat(height, width, DepthType.Cv8U, 1);
                if (smooth) {
                    CvInvoke.Blur(source, blurred, new Size(3, 3), new Point(-1, -1));
                }
                Mat cannyEdges = new Mat(height, width, DepthType.Cv8U, 1);
                double cannyThreshold = 180.0;
                double cannyThresholdLinking = 60.0;
                CvInvoke.Canny(smooth ? blurred : source, cannyEdges, cannyThreshold, cannyThresholdLinking);

                return cannyEdges.GetData();
            }
        }
    }
}
