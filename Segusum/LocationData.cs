using System.Collections.Generic;

namespace Seg
{
    public class LocationData
    {
        public List<LocationDataItem> Locations { get; set; } = new();
        public string BackgroundImageExportedPath { get; set; }
        public double BackgroundImageX { get; set; }
        public double BackgroundImageY { get; set; }
        public double BackgroundImageWidth { get; set; }
        public double BackgroundImageHeight { get; set; }
    }

    public class LocationDataItem
    {
        public string Id { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public List<string> Connections { get; set; } = new();
    }
}
