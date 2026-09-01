
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;


// ReSharper disable ReplaceWithSingleCallToFirstOrDefault

namespace Seg
{

        public enum PossessiveAgreement
        {
                MasculineSingular,
                FeminineSingular,
                MasculinePlural,
                FemininePlural
        }

        //public enum namePurpose
        //{
        //    invOrFloor,
        //    verbMenuOrUiCommand
        //}
        //public enum executeActionRes
        //{
        //    NotHandled,
        //    HandledRestoreScroll,
        //    HandledScrollToTop,
        //}


        //    public enum LogicObjType
        //{
        //    ObjectNormal = 0, // è un oggetto, non un verbo
        //    //VerbNormal = 1, // è un verbo normale, che chiede un obiettivo e non è pinned.
        //    //VerbQuick = 2, // è un verbo rapido . cioè take, o exit-through. non chiede obiettivo, ed è pinned on top.
        //    ObjectObviousExit = 1,  // un'uscita. ad esempio una porta, o un sentiero. quando la clicchi, si illumina il verbo exit-through
        //    //VerbWalkTo = 4, // il verbo exit-through. deve illuminarsi quando clicchi un'uscita

        //}


                public enum GenderNumber
        {
                He, She, It, They

        }

        public enum UseKindForRoomObjects
        {
                UseHere, UseFor, Deduce, Nothing
        }

        //public enum HoverActionWhenInRoom
        //{
        //        //LookAndWorkAsTarget,  // non si capisce perche alcuni ce l'hanno e altri no. inoltre, look c'e' solo se stai investigando, quindi e' bene che sia premuto esplicitamente

                
                        
        //                 ShowMap, Nothing
                        
                        
        //}

        public enum HoverActionWhenInInv
        {
                UseWith = 0
                        , UseHere = 1
                        , UseFor = 2
                        //, IsActually = 3
        }

        public class ManualCoords
        {
                public ManualCoords(double x0, double y0, double x1, double y1)
                {
                        this.x0 = x0;
                        this.y0 = y0;
                        this.x1 = x1;
                        this.y1 = y1;
                }

                public double x0 { get; set; }
                public double y0 { get; set; }
                public double x1 { get; set; }

                
                
                public double y1 { get; set; }
        }


        public class LogicObj : Mentionable
        {
                public string inTheHandOf;


                public string shortNameWithDet { get; set; }


                public NarSize? customNarSizeForDialog;

                public int orderForTextMode { get; set; } = 5;

                public bool canBeUsedAsTargetInTextMode = true;

                public int HotspotPriority { get; set; } = 10;

                public bool IsVerbThatRequiresExplanation { get; set; }


                public ManualCoords ManualCoords { get; set; }

                public string CustomInvIcon { get; set; }

                public GenderNumber genderNumber = GenderNumber.It;

                public bool onlyInGraphics = false;


                public Explanation[] CustomExplanations;
                public string CustomExplanationsIntro { get; set; }
                public string CustomExplanationsFailureTemplate { get; set; }


                //public bool wasLookedManually { get; set; }

                /// <summary>
                /// serve solo al generatore di combinazioni per evidenziare se non hai gestito usa con.
                /// </summary>
                public bool IsPickableHint = false;

                public bool IsConcept = false;

                public bool IsConversationTopic = false;

                

                public bool IsExit { get; set; }

                //public bool AsksForExplanation { get; set; }

                private Aspect aspect;

                private AlternatePosition alternatePos;

                public HoverActionWhenInInv HoverActionWhenInInv = HoverActionWhenInInv.UseWith;


                public string VerbWhenUseHere { get; set; }

                /// <summary>
                /// if you want to override "use with" and show something else (e.g "talk about"), before you hover the second object, use this.
                /// </summary>
                public string VerbWhenUseWithAsFirstObjectOnHoverNotSelected{ get; set; }

                /// <summary>
                /// example : "talk about {1}"
                /// </summary>
                public string VerbWhenUseWithAsFirstObjectSelectedWithPlaceHolder { get; set; }

                public string VerbWhenUseWithAsFirstObjectSelectedWithPlaceHolderOnHoverSecond { get; set; }

                // Required only for templates containing {targetPossessive}.
                // The agreement describes the possessed noun, independently of
                // any particular language.
                public PossessiveAgreement? TargetPossessiveAgreement { get; set; }

                public string VerbWhenUseForInDialogIntro { get; set; }

                public string VerbWhenUseForOnHover { get; set; }

