using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Seg
{
    public class strangeObjectCarriedSeen
    {
        public LogicObj pi;
        public Character whoWasCarryingIt;
        public override bool Equals(object obj)
        {
            var str = obj as strangeObjectCarriedSeen;

            if (str == null)
                return false;

            return str.pi == pi && str.whoWasCarryingIt == whoWasCarryingIt;
        }

        public override int GetHashCode()
        {
            return pi.GetHashCode() + whoWasCarryingIt.GetHashCode();
        }
    }

    public class strangeObjectData
    {
        public ulong timeLastSeen;
    }

}
