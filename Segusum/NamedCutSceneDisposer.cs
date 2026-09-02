using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Seg
{

    
    public class NamedCutSceneDisposer : IDisposable
    {

        public WorldBase wo;

        public void Dispose()
        {
            if (wo != null)
            {
                if (wo.cur_named_cs != null)
                {
                    wo.cur_named_cs.FirstSeenAt ??= wo.EngineNowForInfrastructure;
                    if (!wo.namedCutScenesSeen.Contains(wo.cur_named_cs))
                    {
                        wo.namedCutScenesSeen.Insert(0, wo.cur_named_cs);
                    }
                }
                wo.cur_named_cs = null;
            }
        }
    }
}
