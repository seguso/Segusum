using System.Collections.Generic;

namespace Seg
{
    public class LocationData
    {
        public List<LocationDataItem> Locations { get; set; } = new();
    }

    public class LocationDataItem
    {
        public string Id { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public List<string> Connections { get; set; } = new();
    }
}
