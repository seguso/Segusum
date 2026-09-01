using System.Xml.Linq;
using System.Linq;

namespace Seg
{
        public class NarToken : CutSceneToken
        {
                public string ntPar;

                public NarSize ntSize;

                public LayerForClient[] ntLayers;


                /// <summary>
                /// i token che dicono "arrivi qui" non voglio che siano ultimi nella cutscene, perché subito dopo vedi comunque la room. 
                /// </summary>
                public bool removeIfLast;

                internal void serialize(XElement xelParent){

                        var xelNar = new XElement("cutSceneToken", new XAttribute("type", "nar"));
                        xelParent.Add(xelNar);

                        if (this.img != null)
                        {
                                xelNar.Add(new XAttribute("img", img));
                        }

                        xelNar.Add(new XAttribute("par", ntPar));

                        xelNar.Add(new XAttribute("size", (int)ntSize));


                        xelNar.Add(new XAttribute("removeIfLast", removeIfLast? "1" : "0"));


                        foreach (var la in ntLayers)
                        {
                                la.serialize(xelNar);
                        }


                }

                internal static NarToken deserialize(XElement xelTok, bool cutsceneCanBeSkipped, bool canGoBackToPrev)
                {
                        var par = xelTok.Attribute("par").Value;

                        var img = xelTok.Attribute("img")?.Value; // può non esserci nel nar

                        var size = xelTok.Attribute("size")?.Value??"0"; // può non esserci nel nar

                        var sizei = (NarSize)(int.Parse(size));



                        var ntLayers = xelTok.Elements("layer").Select(elLa => LayerForClient.deserialize(elLa)).ToArray();


                        var removeIfLast = xelTok.Attribute("removeIfLast").Value == "1";

                        return new NarToken(
                                                          canBeSkipped: cutsceneCanBeSkipped,
                                                          img: img,
                                                          par: par,
                                                          canGoBackToPrev: canGoBackToPrev,
                                                          ntLayers: ntLayers,
                                                          removeIfLast: removeIfLast
                                                          , size: sizei) ;
                }

                public NarToken(bool canBeSkipped, string img, string par, bool canGoBackToPrev, LayerForClient[] ntLayers, bool removeIfLast, NarSize size) : base(canBeSkipped, img, canGoBackToPrev)
                {
                        this.ntPar = par;
                        this.ntLayers = ntLayers;
                        this.removeIfLast = removeIfLast;
                        this.ntSize = size;
                }
        }
}
