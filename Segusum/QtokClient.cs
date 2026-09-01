using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using static Seg.StaticUtil;

namespace Seg
{


        public class QtokClient
        {

                public DynamicQtokInfoClient qt_dynamicPart { get; set; }


                public bool qt_requiresArticleForRoomObj { get; set; }
                public bool qt_IsBecause { get; set; }
                public bool qt_IsSoThat { get; set; }

                public string qt_serId;
                public string qt_readableNameHeShe;
                public string qt_readableNameThey;
                //public string qt_readableNameYou;
                public string qt_qualePersona;
                public bool qt_disabledWithChars;
                public bool qt_disabledWithNonChars;
                public bool qt_isStillUnknown { get; set; }

                public string[] qt_failureContinuations = new string[] { };

                public ContinuationKind qt_continuationKind;


                public ContinuationKind qt_continuationKindAfterObject;
                public string[] qt_failureContinuationsAfterObject = new string[] { };

                public QtokClient() // serve per deserializ newton
                {

                }

                public QtokClient(Qtok q, LogicObj lo, XDocIndexed xdocObjects)
                {
                        qt_requiresArticleForRoomObj = q.RequiresArticleForRoomObj;
                        qt_dynamicPart = q.DynamicPart;

                        qt_disabledWithChars = q.DisabledWithCharacters;
                        qt_disabledWithNonChars = q.DisabledWithNonCharacters;

                        qt_qualePersona = q.qualePersona == WhichPerson.HeSheIt ? "heShe" : q.qualePersona == WhichPerson.They ? "they" : throw new NotImplementedException();
                        //if (q.serId == "mikeStallone")
                        //{
                        //        var gfjkgfj = 4;
                        //}

                        qt_IsBecause = q.IsBecause;
                        qt_IsSoThat = q.IsSoThat;

                        qt_serId = q.serId;


                        //setto qt_readableNameHeShe
                        qt_readableNameHeShe = calc(() =>
                     {
                             if (lo != null)
                             {
                                     var dynamicQtNameWithArticle = lo.dynamicNameTranslated(xdocObjects, true, false);
                                     if (dynamicQtNameWithArticle != null)
                                     {
                                             return dynamicQtNameWithArticle; ;
                                     }
                                     else
                                     {
                                             return q.translatedNameHeShe(xdocObjects);
                                     }
                             }
                             else
                             {
                                     return q.translatedNameHeShe(xdocObjects);
                             }
                     });

                        qt_readableNameThey = q.translatedNameThey(xdocObjects);
                        //qt_readableNameYou = q.translatedNameYou(xdocObjects);
                        qt_continuationKind = q.failureContinuationKind;
                        qt_continuationKindAfterObject = q.failureContinuationKindAfterObject;

                        qt_isStillUnknown = false; // !isCurrentlyKnownToPlayer(q);

                        qt_failureContinuations = q.continuations // ntoare che non sono filtrate con l'obiettivo...
                                .Select(qco => qco.serId).ToArray();


                        qt_failureContinuationsAfterObject = q.continuationsAfterObject
                                .Select(qco => qco.serId).ToArray();

                        if (qt_continuationKind == ContinuationKind.EnumeratedObject && qt_failureContinuations.isEmpty())
                        {

                                throw new Exception($"the token   \"{qt_serId}\"    has an enumerated object as continuation. So the list of continuations cannot be emtpy. You need to initialize it.");
                        }

                        if (qt_continuationKindAfterObject == ContinuationKind.EnumeratedObject && qt_failureContinuationsAfterObject.isEmpty())
                        {
                                throw new Exception($"the token   \"{qt_serId}\"     has an enumerated object as continuation after the object. So the list of continuations after the object cannot be emtpy. You need to initialize it.");
                        }
                }

                //public QtokClient(string serId, string readableName, QtokClient [] failureContinuations, ContinuationKind continuationKind)
                //{
                //        qt_serId = serId ?? throw new ArgumentNullException(nameof(serId));
                //        qt_readableName = readableName ?? throw new ArgumentNullException(nameof(readableName));
                //        qt_failureContinuations = failureContinuations ?? throw new ArgumentNullException(nameof(failureContinuations));
                //        qt_continuationKind = continuationKind;


                //        if (continuationKind == ContinuationKind.EnumeratedObject && failureContinuations.isEmpty())
                //        {
                //                throw new Exception($"If the token {serId} has an enumerated object as continuation, the list of continuation cannot be emtpy. You need to initialize it.");
                //        }
                //}

                public override string ToString()
                {
                        return qt_serId;
                }
        }
}