                //public HoverActionWhenInRoom HoverActionWhenInRoom = HoverActionWhenInRoom.Nothing; // pickup è un puzzle, deduce è troppo strano perchè raro che funzioni. look c'e' solo se investighi, ed e' quindi bene che sia esplicito.

                public UseKindForRoomObjects UseKindWhenInRoom { get; set; } = UseKindForRoomObjects.Nothing; // per default gli oggetti non hanno ne' use for ne' use here ne' deduce nel context menu.
                

                //public Qtok qtok()
                //{
                //        return associatedQToks.First();
                //}


                //public Qtok[] associatedQToks = new Qtok[] { };

                //public bool cannotBeUsed = false;
                //public bool nameMustAppearInGraphics = true;
                //public bool makesSenseToPickItUp = false;


                //public Qtok[] failureContinuations = new Qtok[] { };


                internal string dynamicNameTranslated(XDocIndexed xdocObjects, bool withThe, bool isForDialog)
                {
                        //if (this.loId == "exitDraculaHallVersoCustode")
                        //{
                        //        var y = 4;
                        //}
                        var dyn = wo.dynamicObjectName(this, withThe, isForDialog);
                        if (dyn == null)
                        {
                                
                                // vecchio commento: se era chiesto con articolo, devo tornare null, così il client prende dal qt.
                                if (withThe)
                                {
                                        if (shortNameWithDet.is_not_null_or_white())
                                        {
                                                return wo.translateDialogOrNarOrAnnotated(shortNameWithDet, xdocObjects);
                                        }
                                        else
                                        {
                                                // fallback a nome senza articolo
                                                return wo.translateDialogOrNarOrAnnotated(this.name, xdocObjects);
                                        }
                                }
                                else
                                {
                                        var tra = wo.translateDialogOrNarOrAnnotatedAux(this.name, xdocObjects, out bool? found);

                                        if (found != true)
                                        {
                                                return  translatedName(xdocObjects, out bool? _found);
                                        }
                                        else
                                        {
                                                return tra;
                                        }

                                        //string nameTransl = translatedName(xdocObjects, out bool? found);

                                        //if (found == false)
                                        //{
                                        //        //fallback to file dialoghi
                                        //        return wo.translateDialogOrNarOrAnnotated(this.name, xdocObjects);

                                        //}
                                        //else {
                                        //        return nameTransl;
                                        //}
                                }
                        }
                        else
                        {
                                return wo.translateDialogOrNarOrAnnotated(dyn, xdocObjects);
                        }
                }

                internal bool isInInvOfPartyMember(out Character cha)
                {
                        return wo.curParty.Any(ch => ch.inv.Contains(this), out cha);
                }


                public bool isInCurParty()
                {
                        return wo.curParty.Contains(this);
                }

                public string calcImgPortrait()
                {

                        // adesso i portrait non sono nelle room, solo lo sfondo ènella room
                        string def = calcolaDefaultPortraitConsiderandoAspect();
                        return def;

                        ////if (loId == "newsboy")
                        ////{
                        ////        var y = 4;
                        ////}
                        //if (curRoom() == null) // se non è in nessuna stanza, non è possibile avere un portrait...
                        //{
                        //        string def = calcolaDefaultPortraitConsiderandoAspect();
                        //        return def;
                        //}
                        //else
                        //{
                        //        if (roomHasPortraitForMe(curRoom(), out string portraitUrl, out bool? _isReal))
                        //        {
                        //                return portraitUrl;
                        //        }
                        //        else
                        //        {

                        //                string def = calcolaDefaultPortraitConsiderandoAspect();

                        //                return def;
                        //        }


                        //}

                }

                private string calcolaDefaultPortraitConsiderandoAspect()
                {
                        string strAspect = calcolaStrAspect();
                        var portraitUrl = $"{wo.graphicsRootFolderName()}/portraits/{loId}{strAspect}-default-po.png";
                        return portraitUrl;
                }

                //private bool roomHasPortraitForMe(Room ro, out string portraitUrl, out bool? isRealPortrait)
                //{
                //        if (ro.coordFile == null)
                //        {
                //                portraitUrl = null;
                //                isRealPortrait = null;
                //                return false;
                //        }

                //        string strAspect = calcolaStrAspect();

                //        portraitUrl = null;
                //        foreach (var filename in ro.coordFile.Keys)
                //        {

                //                WorldBase.parseFileNameDaCoordFile(filename, out string loId, out string[] aspects, out bool isPortrait);
                //                if (loId == this.loId)
                //                {

