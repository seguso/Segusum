namespace Seg
{
    public class DynLineClient
    {
        public string dlcSerId;

        public MapPoint dlcStartPoint;
        public MapPoint dlcEndPoint;
        public bool dlcIsVisibleNow;

        public override string ToString()
        {
            return $"{dlcSerId}";
        }
    }
}
