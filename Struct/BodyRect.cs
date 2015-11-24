using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SJTU.IOTLab.ManTracking.Struct
{
    public struct BodyRect
    {
        public int top;
        public int bottom;
        public int left;
        public int right;

        public BodyRect(int _top = 0, int _right = int.MaxValue, int _bottom = int.MaxValue, int _left = 0)
        {
            this.top = _top;
            this.left = _left;
            this.bottom = _bottom;
            this.right = _right;
        }
    }
}
