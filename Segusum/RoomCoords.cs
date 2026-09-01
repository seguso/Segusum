using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Seg
{
    public class RoomCoords
    {
        public string rcRoomId;
        public double? rcX;
        public double? rcY;
        public string rcRoomName;
        public bool rcAlreadyVisitedOnce;
        public bool rcAdjacent;
        public bool rcIsAccessibleFromHere;

        public bool rcIsCurRoom;
    }
}
