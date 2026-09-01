using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Seg
{
        public class SwitchHelper
        {
                public HashSet<object> seen = new HashSet<object>();

                public bool equals(object obj1, object obj2)
                {
                        if (seen.Contains(obj2))
                        {
                                throw new Exception($"Already checked {obj1} == {obj2}");
                        }
                        var ret = obj1 == obj2;

                        seen.Add(obj2);
                        return ret;

                }
        }
}
