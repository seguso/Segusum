using System.Globalization;
using System.Xml.Linq;

namespace Seg
{
        public class LayerForClient
        {
                public string lfc_imgPath;
                public int lfc_x;
                public int lfc_y;
                public int lfc_wt;
                public int lfc_ht;
                //public bool lfc_isHires;

                public string lfc_loId;
                public bool lfcIsOutline { get; set; }
                public int lfc_zIndex { get; set; }

                public bool lfc_nameMustAppearInGraphics;


                public static LayerForClient deserialize(XElement xelLayer)
                {

                        var xatIsOutline = xelLayer.Attribute("lfc_is_outline");

                        bool isOutline;
                        if (xatIsOutline == null)
                        {
                                isOutline = false; // purtroppo succede nelle vecchie. molto raro
                        }
                        else
                        {
                                isOutline = xatIsOutline.Value == "Y";
                        }

                        var lfc = new LayerForClient
                        {
                                lfc_ht = int.Parse(xelLayer.Attribute("lfc_ht").Value, CultureInfo.InvariantCulture),
                                lfc_x = int.Parse(xelLayer.Attribute("lfc_x").Value, CultureInfo.InvariantCulture),
                                lfc_y = int.Parse(xelLayer.Attribute("lfc_y").Value, CultureInfo.InvariantCulture),
                                lfc_wt = int.Parse(xelLayer.Attribute("lfc_wt").Value, CultureInfo.InvariantCulture),
                                lfc_nameMustAppearInGraphics = bool.Parse(xelLayer.Attribute("lfc_nameMustAppearInGraphics").Value),

                                lfc_loId= xelLayer.Attribute("lfc_loId").Value,
                                lfc_imgPath = xelLayer.Attribute("lfc_imgPath").Value
                                , lfcIsOutline = isOutline
                        };
                        return lfc;
                }

                public void serialize(XElement xelParent)
                {
                        var xelLayer = new XElement("layer");

                        xelParent.Add(xelLayer);


                        

                        xelLayer.Add(new XAttribute("lfc_imgPath", lfc_imgPath));
                        xelLayer.Add(new XAttribute("lfc_x", lfc_x.ToString(CultureInfo.InvariantCulture)));
                        xelLayer.Add(new XAttribute("lfc_y", lfc_y.ToString(CultureInfo.InvariantCulture)));
                        xelLayer.Add(new XAttribute("lfc_wt", lfc_wt.ToString(CultureInfo.InvariantCulture)));
                        xelLayer.Add(new XAttribute("lfc_ht", lfc_ht.ToString(CultureInfo.InvariantCulture)));
                        xelLayer.Add(new XAttribute("lfc_loId", lfc_loId));
                        xelLayer.Add(new XAttribute("lfc_nameMustAppearInGraphics", lfc_nameMustAppearInGraphics));
                        xelLayer.Add(new XAttribute("lfc_is_outline", lfcIsOutline? "Y" : "N"));
                }
        }
}
