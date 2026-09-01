namespace Seg
{
        public class Filler
        {
                public Filler(string filId, string name, string icon = null, bool IsForSayVerb = false)
                {
                        FilId = filId;
                        Name = name;
                        Icon = icon;
                        this.IsForSayVerb = IsForSayVerb;
                }

                public string FilId { get; set; }
                public string Name { get; set; }

                public string Icon { get; set; }

                public bool IsForSayVerb { get; set; }

        }
}