                //                        if (isPortrait)
                //                        {
                //                                // trovato un portrait di questo loid in questa room. prevale sull'altro

                //                                portraitUrl = $"{wo.graphicsRootFolderName()}/{ro.assetFolderName}/{loId}{strAspect}-po.png";
                //                                break;
                //                        }


                //                }

                //        };

                //        if (portraitUrl != null)
                //        {
                //                isRealPortrait = true;
                //                return true;
                //        }

                //        // cerca il non portrait


                //        foreach (var filename in ro.coordFile.Keys)
                //        {

                //                WorldBase.parseFileNameDaCoordFile(filename, out string loId, out string[] aspects, out bool isPortrait);
                //                if (loId == this.loId)
                //                {

                //                        if (!isPortrait)
                //                        {

                //                                portraitUrl = $"{wo.graphicsRootFolderName()}/{ro.assetFolderName}/{loId}{strAspect}.png";
                //                                // trovato un png ma non portrait , ma che serve per stare nella room
                //                                break;
                //                        }
                //                }

                //        };
                //        if (portraitUrl != null)
                //        {
                //                isRealPortrait = false;
                //                return true;
                //        }

                //        portraitUrl = null;
                //        isRealPortrait = null;
                //        return false;
                //}

                private string calcolaStrAspect()
                {
                        string strAspect;
                        if (Aspect != null)
                        {
                                strAspect = $"-{Aspect.serId}";
                        }
                        else
                        {
                                strAspect = "";
                        }

                        return strAspect;
                }

                //private string getPortraitUrlWhenInRoom(Room room)
                //{
                //        string strAspect;
                //        if (aspect != null)
                //        {
                //                strAspect = $"-{aspect.serId}";
                //        }
                //        else
                //        {
                //                strAspect = "";
                //        }

                //        return $"{wo.graphicsRootFolderName()}/{room.assetFolderName}/{loId}{strAspect}-po.png";
                //}

                public string translatedName(XDocIndexed xdocObjects, out bool? found)
                {

                        if (wo.CurLang == null)
                        {
                                found = null;
                                return name;
                        }


                        string nameTransl;
                        //var xmlPath = WorldBase.getPathXmlTranslationObjs(wo.curLang);
                        //var xdoc = XDocument.Load(xmlPath);
                        if (xdocObjects.loOfLoId.ContainsKey(loId))
                        {
                                var el = xdocObjects.loOfLoId[loId]; // Root?.Elements("logic_obj").Where(lel => lel.Attribute("lo_id")?.Value == this.loId).FirstOrDefault();
                                if (el != null && el.Attribute("transl")?.Value != "+")
                                {
                                        nameTransl = el.Attribute("transl")?.Value.Replace("''", "\"");
                                        found = true;
                                }
                                else
                                {
                                        nameTransl = name;
                                        found = false;
                                }
                        }
                        else
                        {
                                nameTransl = name;
                                found = false;
                        }

                        return nameTransl;
                }
                public string translatedInTheHandOf(XDocIndexed xdoc)
                {
                        if (wo.CurLang == null)
                        {
                                return this.inTheHandOf;
                        }

                        string nameTransl;


                        //var xmlPath = WorldBase.getPathXmlTranslationObjs(wo.curLang);
                        //var xdoc = XDocument.Load(xmlPath);
                        if (xdoc.inTheHandOf_ofLoId.ContainsKey(loId))
                        {
                                var el = xdoc.inTheHandOf_ofLoId[loId]; // Root.Elements("logic_obj_in_the_hand_of").Where(lel => lel.Attribute("lo_id").Value == this.loId).FirstOrDefault();
                                if (el != null && el.Attribute("transl").Value != "+")
                                {
                                        nameTransl = el.Attribute("transl").Value.Replace("''", "\"");

                                }
                                else
                                {
                                        nameTransl = this.inTheHandOf;
                                }
                        }
                        else
                        {
                                nameTransl = this.inTheHandOf;
                        }

                        return nameTransl;
                }


                //public bool canBeSelected = true;


                public Room curRoom()
                {
                        if (isInInvOfPartyMember(out Character ch))
                        {
                                return ch.curRoom();
                        }
                        else
                        {
                                return roomWithThisObjOnFloor;
                        }
                }

                /// <summary>
                /// serve per due scopi: 1) quando serializzo. non posso usare puntatori, quindi devo avere degli id stringa. 2) quando parlo con il client. lì non posso passare puntatori.
                /// </summary>
                public string loId;



                //public UseMode useWith = UseMode.UseFor;

