using System;
using System.Collections.Generic;
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
        public static byte[] Canny(IntPtr buffer, int width, int height)
        {
            unsafe
            {
                Mat source = new Mat(height, width, DepthType.Cv8U, 1, buffer, width);
                Mat cannyEdges = new Mat(height, width, DepthType.Cv8U, 1);
                double cannyThreshold = 180.0;
                double cannyThresholdLinking = 120.0;
                CvInvoke.Canny(source, cannyEdges, cannyThreshold, cannyThresholdLinking);

                return cannyEdges.GetData();
            }
        }
    }
}
