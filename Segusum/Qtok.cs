using System.Xml.Linq;
using System.Linq;

namespace Seg
{

        public enum WhichPerson
        {
                HeSheIt, They

        }


        public enum ToWhomBinds
        {
                BindsToClickedObject = 0,
                BindsToFirstObjectInObjective = 1
        }

        public class DynamicQtokInfoClient
        {
                public DynamicQtokInfoClient(ToWhomBinds toWhom, bool withArticle)
                {
                        dqic_ToWhom = toWhom;
                        dqic_WithArticle = withArticle;
                }

                public ToWhomBinds dqic_ToWhom { get; set; }
                public bool dqic_WithArticle { get; set; }

        }


        public class Qtok
        {
                public string serId;

                public int Priority { get; set; } = 5;
                //public string readableName_you;
                public string readableName_heShe;
                public string readableName_they;
                public WhichPerson qualePersona;


                /// Used for "disguise as": you want "disguise as captain", not "disguise as the captain"
                public bool  requiresObjectWithoutDetArticle { get; set; }


                public DynamicQtokInfoClient DynamicPart { get; set; }

                public bool IsBecause { get; set; }
                public bool IsSoThat { get; set; }


                /// <summary>
                /// must be initialized if continuation kind = enumerated
                /// </summary>
                public Qtok[] continuations = new Qtok[] { };
                public ContinuationKind failureContinuationKind;

                public bool ExcludeFromChiusure { get; set; }
                public bool DisabledWithCharacters { get; set; }
                public bool DisabledWithNonCharacters { get; set; }

                public ContinuationKind failureContinuationKindAfterObject;
                public Qtok[] continuationsAfterObject = new Qtok[] { };

                public Qtok[] toArr()
                {
                        return new Qtok[] { this };
                }

                public string translatedNameHeShe(XDocIndexed xdocObjects)
                {

                        if (xdocObjects == null)
                        {
                                return readableName_heShe;
                        }


                        string nameTransl;
                        //var xmlPath = WorldBase.getPathXmlTranslationObjs(curLang);
                        //var xdoc = XDocument.Load(xmlPath);
                        if (xdocObjects.qtokOfSerId.ContainsKey(serId))
                        {
                                var el = xdocObjects.qtokOfSerId[serId]; // Root?.Elements("qtok").Where(lel => lel.Attribute("ser_id")?.Value == this.serId).FirstOrDefault();
                                if (el != null && el.Attribute("transl_heShe")?.Value != "+")
                                {
                                        nameTransl = el.Attribute("transl_heShe")?.Value.Replace("''", "\"");

                                }
                                else
                                {
                                        nameTransl = readableName_heShe;
                                }
                        }
                        else
                        {
                                nameTransl = readableName_heShe;
                        }

                        return nameTransl;
                }
                public string translatedNameThey(XDocIndexed xdocObjects)
                {

                        if (xdocObjects == null)
                        {
                                return readableName_they;
                        }


                        string nameTransl;
                        //var xmlPath = WorldBase.getPathXmlTranslationObjs(curLang);
                        //var xdoc = XDocument.Load(xmlPath);
                        if (xdocObjects.qtokOfSerId.ContainsKey(serId))
                        {
                                var el = xdocObjects.qtokOfSerId[serId]; // Root?.Elements("qtok").Where(lel => lel.Attribute("ser_id")?.Value == this.serId).FirstOrDefault();
                                if (el != null && el.Attribute("transl_they")?.Value != "+")
                                {
                                        nameTransl = el.Attribute("transl_they")?.Value.Replace("''", "\"");

                                }
                                else
                                {
                                        nameTransl = translatedNameHeShe(xdocObjects) /* e non l'originale!*/;
                                }
                        }
                        else
                        {
                                nameTransl = translatedNameHeShe(xdocObjects) /* e non l'originale!*/;
                        }

                        return nameTransl;
                }

                //public string translatedNameYou(XDocIndexed xdocObjects)
                //{

                //        if (xdocObjects == null)
                //        {
                //                return readableName_you;
                //        }


                //        string nameTransl;
                //        //var xmlPath = WorldBase.getPathXmlTranslationObjs(curLang);
                //        //var xdoc = XDocument.Load(xmlPath);
                //        if (xdocObjects.qtokOfSerId.ContainsKey(serId))
                //        {
                //                var el = xdocObjects.qtokOfSerId[serId]; // Root?.Elements("qtok").Where(lel => lel.Attribute("ser_id")?.Value == this.serId).FirstOrDefault();
                //                if (el != null && el.Attribute("transl_you")?.Value != "+")
                //                {
                //                        nameTransl = el.Attribute("transl_you")?.Value.Replace("''", "\"");

                //                }
                //                else
                //                {
                //                        nameTransl = translatedNameThey(xdocObjects) /* e non la lingua originale*/;
                //                }
                //        }
                //        else
                //        {
                //                nameTransl = translatedNameThey(xdocObjects) /* e non la lingua originale*/;
                //        }

                //        return nameTransl;
                //}

                public bool RequiresArticleForRoomObj { get; set; }

                public Qtok(string serId, string readableName = null, string readableNameHeShe = null, string readableNameThey = null, ContinuationKind continuationKind = ContinuationKind.EnumeratedObject, ContinuationKind continuationKindAfterObject = ContinuationKind.EndsSentence, bool isBecause = false, bool isSoThat = false, WhichPerson whichPerson = WhichPerson.HeSheIt, DynamicQtokInfoClient dynamicPart = null, bool excludeFromChiusure = false, bool disabledWithChars = false, bool disabledWithNonChars = false, bool requiresArticleForRoomObj = false, int? priority = null, bool requiresObjectWithoutDetArticle = false)
                {
                        DisabledWithCharacters = disabledWithChars;
                        DisabledWithNonCharacters = disabledWithNonChars;
                        DynamicPart = dynamicPart;
                        this.requiresObjectWithoutDetArticle = requiresObjectWithoutDetArticle;

                        if (priority != null) {
                                Priority = priority.Value;
                        }
                        RequiresArticleForRoomObj = requiresArticleForRoomObj;

                        this.ExcludeFromChiusure = excludeFromChiusure;

                        this.qualePersona = whichPerson;
                        if (readableName.is_not_null_or_white() && readableNameHeShe.is_not_null_or_white())
                        {
                                throw new System.Exception($"You need to initialize either readableName or readableNameHeShe. {serId}");
                        }

                        if (serId == null)
                        {
                                throw new System.Exception("serid null");
                        }

                        this.serId = serId;
                        if (readableName.is_not_null_or_white())
                        {
                                readableName_heShe = readableName;
                                readableName_they = readableName;
                                //readableName_you = readableName;
                        }
                        else
                        {
                                this.readableName_heShe = readableNameHeShe;
                                this.readableName_they = readableNameThey ?? readableNameHeShe;
                                //readableName_you = readableNameYou ?? readableNameThey ?? readableNameHeShe;
                        }

                        this.failureContinuationKind = continuationKind;
                        this.failureContinuationKindAfterObject = continuationKindAfterObject;
                        this.IsBecause = isBecause;
                        this.IsSoThat = isSoThat;
                }

                public override string ToString()
                {
                        return serId;
                }
        }
}
