using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Seg
{
        public class CycleElement
        {
                public CycleElemId Id { get; set; }

                public Func<DateTime?, bool> cond = x => true;

                public bool IsImportant { get; set; } = false;

                public Action<DateTime?> action = x => { };


                public Repeat repeat = Repeat.Forever;

                public CycleElement(CycleElemId id)
                {
                        Id = id;
                }
        }
}
