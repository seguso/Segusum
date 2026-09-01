using System;
using System.Collections.Generic;
using System.Linq;

using System.Text;
using System.Threading.Tasks;

namespace Seg
{
        public class WalkPath
        {
                public List<Room> locations;


                public bool contains(Room r)
                {
                        return locations.Contains(r);
                }

                public Room locationImmediatelyBefore(Room r)
                {
                        for(var il = 0; il < locations.Count; il++)
                        {
                                var curloc = locations[il];
                                if (curloc == r)
                                {
                                        if (il - 1 >= 0)
                                        {
                                                return locations[il - 1];
                                        }
                                        else
                                        {
                                                return null;
                                        }
                                }
                        }
                        return null;
                }

                //public bool containsInOrder(Room r1, Room r2)
                //{
                //    return locations.Contains(r1)
                //        && locations.Contains(r2)
                //        && locations.IndexOf(r1) < locations.IndexOf(r2);
                //}
        }
}