                /// <summary>
                /// nell'inventario mostro anche gli oggetti movable che hai visto. quindi devo tenere traccia di cosa hai visto
                /// </summary>
                public bool isSeen = false;




                /// <summary>
                /// un oggetto può essere anche un verbo. operativamente non c'è differenza.
                /// </summary>
                //public bool isObviousExit = false;


                // dove si trova questo oggetto? ci sono 3 possibilità:
                //public container containerWithThisObj; // o è dentro un altro ogg
                internal Room roomWithThisObjOnFloor; // o è poggiato sul pavimento... // attenzione . meglio usare la funzione roomWithThisObjIndirectly.

                internal Character charWithThisObj; // o è nell'inv di qualcuno


                //public roomE roomWithThisObjIndirectly()
                //{
                //    if (roomWithThisObjOnTheFloor != null)
                //    {
                //        return roomWithThisObjOnTheFloor;
                //    }
                //    //else if (containerWithThisObj != null)
                //    //{
                //    //    return containerWithThisObj.lo.roomWithThisObjIndirectly();
                //    //}
                //    else if (charWithThisObj != null)
                //    {
                //        return charWithThisObj.roomWithThisObjIndirectly();
                //    }
                //    else
                //    {
                //        throw new NotImplementedException();
                //    }
                //}



                // il logicobj deve sapere in quale mondo si trova, ad esempio per sapere quale personaggio è attivo, per modificare le proprie descrizioni in base a chi lo guarda
                internal WorldBase wo;


                /// <summary>
                /// called by engine automatically
                /// </summary>
                /// <param name="el"></param>
                /// <param name="saveGaveInvalid"></param>
                internal virtual void deserialize(XElement el, out bool saveGaveInvalid)
                {
                        var roomId = el.Attribute("roomWithThisObjOnTheFloor")?.Value;
                        var charId = el.Attribute("charWithThisObj")?.Value;


                        var aspectSerId = el.Attribute("aspect")?.Value;

                        var alternatePositionSerId = el.Attribute("alternatePos")?.Value;

                        if (aspectSerId.is_not_null_or_white())
                        {
                                this.Aspect = wo.allAspects.Where(a => a.serId == aspectSerId).SingleOrDefault(); // default cioe' null succede solo se ho cancellato un aspect
                        }

                        if (alternatePositionSerId.is_not_null_or_white())
                        {
                                this.AlternatePos = wo.allAlternatePositions.Where(p => p.serId == alternatePositionSerId).SingleOrDefault();
                        }

                        //var type = int.Parse(el.Attribute("type")?.Value);
                        //logicObjType = (LogicObjType)type;

                        //var containerId = el.Attribute("containerWithThisObj")?.Value;

                        //Debug.Assert(roomId != null || charId != null || containerId != null); // per la torre ad esempio non è vero

                        if (roomId != null)
                        {
                                if (!wo.roomOfId.ContainsKey(roomId))
                                {
                                        saveGaveInvalid = true; // nel savegame c'è una stanza che non è più nel mondo o ha cambiato serid.
                                        return;
                                }
                                roomWithThisObjOnFloor = wo.roomOfId[roomId];
                        }

                        if (charId != null)
                        {
                                charWithThisObj = wo.loOfId[charId] as Character;
                        }


                        isSeen = bool.Parse(el.Attribute("isSeen").Value);

                        //if (el.Attribute("wasLookedManually") != null)
                        //{
                        //        wasLookedManually = bool.Parse(el.Attribute("wasLookedManually").Value);
                        //}
                        //else
                        //{
                        //        wasLookedManually = false;
                        //}



                        //appearsInInvIfSeenButNotPicked = bool.Parse(el.Attribute("appearsInInvIfNotPicked").Value);

                        saveGaveInvalid = false;
                }

