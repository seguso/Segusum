using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Seg
{
    //public class npcState
    //{

    //    /// <summary>
    //    /// string like:   I am carrying a suitcase, {1}.
    //    /// </summary>
    //    public Action<pc, List<cutSceneToken> > iAmDoingThisPlaceh;

    //    /// <summary>
    //    /// par like " ___Mr. Harris__ is carrying __a suitcase__."
    //    /// </summary>
    //    //public Func<parHtmlServer> heIsDoingThis;

    //    public Func<bool> appariscente;

    //}


    public class npc
    {
        public Dictionary<Character, ulong> timeILastSawHim = new Dictionary<Character, ulong>();

        public npc(Character ch)
        {
            this.asChar = ch;
        }

        
        public Character asChar;

        //public npcSchedule schedule;

        
        
    }
}
