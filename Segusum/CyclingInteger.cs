using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Seg
{
    public class CyclingInteger
    {
        public int curVal = 0;
        public int maxVal;

        public int getAndIncrease()
        {
            var oldVal = curVal;

            curVal++;
            if (curVal >= maxVal)
            {
                curVal = 0;
            }

            return oldVal;
        }

        
    }
}
