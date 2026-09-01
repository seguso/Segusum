using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Seg
{
    public class CutScene : List<CutSceneToken>
    {
        public bool canBeSkipped;

        public  CutScene(bool canBeSkipped)
        {
            this.canBeSkipped = canBeSkipped;
        }
    }
}