                /// <summary>
                /// called by the engine automatically
                /// </summary>
                internal virtual void serialize(XElement xel)
                {




                        xel.Add(new XAttribute("loId", loId));
                        if (Aspect != null)
                        {
                                xel.Add(new XAttribute("aspect", Aspect.serId));
                        }
                        if (AlternatePos != null)
                        {
                                xel.Add(new XAttribute("alternatePos", AlternatePos.serId));
                        }
                        xel.Add(new XAttribute("className", this.GetType().Name));
                        xel.Add(new XAttribute("isSeen", isSeen));
                        //xel.Add(new XAttribute("wasLookedManually", wasLookedManually));
                        //xel.Add(new XAttribute("appearsInInvIfNotPicked", appearsInInvIfSeenButNotPicked));

                        //xel.Add(new XAttribute("isObviousExit", isObviousExit));


                        //// salvo anche assembly e full name, perché se è un oggetto dinamico dovrò istanziarlo, non solo deserializzarlo. questi dai non sono letti dentro deserialize, ma nel 
                        //// codice che istanzia, dentro worldE.
                        //var assembly = GetType().Assembly;
                        //var fullname = GetType().FullName;
                        //xel.Add(new XAttribute("assembly", assembly));
                        //xel.Add(new XAttribute("fullname", fullname));


                        if (charWithThisObj != null)
                        {
                                xel.Add(new XAttribute("charWithThisObj", charWithThisObj.loId));
                        }

                        //if (containerWithThisObj != null)
                        //{
                        //    xel.Add(new XAttribute("containerWithThisObj", containerWithThisObj.containerId));
                        //}

                        if (roomWithThisObjOnFloor != null)
                        {
                                xel.Add(new XAttribute("roomWithThisObjOnTheFloor", roomWithThisObjOnFloor.roomId));
                        }


                        //// se l'oggetto ha dei container, serializzali
                        //foreach (var co in containers)
                        //{
                        //    var coEl = new XElement("container");
                        //    xel.Add(coEl);
                        //    coEl.Add(new XAttribute("containerId", co.containerId));

                        //    co.serialize(coEl);
                        //}


                }






                /// <summary>
                /// called by the engine. except for dynamic objects, where the object constructor must call this. (eg. empty glass, lemon)
                /// </summary>
                /// <param name="w"></param>
                internal void registerInWorld(WorldBase w)
                {
                        this.wo = w;


                        // questa limitazione aveva senso quando avevo usa e raccogli fusi in un solo verbo.
                        //if (useWith == UseMode.UseFor && makesSenseToPickItUp)
                        //{
                        //        throw new Exception($"A LogicObj cannot have at the same time makesSenseToPickItUp and UseWith = UseFor. obj = {this.loId}");
                        //}


                        if (w.loOfId.ContainsKey(this.loId)) // se fallisce, forse ho dato due id allo stesso oggetto. o ho sdoppiato l'oggetto senza cambiare id.
                        {
                                throw new Exception($"You gave the same loId to more than one object: {loId}");
                        }
                        w.loOfId[this.loId] = this;

                }


                //public Func<genre> getGenre;

                /// <summary>
                /// use this to remove the object from a room without putting it in another room.
                /// </summary>
                public void removeFromWorld()
                {


                        if (charWithThisObj != null)
                        {
                                charWithThisObj.inv.Remove(this);
                                charWithThisObj = null;
                        }

                        if (roomWithThisObjOnFloor != null)
                        {
                                roomWithThisObjOnFloor.objectsInRoom.Remove(this);
                                roomWithThisObjOnFloor = null;
                        }

                        //if (containerWithThisObj != null)
                        //{
                        //    containerWithThisObj.content.Remove(this);
                        //    containerWithThisObj = null;
                        //}


                }


                public bool isHere()
                {
                        return wo.curRoom == this.roomWithThisObjOnFloor;
                }


                //public void addDescriptionOfContainerOrCharHolding(List<parHtmlServer> l)
                //{


                //    if (charWithThisObj != null)
                //    {

                //        if (wo.ac.relationWith(charWithThisObj) == relationBetweenChars.formal)
                //            l.Add( charWithThisObj.descObjectHeldPlacehFormal.inst(subjDet).topar());
                //        else if (wo.ac.relationWith(charWithThisObj) == relationBetweenChars.informal)
                //            l.Add(charWithThisObj.descObjectHeldPlacehInformal.inst(subjDet).topar());
                //        else
                //            l.Add(charWithThisObj.descObjectHeldPlacehYou.inst(subjDet).topar());

                //    }

                //    if (containerWithThisObj != null)
                //    {
                //        l.Add(containerWithThisObj.contentStrFocusOnContentPlaceh.inst(subjDet).topar());
                //    }


                //}


                //public virtual bool putHasPrepositionIn { get { return false; } }

                //public void addUnaryHandler(unaryVerb uv, Action<unaryHandlerInput> handler)
                //{
                //    Debug.Assert(!unaryHandlers.ContainsKey(uv));
                //    unaryHandlers[uv] = handler;
                //}



                //public Dictionary<unaryVerb, Action<unaryHandlerInput>> unaryHandlers = new Dictionary<unaryVerb, Action<unaryHandlerInput>>();

