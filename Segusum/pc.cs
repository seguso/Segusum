using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Seg
{


    public class pc
    {
        public pc (Character ch)
        {
            asChar = ch;
        }

        /// <summary>
        /// what concept he has in mind. only if it is an Inactive PC.
        /// </summary>
        public HashSet<LogicObj> mind = new HashSet<LogicObj>();


        


        ///// <summary>
        ///// If the character is an Inactive playing character at this time, this must not be empty
        ///// </summary>
        //public HashSet<pcObjective> objectives = new HashSet<pcObjective>();



        public Character asChar;

        

        //public pcObjective mostUrgentObjective()
        //{
        //    return objectives.OrderByDescending(o => o.priority).First();
        //}

    }
}
