using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Seg
{
    public abstract class carryingCutScene
    {
        
        public abstract Func<Task> cutSceneWhenTheySeeYouCarryingThis(Character whoSeesYou);
    }
}