                //public Dictionary<objective, Action<unaryHandlerInput>> binaryHandlers = new Dictionary<objective, Action<unaryHandlerInput>>();






                /// <summary>
                /// in questa stanza c'è LA PORTA.
                /// </summary>
                public string name { get; set; }
                public Aspect Aspect { get => aspect; set => aspect = value; }
                public AlternatePosition AlternatePos { get => alternatePos; set => alternatePos = value; }


                ///// <summary>
                ///// in questa stanza c'è UN GENTILUOMO DAI CAPELLI SCURI. Pochi oggetti hanno questa descrizione con articolo indet. NOn è 
                ///// obbligatorio fornirla. la devi fornire per gli oggetti che potrebbero essere chiamati con articolo indet. Ad esempio: un secchio, una bottiglia.
                ///// </summary>
                //public virtual string subjInd()
                //{
                //    return subjDet;
                //}









                //public bool isInRoomIndirectly(roomE r)
                //{

                //    // da qualche parte devi essere.
                //    Debug.Assert(isSomewhere());

                //    return (roomWithThisObjIndirectly() == r);


                //    //if (roomWithThisObj == r)
                //    //{
                //    //    return true;
                //    //}

                //    //if (roomWithThisObj != null && roomWithThisObj.parentRoom == r)
                //    //{
                //    //    return true;
                //    //}

                //    //if (charWithThisObj != null && charWithThisObj.roomWithThisObj == r)
                //    //{
                //    //    return true;
                //    //}

                //    //if (containerWithThisObj != null)
                //    //{
                //    //    return containerWithThisObj.lo.isInRoomIndirectly(r);

                //    //}

                //    ////if (containerSingleWithThisObj != null)
                //    ////{
                //    ////    return containerSingleWithThisObj.lo.isInRoomIndirectly(r);

                //    ////}




                //    //return false;
                //}







                //public abstract string nameReadableUi { get; }



                //public bool isInSomebodysInv()
                //{
                //    return eng.gameLogic.allChars().Any(cha => cha.inv.Contains(this));
                //}

                //protected logicObj()
                //{
                //    string iname;
                //    //if (instanceName == null)
                //        iname = GetType().Name;
                //    //else
                //    //{
                //    //    iname = instanceName;
                //    //}

                //    if (iname.Contains("wet"))
                //    {
                //        var y = 4;
                //    }
                //    // registrati, così l'ipertesto può usare te.
                //    Debug.Assert(!eng.objOfName.ContainsKey(iname)); // mi impedisco di definire due classi con stesso nome ma diverso namespace.
                //    eng.objOfName[iname] = this;
                //}

                //public string nameHyper
                //{
                //    get { return  "[" + nameReadable.TrimEnd() + "|" +  GetType().Name + "]"; }
                //}



                //public virtual verb[] verbs
                //{
                //    get
                //    {
                //        return new verb[] { lookAt.i };
                //    }
                //}


                //public enum verbHandled
                //{
                //    yes,
                //    no
                //}



                //public bool isSomewhere()
                //{
                //    return //containerWithThisObj != null || 
                //        roomWithThisObjOnTheFloor != null || charWithThisObj != null;
                //}


                public bool isIn(Room r)
                {
                        return roomWithThisObjOnFloor == r;
                }

                public bool is_in_world()
                {
                        return roomWithThisObjOnFloor != null || charWithThisObj != null;
                }

                public void putInRoom(Room r)
                {
                        //if ( !isObviousExit) // questi oggetti spesso si mettono in più stanze, per fare prima. ma non devo toglierli dalla stanza precedente.
                        {

                                removeFromWorld();
                        }

                        r.objectsInRoom.Add(this);
                        roomWithThisObjOnFloor = r;
                }

                //public void putInRoomWithoutRemoving(Room r)
                //{
                //    //removeFromWorld();
                //    r.objectsInRoom.Add(this);
                //    roomWithThisObjOnTheFloor = r;
                //}



                //public abstract Func<unaryVerb, Task<verbHandled>> executeUnaryAction();

                //public virtual  Task<verbHandled> executeBinaryAction(binaryVerb bv, logicObj o2)
                //{
                //    return verbHandled.no;
                //}


                //public List<verb> computeVerbs()
                //{

                //    var ret = new List<verb>();




                //    foreach (var uv in verbs())
                //    {
                //        ret.MaybeAdd(uv);
                //    }





                //    return ret;

                //}


                public override string ToString()
                {
                        return loId;
                }
        }


}
