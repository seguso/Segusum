using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.Xml.Linq;

namespace Seg
{
    internal class NamedCutScene
    {
        public NamedCutSceneId id;

        // Nullable on purpose: old saves record that a scene was seen, but
        // cannot know when it was first completed.
        public DateTime? FirstSeenAt { get; set; }

        public NamedCutScene(NamedCutSceneId id)
        {
            this.id = id;
        }

        public CutScene cs;

        //public string title;

        public List<Mentionable> oggettiMenzionati;

        public Room roomDoveEri;

        internal void serialize(XElement parent)
        {
            if (FirstSeenAt != null)
            {
                parent.Add(new XAttribute("firstSeenAt", FirstSeenAt.Value.ToString(CultureInfo.InvariantCulture)));
            }
        }

        internal void deserialize(XElement element)
        {
            var attribute = element.Attribute("firstSeenAt");
            FirstSeenAt = attribute == null
                ? null
                : DateTime.Parse(attribute.Value, CultureInfo.InvariantCulture);
        }
    }
}
