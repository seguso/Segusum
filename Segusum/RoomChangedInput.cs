using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Seg
{
        public class RoomChangedInput
        {
                public RoomChangedInput(/*bool justChangedRoom, */RandomInputs randomInputs)
                {
                        //this.justChangedRoom = justChangedRoom;
                        this.randomInputs = randomInputs;
                }

                //public bool justChangedRoom { get; set; }

                public TextInput textInputToShow = null;

                public RandomInputs randomInputs { get; set; }
        }

        public class RoomChangedHandler
        {
                public Room roomEntered { get; set; }

                public Action<RoomChangedInput> handler;
        }
}
