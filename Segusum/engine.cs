#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
namespace Seg
{
    /// <summary>
    /// classe mirrored nell'editor
    /// </summary>
    public class SpriteDataJson
    {
        public string SpriteScaledAdjustedFileName { get; set; }
        // XOrig: Adjusted X for client (top-left of unflipped bounding box)
        public double XOrig { get; set; }
        public double YOrig { get; set; } // Y remains the same
        public double Width { get; set; } // Add this line
        public double Height { get; set; } // Add this line
        public string? LogicalObj { get; set; } = null;
        public string? Aspect { get; set; } = null; // Add Aspect property
        public string? PositionName { get; set; } = null;
        public bool IsFlippedHorizontally { get; set; }
        public bool IsOutline { get; set; }
        public int ZIndex { get; set; }
    }
/// <summary>
    /// classe mirrored nell'editor
    /// </summary>
    public class RoomDataEditor
    {
        public List<SpriteDataJson> Sprites { get; set; }
        public List<ColorLayerDataJson> Layers { get; set; }
        public int BackgroundWidth { get; set; }
        public int BackgroundHeight { get; set; }
    }


    /// <summary>
    /// classe mirrored nell'editor
    /// </summary>
    public class ColorLayerDataJson
    {
        public string FileName { get; set; } = string.Empty;
        public string? LogicalObj { get; set; } = null; // New nullable string field

        public double Width { get; set; } // Add this line
        public double Height { get; set; } // Add this line
        public double X { get; set; }
        public double Y { get; set; }
        public int ZIndex { get; set; }


      
    }

    public record LayerInfoParsed(RectSeg rect/*, bool isHiRes*/);

    public record RectSeg(int x, int y, int wt, int ht);

    public static class eng // di motore ce n'è uno solo, non devo avere più istanze. quindi faccio static, così ho una sola
                            // istanza globale implicita, e quindi non ho il 
                            // problema di far arrivare l'istanza alla gamelogic.
    {





        //public static string xIsHoldingY = "[{1}|1] sta portando [{2}|2].".tr();

        //public static string youAreHoldingY = "Stai portando [{1}|1].".tr();

        //public static TaskCompletionSource<bool> engineStaAspettandoCheIlTaskSiAddormenti;

        //public static readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        //public static Dictionary<string, unaryVerb> unaryVerbOfVerbId = new Dictionary<string, unaryVerb>();
        internal static Dictionary<string, Objective> objectiveOfObjectiveId = new Dictionary<string, Objective>();


        //public static Dictionary<string, topic> topicOfTopicId = new Dictionary<string, topic>();

        //public static void nar(string s, List<cut_scene_token> cs, string img = null)
        //{

        //    cs.Add( new nar_token { par = s, img = img});
        //}

        //public static void dial(character c, string s, List<cut_scene_token> cs)
        //{

        //    cs.Add( s.todial(c));
        //}


        internal static ConcurrentDictionary<int, WorldBase> worldOfUser = new ConcurrentDictionary<int, WorldBase>();

        /// <summary>
        /// Vista minima della cache necessaria all'host per il cleanup degli
        /// utenti inattivi. La struttura interna resta privata al motore.
        /// </summary>
        public static IReadOnlyCollection<int> CachedWorldUserIds => worldOfUser.Keys.ToArray();

        public static bool TryRemoveCachedWorld(int userId)
            => worldOfUser.TryRemove(userId, out _);

        internal static ConcurrentDictionary<string, XDocIndexed> xdocIndexedCached = new ConcurrentDictionary<string, XDocIndexed>();






        //public class charHat
        //{
        //    public character ch;
        //    public hat hat;

        //    public override bool Equals(object obj)
        //    {
        //        var o = obj as charHat;
        //        if (o == null)
        //        {
        //            return false;
        //        }

        //        return o.ch == ch && o.hat == hat;
        //    }
        //}





        internal static Random rnd = new Random();

        //public static IGameLogic gameLogic;




        //public static HashSet<logicObj> oggettiMemorizzati = new HashSet<logicObj>();

        // alcuni verbi sono gestiti a livello di motore, perché servono per i cappelli

        //public static room[] rooms { get; set; }




        //public static void insertNarInCutsceneIfNotDuplicate(List<cutSceneToken> cs2, string nar, string idForDuplicates)
        //{
        //    var quanti = cs2.Count(ct => ct.idForDuplicates == idForDuplicates);
        //    if (quanti == 0)
        //        cs2.Add(eng.nar(nar, idForDuplicates: idForDuplicates));
        //    else if (quanti == 1)
        //    {
        //        var first = cs2.First(ct => ct.idForDuplicates == idForDuplicates);
        //        var iFirst = cs2.IndexOf(first);
        //        cs2.Insert(iFirst + 1, eng.nar("Anche altri fanno lo stesso.".tr(), idForDuplicates: idForDuplicates));

        //    }
        //    else
        //    {
        //        // non devo fare niente
        //    }
        //}

        //public static void insertDialogInCutsceneIfNotDuplicate(List<cutSceneToken> cs2, Character ch, string dial, string idForDuplicates)
        //{
        //    var quanti = cs2.Count(ct => ct.idForDuplicates == idForDuplicates);
        //    if (quanti == 0)
        //        cs2.Add(ch.dial(dial, idForDup: idForDuplicates));
        //    else if (quanti == 1)
        //    {
        //        var first = cs2.First(ct => ct.idForDuplicates == idForDuplicates);
        //        var iFirst = cs2.IndexOf(first);
        //        cs2.Insert(iFirst + 1, ch.dial("Anch'io.".tr(), idForDup: idForDuplicates));

        //    }
        //    else
        //    {
        //        // non devo fare niente
        //    }
        //}



        //internal static List<ObjForClient> computeParsOfMindOfActiveChar(WorldBase wo, XDocIndexed xdocObjs)
        //{
        //        // i concetti nella tua mente PIU' gli oggetti in mano agli altri membri del party



        //        var concetti = wo.activeChar.mind.Select(conc => new ObjForClient
        //        (conc, xdocObjs)).ToList();


        //        var allRooms = wo.roomOfId.Values;
        //        var curRoom = wo.curRoom;


        //        var oggettiInManoAdAltriDelParty = wo.curParty.Where(ch => ch != wo.ActiveChar).SelectMany(ch =>
        //        {

        //                var objsOfChar = ch.inv.Select(lo => new ObjForClient(lo, xdocObjs));

        //                return objsOfChar;
        //        });

        //        //var oggettiMovableVisti = allRooms.Where(r => r != curRoom).SelectMany(ro => ro.objectsInRoom.Select(lo => new { room = ro.whatToSayWhenObjectIsInThisRoom, lo })).Where(pair => pair.lo.isSeen 

        //        ////&& pair.lo.appearsInInvIfSeenButNotPicked  


        //        //)
        //        //    .Select(pair => new objForClient
        //        //    {
        //        //        loId = pair.lo.loId,
        //        //        loUiName = $"{pair.lo.name}",
        //        //        loUiNameWithIn = $"{pair.lo.name} ({pair.room})",
        //        //        ofcUseWith = pair.lo.useWith,
        //        //        ofcCanBeSelected = pair.lo.canBeSelected,
        //        //    });






        //        return concetti
        //                .Concat(oggettiInManoAdAltriDelParty)
        //                .ToList();
        //}

        internal static List<ObjForClient> computeParsOfInv(Character ch, XDocIndexed xdocObjs)
        {




            var oggetti = ch.inv.Select(lo => new ObjForClient(lo, xdocObjs));






            return oggetti.ToList();
        }





        //public static List<parHtmlServer> parsOfObjectsWithOuterSentence(List<logicObjE> pickablesToDescribe, string preambleSing, string preamblePlur)
        //{
        //    var ret = new List<parHtmlServer>();

        //    //var pickablesToDescribe = r.pickablesOnFloor().Where(p => except != null &&  !except.Contains(p)).ToList();

        //    if (pickablesToDescribe.Any())
        //    {
        //        if (pickablesToDescribe.Count == 1)
        //        {
        //            var par = pickablesToDescribe.toPar(det: false); // per terra c'è UN secchio


        //            var x = eng.parOfString2(preambleSing, new List<eng.pairParPos> { new eng.pairParPos { par = par, pos = 1 } });


        //            ret.Add(x);

        //            //// ora le cose dentro QUALCHE ogg nel pavimento
        //            //foreach (var o in pickablesToDescribe)
        //            //{
        //            //    makeParCorContentOfContainerIfAny(o, ret);
        //            //}


        //        }
        //        else
        //        {

        //            var par = pickablesToDescribe.toPar(det: false); // per terra c'è UN secchio

        //            var x = eng.parOfString2(preamblePlur, new List<eng.pairParPos> { new eng.pairParPos { par = par, pos = 1 } });


        //            ret.Add(x);

        //            // ora le cose dentro QUALCHE ogg nel pavimento
        //            //foreach (var o in pickablesToDescribe)
        //            //{

        //            //    makeParCorContentOfContainerIfAny(o, ret);

        //            //}

        //        }

        //    }

        //    return ret;
        //}

        //public static void makeParCorContentOfContainerIfAny(logicObjE o, List<parHtmlServer> pars)
        //{


        //    foreach (var cont in o.containers)
        //    {

        //        var content = cont.content;
        //        if (content.Count > 0)
        //        {
        //            var parOggetti = content.toPar(det: false); // nel secchio c'è UNA penna


        //            if (content.Count == 1)
        //            {
        //                var x = eng.parOfString2(cont.contentStrSingPlaceh, new List<eng.pairParPos> { new eng.pairParPos { par = parOggetti, pos = 1 } });
        //                pars.Add(x);

        //            }
        //            else
        //            {
        //                var x = eng.parOfString2(cont.contentStrPlurPlaceh, new List<eng.pairParPos> { new eng.pairParPos { par = parOggetti, pos = 1 } });
        //                pars.Add(x);
        //            }

        //        }
        //    }

        //}

        private const string spacingLeft = "   ";

        //public static List<Paragraph> __parsObjectsOnFloorOrInInv(pickable cp, Paragraph paragrafoDaContinuare = null)
        //{

        //    var ret = new List<Paragraph>();

        //    var r = MakeClickableRun(cp.descrWithArticle);


        //    r.PreviewMouseLeftButtonDown +=  (o, args) =>
        //    {
        //        args.Handled = true;

        //        // potrebbe essere il clic sul secondo oggetto o sul primo.
        //        if (qualcheTaskStaAspettandoClicSecondoOggetto != null)
        //        {
        //            qualcheTaskStaAspettandoClicSecondoOggetto.TrySetResult(cp.lo);
        //        }
        //        else
        //        {
        //             ShowVerbMenuForObject(args, cp.lo);
        //        }
        //    };


        //    Paragraph parItem1;
        //    if (paragrafoDaContinuare != null)
        //        parItem1 = paragrafoDaContinuare;
        //    else
        //    {
        //        parItem1 = new par();
        //        parItem1.Inlines.Add(spacingLeft);
        //    }

        //    parItem1.Inlines.Add(r);


        //    //parItem1.Inlines.Add(".");
        //    var parItem = parItem1;
        //    ret.Add(parItem);


        //    if (cp.lo.asContainerSingle != null)
        //    {


        //        if (cp.lo.asContainerSingle.content != null)
        //        {
        //            var parItem2 = MakeParForContentOfContainer(cp.lo.asContainerSingle);


        //            ret.Add(parItem2);
        //        }
        //    }

        //    return ret;
        //}


        //public class pairLoPos
        //{
        //    public int pos;
        //    public LogicObj lo;
        //}


        //public class pairParPos
        //{
        //    public int pos;
        //    public parHtmlServer par;
        //}


        ///// <summary>
        ///// questa prende una stringa del tipo  "ciao [maurizio|1] come stai [bene|2]"  e la converte in paragrafi cliccabili.
        ///// </summary>
        ///// <param name="para"></param>
        ///// <param name="pairs"></param>
        ///// <returns></returns>
        //public static parHtmlServer parOfString(string para, List<pairLoPos> pairs)
        //{

        //    Debug.Assert(!pairs.Any(p => p.lo == null));
        //    var spl = para.Split(new char[] { '[', '|', ']' });

        //    var i = 0;

        //    var par = new parHtmlServer();



        //    again:

        //    if (i + 2 < spl.Length)
        //    {
        //        var testo = spl[i];
        //        //if (testo != "")
        //        {
        //            var nomeOgg = spl[i + 1];
        //            var posStr = spl[i + 2];
        //            var pos = Int32.Parse(posStr);

        //            par.elements.Add(new simpleText { s = testo });

        //            var lo = pairs.Where(pa => pa.pos == pos).Select(pa => pa.lo).Single();
        //            Debug.Assert(lo != null);
        //            par.elements.Add(new keywordServer { text = nomeOgg, lo = lo });

        //            //var r = MakeClickableRun(nomeOgg);

        //            ////r.Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 155));


        //            //r.PreviewMouseLeftButtonDown +=  (o, args) =>
        //            //{
        //            //    args.Handled = true;
        //            //    var lo = pairs.Where(pa => pa.pos == pos).Select(pa => pa.lo).Single();
        //            //    Debug.Assert(lo != null);
        //            //    if (qualcheTaskStaAspettandoClicSecondoOggetto != null)
        //            //    {
        //            //        qualcheTaskStaAspettandoClicSecondoOggetto.TrySetResult(lo);
        //            //    }
        //            //    else
        //            //    {
        //            //        // era il clic sul primo ogg
        //            //         ShowVerbMenuForObject(args, lo);
        //            //    }
        //            //};


        //            //par.Inlines.Add(testo);
        //            ////        par.Inlines.Add(" ");
        //            //par.Inlines.Add(r);

        //        }
        //        i += 3;
        //        goto again;
        //    }
        //    else
        //    {

        //        //par.Inlines.Add(spl[i]);
        //        par.elements.Add(new simpleText { s = spl[i] });
        //    }

        //    // alla fine di questo algo , se avevo passato una stringa tipo "[wallace|1]", restano due simpletext vuoti, che elimino

        //    par.elements = (from e in par.elements
        //                    where !e.isUseless()
        //                    select e).ToList();

        //    return par;
        //}

        /// <summary>
        /// Questa prende una stringa del tipo  "ciao [1]  come stai [2]" e inserisce al posto dei placeholder dei paragrafi già cliccabili.
        /// </summary>
        /// <param name="para"></param>
        /// <param name="pairs"></param>
        /// <returns></returns>
        //public static parHtmlServer parOfString2(string para, List<pairParPos> pairs)
        //{
        //    var spl = para.Split(new char[] { '[', '|', ']' });

        //    var i = 0;

        //    var parRes = new parHtmlServer();



        //    again:

        //    if (i + 1 < spl.Length)
        //    {
        //        var testo = spl[i];
        //        var posParStr = spl[i + 1];

        //        var posPar = Int32.Parse(posParStr);
        //        var par1 = pairs.Where(pa => pa.pos == posPar).Select(pa => pa.par).Single();

        //        parRes.elements.Add(new simpleText { s = testo });
        //        parRes.elements.AddRange(par1.elements);

        //        //parRes.Inlines.Add(testo);
        //        //parRes.Inlines.AddRange(par1.Inlines.ToList());



        //        i += 2;
        //        goto again;
        //    }
        //    else
        //    {
        //        //parRes.Inlines.Add(spl[i]);
        //        parRes.elements.Add(new simpleText { s = spl[i] });
        //    }
        //    return parRes;
        //}

        //public static void putObjectInContainerMultiple(logicObjE o, container c)
        //{
        //    Debug.Assert(c != null);
        //    Debug.Assert(o != null);

        //    // per i container, anche se sono inamovibili, devi per forza specificare in che stanza sono, altrimenti quando uno ci si siede sopra non è possibile capire in che stanza è.
        //    Debug.Assert(c.lo.isSomewhere());

        //    if (o.containerWithThisObj == c)
        //    {
        //        // ce l'ha già
        //    }
        //    else
        //    {
        //        // se ce l'ha qualcun altro, registra che non l'ha più
        //        o.removeFromWorld();


        //        o.containerWithThisObj = c;
        //        c.content.Add(o);




        //    }
        //}


        /// <summary>
        /// low level: non controlla se l'oggetto è nella tua stessa stanza, se è in mano a qualcun altro, ecc. lo prende e lo toglie da dove era.
        /// </summary>
        /// <param name="c"></param>
        /// <param name="o"></param>


        //public static void notifyNewFactInMind(List<cutSceneToken> cs, LogicObj fact)
        //{
        //    eng.nar("<i>(Adesso hai memorizzato un nuovo fatto: <b>{1}</b>.)</i>".tr().inst(fact.name)), cs);
        //}


        //public static void sitOnChair(characterE charac, chairSofa chair)
        //{
        //    chair.charsSitting.Add(charac);
        //    charac.chairWhereHeIsSitting = chair;

        //}

        //public static void standUpIfSitting(characterE c)
        //{
        //    if (c.chairWhereHeIsSitting != null)
        //    {

        //        c.chairWhereHeIsSitting.charsSitting.Remove(c);
        //        c.chairWhereHeIsSitting = null;
        //    }
        //}

        /// <summary>
        /// chiamata sia quando cambia stanza un giocante, che un NPC ai. gestisce i commenti su oggetti appariscenti e i saluti. 
        /// i saluti devono avvenire sia se si sposta un giocante che se si sposta un npc. quindi questa la devi chiamare 
        /// anche quando sposti un npc.
        /// </summary>
        /// <param name="whoIsMoving"></param>
        /// <param name="roomTarget"></param>
        /// <param name="cutSceneCheDiceCheTiSposti">// prima dei rimproveri, devi dire "arrivi nella stanza x" oppure "tizio arriva nella stanza dove sei tu". altrimenti confonde</param>
        /// <returns></returns>

        //public static bool tiStaiSpostandoAllInternoDiUnaStessaStanza(characterE ch, roomE roomTarget)
        //{
        //    Debug.Assert(ch.roomWithThisObjOnTheFloor != null); // altrimenti questo codice non funge. quindi sta al chiamante non chiamare questo codice se il ch è seduto sul divano

        //    bool tiStaiSpostandoAllInternoDiUnaStessaStanza;
        //    if (roomTarget.parentRoom == ch.roomWithThisObjOnTheFloor)
        //        tiStaiSpostandoAllInternoDiUnaStessaStanza = true;
        //    else if (ch.roomWithThisObjOnTheFloor.parentRoom == roomTarget)
        //        tiStaiSpostandoAllInternoDiUnaStessaStanza = true;
        //    else
        //    {
        //        tiStaiSpostandoAllInternoDiUnaStessaStanza = false;
        //    }
        //    return tiStaiSpostandoAllInternoDiUnaStessaStanza;
        //}


        //public static void addTitleLevel2(List<parHtmlServer> ret, string text)
        //{
        //    ret.Add($"<span class=\"parTitle2Room\">{text}</span>".topar());
        //}

        //public static List<parHtmlServer> buildDescrizioniDeiPersonaggi2(characterE[] charsHere)
        //{
        //    var ret = new List<parHtmlServer>();

        //    if (charsHere.Length == 1)
        //    {
        //        ////se ce n'è uno solo, sei per forza tu

        //        //var singleChar = charsHere.Single();
        //        //Debug.Assert(singleChar == singleChar.wo.ac);
        //        //var ac = singleChar.wo.ac;

        //        //ret.Add("Qui ci sei tu, [{1}|1].".tr(ac.howHeCallsSomeoneElseAsSubject(singleChar, det: true)).topar(singleChar));

        //        //var desc = singleChar.strDescWhatHeIsDoing(soloCoseAppariscenti: true);
        //        //ret.AddRange(desc);

        //    }
        //    else if (charsHere.Length == 2)
        //    {
        //        var charCheNonSeiTu = charsHere.Single(c => c != c.wo.ac);

        //        var ac = charCheNonSeiTu.wo.ac;
        //        var str = "Qui con te c'è [{1}|1].".tr(ac.howHeCallsSomeoneElseAsSubject(charCheNonSeiTu, det: false)).topar(charCheNonSeiTu); // det false: qui c'è UNA signora anziana
        //        ret.Add(str);



        //        var desc = charCheNonSeiTu.strDescWhatHeIsDoing(soloCoseAppariscenti: true);
        //        ret.AddRange(desc);





        //        //if (addYouTooAreHere)
        //        //{
        //        //    // ora tu
        //        //    ret.Add("Ci sei anche tu, [{1}|1].".tr(ac.howHeCallsSomeoneElseAsSubject(ac, det: true)).topar(ac));

        //        //    var desc2 = ac.strDescWhatHeIsDoing(soloCoseAppariscenti: true);
        //        //    ret.AddRange(desc2);
        //        //}

        //    }
        //    else
        //    {

        //        var ac = charsHere.First().wo.ac;

        //        var charsTranneTe = charsHere.Where(c => c != ac).ToList();

        //        var parCharsTranneTe = charsTranneTe.toPar(det: false); // qui ci sono UN gentiluomo dai capelli scuri...

        //        var str = eng.parOfString2("Qui con te ci sono [1].", new List<eng.pairParPos> { new eng.pairParPos { par = parCharsTranneTe, pos = 1 } });


        //        ret.Add(str);


        //        foreach (var ch in charsTranneTe)
        //        {
        //            var desc = ch.strDescWhatHeIsDoing(soloCoseAppariscenti: true);
        //            ret.AddRange(desc);

        //        }

        //        //if (addYouTooAreHere)
        //        //{
        //        //    // ora tu
        //        //    ret.Add("Ci sei anche tu, [{1}|1].".tr(ac.howHeCallsSomeoneElseAsSubject(ac, det: true)).topar(ac));

        //        //    var desc2 = ac.strDescWhatHeIsDoing(soloCoseAppariscenti: true);
        //        //    ret.AddRange(desc2);

        //        //}

        //    }



        //    return ret;
        //}






        //public static void addDescriptionOfCharAndObjectivesToCutScene(worldE w, List<cutSceneToken> cs)
        //{

        //    var objectivesToMention = w.curObjectives.Where(o => o.toBeMentionedWhenSceneStarts).ToList();

        //    string strObiet;
        //    if (objectivesToMention.Count == 1)
        //    {
        //        strObiet = objectivesToMention.First().readableName;
        //    }
        //    else
        //    {
        //        strObiet = objectivesToMention.Select(o => o.readableName).Aggregate((a, b) => a + ", " + b);

        //        var i = strObiet.LastIndexOf(',');
        //        strObiet = strObiet.Remove(i, 1).Insert(i, " " + "e".tr());

        //    }



        //    //var l = Utils.lp;
        //    ////if (w.curParty.Count == 1)
        //    //{
        //    //    l.Add("<i>Adesso controlli <b>{1}</b> ed hai come obiettivi: <b>{2}</b>.</i>".tr().inst(w.ac.name).inst(strObiet).topar());
        //    //}

        //    //else
        //    //{

        //    //    string strChars;
        //    //    strChars = w.curParty.Select(c => c.subjDet).Aggregate((a, b) => a + ", " + b);
        //    //    var i = strChars.LastIndexOf(',');
        //    //    strChars = strChars.Remove(i, 1).Insert(i, " " + "e".tr());


        //    //    l.Add("<i>Adesso controlli <b>{1}</b> ed hai come obiettivi: <b>{2}</b>.</i>".tr().inst(strChars).inst(strObiet).topar());
        //    //}


        //    //l.Add("<i>(Puoi rileggere in ogni momento l'elenco degli obiettivi premendo il pulsante apposito nella parte bassa dello schermo.)</i>".tr().topar());

        //    cs.Add(l.toNarMultiTextOnly());
        //}


        //public static void addDescriptionOfCharAndObjectivesAndEndOfRoom(worldE w, List<parHtmlServer> pars)
        //{

        //    var objectivesToMention = w.curObjectives;

        //    string strObiet;
        //    if (objectivesToMention.Count == 1)
        //    {
        //        strObiet = objectivesToMention.First().readableName;
        //    }
        //    else
        //    {
        //        strObiet = objectivesToMention.Select(o => o.readableName).Aggregate((a, b) => a + ", " + b);

        //        var i = strObiet.LastIndexOf(',');
        //        strObiet = strObiet.Remove(i, 1).Insert(i, " " + "e".tr());

        //    }


        //    pars.Add(" ".topar());
        //    eng.addTitleLevel2(pars, "OBIETTIVI".tr());


        //    //if (w.curParty.Count == 1)
        //    {
        //        pars.Add("Sei {1} ed hai come obiettivi: {2}.".tr().inst(w.ac.name).inst(strObiet).topar());
        //    }
        //    //else
        //    //{

        //    //    string strChars;
        //    //    strChars = w.curParty.Select(c => c.subjDet).Aggregate((a, b) => a + ", " + b);
        //    //    var i = strChars.LastIndexOf(',');
        //    //    strChars = strChars.Remove(i, 1).Insert(i, " " + "e".tr());

        //    //    pars.Add("Controlli {1} ed hai come obiettivi: {2}.".tr().inst(strChars).inst(strObiet).topar());
        //    //}



        //}





        //public static void notifyUserOfNewObjective(List<cutSceneToken> cs, objective ob)
        //{
        //    var l = Utils.lp;
        //    l.Add("<i>Adesso hai un nuovo obiettivo: <b>{1}</b>.</i>".tr().inst(ob.readableName).topar());
        //    //l.Add("<i>(Puoi rileggere in ogni momento l'elenco degli obiettivi premendo il pulsante apposito nella parte bassa dello schermo.)</i>".tr().topar());

        //    cs.Add(l.toNarMultiTextOnly());

        //}



        internal static void increaseTimeAndExecuteAfterActionScript(CutScene cs, WorldBase w, ActionContext actionContext)
        {

            w.cur_time++;






            w.setCurrentCs(cs);
            w.after_action_executed(cs, actionContext);



            // ora lo scopo di questo ciclo è che se la scritta "arrivi a..."  è ultima nella cutscene, la devo togliere, non solo la scritta, ma proprio
            // togliere il cutscene token. perché
            // tanto subito dopo c'è la grafica che è identica. ma se è in mezzo, non la devo togliere il cutscenetoken.
            while (!cs.isEmpty() && cs.Last() is NarToken nar && nar.removeIfLast)
            {

                var last = cs.Last();
                var removed = cs.Remove(last);
                Debug.Assert(removed);
            }

            w.clearCurrentCs();


        }




        internal static SegActionRes calcolaActionResTalkORoom(WorldBase w, GameStateShowingQuestions gstqDaAttivareDopoLaCutScene, GameStateWaitingForText gstDaAttivareDopo, GameStateFinished gameStateFinished, string[] savenames, bool isTextMode)
        {
            SegActionRes ret;

            if (gstqDaAttivareDopoLaCutScene != null)
            {

                var dialog = gstqDaAttivareDopoLaCutScene.dialog;
                ret = startDialogOrAskFirstQuestion(w, dialog, savenames, isTextMode);


            }
            else if (gstDaAttivareDopo != null)
            {


                ret = startTextInput(w, gstDaAttivareDopo.textInput);


            }
            else if (gameStateFinished != null)
            {
                w.gs = new GameStateFinished();
                var untr = w.getEndGameData();

                EndGameStuffClient tr = traduciEndGameStuff(w, untr);

                ret = new SegActionRes(w.cur_time)
                {
                    arEndGame = tr
                        ,
                    room = w.getRoomDescForClient(savenames, isTextMode) // per i salvataggi a gioco finioto
                };


            }
            else
            {
                GetRoomRes roomDesc = creaRoomDaDareAlClient(w, savenames, isTextMode);

                w.gs = new GameStateViewingRoom();



                ret = new SegActionRes(w.cur_time)
                {
                    room = roomDesc,
                };

            }

            return ret;
        }

        public static GetRoomRes creaRoomDaDareAlClient(WorldBase w, string[] savenames, bool isTextMode)
        {
            w.beforeRoomChangeManualAndAutoSetRoomAspects(w.curRoom); // risetto gli aspect prima di darli al client. necessario se no restano gli aspect dell'ultimo dialogo

            var roomDesc = w.getRoomDescForClient(savenames, isTextMode);
            return roomDesc;
        }

        private static TextInputClient textInputClientOfTextInput(WorldBase w, TextInput ti)
        {
            var xdi = w.getXdocObjIndexedCached();

            var tic = new TextInputClient(serId: ti.serId

                    , shortTitle: w.translateDialogOrNarOrAnnotated(ti.tiShortTitle, xdi)
                    , introBeforeTextbox: w.translateDialogOrNarOrAnnotated(ti.tiIntroBeforeTextbox, xdi)


                    );
            if (ti.tiIntroBeforeSecondTextbox != null)
            {
                tic.tiIntroBeforeSecondTextbox = w.translateDialogOrNarOrAnnotated(ti.tiIntroBeforeSecondTextbox, xdi);
            }
            if (ti.tiCorrectExplanation != null)
            {
                tic.tiCorrectExplanation = new ExplanationClient(ti.tiCorrectExplanation.expId

                        , w.translateDialogOrNarOrAnnotated(ti.tiCorrectExplanation.exName, xdi));

            }

            if (ti.tiVisibleExplanations != null)
            {
                tic.tiVisibleExplanations = ti.tiVisibleExplanations

                        .Where(ex => w.explanationIsVisibleForTextInput(ti, ex))
                        .Select(ex =>
                        new ExplanationClient(ex.expId, w.translateDialogOrNarOrAnnotated(ex.exName, xdi))).ToArray();
            }

            if (ti.tiPreamboloExplanation != null)
            {
                tic.tiPreamboloExplanation = w.translateDialogOrNarOrAnnotated(ti.tiPreamboloExplanation, xdi);
            }

            return tic;
        }

        internal static SegActionRes startTextInput(WorldBase w, TextInput ti)
        {
            SegActionRes ret;







            // finita la cut scene, entro in questo dialogo

            w.gs = new GameStateWaitingForText(ti);

            var tic = textInputClientOfTextInput(w, ti);


            ret = new SegActionRes(w.cur_time)
            {
                textInputRes = tic

            };

            return ret;
        }

        internal static SegActionRes startDialogOrAskFirstQuestion(WorldBase w, Dialog dialog, string[] saveNames, bool isTextMode)
        {
            SegActionRes ret;



            // finita la cut scene, entro in questo dialogo
            w.gs = new GameStateShowingQuestions
            {

                dialog = dialog,

            };




            var questionOfId = new Dictionary<string, Question>();
            foreach (var q in dialog.questions)
            {
                questionOfId.Add(q.id, q);
            }


            // devo calcolare quali domande sono visibili in questo momento, DATO quali domande sono state già chieste.
            // una domanda è visibile ora se tutte le sue dipedenze sono state lette.

            var visible = dialog.questions.Where(q => (!q.asked || dialog.askedQuestionsAreVisible) && (q.visibleIfReadAllOf.All(depId =>
          {
              var de = questionOfId[depId];
              return de.asked;
          })
            )
            ).Select(q => q.id).to_hashset();



            var hiddenObsolete = dialog.questions.Where(q => q.obsoleteIfReadAnyOf.Any(depId =>
            {
                var de = questionOfId[depId];
                return de.asked;
            })
            ).Select(q => q.id).to_hashset();


            var visibleQuestions = dialog.questions.Where(q => visible.Contains(q.id) && !hiddenObsolete.Contains(q.id)).ToList();

            if (visibleQuestions.Count == 1)
            {
                ret = askQuestion(w, dialog, visibleQuestions.First(), saveNames, isTextMode);
            }
            else
            {
                ret = new SegActionRes(w.cur_time)
                {
                    questions = visibleQuestions.Select(q => new QuestionClient { questionId = q.id, questionText = q.questionText }).ToList(),
                };
            }
            //}

            return ret;
        }
        internal static void serializzaDialogoToXml(XElement xelRoot, Dialog d)
        {
            var xeld = new XElement("dialog");
            xelRoot.Add(xeld);
            xeld.Add(new XAttribute("id", d.id));

            xeld.Add(new XAttribute("askedQuestionsAreVisible", d.askedQuestionsAreVisible));

            foreach (var q in d.questions)
            {
                var xelq = new XElement("question");
                xeld.Add(xelq);

                xelq.Add(new XAttribute("id", q.id));

                xelq.Add(new XAttribute("asked", q.asked));
            }
        }




        internal static void deserializzaDialogoDaXel(XElement elDial, Dialog dial)
        {

            dial.askedQuestionsAreVisible = bool.Parse(elDial.Attribute("askedQuestionsAreVisible")?.Value ?? throw new InvalidOperationException());

            foreach (var elq in elDial.Elements("question"))
            {
                var questionId = elq.Attribute("id")?.Value;
                var question = dial.questions.Single(q => q.id == questionId);


                question.asked = bool.Parse(elq.Attribute("asked")?.Value ?? throw new InvalidOperationException());
            }
        }


        internal static SegActionRes askQuestion(WorldBase w, Dialog curDialog, Question question, string[] saveNames, bool isTextMode)
        {
            var ri = new ResponseInput(new CutScene(canBeSkipped: false), ConversationRes.ContinueDialog, w.activeChar);

            w.setCurrentCs(ri.cs);
            question.response(ri); // scrive in ri la cutscene ed altro
            w.clearCurrentCs();

            question.asked = true;

            GameStateShowingQuestions dopoDevoPassareATalk = null;
            if (ri.res == ConversationRes.ContinueDialog)
            {
                dopoDevoPassareATalk = new GameStateShowingQuestions
                {
                    dialog = curDialog // per ora lascio gli stessi; poi dovrò aggiungere dei topic, se si possono scoprire parlando, come in ultima 7.
                };
            }








            w.gs = new GameStateCutScene
            (
                    cs: ri.cs,
                    iCurToken: 0,
                    afterCutSceneShowDialog: dopoDevoPassareATalk
                    , afterCutSceneWaitForTextInput: null
                    , afterCutSceneGameFinished: null


            );

            var ret = new SegActionRes(w.cur_time)
            {
                nextCutSceneToken = new CutSceneTokenWithTitle { actionReadable = null, cutSceneToken = ri.cs.First() },
                room = creaRoomDaDareAlClient(w, saveNames, isTextMode),
                questions = null,
            };
            return ret;
        }




        //public static actionRes2 executeUnaryActionTake(logicObjE lo, worldE w) // bm_executeunary bm_unaryac
        //{

        //    // se hai eseguito un'azione, vuole dire che stavi guardando la stanza, non eri in una cutscene o in un dialogo.
        //    Debug.Assert(w.gs is gameStateViewingRoom);



        //    var i = new unaryHandlerInput { };



        //    //if (verb == lookAt.i)
        //    //{
        //    //    w.curRoom.onBeforeLook(new eventArg { cs = i.cs });
        //    //}


        //    // prima di eseguire la logica dell'azione, calcolo la frase completa. lo devo fare prima perché l'azione potrebbe settare "conosce il nome di mark = true",
        //    // e quindi la frase completa apparirebbe erroneamente con "parla con mark" anziché "parla con sconosciuto".
        //    //string fraseCompleta;
        //    //if (verb.showObjectNameAfterVerb)
        //    //{
        //    //    var oggNome = lo.complOgg(w.ac, det: true);
        //    //    fraseCompleta = verb.firstPartForSentence.flu() + " " + oggNome;
        //    //}
        //    //else
        //    //{
        //    //    fraseCompleta = verb.firstPartForSentence.flu();
        //    //}














        //    if (lo.unaryHandlers.ContainsKey(verb))
        //    {

        //        i.timeMustAdvance = verb != lookAt.i;

        //        var handler = lo.unaryHandlers[verb];



        //        handler(i); // scrive la cutscene, e inoltre può modificare timeMustAdvance e dontEnterDialog


        //    }
        //    else
        //    {

        //        if (verb == lookAt.i)
        //        {
        //            "Non ci vedi niente di speciale.".tr().tonar().add(i.cs);
        //        }
        //        else
        //        {

        //            "Non vedi che utilità abbia in questo momento.".tr().tonar().add(i.cs); // non credi che sia utile
        //        }






        //    }



        //    if (i.timeMustAdvance)
        //    {


        //        eng.increaseTimeAndUpdateNpcSchedules(i, w); // modifica la cutcene e potrebbe anche modificare il dialogo dopo.

        //    }





        //    //
        //    gameStateShowingQuestions gameStateTalkDopoLaCutScene;
        //    if (i.dialogToStart != null)
        //    {

        //        gameStateTalkDopoLaCutScene = new gameStateShowingQuestions
        //        {
        //            dialog = i.dialogToStart
        //        };
        //    }
        //    else
        //    {
        //        gameStateTalkDopoLaCutScene = null;
        //    }

        //    //




        //    if (i.cs.Any())
        //    {





        //        w.gs = new gameStateCutScene
        //        {
        //            cs = i.cs.ToArray(),
        //            iCurToken = 0,
        //            afterCutSceneShowDialog = gameStateTalkDopoLaCutScene,

        //        };

        //        return new actionRes2
        //        {
        //            nextCutSceneToken = new cutSceneTokenWithTitle { cutSceneToken = i.cs.First(), actionReadable = fraseCompleta },
        //        };
        //    }
        //    else
        //    {
        //        // siamo già dopo la cut scene. Ora, vedi se deve passare a room o a dialogo.


        //        var retv = calcolaActionResTalkORoom(w, gameStateTalkDopoLaCutScene);

        //        return retv;

        //    }
        //}




















        //internal static SegActionRes executeQuatAction(BinVerb binVerb, LogicObj lo1, LogicObj lo2, Objective ob, WorldBase w) // bm_executebinary bm_binary
        //{

        //        // se hai eseguito un'azione, vuole dire che stavi guardando la stanza, non eri in una cutscene o in un dialogo.
        //        Debug.Assert(w.gs is GameStateViewingRoom);

        //        w.pastActions.Add(new PastActionQuat
        //        {
        //                dateTime = DateTime.Now,
        //                lo1 = lo1,
        //                lo2 = lo2,
        //                puzzle = ob,
        //                binVerb = binVerb,
        //        });


        //        // prima di eseguire la logica dell'azione, calcolo la frase completa. lo devo fare prima perché l'azione potrebbe settare "conosce il nome di mark = true",
        //        // e quindi la frase completa apparirebbe erroneamente con "parla con mark" anziché "parla con sconosciuto".
        //        string fraseCompleta;


        //        //fraseCompleta = "{1} {2} {3} {4} PER {5}".tr().inst(binVerb.name).inst(lo1.name).inst(binVerb.secondPart).inst(lo2.name).inst(ob.readableName);



        //        // non mi piace più la logica ad hoc

        //        calcolaChiECharacter(lo1, lo2, out Character cha, out LogicObj nonCha);


        //        var inOrderTo = w.translateSentenceWithIdFromObjfile(strToTranslate: "per", xelementName: "in_order_to");



        //        if (binVerb.charIsAlwaysLast && cha != null)
        //        {
        //                // forzo il char per secondo. la stessa logica è nel client javascript in 2 punti



        //                fraseCompleta = "{1} {2} {3} {4} {6} {5}".tr().inst(binVerb.translated_name(w.curLang)).inst(nonCha.translatedName()).inst(binVerb.translated_second_part(w.curLang)).inst(cha.translatedName()).inst(ob.translated_name(w.curLang)).inst(inOrderTo);
        //        }
        //        else if (binVerb.charIsAlwaysFirst && cha != null)
        //        {
        //                // forzo il char per primo. la stessa logica è nel client javascript in 2 punti
        //                fraseCompleta = "{1} {2} {3} {4} {6} {5}".tr().inst(binVerb.translated_name(w.curLang)).inst(cha.translatedName()).inst(binVerb.translated_second_part(w.curLang)).inst(nonCha.translatedName()).inst(ob.translated_name(w.curLang)).inst(inOrderTo);
        //        }
        //        else
        //        {
        //                fraseCompleta = "{1} {2} {3} {4} {6} {5}".tr().inst(binVerb.translated_name(w.curLang)).inst(lo1.translatedName()).inst(binVerb.translated_second_part(w.curLang)).inst(lo2.translatedName()).inst(ob.translated_name(w.curLang)).inst(inOrderTo);
        //        }






        //        var i = new HandlerInput { };

        //        var cs = new CutScene(canBeSkipped: false);

        //        var ha = w.useWithHandlers.FirstOrDefault(h => h.containsObj(lo1) && h.containsObj(lo2) && h.puzzle == ob && h.binVerb == binVerb);



        //        w.setCurrentCs(cs);


        //        w.beforeActionExecuted(binVerb, new[] { lo1, lo2 }, ob, w.curRoom, out bool canceled);

        //        if (!canceled)
        //        {

        //                if (ha != null)
        //                {

        //                        //i.timeMustAdvance = verb != talkTo.i && (!verbsForWhichTimeDoesNotAdvance.Contains(verb)); // per default il tempo avanza sempre tranne per look.


        //                        ha.handler(i); // scrive la cutscene, e inoltre può modificare timeMustAdvance e dontEnterDialog

        //                        if (i.makesNoSenseAtThisTime == true)
        //                        {
        //                                var nonVedi = w.translateSentenceWithIdFromObjfile(strToTranslate: "Non vedi come questo possa aiutarti a {1}.", xelementName: "you_dont_see_how_this_can_help");

        //                                w.nar(nonVedi.inst(ob.translated_name(w.curLang)));


        //                        }
        //                }
        //                else
        //                {

        //                        var nonVedi = w.translateSentenceWithIdFromObjfile(strToTranslate: "Non vedi come questo possa aiutarti a {1}.", xelementName: "you_dont_see_how_this_can_help");

        //                        w.nar(nonVedi.inst(ob.translated_name(w.curLang)));

        //                        //forse_aggiungi_non_ho_voglia(ob, w);


        //                }
        //        }
        //        w.clearCurrentCs();




        //        if (i.timeMustAdvance)
        //        {


        //                increaseTimeAndExecuteAfterActionScript(cs, w); // modifica la cutcene e potrebbe anche modificare il dialogo dopo.

        //        }





        //        //
        //        vediStatoGameTalkOText(i, out GameStateShowingQuestions gameStateTalkDopoLaCutScene, out GameStateWaitingForText gameStateWaitingTextDopoCutScene);

        //        //




        //        if (cs.Any())
        //        {





        //                w.gs = new GameStateCutScene
        //                {
        //                        cs = cs,
        //                        iCurToken = 0,
        //                        afterCutSceneShowDialog = gameStateTalkDopoLaCutScene,
        //                        afterCutSceneWaitForTextInput = gameStateWaitingTextDopoCutScene

        //                };

        //                return new SegActionRes
        //                {
        //                        nextCutSceneToken = new CutSceneTokenWithTitle { cutSceneToken = cs.First(), actionReadable = fraseCompleta },
        //                };
        //        }
        //        else
        //        {
        //                // siamo già dopo la cut scene. Ora, vedi se deve passare a room o a dialogo.


        //                var retv = calcolaActionResTalkORoom(w, gameStateTalkDopoLaCutScene, gameStateWaitingTextDopoCutScene);

        //                return retv;

        //        }
        //}



        internal static SegActionRes executeActionUseWith(LogicObj lo1, LogicObj lo2, Explanation explanation, bool youAlreadyKnowItWillFail, WorldBase w, string[] saveNames, XDocIndexed xdi, bool isTextMode) // bm_executebinary bm_binary
        {

            // se hai eseguito un'azione, vuole dire che stavi guardando la stanza, non eri in una cutscene o in un dialogo.
            Debug.Assert(w.gs is GameStateViewingRoom);




            // prima di eseguire la logica dell'azione, calcolo la frase completa. lo devo fare prima perché l'azione potrebbe settare "conosce il nome di mark = true",
            // e quindi la frase completa apparirebbe erroneamente con "parla con mark" anziché "parla con sconosciuto".
            string fraseCompleta;


            //fraseCompleta = "{1} {2} {3} {4} PER {5}".tr().inst(binVerb.name).inst(lo1.name).inst(binVerb.secondPart).inst(lo2.name).inst(ob.readableName);



            // non mi piace più la logica ad hoc

            //calcolaChiECharacter(lo1, lo2, out Character cha, out LogicObj nonCha);







            var i = new HandlerInput { };

            var cs = new CutScene(canBeSkipped: false);

            CombineHandler ha1;

            if (lo1 != lo2)
            {

                // La ricerca deve rispettare l'ordine della coppia: lo1 è
                // l'oggetto usato, lo2 è il target.
                ha1 = w.combineHandlers.FirstOrDefault(h => h.lo1 == lo1 && h.lo2 == lo2 && h.Explanation == explanation);
                if (w.IsCasual() && explanation == null && !w.CasualModeKeepsExplanation(lo1, lo2))
                {
                    // In Casual l'utente non sceglie l'explanation: la coppia
                    // ordinata identifica l'unico handler e il motore gli passa
                    // comunque la sua explanation interna.
                    var casualExactHandler = w.combineHandlers.FirstOrDefault(h => h.lo1 == lo1 && h.lo2 == lo2);
                    if (casualExactHandler?.Explanation == null
                    || (casualExactHandler != null
                        && w.explanationIsVisible(casualExactHandler.Explanation)
                        && w.isCombineExplanationAvailableNow(lo1, lo2, casualExactHandler.Explanation)))
                    {
                        ha1 = casualExactHandler;
                        explanation = ha1?.Explanation;
                    }
                    else
                    {
                        // L'handler esiste, ma la sua explanation è ancora
                        // narrativa-mente nascosta: non rivelare il puzzle.
                        ha1 = null;
                        explanation = null;
                    }
                }
            }
            else
            {
                ha1 = null;
            }





            ////string fraseCompletaSenzaObiett;
            //{ // crea frase completa senza obiettivo
            //  // var inOrderTo = w.translateSentenceWithIdFromObjfile(strToTranslate: "per", xelementName: "in_order_to");



            //        //if (binVerb.charIsAlwaysLast && cha != null)
            //        //{
            //        //        // forzo il char per secondo. la stessa logica è nel client javascript in 2 punti

            //        //        fraseCompletaSenzaObiett = "{1} {2} {3} {4}".tr().inst(binVerb.translated_name(w.curLang)).inst(nonCha.translatedName()).inst(binVerb.translated_second_part(w.curLang)).inst(cha.translatedName())
            //        //            //.inst(inOrderTo)
            //        //            //.inst(ha.puzzle.translated_name(w.curLang))
            //        //            ;
            //        //}
            //        //else if (binVerb.charIsAlwaysFirst && cha != null)
            //        //{
            //        //        // forzo il char per primo. la stessa logica è nel client javascript in 2 punti
            //        //        fraseCompletaSenzaObiett = "{1} {2} {3} {4}".tr().inst(binVerb.translated_name(w.curLang)).inst(cha.translatedName()).inst(binVerb.translated_second_part(w.curLang)).inst(nonCha.translatedName())
            //        //            //.inst(inOrderTo)
            //        //            //.inst(ha.puzzle.translated_name(w.curLang))
            //        //            ;
            //        //}
            //        //else
            //        {
            //                var translatedUse = w.translateDialogOrNarOrAnnotated("usa {1} con {2}");

            //                fraseCompletaSenzaObiett = translatedUse.inst(lo1.dynamicNameTranslated(xdocObj, false)).inst(lo2.dynamicNameTranslated(xdocObj, false))
            //                    //.inst(inOrderTo)
            //                    //.inst(ha.puzzle.translated_name(w.curLang))
            //                    ;
            //        }


            //}

            void nonHaSensoFarloONonOra()
            {

                if (w.IsCasual() && w.CasualGenericFailureCycle() != null)
                {
                    w.execNextInCycle(w.CasualGenericFailureCycle());
                    return;
                }

                var nonVedi = w.translateDialogOrNarOrAnnotated("Non capisco che senso abbia!".translatable(), xdi);
                w.activeChar.Aspect = null;
                w.dial(w.activeChar, nonVedi);


            }


            //  else
            {

                w.setCurrentCs(cs);


                // non serve per use with, perchè non devo mai annullare
                //w.beforeActionExecuted(ro: w.curRoom, cancel: out bool canceled);
                //if (!canceled)
                {

                    if (ha1 != null)
                    {
                        string fullText;
                        if (ha1.DynamicSentence != null)
                        {
                            fullText = w.translateDialogOrNarOrAnnotated(ha1.DynamicSentence(), xdi);
                        }
                        else
                        {
                            fullText = w.translateDialogOrNarOrAnnotated(ha1.SentenceUntransl, xdi);
                        }
                        fraseCompleta = fondiParole(fullText, w.CurLang);

                        w.pastActions.Add(new PastActionUseWith(!youAlreadyKnowItWillFail, lo1, lo2, explanation, fullText, DateTime.Now));


                        {

                            //fraseCompleta = fraseCompletaSenzaObiett;



                            //i.timeMustAdvance = verb != talkTo.i && (!verbsForWhichTimeDoesNotAdvance.Contains(verb)); // per default il tempo avanza sempre tranne per look.

                            w.beforeExecuteDialogSetAspects(); // all'inizio di ogni dialogo, devo mettere aspect = null, se no partono con l'aspect della room, e ogni dialogo deve sempre dire aspect = null


                            if (!youAlreadyKnowItWillFail)
                            {
                                ha1.handler(i); // scrive la cutscene, e inoltre può modificare timeMustAdvance e dontEnterDialog e makesNoSenseWithCurrentObjectives

                            }
                            else
                            {
                                // non lancio l'handler perche' non voglio creare la cutscene, se no cambia gamestate in cutscene e il client e il server diventano desincronizzati
                            }
                            if (i.makesNoSenseAtThisTime == true)
                            {
                                nonHaSensoFarloONonOra();


                            }
                        }

                    }

                    else
                    {



                        w.pastActions.Add(new PastActionUseWith(false, lo1, lo2, explanation, "_fallimento_", DateTime.Now));


                        if (youAlreadyKnowItWillFail)
                        {
                            // non creare nessuna cutscene! se no il gamestate cambia in cutscene , e il server e il client si desincronizzano
                        }
                        else
                        {
                            nonHaSensoFarloONonOra();
                        }


                        var name2Transl = lo2.dynamicNameTranslated(xdi, withThe: true, isForDialog: false);


                        string fraseConPlaceHolderTransl;
                        if (lo1.VerbWhenUseWithAsFirstObjectSelectedWithPlaceHolderOnHoverSecond.is_not_null_or_white())
                        {
                            fraseConPlaceHolderTransl = w.translateDialogOrNarOrAnnotated(lo1.VerbWhenUseWithAsFirstObjectSelectedWithPlaceHolderOnHoverSecond, xdi);
                            fraseConPlaceHolderTransl = w.resolveTargetPossessiveTemplate(fraseConPlaceHolderTransl, lo1, lo2, xdi);
                        }
                        else
                        {
                            fraseConPlaceHolderTransl = w.translateDialogOrNarOrAnnotated("usa {1} con {2}".translatable(), xdi).inst(lo1.dynamicNameTranslated(xdi, withThe: true, isForDialog: false));
                        }


                        var perFareXAgisciSuy = fraseConPlaceHolderTransl.inst(name2Transl);

                        if (explanation != null)
                        {

                            string inModoChe;

                            if (lo1.CustomExplanationsFailureTemplate != null)
                            {
                                inModoChe = lo1.wo.translateDialogOrNarOrAnnotated(lo1.CustomExplanationsFailureTemplate, xdi).inst(perFareXAgisciSuy);
                            }
                            else
                            {
                                inModoChe = lo1.wo.translateDialogOrNarOrAnnotated("{1} in modo che {2}".translatable(), xdi).inst(perFareXAgisciSuy);
                            }

                            // resta da istanziare in modo che

                            var expstr = w.translateDialogOrNarOrAnnotated(explanation.exName, xdi).Replace("{1}", name2Transl);

                            fraseCompleta = fondiParole(inModoChe.inst(expstr), w.CurLang);
                        }
                        else
                        {
                            fraseCompleta = fondiParole(perFareXAgisciSuy, w.CurLang);
                        }


                    }
                }
                //else
                //{
                //        fraseCompleta = null;
                //}
                w.clearCurrentCs();


            }

            if (i.timeMustAdvance
                    && !youAlreadyKnowItWillFail // in questo caso il client non mostrerebbe la risposta!!! il client assume che non ci sia una cutscene di ritorno.
                    )
            {


                increaseTimeAndExecuteAfterActionScript(cs, w, new CombineActionContext(lo1, lo2, explanation)); // modifica la cutscene e potrebbe anche modificare il dialogo dopo.

            }





            //
            vediStatoGameTalkOText(i, out GameStateShowingQuestions gameStateTalkDopoLaCutScene, out GameStateWaitingForText gameStateWaitingTextDopoCutScene, out GameStateFinished gameStateFin);
            //




            if (cs.Any())
            {

                w.AppendAdminNarrativeMessages(cs, gameStateTalkDopoLaCutScene, gameStateWaitingTextDopoCutScene, gameStateFin);





                w.gs = new GameStateCutScene
                (
                        cs: cs,
                        iCurToken: 0,
                        afterCutSceneShowDialog: gameStateTalkDopoLaCutScene,
                        afterCutSceneWaitForTextInput: gameStateWaitingTextDopoCutScene
                        , afterCutSceneGameFinished: gameStateFin

                );

                return new SegActionRes(w.cur_time)
                {
                    nextCutSceneToken = new CutSceneTokenWithTitle { cutSceneToken = cs.First(), actionReadable = fraseCompleta },
                    room = creaRoomDaDareAlClient(w, saveNames, isTextMode)
                };
            }
            else
            {
                // siamo già dopo la cut scene. Ora, vedi se deve passare a room o a dialogo.


                var retv = calcolaActionResTalkORoom(w, gameStateTalkDopoLaCutScene, gameStateWaitingTextDopoCutScene, gameStateFin, saveNames, isTextMode);

                return retv;

            }
        }


        internal static SegActionRes executeActionUseFor(LogicObj lo, Objective ob, Explanation explanation, WorldBase w, string[] saveNames, XDocIndexed xdi, bool isTextMode) // bm_executebinary bm_binary
        {

            // se hai eseguito un'azione, vuole dire che stavi guardando la stanza, non eri in una cutscene o in un dialogo.
            Debug.Assert(w.gs is GameStateViewingRoom);




            // prima di eseguire la logica dell'azione, calcolo la frase completa. lo devo fare prima perché l'azione potrebbe settare "conosce il nome di mark = true",
            // e quindi la frase completa apparirebbe erroneamente con "parla con mark" anziché "parla con sconosciuto".
            string fraseCompleta;


            //fraseCompleta = "{1} {2} {3} {4} PER {5}".tr().inst(binVerb.name).inst(lo1.name).inst(binVerb.secondPart).inst(lo2.name).inst(ob.readableName);



            // non mi piace più la logica ad hoc

            //calcolaChiECharacter(lo1, lo2, out Character cha, out LogicObj nonCha);







            var i = new HandlerInput { };

            var cs = new CutScene(canBeSkipped: false);

            UseForHandler ha1;


            ha1 = w.useForHandlers.FirstOrDefault(h => h.Lo == lo && h.Objective == ob && h.Explanation == explanation);
            if (w.IsCasual() && explanation == null)
            {
                var casualExactHandler = w.useForHandlers.FirstOrDefault(h => h.Lo == lo && h.Objective == ob);
                if (casualExactHandler?.Explanation == null
                    || (casualExactHandler != null && w.explanationIsVisible(casualExactHandler.Explanation)))
                {
                    ha1 = casualExactHandler;
                    explanation = ha1?.Explanation;
                }
                else
                {
                    ha1 = null;
                    explanation = null;
                }
            }







            void nonHaSensoFarloONonOra()
            {

                if (w.IsCasual() && w.CasualGenericFailureCycle() != null)
                {
                    w.execNextInCycle(w.CasualGenericFailureCycle());
                    return;
                }

                var nonVedi = w.translateDialogOrNarOrAnnotated("Non capisco che senso abbia!".translatable(), xdi);
                w.activeChar.Aspect = null;
                w.dial(w.activeChar, nonVedi);


            }


            string cercaInValigiaTrad;
            if (lo.VerbWhenUseForOnHover.is_not_null_or_white())
            {
                cercaInValigiaTrad = lo.wo.translateDialogOrNarOrAnnotated(lo.VerbWhenUseForOnHover, xdi).inst(lo.dynamicNameTranslated(xdi, withThe: true, isForDialog: false));
            }
            else
            {
                cercaInValigiaTrad = lo.wo.translateDialogOrNarOrAnnotated("usa {1}".translatable(), xdi).inst(lo.dynamicNameTranslated(xdi, withThe: true, isForDialog: false));
            }

            if (explanation == null)
            {




                string templTransl;
                templTransl = lo.wo.translateDialogOrNarOrAnnotated("{1} per {2}".translatable(), xdi);

                fraseCompleta = templTransl.inst(cercaInValigiaTrad).inst(ob.translated_name(lo.wo, xdi));

            }
            else
            {

                string templTransl;
                if (ob.CustomExplanationsFailureTemplate.is_not_null_or_white())
                {
                    templTransl = lo.wo.translateDialogOrNarOrAnnotated(ob.CustomExplanationsFailureTemplate, xdi);
                }
                else
                {
                    templTransl = lo.wo.translateDialogOrNarOrAnnotated("{1} per {2} in modo che {3}".translatable(), xdi);
                }


                string explanationInstTransl = lo.wo.translateDialogOrNarOrAnnotated(explanation.exName, xdi).inst(lo.dynamicNameTranslated(xdi, withThe: true, isForDialog: false));
                fraseCompleta = templTransl.inst(cercaInValigiaTrad).inst(ob.translated_name(lo.wo, xdi)).inst(explanationInstTransl);



            }

            //  else
            {

                w.setCurrentCs(cs);


                // c'è il rischio teleport, quindi vediamo se annullare
                w.beforeActionExecuted(lo, ob, ro: w.curRoom, cancel: out bool canceled);

                if (!canceled)
                {

                    if (ha1 != null)
                    {

                        w.pastActions.Add(new PastActionUseFor(lo, ob, explanation, DateTime.Now, true));


                        {

                            //fraseCompleta = fraseCompletaSenzaObiett;



                            //i.timeMustAdvance = verb != talkTo.i && (!verbsForWhichTimeDoesNotAdvance.Contains(verb)); // per default il tempo avanza sempre tranne per look.

                            w.beforeExecuteDialogSetAspects(); // all'inizio di ogni dialogo, devo mettere aspect = null, se no partono con l'aspect della room, e ogni dialogo deve sempre dire aspect = null



                            ha1.Handler(i); // scrive la cutscene, e inoltre può modificare timeMustAdvance e dontEnterDialog e makesNoSenseWithCurrentObjectives


                            if (i.makesNoSenseAtThisTime == true)
                            {
                                nonHaSensoFarloONonOra();


                            }
                        }

                    }

                    else
                    {


                        w.pastActions.Add(new PastActionUseFor(lo, ob, explanation, DateTime.Now, false));





                        {
                            nonHaSensoFarloONonOra();
                        }




                    }
                }
                else
                {

                }
                //else
                //{
                //        fraseCompleta = null;
                //}
                w.clearCurrentCs();


            }

            if (i.timeMustAdvance)
            {


                increaseTimeAndExecuteAfterActionScript(cs, w, new UseForActionContext(lo, ob, explanation)); // modifica la cutscene e potrebbe anche modificare il dialogo dopo.

            }





            //
            vediStatoGameTalkOText(i, out GameStateShowingQuestions gameStateTalkDopoLaCutScene, out GameStateWaitingForText gameStateWaitingTextDopoCutScene, out GameStateFinished gameStateFin);
            //




            if (cs.Any())
            {

                w.AppendAdminNarrativeMessages(cs, gameStateTalkDopoLaCutScene, gameStateWaitingTextDopoCutScene, gameStateFin);





                w.gs = new GameStateCutScene
                (
                        cs: cs,
                        iCurToken: 0,
                        afterCutSceneShowDialog: gameStateTalkDopoLaCutScene,
                        afterCutSceneWaitForTextInput: gameStateWaitingTextDopoCutScene
                        , afterCutSceneGameFinished: gameStateFin

                );

                return new SegActionRes(w.cur_time)
                {
                    nextCutSceneToken = new CutSceneTokenWithTitle { cutSceneToken = cs.First(), actionReadable = fondiParole(fraseCompleta, w.CurLang) },
                    room = creaRoomDaDareAlClient(w, saveNames, isTextMode)
                };
            }
            else
            {
                // siamo già dopo la cut scene. Ora, vedi se deve passare a room o a dialogo.


                var retv = calcolaActionResTalkORoom(w, gameStateTalkDopoLaCutScene, gameStateWaitingTextDopoCutScene, gameStateFin, saveNames, isTextMode);

                return retv;

            }
        }













        internal static SegActionRes executeActionIsActually(LogicObj lo, Explanation exp1, Explanation exp2, WorldBase w, string[] saveNames, XDocIndexed xdi, bool isTextMode) // bm_executebinary bm_binary
        {

            // se hai eseguito un'azione, vuole dire che stavi guardando la stanza, non eri in una cutscene o in un dialogo.
            Debug.Assert(w.gs is GameStateViewingRoom);




            // prima di eseguire la logica dell'azione, calcolo la frase completa. lo devo fare prima perché l'azione potrebbe settare "conosce il nome di mark = true",
            // e quindi la frase completa apparirebbe erroneamente con "parla con mark" anziché "parla con sconosciuto".
            string fraseCompleta;


            //fraseCompleta = "{1} {2} {3} {4} PER {5}".tr().inst(binVerb.name).inst(lo1.name).inst(binVerb.secondPart).inst(lo2.name).inst(ob.readableName);



            // non mi piace più la logica ad hoc

            //calcolaChiECharacter(lo1, lo2, out Character cha, out LogicObj nonCha);







            var i = new HandlerInput { };

            var cs = new CutScene(canBeSkipped: false);



            var ha1 = w.isActuallyHandlers.FirstOrDefault(h => h.Lo == lo && h.Explanation1 == exp1 && h.Explanation2 == exp2);







            void nonHaSensoFarloONonOra()
            {

                var nonVedi = w.translateDialogOrNarOrAnnotated("Non capisco che senso abbia!".translatable(), xdi);
                w.activeChar.Aspect = null;
                w.dial(w.activeChar, nonVedi);


            }


            string cercaInValigiaTrad;
            if (lo.VerbWhenUseForOnHover.is_not_null_or_white())
            {
                cercaInValigiaTrad = lo.wo.translateDialogOrNarOrAnnotated(lo.VerbWhenUseForOnHover, xdi).inst(lo.dynamicNameTranslated(xdi, withThe: true, isForDialog: false));
            }
            else
            {
                cercaInValigiaTrad = lo.wo.translateDialogOrNarOrAnnotated("usa {1}".translatable(), xdi).inst(lo.dynamicNameTranslated(xdi, withThe: true, isForDialog: false));
            }




            string templTransl;
            templTransl = lo.wo.translateDialogOrNarOrAnnotated("deduci che {1} {2} {3}".translatable(), xdi);

            fraseCompleta = templTransl.inst(lo.dynamicNameTranslated(xdi, withThe: true, isForDialog: false)
                                                                            //translatedName(xdi, out bool? found)
                                                                            )
                                                            .inst(lo.wo.translateDialogOrNarOrAnnotated(exp1.exName, xdi).Replace("...", ""))
                                                            .inst(lo.wo.translateDialogOrNarOrAnnotated(exp2.exName, xdi))
                                                            ;





            //  else
            {

                w.setCurrentCs(cs);


                // non serve per use with, perchè non devo mai annullare
                //w.beforeActionExecuted(ro: w.curRoom, cancel: out bool canceled);
                //if (!canceled)
                {

                    if (ha1 != null)
                    {

                        w.pastActions.Add(new PastActionIsActually(fraseCompleta, lo, exp1, exp2, DateTime.Now));


                        {

                            //fraseCompleta = fraseCompletaSenzaObiett;



                            //i.timeMustAdvance = verb != talkTo.i && (!verbsForWhichTimeDoesNotAdvance.Contains(verb)); // per default il tempo avanza sempre tranne per look.

                            w.beforeExecuteDialogSetAspects(); // all'inizio di ogni dialogo, devo mettere aspect = null, se no partono con l'aspect della room, e ogni dialogo deve sempre dire aspect = null



                            ha1.Handler(i); // scrive la cutscene, e inoltre può modificare timeMustAdvance e dontEnterDialog e makesNoSenseWithCurrentObjectives


                            if (i.makesNoSenseAtThisTime == true)
                            {
                                nonHaSensoFarloONonOra();


                            }
                        }

                    }

                    else
                    {


                        w.pastActions.Add(new PastActionIsActually(fraseCompleta, lo, exp1, exp2, DateTime.Now));





                        {
                            nonHaSensoFarloONonOra();
                        }




                    }
                }
                //else
                //{
                //        fraseCompleta = null;
                //}
                w.clearCurrentCs();


            }

            if (i.timeMustAdvance)
            {


                increaseTimeAndExecuteAfterActionScript(cs, w, new IsActuallyActionContext(lo, exp1, exp2)); // modifica la cutscene e potrebbe anche modificare il dialogo dopo.

            }





            //
            vediStatoGameTalkOText(i, out GameStateShowingQuestions gameStateTalkDopoLaCutScene, out GameStateWaitingForText gameStateWaitingTextDopoCutScene, out GameStateFinished gameStateFin);
            //




            if (cs.Any())
            {

                w.AppendAdminNarrativeMessages(cs, gameStateTalkDopoLaCutScene, gameStateWaitingTextDopoCutScene, gameStateFin);





                w.gs = new GameStateCutScene
                (
                        cs: cs,
                        iCurToken: 0,
                        afterCutSceneShowDialog: gameStateTalkDopoLaCutScene,
                        afterCutSceneWaitForTextInput: gameStateWaitingTextDopoCutScene
                        , afterCutSceneGameFinished: gameStateFin

                );

                return new SegActionRes(w.cur_time)
                {
                    nextCutSceneToken = new CutSceneTokenWithTitle { cutSceneToken = cs.First(), actionReadable = fraseCompleta },
                    room = creaRoomDaDareAlClient(w, saveNames, isTextMode)
                };
            }
            else
            {
                // siamo già dopo la cut scene. Ora, vedi se deve passare a room o a dialogo.


                var retv = calcolaActionResTalkORoom(w, gameStateTalkDopoLaCutScene, gameStateWaitingTextDopoCutScene, gameStateFin, saveNames, isTextMode);

                return retv;

            }
        }








        /// <summary>
        /// 
        /// </summary>
        /// <param name="lo">can be null</param>
        /// <param name="te"></param>
        /// <param name="fi1"></param>
        /// <param name="fi2"></param>
        /// <param name="w"></param>
        /// <param name="saveNames"></param>
        /// <param name="xdocObj"></param>
        /// <returns></returns>
        internal static SegActionRes executeUseInComposerAction(LogicObj lo, cinComposer[] pezzi, Template te, Filler fi1, Filler fi2, WorldBase w, string[] saveNames, XDocIndexed xdocObj, bool isTextMode) // bm_executebinary bm_binary
        {

            // se hai eseguito un'azione, vuole dire che stavi guardando la stanza, non eri in una cutscene o in un dialogo.
            Debug.Assert(w.gs is GameStateViewingRoom);

            w.pastActions.Add(new PastActionUseInComposer
            {
                dateTime = DateTime.Now,
                lo = lo,
                fi1 = fi1
                    ,
                fi2 = fi2
                    ,
                te = te


            });


            // prima di eseguire la logica dell'azione, calcolo la frase completa. lo devo fare prima perché l'azione potrebbe settare "conosce il nome di mark = true",
            // e quindi la frase completa apparirebbe erroneamente con "parla con mark" anziché "parla con sconosciuto".






            var i = new HandlerInput { };

            var cs = new CutScene(canBeSkipped: false);

            var ha1 = w.deduceHandlers.FirstOrDefault(h => h.lo == lo && h.template == te && h.fillers.Contains(fi1) && (fi2 == null || h.fillers.Contains(fi2)));



            string fraseCompletaParteFinale = "";
            foreach (var pe in pezzi)
            {
                if (pe.cinCliccabile)
                {
                    var filler = w.fillerOfId[pe.cinFiId];
                    var finametransl = w.translateDialogOrNarOrAnnotated(filler.Name, xdocObj);
                    fraseCompletaParteFinale += finametransl;
                }
                else
                {
                    fraseCompletaParteFinale += pe.cinText;
                }
            }


            string fraseCompleta = fraseCompletaParteFinale;
            //if (lo != null)
            //{
            //        var usaTempl = w.translateDialogOrNarOrAnnotated("usa {1} ".translatable());

            //        fraseCompleta = usaTempl.inst(lo.dynamicNameTranslated(xdocObj, false)) + fraseCompletaParteFinale;
            //}
            //else
            //{
            //        fraseCompleta = fraseCompletaParteFinale;
            //}



            //{ // crea frase completa senza obiettivo
            //  // var inOrderTo = w.translateSentenceWithIdFromObjfile(strToTranslate: "per", xelementName: "in_order_to");

            //        var translatedSoThat = w.translateDialogOrNarOrAnnotated(te.heShe);

            //        var fraseCompletaSenzaUsaOggetto = translatedSoThat.inst(fi1.Name);

            //        if (fi2 != null)
            //        {
            //                fraseCompletaSenzaUsaOggetto = fraseCompletaSenzaUsaOggetto.inst(fi2.Name);
            //        }

            //        if (lo != null)
            //        {


            //                var usaTempl = w.translateDialogOrNarOrAnnotated("usa {1} ".translatable());

            //                fraseCompleta = usaTempl.inst(lo.dynamicNameTranslated(xdocObj, false)) + fraseCompletaSenzaUsaOggetto;
            //        }
            //        else
            //        {
            //                fraseCompleta = fraseCompletaSenzaUsaOggetto;
            //        }
            //}

            void nonHaSensoFarloONonOra()
            {

                // TODO sostituire con ciclo definito dall utente
                var nonVedi = w.translateDialogOrNarOrAnnotated("Non c'è motivo di credere questo!".translatable(), xdocObj);

                w.dial(w.activeChar, nonVedi);


            }


            //  else
            {

                w.setCurrentCs(cs);


                //w.beforeActionExecutedUseInComposer(lo, ro: w.curRoom, cancel: out bool canceled);

                //if (!canceled)
                {

                    if (ha1 != null)
                    {

                        {





                            //i.timeMustAdvance = verb != talkTo.i && (!verbsForWhichTimeDoesNotAdvance.Contains(verb)); // per default il tempo avanza sempre tranne per look.

                            w.beforeExecuteDialogSetAspects(); // all'inizio di ogni dialogo, devo mettere aspect = null, se no partono con l'aspect della room, e ogni dialogo deve sempre dire aspect = null

                            ha1.handler(i); // scrive la cutscene, e inoltre può modificare timeMustAdvance e dontEnterDialog e makesNoSenseWithCurrentObjectives

                            if (i.makesNoSenseAtThisTime == true)
                            {
                                nonHaSensoFarloONonOra();


                            }
                        }

                    }
                    else
                    {
                        nonHaSensoFarloONonOra();


                    }
                }
                //else
                //{
                //        //fraseCompletaParteFinale = null;
                //}
                w.clearCurrentCs();


            }

            if (i.timeMustAdvance)
            {


                increaseTimeAndExecuteAfterActionScript(cs, w, new UseInComposerActionContext(lo, pezzi, te, fi1, fi2)); // modifica la cutscene e potrebbe anche modificare il dialogo dopo.

            }





            //
            vediStatoGameTalkOText(i, out GameStateShowingQuestions gameStateTalkDopoLaCutScene, out GameStateWaitingForText gameStateWaitingTextDopoCutScene, out GameStateFinished gameStateFin);
            //




            if (cs.Any())
            {

                w.AppendAdminNarrativeMessages(cs, gameStateTalkDopoLaCutScene, gameStateWaitingTextDopoCutScene, gameStateFin);





                w.gs = new GameStateCutScene
                (
                        cs: cs,
                        iCurToken: 0,
                        afterCutSceneShowDialog: gameStateTalkDopoLaCutScene,
                        afterCutSceneWaitForTextInput: gameStateWaitingTextDopoCutScene
                        , afterCutSceneGameFinished: gameStateFin

                );

                return new SegActionRes(w.cur_time)
                {
                    nextCutSceneToken = new CutSceneTokenWithTitle { cutSceneToken = cs.First(), actionReadable = fraseCompleta },
                    room = creaRoomDaDareAlClient(w, saveNames, isTextMode)
                };
            }
            else
            {
                // siamo già dopo la cut scene. Ora, vedi se deve passare a room o a dialogo.


                var retv = calcolaActionResTalkORoom(w, gameStateTalkDopoLaCutScene, gameStateWaitingTextDopoCutScene, gameStateFin, saveNames, isTextMode);

                return retv;

            }
        }

        private static void calcolaChiECharacter(LogicObj lo1, LogicObj lo2, out Character cha, out LogicObj nonCha)
        {
            cha = null;
            nonCha = null;
            if (lo1 is Character char1 && !(lo2 is Character))
            {
                cha = char1; nonCha = lo2;
            }
            else if (lo2 is Character char2 && !(lo1 is Character))
            {
                cha = char2; nonCha = lo1;
            }
        }




        //internal static SegActionRes executeTerActionBin(BinVerb binVerb, LogicObj lo, Objective pu, WorldBase w)
        //{

        //        // se hai eseguito un'azione, vuole dire che stavi guardando la stanza, non eri in una cutscene o in un dialogo.
        //        Debug.Assert(w.gs is GameStateViewingRoom);

        //        w.pastActions.Add(new PastActionTerBin
        //        {
        //                dateTime = DateTime.Now,
        //                lo = lo,
        //                puzzle = pu,
        //                binVerb = binVerb
        //        });


        //        // prima di eseguire la logica dell'azione, calcolo la frase completa. lo devo fare prima perché l'azione potrebbe settare "conosce il nome di mark = true",
        //        // e quindi la frase completa apparirebbe erroneamente con "parla con mark" anziché "parla con sconosciuto".
        //        //string fraseCompleta;


        //        //fraseCompleta = "{1} {2} PER {3}".tr().inst(binVerb.name).inst(lo.name).inst(pu.readable_name);


        //        string fraseCompleta;
        //        var inOrderTo = w.translateSentenceWithIdFromObjfile(strToTranslate: "per", xelementName: "in_order_to");
        //        fraseCompleta = "{1} {2} {3} {4}".tr().inst(binVerb.translated_name(w.curLang)).inst(lo.translatedName()).inst(inOrderTo).inst(pu.translated_name(w.curLang));





        //        var i = new HandlerInput { };
        //        var cs = new CutScene(canBeSkipped: false);


        //        var ha = w.puzzleSolvedHandlers.FirstOrDefault(h => h.containsObj(lo) && h.binVerb == binVerb && h.puzzle == pu);

        //        w.setCurrentCs(cs);

        //        w.beforeActionExecuted(binVerb, new[] { lo }, pu, w.curRoom, out bool canceled);

        //        if (!canceled)
        //        {
        //                if (ha != null)
        //                {


        //                        ha.handler(i); // scrive la cutscene, e inoltre può modificare timeMustAdvance e dontEnterDialog


        //                }
        //                else
        //                {

        //                        var nonVedi = w.translateSentenceWithIdFromObjfile(strToTranslate: "Non vedi come questo possa aiutarti a {1}.", xelementName: "you_dont_see_how_this_can_help");
        //                        w.nar(nonVedi.inst(pu.translated_name(w.curLang)));


        //                        // devo anche appendere olivia che si lamenta che non vuole più provare cose a casaccio
        //                        //forse_aggiungi_non_ho_voglia(pu, w);
        //                }
        //        }
        //        w.clearCurrentCs();


        //        if (i.timeMustAdvance)
        //        {


        //                increaseTimeAndExecuteAfterActionScript(cs, w); // modifica la cutcene e potrebbe anche modificare il dialogo dopo.

        //        }


        //        vediStatoGameTalkOText(i, out GameStateShowingQuestions gameStateTalkDopoLaCutScene, out GameStateWaitingForText gameStateWaitingTextDopoCutScene);

        //        //




        //        if (cs.Any())
        //        {





        //                w.gs = new GameStateCutScene
        //                {
        //                        cs = cs,
        //                        iCurToken = 0,
        //                        afterCutSceneShowDialog = gameStateTalkDopoLaCutScene,
        //                        afterCutSceneWaitForTextInput = gameStateWaitingTextDopoCutScene,

        //                };

        //                return new SegActionRes
        //                {
        //                        nextCutSceneToken = new CutSceneTokenWithTitle { cutSceneToken = cs.First(), actionReadable = fraseCompleta },
        //                };
        //        }
        //        else
        //        {
        //                // siamo già dopo la cut scene. Ora, vedi se deve passare a room o a dialogo.


        //                var retv = calcolaActionResTalkORoom(w, gameStateTalkDopoLaCutScene, gameStateWaitingTextDopoCutScene);

        //                return retv;

        //        }
        //}



        internal static SegActionRes executeActionPickup(LogicObj lo, WorldBase w, string[] saveNames, XDocIndexed xdocObj, bool isTextMode)
        {

            // se hai eseguito un'azione, vuole dire che stavi guardando la stanza, non eri in una cutscene o in un dialogo.
            Debug.Assert(w.gs is GameStateViewingRoom);

            w.pastActions.Add(new PastActionPickup
            {
                dateTime = DateTime.Now,
                lo = lo,

            });


            // prima di eseguire la logica dell'azione, calcolo la frase completa. lo devo fare prima perché l'azione potrebbe settare "conosce il nome di mark = true",
            // e quindi la frase completa apparirebbe erroneamente con "parla con mark" anziché "parla con sconosciuto".
            //string fraseCompleta;


            //fraseCompleta = "{1} {2} PER {3}".tr().inst(binVerb.name).inst(lo.name).inst(pu.readable_name);


            string fraseCompleta;

            var raccogliTrad = w.translateDialogOrNarOrAnnotated("raccogli {1}".translatable(), xdocObj);
            fraseCompleta = raccogliTrad.inst(lo.dynamicNameTranslated(xdocObj, withThe: true, isForDialog: false));





            var i = new PickUpHandlerInput { };
            var cs = new CutScene(canBeSkipped: false);



            var ha = w.pickUpHandlers.SingleOrDefault(h => h.containsObj(lo));

            w.setCurrentCs(cs);

            if (w.curParty.Any(ch => ch.hasObject(lo)))
            {
                w.activeChar.Aspect = null;
                w.dial(w.activeChar, "Ce l'ho già!");
            }
            else
            {


                // non serve, perche pickup non devo mai annullarlo
                //w.beforeActionExecuted(w.curRoom, out bool canceled);


                //if (!canceled)
                {
                    if (ha != null)
                    {

                        w.beforeExecuteDialogSetAspects(); // all'inizio di ogni dialogo, devo mettere aspect = null, se no partono con l'aspect della room, e ogni dialogo deve sempre dire aspect = null

                        ha.handler(i); // scrive la cutscene, e inoltre può modificare timeMustAdvance e dontEnterDialog

                        if (cs.isEmpty())
                        {
                            if (i.makesNoSenseAtThisTime == true)
                            {
                                w.activeChar.Aspect = null;
                                w.dial(w.activeChar, "Non posso raccoglierlo!");
                            }
                            else
                            {
                                if (i.dontSayDefaultTextIfCsEmpty)
                                {

                                }
                                else
                                {
                                    w.activeChar.Aspect = null;
                                    w.dial(w.activeChar, "Preso!");
                                }
                            }
                        }

                    }
                    else
                    {
                        w.activeChar.Aspect = null;
                        w.dial(w.activeChar, "Non posso raccoglierlo!");


                    }
                }
            }
            w.clearCurrentCs();


            if (i.timeMustAdvance)
            {


                increaseTimeAndExecuteAfterActionScript(cs, w, new PickUpActionContext(lo)); // modifica la cutscene e potrebbe anche modificare il dialogo dopo.

            }


            vediStatoGameTalkOText(i, out GameStateShowingQuestions gameStateTalkDopoLaCutScene, out GameStateWaitingForText gameStateWaitingTextDopoCutScene, out GameStateFinished gameStateFin);

            //




            if (cs.Any())
            {

                w.AppendAdminNarrativeMessages(cs, gameStateTalkDopoLaCutScene, gameStateWaitingTextDopoCutScene, gameStateFin);





                w.gs = new GameStateCutScene(

                        cs: cs,
                        iCurToken: 0,
                        afterCutSceneShowDialog: gameStateTalkDopoLaCutScene,
                        afterCutSceneWaitForTextInput: gameStateWaitingTextDopoCutScene
                        , afterCutSceneGameFinished: gameStateFin
                );

                return new SegActionRes(w.cur_time)
                {
                    nextCutSceneToken = new CutSceneTokenWithTitle { cutSceneToken = cs.First(), actionReadable = fraseCompleta },
                    room = creaRoomDaDareAlClient(w, saveNames, isTextMode)
                };
            }
            else
            {
                // siamo già dopo la cut scene. Ora, vedi se deve passare a room o a dialogo.


                var retv = calcolaActionResTalkORoom(w, gameStateTalkDopoLaCutScene, gameStateWaitingTextDopoCutScene, gameStateFin, saveNames, isTextMode);

                return retv;

            }
        }



        internal static SegActionRes executeActionUseHere(LogicObj lo, WorldBase w, string[] saveNames, XDocIndexed xdocObj, bool isTextMode)
        {

            // se hai eseguito un'azione, vuole dire che stavi guardando la stanza, non eri in una cutscene o in un dialogo.
            Debug.Assert(w.gs is GameStateViewingRoom);



            // prima di eseguire la logica dell'azione, calcolo la frase completa. lo devo fare prima perché l'azione potrebbe settare "conosce il nome di mark = true",
            // e quindi la frase completa apparirebbe erroneamente con "parla con mark" anziché "parla con sconosciuto".
            //string fraseCompleta;


            //fraseCompleta = "{1} {2} PER {3}".tr().inst(binVerb.name).inst(lo.name).inst(pu.readable_name);


            string fraseCompleta;

            //var raccogliTrad = w.translateDialogOrNarOrAnnotated("usa {1}");
            //fraseCompleta = raccogliTrad.inst(lo.dynamicNameTranslated(xdocObj, withArticle: false /* con articolo potrebbe essere null*/));





            var i = new HandlerInput { };
            var cs = new CutScene(canBeSkipped: false);



            var ha = w.useHereHandlers.SingleOrDefault(h => h.containsObj(lo));

            w.setCurrentCs(cs);



            {


                // non serve, perche' use here non c'e' mai bisogno di annullarlo
                //w.beforeActionExecuted(w.curRoom, out bool canceled);


                //if (!canceled)
                {
                    if (ha != null)
                    {


                        string fullText;
                        if (ha.DynamicSentence != null)
                        {
                            fullText = w.translateDialogOrNarOrAnnotated(ha.DynamicSentence(), xdocObj);
                        }
                        else
                        {
                            string str;
                            if (lo.VerbWhenUseHere.is_not_null_or_white())
                            {
                                var VerbWhenUseHereInRoomTransl = lo.wo.translateDialogOrNarOrAnnotated(lo.VerbWhenUseHere, xdocObj);
                                str = VerbWhenUseHereInRoomTransl;
                            }
                            else
                            {
                                str = lo.wo.translateDialogOrNarOrAnnotated("usa {1}".translatable(), xdocObj);
                            }


                            string loNameTransl = lo.dynamicNameTranslated(xdocObj, withThe: true, isForDialog: false);

                            fullText = str.inst(loNameTransl);
                        }
                        fraseCompleta = fullText;
                        w.pastActions.Add(new PastActionUseHere(lo, fullText, DateTime.Now));





                        w.beforeExecuteDialogSetAspects(); // all'inizio di ogni dialogo, devo mettere aspect = null, se no partono con l'aspect della room, e ogni dialogo deve sempre dire aspect = null

                        ha.handler(i); // scrive la cutscene, e inoltre può modificare timeMustAdvance e dontEnterDialog


                    }
                    else
                    {
                        throw new Exception("handler missing");



                    }
                }
            }
            w.clearCurrentCs();


            if (i.timeMustAdvance)
            {


                increaseTimeAndExecuteAfterActionScript(cs, w, new UseHereActionContext(lo)); // modifica la cutscene e potrebbe anche modificare il dialogo dopo.

            }


            vediStatoGameTalkOText(i, out GameStateShowingQuestions gameStateTalkDopoLaCutScene, out GameStateWaitingForText gameStateWaitingTextDopoCutScene, out GameStateFinished gameStateFin);

            //




            if (cs.Any())
            {




                w.AppendAdminNarrativeMessages(cs, gameStateTalkDopoLaCutScene, gameStateWaitingTextDopoCutScene, gameStateFin);

                w.gs = new GameStateCutScene(

                        cs: cs,
                        iCurToken: 0,
                        afterCutSceneShowDialog: gameStateTalkDopoLaCutScene,
                        afterCutSceneWaitForTextInput: gameStateWaitingTextDopoCutScene
                        , afterCutSceneGameFinished: gameStateFin

                );

                return new SegActionRes(w.cur_time)
                {
                    nextCutSceneToken = new CutSceneTokenWithTitle { cutSceneToken = cs.First(), actionReadable = fraseCompleta },
                    room = creaRoomDaDareAlClient(w, saveNames, isTextMode)
                };
            }
            else
            {
                // siamo già dopo la cut scene. Ora, vedi se deve passare a room o a dialogo.


                var retv = calcolaActionResTalkORoom(w, gameStateTalkDopoLaCutScene, gameStateWaitingTextDopoCutScene, gameStateFin, saveNames, isTextMode);

                return retv;

            }
        }






        internal static SegActionRes executeCancelTextInput(TextInput ti, WorldBase w, string[] saveNames, XDocument xdocObj, bool isTextMode)
        {

            // se hai eseguito un'azione, vuole dire che stavi guardando la stanza, non eri in una cutscene o in un dialogo.
            //Debug.Assert(w.gs is GameStateWaitingForText); // a runtime ho gamestateviewingroom


            // todo 
            //w.pastActions.Add(new PastActionTerBin
            //{
            //        dateTime = DateTime.Now,
            //        lo = lo,
            //        puzzle = pu,
            //        binVerb = binVerb
            //});




            w.pastActions.Add(new PastActionCancelText
            {
                dateTime = DateTime.Now
            });





            string fraseCompleta;
            var nonLoSai = w.translateSentenceWithIdFromObjfile(strToTranslate: "non lo sai", xelementName: "you_dont_know", xdocObj: xdocObj);
            fraseCompleta = nonLoSai;





            var i = new HandlerInput { };
            var cs = new CutScene(canBeSkipped: false);


            var ha = w.cancelTextInputHandlers.FirstOrDefault(h => h.ti == ti);

            w.setCurrentCs(cs);

            //w.beforeActionExecuted(binVerb, new[] { lo }, pu, w.curRoom, out bool canceled);

            //if (!canceled)
            {
                if (ha != null)
                {

                    w.beforeExecuteDialogSetAspects(); // all'inizio di ogni dialogo, devo mettere aspect = null, se no partono con l'aspect della room, e ogni dialogo deve sempre dire aspect = null

                    ha.handler(i); // scrive la cutscene, e inoltre può modificare timeMustAdvance e dontEnterDialog


                }
                else
                {

                    throw new Exception($"You need to add a cancelTextInputHandler for {ti.serId}");
                }
            }
            w.clearCurrentCs();


            if (i.timeMustAdvance)
            {
                increaseTimeAndExecuteAfterActionScript(cs, w, new CancelTextInputActionContext(ti)); // modifica la cutscene e potrebbe anche modificare il dialogo dopo.

            }

            vediStatoGameTalkOText(i, out GameStateShowingQuestions gameStateTalkDopoLaCutScene, out GameStateWaitingForText gameStateWaitingTextDopoCutScene
                    , out GameStateFinished gameStateFin);

            //

            if (cs.Any())
            {

                w.AppendAdminNarrativeMessages(cs, gameStateTalkDopoLaCutScene, gameStateWaitingTextDopoCutScene, gameStateFin);

                w.gs = new GameStateCutScene(

                        cs: cs,
                        iCurToken: 0,
                        afterCutSceneShowDialog: gameStateTalkDopoLaCutScene,
                        afterCutSceneWaitForTextInput: gameStateWaitingTextDopoCutScene
                        , afterCutSceneGameFinished: gameStateFin

                );

                return new SegActionRes(w.cur_time)
                {
                    nextCutSceneToken = new CutSceneTokenWithTitle { cutSceneToken = cs.First(), actionReadable = fraseCompleta },
                    room = creaRoomDaDareAlClient(w, saveNames, isTextMode)
                };
            }
            else
            {
                // siamo già dopo la cut scene. Ora, vedi se deve passare a room o a dialogo.


                var retv = calcolaActionResTalkORoom(w, gameStateTalkDopoLaCutScene, gameStateWaitingTextDopoCutScene, gameStateFin, saveNames, isTextMode);

                return retv;

            }
        }

        internal static SegActionRes executeSubmitTextInput(TextInput ti, string chosenText, string chosenText2, string chosenExplId, WorldBase w, string[] saveNames, XDocIndexed xdocObj, bool isTextMode)
        {

            // NO. risulta cutscene
            //Debug.Assert(w.gs is GameStateWaitingForText);


            // todo 
            //w.pastActions.Add(new PastActionTerBin
            //{
            //        dateTime = DateTime.Now,
            //        lo = lo,
            //        puzzle = pu,
            //        binVerb = binVerb
            //});







            w.pastActions.Add(new PastActionSubmitText
            {
                dateTime = DateTime.Now,

                TextTyped = chosenText
                    ,
                TextTyped2 = chosenText2
                    ,
                explId = chosenExplId

            });


            string fraseCompleta;
            if (chosenText2.isNullOrWhite())
            {
                var rispondiTradotto = w.translateDialogOrNarOrAnnotated("Rispondi {1}".translatable(), xdocObj);
                fraseCompleta = rispondiTradotto.inst($"\"{chosenText}\"");
            }
            else
            {
                var rispondiTradotto = w.translateDialogOrNarOrAnnotated("Rispondi {1} e {2}".translatable(), xdocObj);
                fraseCompleta = rispondiTradotto.inst($"\"{chosenText}\"").inst($"\"{chosenText2}\"");
            }





            var i = new TextHandlerInput { chosenText = chosenText, chosenText2 = chosenText2, explSerId = chosenExplId };
            var cs = new CutScene(canBeSkipped: false);


            var ha = w.submitTextInputHandlers.FirstOrDefault(h => h.ti == ti);

            w.setCurrentCs(cs);

            //w.beforeActionExecuted(binVerb, new[] { lo }, pu, w.curRoom, out bool canceled);

            //if (!canceled)
            {
                if (ha != null)
                {

                    w.beforeExecuteDialogSetAspects(); // all'inizio di ogni dialogo, devo mettere aspect = null, se no partono con l'aspect della room, e ogni dialogo deve sempre dire aspect = null


                    ha.handler(i); // scrive la cutscene, e inoltre può modificare timeMustAdvance e dontEnterDialog


                }
                else
                {

                    throw new Exception($"You need to add a submitTextInputHandler for {ti.serId}");
                }
            }
            w.clearCurrentCs();


            if (i.timeMustAdvance)
            {
                increaseTimeAndExecuteAfterActionScript(cs, w, new SubmitTextInputActionContext(ti, chosenText, chosenText2, chosenExplId)); // modifica la cutscene e potrebbe anche modificare il dialogo dopo.

            }

            vediStatoGameTalkOText(i, out GameStateShowingQuestions gameStateTalkDopoLaCutScene, out GameStateWaitingForText gameStateWaitingTextDopoCutScene
                     , out GameStateFinished gameStateFin);

            //

            if (cs.Any())
            {

                w.AppendAdminNarrativeMessages(cs, gameStateTalkDopoLaCutScene, gameStateWaitingTextDopoCutScene, gameStateFin);

                w.gs = new GameStateCutScene(

                        cs: cs,
                        iCurToken: 0,
                        afterCutSceneShowDialog: gameStateTalkDopoLaCutScene,
                        afterCutSceneWaitForTextInput: gameStateWaitingTextDopoCutScene
                        , afterCutSceneGameFinished: gameStateFin

                );

                return new SegActionRes(w.cur_time)
                {

                    nextCutSceneToken = new CutSceneTokenWithTitle { cutSceneToken = cs.First(), actionReadable = fraseCompleta },
                    room = creaRoomDaDareAlClient(w, saveNames, isTextMode)
                };
            }
            else
            {
                // siamo già dopo la cut scene. Ora, vedi se deve passare a room o a dialogo.


                var retv = calcolaActionResTalkORoom(w, gameStateTalkDopoLaCutScene, gameStateWaitingTextDopoCutScene, gameStateFin, saveNames, isTextMode);

                return retv;

            }
        }

        internal static void vediStatoGameTalkOText(HandlerInput i, out GameStateShowingQuestions gameStateTalkDopoLaCutScene, out GameStateWaitingForText gameStateWaitingTextDopoCutScene, out GameStateFinished gameStateFinished)
        {

            //
            if (i.dialogToStart != null)
            {

                gameStateTalkDopoLaCutScene = new GameStateShowingQuestions
                {
                    dialog = i.dialogToStart
                };
                gameStateWaitingTextDopoCutScene = null;
                gameStateFinished = null;
            }
            else if (i.gameFinished)
            {
                gameStateFinished = new GameStateFinished();
                gameStateWaitingTextDopoCutScene = null;
                gameStateTalkDopoLaCutScene = null;
            }
            else if (i.textInputToShow != null)
            {

                gameStateWaitingTextDopoCutScene = new GameStateWaitingForText(i.textInputToShow);
                gameStateTalkDopoLaCutScene = null;
                gameStateFinished = null;
            }
            else
            {
                gameStateTalkDopoLaCutScene = null;
                gameStateWaitingTextDopoCutScene = null;
                gameStateFinished = null;
            }
        }

        //internal static SegActionRes executeUseInLocationAction(BinVerb binVerb, LogicObj lo, Room ro, Objective pu, WorldBase w)
        //{

        //        // se hai eseguito un'azione, vuole dire che stavi guardando la stanza, non eri in una cutscene o in un dialogo.
        //        Debug.Assert(w.gs is GameStateViewingRoom);

        //        w.pastActions.Add(new PastActionUseInLocation
        //        {
        //                dateTime = DateTime.Now,
        //                lo = lo,
        //                ro = ro,
        //                binVerb = binVerb,
        //                pu = pu
        //        });


        //        // prima di eseguire la logica dell'azione, calcolo la frase completa. lo devo fare prima perché l'azione potrebbe settare "conosce il nome di mark = true",
        //        // e quindi la frase completa apparirebbe erroneamente con "parla con mark" anziché "parla con sconosciuto".
        //        //string fraseCompleta;


        //        //fraseCompleta = "{1} {2} PER {3}".tr().inst(binVerb.name).inst(lo.name).inst(pu.readable_name);


        //        string fraseCompleta;

        //        fraseCompleta = "{1} {2}".tr().inst(binVerb.translated_name(w.curLang)).inst(lo.translatedName());





        //        var i = new HandlerInput { };
        //        var cs = new CutScene(canBeSkipped: false);


        //        var ha = w.useInLocationHandlers.FirstOrDefault(h => h.containsObj(lo) && h.binVerb == binVerb /*&& h.room == ro*/);

        //        w.setCurrentCs(cs);

        //        w.beforeActionExecuted(binVerb, new[] { lo }, null, w.curRoom, out bool canceled);

        //        if (!canceled)
        //        {
        //                if (ha != null)
        //                {


        //                        ha.handler(i); // scrive la cutscene, e inoltre può modificare timeMustAdvance e dontEnterDialog

        //                        if (i.makesNoSenseWithCurrentObjectives == true)
        //                        {
        //                                var nonVedi = w.translateSentenceWithIdFromObjfile(strToTranslate: "Non ha senso usarlo in questo posto per {1}.", xelementName: "does_not_make_sense_here_for");
        //                                nonVedi = nonVedi.inst(pu.translated_name(w.curLang));
        //                                w.nar(nonVedi);

        //                        }
        //                }
        //                else
        //                {


        //                        var nonVedi = w.translateSentenceWithIdFromObjfile(strToTranslate: "Non ha senso usarlo in questo posto per {1}.", xelementName: "does_not_make_sense_here_for");
        //                        nonVedi = nonVedi.inst(pu.translated_name(w.curLang));

        //                        w.nar(nonVedi);


        //                        // devo anche appendere olivia che si lamenta che non vuole più provare cose a casaccio
        //                        //forse_aggiungi_non_ho_voglia(pu, w);
        //                }
        //        }
        //        w.clearCurrentCs();


        //        if (i.timeMustAdvance)
        //        {


        //                increaseTimeAndExecuteAfterActionScript(cs, w); // modifica la cutcene e potrebbe anche modificare il dialogo dopo.

        //        }





        //        //
        //        vediStatoGameTalkOText(i, out GameStateShowingQuestions gameStateTalkDopoLaCutScene, out GameStateWaitingForText gameStateWaitingTextDopoCutScene);



        //        if (cs.Any())
        //        {





        //                w.gs = new GameStateCutScene
        //                {
        //                        cs = cs,
        //                        iCurToken = 0,
        //                        afterCutSceneShowDialog = gameStateTalkDopoLaCutScene,
        //                        afterCutSceneWaitForTextInput = gameStateWaitingTextDopoCutScene
        //                        //gcsCanBeSkipped = false
        //                };

        //                return new SegActionRes
        //                {
        //                        nextCutSceneToken = new CutSceneTokenWithTitle { cutSceneToken = cs.First(), actionReadable = fraseCompleta },
        //                };
        //        }
        //        else
        //        {
        //                // siamo già dopo la cut scene. Ora, vedi se deve passare a room o a dialogo.


        //                var retv = calcolaActionResTalkORoom(w, gameStateTalkDopoLaCutScene, gameStateWaitingTextDopoCutScene);

        //                return retv;

        //        }
        //}


        //private static void forse_aggiungi_non_ho_voglia(Objective pu, WorldBase w)
        //{
        //        if (pu.you_have_all_data_to_solve_it != null && pu.you_have_all_data_to_solve_it())
        //        {
        //                if (pu.how_many_times_tried == 1)
        //                {
        //                        string nonHoVoglia;
        //                        {
        //                                if (pu.has_at_least_a_clue_in_past_scenes)
        //                                {
        //                                        nonHoVoglia = w.translateSentenceWithIdFromObjfile(strToTranslate: "Non ho voglia di provare cose a casaccio per {1}. Dovrei prima rileggere le scene passate. Lì ci sono tutte le informazioni che mi servono per risolvere il problema.", xelementName: "i_have_no_idea_how_to");
        //                                }
        //                                else
        //                                {
        //                                        nonHoVoglia = w.translateSentenceWithIdFromObjfile(strToTranslate: "Non ho voglia di provare cose a casaccio per {1}. Sento che ho già tutte le informazioni per risolvere il problema.", xelementName: "i_have_no_idea_how_to_2");
        //                                }
        //                        }

        //                        nonHoVoglia = nonHoVoglia.inst(pu.translated_name(w.curLang));

        //                        w.dial(w.ActiveChar, nonHoVoglia);
        //                }
        //        }
        //}

        //internal static SegActionRes executeTerActionUn(UnVerb unVerb, LogicObj lo, Objective pu, WorldBase w) // bm_executebinary bm_binary
        //{

        //        // se hai eseguito un'azione, vuole dire che stavi guardando la stanza, non eri in una cutscene o in un dialogo.
        //        Debug.Assert(w.gs is GameStateViewingRoom);




        //        w.pastActions.Add(new PastActionTerUn
        //        {
        //                dateTime = DateTime.Now,
        //                lo = lo,
        //                puzzle = pu,
        //                unVerb = unVerb
        //        });


        //        // prima di eseguire la logica dell'azione, calcolo la frase completa. lo devo fare prima perché l'azione potrebbe settare "conosce il nome di mark = true",
        //        // e quindi la frase completa apparirebbe erroneamente con "parla con mark" anziché "parla con sconosciuto".
        //        string fraseCompleta;
        //        var inOrderTo = w.translateSentenceWithIdFromObjfile(strToTranslate: "per", xelementName: "in_order_to");
        //        fraseCompleta = "{1} {2} {3} {4}".tr().inst(unVerb.translated_name(w.curLang)).inst(lo.translatedName()).inst(inOrderTo).inst(pu.translated_name(w.curLang));






        //        var i = new HandlerInput { };
        //        var cs = new CutScene(canBeSkipped: false);


        //        var ha = w.terHandlersUn.FirstOrDefault(h => h.containsObj(lo) && h.unVerb == unVerb && h.puzzle == pu);

        //        w.setCurrentCs(cs);

        //        w.beforeActionExecuted(unVerb, new[] { lo }, pu, w.curRoom, out bool canceled);

        //        if (!canceled)
        //        {
        //                if (ha != null)
        //                {
        //                        ha.handler(i); // scrive la cutscene, e inoltre può modificare timeMustAdvance e dontEnterDialog

        //                }
        //                else
        //                {
        //                        var nonVedi = w.translateSentenceWithIdFromObjfile(strToTranslate: "Non vedi come questo possa aiutarti a {1}.", xelementName: "you_dont_see_how_this_can_help");
        //                        w.nar(nonVedi.inst(pu.translated_name(w.curLang)));

        //                        //forse_aggiungi_non_ho_voglia(pu, w);
        //                }
        //        }
        //        w.clearCurrentCs();


        //        if (i.timeMustAdvance)
        //        {


        //                increaseTimeAndExecuteAfterActionScript(cs, w); // modifica la cutcene e potrebbe anche modificare il dialogo dopo.

        //        }





        //        //
        //        vediStatoGameTalkOText(i, out GameStateShowingQuestions gameStateTalkDopoLaCutScene, out GameStateWaitingForText gameStateWaitingTextDopoCutScene);

        //        //




        //        if (cs.Any())
        //        {





        //                w.gs = new GameStateCutScene
        //                {
        //                        cs = cs,
        //                        iCurToken = 0,
        //                        afterCutSceneShowDialog = gameStateTalkDopoLaCutScene,
        //                        afterCutSceneWaitForTextInput = gameStateWaitingTextDopoCutScene
        //                };

        //                return new SegActionRes
        //                {
        //                        nextCutSceneToken = new CutSceneTokenWithTitle
        //                        {
        //                                cutSceneToken = cs.First(),


        //                                actionReadable = fraseCompleta
        //                        },
        //                };
        //        }
        //        else
        //        {
        //                // siamo già dopo la cut scene. Ora, vedi se deve passare a room o a dialogo.


        //                var retv = calcolaActionResTalkORoom(w, gameStateTalkDopoLaCutScene, gameStateWaitingTextDopoCutScene);

        //                return retv;

        //        }
        //}













        internal static SegActionRes replay_cut_scene(string serId, WorldBase w, XDocIndexed xdocObj, string[] saveNames, bool isTextMode)
        {

            // se hai eseguito un'azione, vuole dire che stavi guardando la stanza, non eri in una cutscene o in un dialogo.
            Debug.Assert(w.gs is GameStateViewingRoom);




            // todo memorizzala come past action
            //w.past_actions.Add(new past_action_ter_un
            //{
            //    dateTime = DateTime.Now,
            //    lo = lo,
            //    puzzle = pu,
            //    unVerb = unVerb
            //});


            var ncs = w.namedCutScenesSeen.Single(n => n.id.serId == serId);



            var cs = new CutScene(canBeSkipped: true);

            w.setCurrentCs(cs);


            var titr = w.translateDialogOrNarOrAnnotated("Ti trovavi qui".translatable(), xdocObj);

            var cosifin = w.translateDialogOrNarOrAnnotated("[Così finisce il tuo ricordo.]".translatable(), xdocObj);

            w.narRoom($"{titr}: {ncs.roomDoveEri.dynamicNameForMapTranslated(xdocObj)}.", ncs.roomDoveEri, removeIfLast: false);

            foreach (var tok in ncs.cs)
            {


                cs.Add(tok);

            }
            w.narText(cosifin);

            w.clearCurrentCs();

            //






            w.gs = new GameStateCutScene(

                    cs: cs,
                    iCurToken: 0,
                    afterCutSceneShowDialog: null
                    , afterCutSceneWaitForTextInput: null
                    , afterCutSceneGameFinished: null

            );

            return new SegActionRes(w.cur_time)
            {
                nextCutSceneToken = new CutSceneTokenWithTitle
                {
                    cutSceneToken = cs.First(),
                    actionReadable = w.translateDialogOrNarOrAnnotated(ncs.id.titleUntranslated, xdocObj),

                },
                room = creaRoomDaDareAlClient(w, saveNames, isTextMode)
            };

        }








        internal static SegActionRes executeActionLook(string loId, WorldBase w, XDocIndexed xdocObj, string[] saveNames, bool isTextMode)
        {

            // se hai eseguito un'azione, vuole dire che stavi guardando la stanza, non eri in una cutscene o in un dialogo.
            Debug.Assert(w.gs is GameStateViewingRoom);




            var lo = w.loOfId[loId];



            var cs = new CutScene(canBeSkipped: true);


            w.setCurrentCs(cs);


            // prima di tutto vedi se c'e' un look handler
            var handlerLook = w.lookHandlers.Where(h => h.lo1 == lo).SingleOrDefault();

            if (handlerLook != null)
            {

                string fullText;

                if (handlerLook.DynamicSentence != null)
                {
                    fullText = w.translateDialogOrNarOrAnnotated(handlerLook.DynamicSentence(), xdocObj);
                }
                else
                {
                    fullText = "_look_";
                }

                w.pastActions.Add(new PastActionLookRemember(lo, fullText, DateTime.Now));




                w.beforeExecuteDialogSetAspects(); // all'inizio di ogni dialogo, devo mettere aspect = null, se no partono con l'aspect della room, e ogni dialogo deve sempre dire aspect = null

                var i = new HandlerInput { };



                handlerLook.handler(i); // scrive la cutscene, e inoltre può modificare timeMustAdvance e dontEnterDialog e makesNoSenseWithCurrentObjectives



                if (cs.isEmpty())
                {
                    if (lo is Character cha)
                    {
                        var fraseLei = "Non ci vedo nient'altro di speciale in lei!".translatable();
                        var fraseLui = "Non ci vedo nient'altro di speciale in lui!".translatable();
                        w.dial(w.ActiveChar, cha.isMale ? fraseLui : fraseLei);
                    }
                    else
                    {
                        w.dial(w.ActiveChar, "Non ci vedo nient'altro di speciale! ".translatable());
                    }
                }


                if (i.timeMustAdvance)
                {

                    increaseTimeAndExecuteAfterActionScript(cs, w, new LookActionContext(lo)); // modifica la cutscene e potrebbe anche modificare il dialogo dopo.

                }


            }
            else
            {
                w.beforeExecuteDialogSetAspects(); // all'inizio di ogni dialogo, devo mettere aspect = null, se no partono con l'aspect della room, e ogni dialogo deve sempre dire aspect = null
                if (lo is Character cha)
                {
                    var fraseLei = "Non ci vedo niente di speciale in lei!".translatable();
                    var fraseLui = "Non ci vedo niente di speciale in lui!".translatable();
                    w.dial(w.ActiveChar, cha.isMale ? fraseLui : fraseLei);

                }
                else
                {
                    w.dial(w.ActiveChar, "Non ci vedo niente di speciale! ");
                }


                //var ncs = w.namedCutScenesSeen.Where(n => n.oggettiMenzionati.Any(om => om is LogicObj && om.to_logic_obj().loId == loId)).ToList();

                //if (ncs.isEmpty())
                //{
                //        w.rememberFailedOnObject(lo);

                //        if (cs.isEmpty())
                //        {
                //                w.beforeExecuteDialogSetAspects(); // all'inizio di ogni dialogo, devo mettere aspect = null, se no partono con l'aspect della room, e ogni dialogo deve sempre dire aspect = null
                //                if (lo is Character cha)
                //                {
                //                        var fraseLei = "Non ci vedo niente di speciale in lei!".translatable();
                //                        var fraseLui = "Non ci vedo niente di speciale in lui!".translatable();
                //                        w.dial(w.ActiveChar, cha.isMale ? fraseLui : fraseLei);

                //                }
                //                else
                //                {
                //                        w.dial(w.ActiveChar, "Non ci vedo niente di speciale! ");
                //                }
                //        }
                //}
                //else if (ncs.Count == 1)
                //{
                //        ricordoQualcosaSuQuesto(w, lo);

                //        //ricordo_qualcosa_su_questo(w, lo);

                //        NamedCutScene namedCutScene = ncs.Single();


                //        var titr = w.translateDialogOrNarOrAnnotated("Ti trovavi qui".translatable());

                //        var cosifin = w.translateDialogOrNarOrAnnotated("[Così finisce il tuo ricordo.]".translatable());

                //        var iniziailtuoric = w.translateDialogOrNarOrAnnotated("[Inizia il tuo ricordo...]".translatable());

                //        w.narRoom(iniziailtuoric, namedCutScene.roomDoveEri, removeIfLast: false);





                //        if (w.curRoom != namedCutScene.roomDoveEri)// altrimenti dà fastidio, perché tu sei lì e l'oggetto anche
                //        {
                //                //var str_ti_trovavi_qui = "Ti trovavi qui".translatable();
                //                w.narRoom($"{titr}: {namedCutScene.roomDoveEri.translatedNameForMap(xdocObj)}.", namedCutScene.roomDoveEri, removeIfLast: false);
                //        }

                //        foreach (var tok in namedCutScene.cs)
                //        {
                //                cs.Add(tok);
                //        }
                //        w.nar(cosifin);
                //}
                //else
                //{
                //        ricordoQualcosaSuQuesto(w, lo);

                //        //ricordo_qualcosa_su_questo(w, lo);

                //        var titr = w.translateDialogOrNarOrAnnotated("Ti trovavi qui".translatable());
                //        var iniziaprimo = w.translateDialogOrNarOrAnnotated("[Inizia il primo ricordo...]".translatable());
                //        var iniziaaltro = w.translateDialogOrNarOrAnnotated("[Inizia un altro ricordo...]".translatable());

                //        var cosifinisce = w.translateDialogOrNarOrAnnotated("[Così finisce il tuo ricordo.]".translatable());

                //        foreach (var nc in ncs.Select((curCs, i) => new { cur_cs = curCs, i }))
                //        {
                //                w.narRoom(nc.i == 0 ? iniziaprimo : iniziaaltro, nc.cur_cs.roomDoveEri, removeIfLast: false);

                //                if (w.curRoom != nc.cur_cs.roomDoveEri)// altrimenti dà fastidio, perché tu sei lì e l'oggetto anche
                //                {
                //                        w.narRoom($"{titr}: {nc.cur_cs.roomDoveEri.translatedNameForMap(xdocObj)}.", nc.cur_cs.roomDoveEri, removeIfLast: false);
                //                }

                //                foreach (var tok in nc.cur_cs.cs)
                //                {
                //                        cs.Add(tok);
                //                }

                //                w.nar(cosifinisce);
                //        }
                //}

            }


            w.clearCurrentCs();

            //

            //lo.wasLookedManually = true; // sia se esisteva handler che se non esisteva




            w.gs = new GameStateCutScene(

                    cs: cs,
                    iCurToken: 0,
                    afterCutSceneShowDialog: null
                    , afterCutSceneWaitForTextInput: null
                    , afterCutSceneGameFinished: null

            );

            return new SegActionRes(w.cur_time)
            {
                nextCutSceneToken = new CutSceneTokenWithTitle
                {
                    cutSceneToken = cs.First()

                },
                room = creaRoomDaDareAlClient(w, saveNames, isTextMode)
            };

        }

        internal static SegActionRes executeActionRemember(string loId, WorldBase w, XDocIndexed xdi, string[] saveNames, bool isTextMode)
        {

            // se hai eseguito un'azione, vuole dire che stavi guardando la stanza, non eri in una cutscene o in un dialogo.
            Debug.Assert(w.gs is GameStateViewingRoom);




            var lo = w.loOfId[loId];

            var loNameTranslatedWithArticle = lo.dynamicNameTranslated(xdi, withThe: true, isForDialog: false);

            var cs = new CutScene(canBeSkipped: true);


            w.setCurrentCs(cs);








            var ncs = w.namedCutScenesSeen.Where(n => n.oggettiMenzionati.Any(om => om is LogicObj && om.to_logic_obj().loId == loId)).ToList();

            if (ncs.isEmpty())
            {
                w.rememberFailedOnObject(lo);

                if (cs.isEmpty())
                {
                    w.beforeExecuteDialogSetAspects(); // all'inizio di ogni dialogo, devo mettere aspect = null, se no partono con l'aspect della room, e ogni dialogo deve sempre dire aspect = null
                                                       //if (lo is Character cha)
                                                       //{
                    var fraseTempl = w.translateDialogOrNarOrAnnotated("Non ricordo niente di particolare su {1}!".translatable(), xdi);
                    var fras = fondiParole(fraseTempl.inst(loNameTranslatedWithArticle), w.CurLang);

                    w.dial(w.ActiveChar, fras);

                    //}
                    //else
                    //{
                    //        w.dial(w.ActiveChar, "Non ricordo niente di particolare su questo!".translatable());
                    //}
                }
            }
            else if (ncs.Count == 1)
            {
                ricordoQualcosaSuQuesto(w, lo, xdi);

                //ricordo_qualcosa_su_questo(w, lo);

                NamedCutScene namedCutScene = ncs.Single();


                var titr = w.translateDialogOrNarOrAnnotated("Ti trovavi qui".translatable(), xdi);

                var cosifin = w.translateDialogOrNarOrAnnotated("[Così finisce il tuo ricordo.]".translatable(), xdi);

                var iniziailtuoric = w.translateDialogOrNarOrAnnotated("[Inizia il tuo ricordo...]".translatable(), xdi);

                w.narRoom(iniziailtuoric, namedCutScene.roomDoveEri, removeIfLast: false);





                if (w.curRoom != namedCutScene.roomDoveEri)// altrimenti dà fastidio, perché tu sei lì e l'oggetto anche
                {
                    //var str_ti_trovavi_qui = "Ti trovavi qui".translatable();
                    w.narRoom($"{titr}: {namedCutScene.roomDoveEri.dynamicNameForMapTranslated(xdi)}.", namedCutScene.roomDoveEri, removeIfLast: false);
                }

                foreach (var tok in namedCutScene.cs)
                {
                    cs.Add(tok);
                }
                w.narText(cosifin);
            }
            else
            {
                ricordoQualcosaSuQuesto(w, lo, xdi);

                //ricordo_qualcosa_su_questo(w, lo);

                var titr = w.translateDialogOrNarOrAnnotated("Ti trovavi qui".translatable(), xdi);
                var iniziaprimo = w.translateDialogOrNarOrAnnotated("[Inizia il primo ricordo...]".translatable(), xdi);
                var iniziaaltro = w.translateDialogOrNarOrAnnotated("[Inizia un altro ricordo...]".translatable(), xdi);

                var cosifinisce = w.translateDialogOrNarOrAnnotated("[Così finisce il tuo ricordo.]".translatable(), xdi);

                foreach (var nc in ncs.Select((curCs, i) => new { cur_cs = curCs, i }))
                {
                    w.narRoom(nc.i == 0 ? iniziaprimo : iniziaaltro, nc.cur_cs.roomDoveEri, removeIfLast: false);

                    if (w.curRoom != nc.cur_cs.roomDoveEri)// altrimenti dà fastidio, perché tu sei lì e l'oggetto anche
                    {
                        w.narRoom($"{titr}: {nc.cur_cs.roomDoveEri.dynamicNameForMapTranslated(xdi)}.", nc.cur_cs.roomDoveEri, removeIfLast: false);
                    }

                    foreach (var tok in nc.cur_cs.cs)
                    {
                        cs.Add(tok);
                    }

                    w.narText(cosifinisce);
                }
            }










            //// prima di tutto vedi se c'e' un look handler
            //var handlerLook = w.lookHandlers.Where(h => h.lo1 == lo).SingleOrDefault();

            //if (handlerLook != null)
            //{

            //        string fullText;

            //        if (handlerLook.DynamicSentence != null)
            //        {
            //                fullText = w.translateDialogOrNarOrAnnotated(handlerLook.DynamicSentence(), xdocObj);
            //        }
            //        else
            //        {
            //                fullText = "_look_";
            //        }

            //        w.pastActions.Add(new PastActionLookRemember(lo, fullText, DateTime.Now));




            //        w.beforeExecuteDialogSetAspects(); // all'inizio di ogni dialogo, devo mettere aspect = null, se no partono con l'aspect della room, e ogni dialogo deve sempre dire aspect = null

            //        var i = new HandlerInput { };



            //        handlerLook.handler(i); // scrive la cutscene, e inoltre può modificare timeMustAdvance e dontEnterDialog e makesNoSenseWithCurrentObjectives



            //        if (cs.isEmpty())
            //        {
            //                if (lo is Character cha)
            //                {
            //                        var fraseLei = "Non ci vedo nient'altro di speciale in lei!".translatable();
            //                        var fraseLui = "Non ci vedo nient'altro di speciale in lui!".translatable();
            //                        w.dial(w.ActiveChar, cha.isMale ? fraseLui : fraseLei);
            //                }
            //                else
            //                {
            //                        w.dial(w.ActiveChar, "Non ci vedo nient'altro di speciale! ".translatable());
            //                }
            //        }


            //        if (i.timeMustAdvance)
            //        {

            //                increaseTimeAndExecuteAfterActionScript(cs, w, theActionWasAMove: false); // modifica la cutcene e potrebbe anche modificare il dialogo dopo.

            //        }


            //}
            //else
            //{
            //        w.beforeExecuteDialogSetAspects(); // all'inizio di ogni dialogo, devo mettere aspect = null, se no partono con l'aspect della room, e ogni dialogo deve sempre dire aspect = null
            //        if (lo is Character cha)
            //        {
            //                var fraseLei = "Non ci vedo niente di speciale in lei!".translatable();
            //                var fraseLui = "Non ci vedo niente di speciale in lui!".translatable();
            //                w.dial(w.ActiveChar, cha.isMale ? fraseLui : fraseLei);

            //        }
            //        else
            //        {
            //                w.dial(w.ActiveChar, "Non ci vedo niente di speciale! ");
            //        }



            //}


            w.clearCurrentCs();

            //

            //lo.wasLookedManually = true; // sia se esisteva handler che se non esisteva




            w.gs = new GameStateCutScene(

                    cs: cs,
                    iCurToken: 0,
                    afterCutSceneShowDialog: null
                    , afterCutSceneWaitForTextInput: null
                    , afterCutSceneGameFinished: null

            );


            string titolo;


            var templTitolo = w.translateDialogOrNarOrAnnotated("Ricorda cos'è {1}".translatable(), xdi);
            titolo = templTitolo.inst(loNameTranslatedWithArticle);

            return new SegActionRes(w.cur_time)
            {
                nextCutSceneToken = new CutSceneTokenWithTitle
                {
                    cutSceneToken = cs.First()
                            ,
                    actionReadable = titolo

                },
                room = creaRoomDaDareAlClient(w, saveNames, isTextMode)
            };

        }

        private static void ricordoQualcosaSuQuesto(WorldBase w, LogicObj lo, XDocIndexed xdi)
        {

            var tra = lo.dynamicNameTranslated(xdi, withThe: true, isForDialog: false);
            var templ = w.translateDialogOrNarOrAnnotated("Ricordo qualcosa su {1}!".translatable(), xdi);

            var full = fondiParole(templ.inst(tra), w.CurLang);
            w.dial(w.activeChar, full);

            //if (lo is Character cha)
            //{
            //        w.dial(w.ActiveChar, cha.isMale ? "Ricordo qualcosa su di lui!" : "Ricordo qualcosa su di lei!");
            //}
            //else
            //{
            //        w.dial(w.ActiveChar, "Ricordo qualcosa su questo! ");
            //}
        }

        //private static void ricordo_qualcosa_su_questo(world_base w, logic_obj lo)
        //{
        //    if (lo is character cha)
        //    {
        //        if (cha.is_male)
        //        {
        //            w.dial(w.active_char, "Ricordo qualcosa su di lui!");
        //        }
        //        else
        //        {
        //            w.dial(w.active_char, "Ricordo qualcosa su di lei!");
        //        }
        //    }
        //    else
        //    {
        //        w.dial(w.active_char, "Ricordo qualcosa su questo oggetto!");

        //    }
        //}







        internal static SegActionRes executeQuickMove(Room roomTarget, WorldBase w, string[] saveNames, XDocIndexed xdocObj, bool isTextMode)
        {

            // se hai eseguito un'azione, vuole dire che stavi guardando la stanza, non eri in una cutscene o in un dialogo.
            //Debug.Assert(w.gs is GameStateViewingRoom); // a volte fallisce perché siamo in gamestatecutscene...



            w.pastActions.Add(new PastActionMove
            {
                dateTime = DateTime.Now,
                room = roomTarget
            });


            // vedo prima di tutto se è una stanza adiacente, nel qual caso mi sposto in modo normale! così sono sicuro che scattano tutte le condizioni
            // di blocco, perché viene chiamato l'handler.
            //var exitsInCurRoom = w.loOfId.Values.Where(lo => lo.isObviousExit)
            //                    .Where(lo => lo.isIn(w.curRoom))


            var roomBeforeMove = w.curRoom;
            TextInput textInputToShow = null;

            var path = w.findShortestPath(w.curRoom, roomTarget);
            var cs = new CutScene(canBeSkipped: false);
            w.setCurrentCs(cs);
            if (path == null)
            {
                w.onWalkPathNotFound(roomTarget);
            }
            else
            {

                // prima di tutto chiamo la funzione che resetta le variabili che devono esistere solo durante il walkpath
                w.beforeWalkPathResetVariables();

                // ora spezzo il percorso in segmenti, ed eseguo tutti i segmenti in sequenza. il motivo è che
                // devo chiamare beforeroomchange per tutti i segmenti, nell'ordine giusto, e fermarmi al primo segmento che dice "stop".
                // invece il room changed handler lo devo chiamare solo per L'ULTIMO segmento.

                for (var curLo = 0; curLo < path.locations.Count - 1; curLo++)
                {
                    var curStartLoc = path.locations[curLo];
                    var curEndLoc = path.locations[curLo + 1];


                    var curSegment = new WalkPath { locations = new[] { curStartLoc, curEndLoc }.ToList() };

                    var i = new BeforeRoomChangeInput { };



                    w.beforeRoomChangeManual(curStartLoc, curEndLoc, curSegment, completePath: path, i: i); // puo' annullare il cambio di room


                    if (i.canChangeRoom)
                    {


                        // il room changed handler va chiamato solo per l'ultimo, a differenza del before roomchanged
                        if (curLo == path.locations.Count - 2)
                        {
                            //var triggerCutScenes =
                            //    //i.doNotTriggerRoomChanged ? TriggerRoomChangeScene.OnlySentenceYouArriveAt : 
                            //    TriggerRoomChangeScene.CallRoomChangedHander;

                            w.changeRoomAux(curEndLoc, out textInputToShow
                                 , addSentenceYouArriveAt: true,
                                 callRoomChangedHandler: true
                                 , xdocObj: xdocObj

                                 , customSentenceYouArriveAt: null
                                 , alsoShowGraphicsInTextMode: false
                                    );
                        }

                    }
                    else
                    {
                        break; // non posso proseguire al prossimo segmento

                        // non mi devo spostare ma solo mostrare la cutscene
                    }
                }

            }
            w.clearCurrentCs();

            //var fraseCompleta = w.quickMove( roomTarget , i.cs);



            //if (i.timeMustAdvance)
            {

                increaseTimeAndExecuteAfterActionScript(cs, w, new MoveActionContext(roomBeforeMove, roomTarget)); // modifica la cutscene e potrebbe anche modificare il dialogo dopo.

            }



            GameStateWaitingForText gameStateWaitingTextDopoCutScene;
            if (textInputToShow != null)
            {
                gameStateWaitingTextDopoCutScene = new GameStateWaitingForText(textInputToShow);
            }
            else
            {
                gameStateWaitingTextDopoCutScene = null;
            }





            if (cs.Any())
            {

                w.AppendAdminNarrativeMessages(cs, null, gameStateWaitingTextDopoCutScene, null);

                w.gs = new GameStateCutScene(

                        cs: cs,
                        iCurToken: 0,
                        afterCutSceneShowDialog: null, // per ora non è previsto che parta un dialogo con scelte appena entri in una stanza, ma solo in seguito ad azione
                        afterCutSceneWaitForTextInput: gameStateWaitingTextDopoCutScene
                        , afterCutSceneGameFinished: null // il gioco non finisce dopo un move, non è previsto
                );

                return new SegActionRes(w.cur_time)
                {
                    nextCutSceneToken = new CutSceneTokenWithTitle
                    {
                        cutSceneToken = cs.First(),


                        actionReadable = null, // fraseCompleta // nell'azione binaria (che è solo walk, non voglio mettere il titolo "cammina verso" perché c'è l'immagine
                    },
                    room = creaRoomDaDareAlClient(w, saveNames, isTextMode)
                };
            }
            else
            {
                // siamo già dopo la cut scene. Ora, vedi se deve passare a room o a dialogo.
                // diciamo che dopo un quick move ci può essere solo cutscene o room . quindi qui è sicuramente room. quindi paso tutti null

                var retv = calcolaActionResTalkORoom(w, null, null, null, saveNames, isTextMode);

                return retv;

            }
        }







        //                internal static SegActionRes executeZeroVerb(ZeroVerb zeroVerb, WorldBase w, string[] saveNames)
        //                {

        //                        // se hai eseguito un'azione, vuole dire che stavi guardando la stanza, non eri in una cutscene o in un dialogo.
        //                        //Debug.Assert(w.gs is GameStateViewingRoom); // è successo che fosse gamestate cutscene







        //                        w.pastActions.Add(new PastActionUn
        //                        {
        //                                dateTime = DateTime.Now,

        //                                zeroVerb = zeroVerb
        //                        });



        //#pragma warning disable CS0168 // Variable is declared but never used
        //                        string fraseCompleta;
        //#pragma warning restore CS0168 // Variable is declared but never used

        //                        /*
        //                                    fraseCompleta = zeroVerb.name;
        //                        */






        //                        var i = new HandlerInput { };
        //                        var cs = new CutScene(canBeSkipped: false);

        //                        //if (zeroVerb.is_talk)
        //                        //{
        //                        //    // chimare talk è come simulare un cambio di camera. partono le stesse scenette
        //                        //    var r = eng.rnd.Next();
        //                        //    var rnd = new RandomInputs
        //                        //    {
        //                        //        rnd10 = r % 10,
        //                        //        rnd2 = r % 2,
        //                        //        rnd3 = r % 3,
        //                        //        rnd4 = r % 4,
        //                        //        rnd5 = r % 5,
        //                        //    };




        //                        //    w.setCurrentCs(cs);
        //                        //    w.onRoomChanged(false, rnd);
        //                        //    w.clearCurrentCs();




        //                        //    // se la onRoomChanged non ha detto niente, scatta la chat context aware tra i personaggi giocanti
        //                        //    if (cs.isEmpty())
        //                        //    {
        //                        //        w.setCurrentCs(cs);
        //                        //        //w.fallback_chat_inter_pc(cs);
        //                        //        w.clearCurrentCs();
        //                        //    }

        //                        //}
        //                        //else
        //                        {
        //                                // è un normale zero verb, come "nasconditi"
        //                                var ha = w.zeroHandlers.SingleOrDefault(h => h.zeroVerb == zeroVerb);
        //                                if (ha != null)
        //                                {
        //                                        w.setCurrentCs(cs);

        //                                        w.beforeExecuteDialogSetAspects(); // all'inizio di ogni dialogo, devo mettere aspect = null, se no partono con l'aspect della room, e ogni dialogo deve sempre dire aspect = null

        //                                        ha.handler(i);
        //                                        w.clearCurrentCs();
        //                                }
        //                                else
        //                                {
        //                                        throw new Exception($"You have defined a zero verb {zeroVerb} but no zeroHandler for that verb.");
        //                                }
        //                        }


        //                        if (i.timeMustAdvance)
        //                        {

        //                                increaseTimeAndExecuteAfterActionScript(cs, w, theActionWasAMove: false); // modifica la cutcene e potrebbe anche modificare il dialogo dopo.

        //                        }



        //                        //
        //                        vediStatoGameTalkOText(i, out GameStateShowingQuestions gameStateTalkDopoLaCutScene, out GameStateWaitingForText gameStateWaitingTextDopoCutScene);




        //                        if (cs.Any())
        //                        {





        //                                w.gs = new GameStateCutScene
        //                                {
        //                                        cs = cs,
        //                                        iCurToken = 0,
        //                                        afterCutSceneShowDialog = gameStateTalkDopoLaCutScene,
        //                                        afterCutSceneWaitForTextInput = gameStateWaitingTextDopoCutScene,
        //                                };

        //                                return new SegActionRes(w.cur_time)
        //                                {
        //                                        nextCutSceneToken = new CutSceneTokenWithTitle
        //                                        {
        //                                                cutSceneToken = cs.First(),


        //                                                actionReadable = null, // fraseCompleta // nell'azione binaria (che è solo walk, non voglio mettere il titolo "cammina verso" perché c'è l'immagine
        //                                        },
        //                                };
        //                        }
        //                        else
        //                        {
        //                                // siamo già dopo la cut scene. Ora, vedi se deve passare a room o a dialogo.


        //                                var retv = calcolaActionResTalkORoom(w, gameStateTalkDopoLaCutScene, gameStateWaitingTextDopoCutScene, saveNames);

        //                                return retv;

        //                        }
        //                }


        internal static SegActionRes executeTalkHere(WorldBase w, string[] saveNames, bool isTextMode)
        {

            // se hai eseguito un'azione, vuole dire che stavi guardando la stanza, non eri in una cutscene o in un dialogo.
            //Debug.Assert(w.gs is GameStateViewingRoom); // è successo che fosse gamestate cutscene







            w.pastActions.Add(new PastActionTalkHere
            {
                dateTime = DateTime.Now,
            });



#pragma warning disable CS0168 // Variable is declared but never used
            string fraseCompleta;
#pragma warning restore CS0168 // Variable is declared but never used

            /*
                        fraseCompleta = zeroVerb.name;
            */






            var i = new HandlerInput { };
            var cs = new CutScene(canBeSkipped: false);

            {
                w.setCurrentCs(cs);
                // prima di tutto vedo se c'è la frase "non è il caso di parlare ora"
                //if (w.cannotTalkNow(w.curRoom)) // scrive cutscene
                //{
                //        // la cutscene "non è il caso" è stata scritta
                //        w.cutSceneCannotTalkNow();
                //}
                //else
                {


                    // è un normale zero verb, come "nasconditi"
                    var ha = w.talkHereHandlers.SingleOrDefault(h => h.room == w.curRoom);
                    if (ha != null)
                    {


                        w.beforeExecuteDialogSetAspects(); // all'inizio di ogni dialogo, devo mettere aspect = null, se no partono con l'aspect della room, e ogni dialogo deve sempre dire aspect = null

                        ha.handler(i);

                    }
                    else
                    {
                        throw new Exception($"You have called talk here in room {w.curRoom} but no talkHandler for that room.");
                    }
                }
                w.clearCurrentCs();
            }


            if (i.timeMustAdvance)
            {

                increaseTimeAndExecuteAfterActionScript(cs, w, new TalkHereActionContext(w.curRoom)); // modifica la cutscene e potrebbe anche modificare il dialogo dopo.

            }



            //
            vediStatoGameTalkOText(i, out GameStateShowingQuestions gameStateTalkDopoLaCutScene, out GameStateWaitingForText gameStateWaitingTextDopoCutScene
                    , out GameStateFinished gameStateFin);




            if (cs.Any())
            {





                w.AppendAdminNarrativeMessages(cs, gameStateTalkDopoLaCutScene, gameStateWaitingTextDopoCutScene, gameStateFin);

                w.gs = new GameStateCutScene
                (
                        cs: cs,
                        iCurToken: 0,
                        afterCutSceneShowDialog: gameStateTalkDopoLaCutScene,
                        afterCutSceneWaitForTextInput: gameStateWaitingTextDopoCutScene
                        , afterCutSceneGameFinished: gameStateFin
                );

                return new SegActionRes(w.cur_time)
                {
                    nextCutSceneToken = new CutSceneTokenWithTitle
                    {
                        cutSceneToken = cs.First(),


                        actionReadable = null, // fraseCompleta // nell'azione binaria (che è solo walk, non voglio mettere il titolo "cammina verso" perché c'è l'immagine


                    },

                    room = creaRoomDaDareAlClient(w, saveNames, isTextMode)
                };
            }
            else
            {
                // siamo già dopo la cut scene. Ora, vedi se deve passare a room o a dialogo.


                var retv = calcolaActionResTalkORoom(w, gameStateTalkDopoLaCutScene, gameStateWaitingTextDopoCutScene, gameStateFin, saveNames, isTextMode);

                return retv;

            }
        }


        //internal static SegActionRes executePuzzleSolution(Objective pu, PuzzleSolutionPieceSentByClient[] solutionSent, WorldBase w, string[] saveNames, XDocIndexed xdocObj)
        //{




        //        // se hai eseguito un'azione, vuole dire che stavi guardando la stanza, non eri in una cutscene o in un dialogo.
        //        //Debug.Assert(w.gs is GameStateViewingRoom); // è successo che fosse gamestate cutscene





        //        //w.pastActions.Add(new PastActionUn
        //        //{
        //        //        dateTime = DateTime.Now,

        //        //        zeroVerb = zeroVerb
        //        //});



        //        string fraseCompleta;



        //        var inOrderTo = w.translateSentenceWithIdFromObjfile(strToTranslate: "per", xelementName: "in_order_to", xdocObj: xdocObj?.Xdoc);
        //        var fraseParz = "{1} {2}".inst(inOrderTo).inst(pu.translated_name(xdocObj));

        //        //Qtok[] allPuzzleTokens = w.puzzleSolvedHandlers.Select(ha => ha.puzzleSolution.solution).Flatten().Distinct().ToList();

        //        var seqfrase = solutionSent.Select(qt => qt.psi_readableName).aggregateStringList(sep: " ");


        //        //var testFrase = "guarda in l'albero perche' ciao ciao";
        //        //testFrase = fondiParole(testFrase);

        //        seqfrase = fondiParole(seqfrase);

        //        fraseCompleta = $"{fraseParz}, {seqfrase}";



        //        w.pastActions.Add(new PastActionSolvePuzzle
        //        {
        //                dateTime = DateTime.Now,
        //                Solution = fraseCompleta
        //        });


        //        var i = new HandlerInput { };
        //        var cs = new CutScene(canBeSkipped: false);

        //        bool matcha(List<PuzzleToken> solutionCur, List<PuzzleSolutionPieceSentByClient> selectedByUserCur)
        //        {
        //                if (selectedByUserCur.isEmpty() && solutionCur.isEmpty())
        //                {
        //                        return true;
        //                }
        //                else
        //                {
        //                        if (selectedByUserCur.First().isEnu && solutionCur.First() is EnumeratedToken ent && selectedByUserCur.First().qt_serId == ent.correct.serId)
        //                        {
        //                                return matcha(solutionCur.Skip(1).ToList(), selectedByUserCur.Skip(1).ToList());
        //                        }
        //                        else if (!selectedByUserCur.First().isEnu && solutionCur.First() is ObjInRoomToken ort && selectedByUserCur.First().oir_loIdCorrect == ort.correct.loId)
        //                        {
        //                                return matcha(solutionCur.Skip(1).ToList(), selectedByUserCur.Skip(1).ToList());
        //                        }
        //                        else
        //                        {
        //                                return false;
        //                        }
        //                }



        //        }
        //        // è un normale zero verb, come "nasconditi"
        //        var frasiConQuellObiettivo = w.puzzleSolvedHandlersOldUi.Where(h => h.puzzleSolution.objective.serId == pu.serId).ToList();

        //        var frasiCheMatchano = frasiConQuellObiettivo
        //                .Where(fr => matcha(fr.puzzleSolution.solution.ToList(), solutionSent.ToList()))
        //                .ToList();


        //        void makesNoSense()
        //        {
        //                List<PuzzleToken> sol2 = new List<PuzzleToken>();
        //                foreach (var s in solutionSent)
        //                {
        //                        if (s.oir_loIdCorrect != null)
        //                        {
        //                                var lo = w.loOfId[s.oir_loIdCorrect];
        //                                sol2.Add(w.ort(lo));

        //                        }
        //                        else if (s.qt_serId != null)
        //                        {
        //                                var qt = w.qtokOfId[s.qt_serId];
        //                                sol2.Add(w.ert(qt));
        //                        }
        //                        else
        //                        {
        //                                throw new Exception("kfjdkfdj");
        //                        }
        //                }
        //                w.processWrongSolution(pu, pu.translated_name(xdocObj), sol2.ToArray(), xdocObj);


        //                //var cyc = w.startCycle(x => {
        //                //        w.nar("Questo non ha senso!");
        //                //});

        //                //w.execNextInCycle

        //                //var nonVedi = w.translateSentenceWithIdFromObjfile(strToTranslate: "Questo non ha senso!", xelementName: "this_makes_no_sense", xdocObj: xdocObj?.Xdoc);

        //                //w.nar(nonVedi.inst(pu.translated_name(xdocObj)));

        //        }

        //        w.setCurrentCs(cs);


        //        // prima di tutto devo rifiutarmi di compiere l'azione se ci sono condizioni di urgenza o di prigionia (dato che l'azione potrebbe contenere changeRoom che ti farebbero
        //        // superare la prigionia)

        //        w.beforeActionExecuted(pu, w.curRoom, out bool canceled);

        //        if (!canceled)
        //        {


        //                if (frasiCheMatchano.isEmpty())
        //                {
        //                        makesNoSense();

        //                }
        //                else
        //                {

        //                        var ha = frasiCheMatchano.Single();


        //                        w.beforeExecuteDialogSetAspects(); // all'inizio di ogni dialogo, devo mettere aspect = null, se no partono con l'aspect della room, e ogni dialogo deve sempre dire aspect = null

        //                        ha.handler(i);

        //                        if (i.makesNoSenseAtThisTime == true)
        //                        {
        //                                makesNoSense();
        //                        }

        //                }
        //        }
        //        w.clearCurrentCs();



        //        if (i.timeMustAdvance)
        //        {

        //                increaseTimeAndExecuteAfterActionScript(cs, w, theActionWasAMove: false); // modifica la cutcene e potrebbe anche modificare il dialogo dopo.

        //        }



        //        //
        //        vediStatoGameTalkOText(i, out GameStateShowingQuestions gameStateTalkDopoLaCutScene, out GameStateWaitingForText gameStateWaitingTextDopoCutScene);




        //        if (cs.Any())
        //        {


        //                w.gs = new GameStateCutScene
        //                {
        //                        cs = cs,
        //                        iCurToken = 0,
        //                        afterCutSceneShowDialog = gameStateTalkDopoLaCutScene,
        //                        afterCutSceneWaitForTextInput = gameStateWaitingTextDopoCutScene,
        //                };

        //                return new SegActionRes(w.cur_time)
        //                {
        //                        nextCutSceneToken = new CutSceneTokenWithTitle
        //                        {
        //                                cutSceneToken = cs.First(),


        //                                actionReadable = fraseCompleta
        //                        },
        //                };
        //        }
        //        else
        //        {
        //                // siamo già dopo la cut scene. Ora, vedi se deve passare a room o a dialogo.


        //                var retv = calcolaActionResTalkORoom(w, gameStateTalkDopoLaCutScene, gameStateWaitingTextDopoCutScene, saveNames);

        //                return retv;

        //        }
        //}

        //internal static SegActionRes executePuzzleSolutionAuto(Objective pu, WorldBase w, string[] saveNames, XDocIndexed xdocObj)
        //{




        //        // se hai eseguito un'azione, vuole dire che stavi guardando la stanza, non eri in una cutscene o in un dialogo.
        //        //Debug.Assert(w.gs is GameStateViewingRoom); // è successo che fosse gamestate cutscene







        //        //w.pastActions.Add(new PastActionUn
        //        //{
        //        //        dateTime = DateTime.Now,

        //        //        zeroVerb = zeroVerb
        //        //});



        //        string fraseCompleta;



        //        var inOrderTo = w.translateSentenceWithIdFromObjfile(strToTranslate: "Fai qualcosa per", xelementName: "do_something_to", xdocObj: xdocObj?.Xdoc);
        //        fraseCompleta = "{1} {2}".inst(inOrderTo).inst(pu.translated_name(xdocObj));




        //        var i = new HandlerInput { };
        //        var cs = new CutScene(canBeSkipped: false);

        //        // è un normale zero verb, come "nasconditi"
        //        var handlerDaEseguire = w.autoSolvePuzzleHandlers.Where(h => h.objective.serId == pu.serId).SingleOrDefault();



        //        //i.timeMustAdvance = verb != talkTo.i && (!verbsForWhichTimeDoesNotAdvance.Contains(verb)); // per default il tempo avanza sempre tranne per look.


        //        w.setCurrentCs(cs);


        //        // prima di tutto devo rifiutarmi di compiere l'azione se ci sono condizioni di urgenza o di prigionia (dato che l'azione potrebbe contenere changeRoom che ti farebbero
        //        // superare la prigionia)

        //        w.beforeActionExecuted(pu, w.curRoom, out bool canceled);

        //        if (!canceled)
        //        {


        //                if (handlerDaEseguire == null)
        //                {
        //                        //makesNoSense();
        //                        w.processWrongSolutionAuto(pu, pu.translated_name(xdocObj), xdocObj);
        //                        //throw new Exception("fkdjfvk");

        //                }
        //                else
        //                {




        //                        w.beforeExecuteDialogSetAspects(); // all'inizio di ogni dialogo, devo mettere aspect = null, se no partono con l'aspect della room, e ogni dialogo deve sempre dire aspect = null

        //                        handlerDaEseguire.handler(i);

        //                        if (i.makesNoSenseAtThisTime == true)
        //                        {
        //                                w.processWrongSolutionAuto(pu, pu.translated_name(xdocObj), xdocObj);
        //                                //makesNoSense();
        //                                //throw new Exception("£jiugbtj"); // todo succede
        //                        }

        //                }
        //        }
        //        w.clearCurrentCs();



        //        if (i.timeMustAdvance)
        //        {

        //                increaseTimeAndExecuteAfterActionScript(cs, w, theActionWasAMove: false); // modifica la cutcene e potrebbe anche modificare il dialogo dopo.

        //        }



        //        //
        //        vediStatoGameTalkOText(i, out GameStateShowingQuestions gameStateTalkDopoLaCutScene, out GameStateWaitingForText gameStateWaitingTextDopoCutScene);




        //        if (cs.Any())
        //        {


        //                w.gs = new GameStateCutScene
        //                {
        //                        cs = cs,
        //                        iCurToken = 0,
        //                        afterCutSceneShowDialog = gameStateTalkDopoLaCutScene,
        //                        afterCutSceneWaitForTextInput = gameStateWaitingTextDopoCutScene,
        //                };

        //                return new SegActionRes(w.cur_time)
        //                {
        //                        nextCutSceneToken = new CutSceneTokenWithTitle
        //                        {
        //                                cutSceneToken = cs.First(),


        //                                actionReadable = fraseCompleta
        //                        },
        //                };
        //        }
        //        else
        //        {
        //                // siamo già dopo la cut scene. Ora, vedi se deve passare a room o a dialogo.


        //                var retv = calcolaActionResTalkORoom(w, gameStateTalkDopoLaCutScene, gameStateWaitingTextDopoCutScene, saveNames);

        //                return retv;

        //        }
        //}

        private static string fondiParole(string seqfrase, string lang)
        {

            if (lang != null) // non italiano
            {
                return seqfrase;
            }

            var spl = seqfrase.Split(new[] { " ", "\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var patterns = new PatternReplace[]
            {
                                new PatternReplace("in", "la", "nella"),
                                   new PatternReplace("in", "il", "nel"),
                                   new PatternReplace("in", "lo", "nello"),
                                   new PatternReplace("in", "le", "nelle"),
                                   new PatternReplace("in", "i", "nei"),
                                   new PatternReplace("in", "gli", "negli"),
                                   new PatternReplace("in", "l'", "nell'",matchStartOfSecondWord: true), // non funziona se nonmatcho l'inizio della seconda parola, la parola è    "l'albero"


                                   new PatternReplace("su", "la", "sulla"),
                                   new PatternReplace("su", "il", "sul"),
                                   new PatternReplace("su", "lo", "sullo"),
                                   new PatternReplace("su", "le", "sulle"),
                                   new PatternReplace("su", "i", "sui"),
                                   new PatternReplace("su", "gli", "sugli"),
                                   new PatternReplace("su", "l'", "sull'",matchStartOfSecondWord: true),

                                new PatternReplace("da", "la", "dalla"),
                                   new PatternReplace("da", "il", "dal"),
                                   new PatternReplace("da", "lo", "dallo"),
                                   new PatternReplace("da", "le", "dalle"),
                                   new PatternReplace("da", "i", "dai"),
                                   new PatternReplace("da", "gli", "dagli"),
                                   new PatternReplace("da", "l'", "dall'",matchStartOfSecondWord: true),


                                new PatternReplace("di", "la", "della"),



                                new PatternReplace("a", "il", "al"),
                                new PatternReplace("a", "gli", "agli"),
                                new PatternReplace("a", "lo", "allo"),
                                new PatternReplace("a", "la", "alla"),
                                new PatternReplace("a", "le", "alle"),
                                new PatternReplace("a", "i", "ai"),
                                new PatternReplace("a", "l'", "all'", matchStartOfSecondWord: true)
            };

            for (var j = 0; j < spl.Count - 1; j++)
            {
                var cur = spl[j];
                var next = spl[j + 1]; // potrebbe essere   "l'albero"

                if (patterns.Any(p => !p.MatchStartOfSecondWord && p.Word1 == cur && p.Word2 == next, out PatternReplace pa))
                {
                    spl.RemoveAt(j);
                    spl.RemoveAt(j);
                    spl.Insert(j, pa.Repl);
                }
                else if (patterns.Any(p => p.MatchStartOfSecondWord && p.Word1 == cur && next.StartsWith(p.Word2), out PatternReplace pa2))
                {
                    spl.RemoveAt(j); // toglie in
                    spl.RemoveAt(j); // toglie l'albero

                    var nellalbero = next.Replace(pa2.Word2, pa2.Repl);
                    spl.Insert(j, nellalbero);
                }
            }

            seqfrase = spl.aggregateStringList(sep: " ");
            return seqfrase;
        }










        //public static actionRes2 executeUnaryAction(logicObjE lo,  worldE w) 
        //{

        //    // se hai eseguito un'azione, vuole dire che stavi guardando la stanza, non eri in una cutscene o in un dialogo.
        //    Debug.Assert(w.gs is gameStateViewingRoom);




        //    // prima di eseguire la logica dell'azione, calcolo la frase completa. lo devo fare prima perché l'azione potrebbe settare "conosce il nome di mark = true",
        //    // e quindi la frase completa apparirebbe erroneamente con "parla con mark" anziché "parla con sconosciuto".
        //    string fraseCompleta;


        //    fraseCompleta = "Vai verso {1}".tr().inst(lo.name);







        //    var i = new handlerInput { };


        //    var ha = w.unaryHandlers.FirstOrDefault(h => h.containsObj(lo) );

        //    if (ha != null)
        //    {

        //        //i.timeMustAdvance = verb != talkTo.i && (!verbsForWhichTimeDoesNotAdvance.Contains(verb)); // per default il tempo avanza sempre tranne per look.


        //        ha.handler(i); // scrive la cutscene, e inoltre può modificare timeMustAdvance e dontEnterDialog


        //    }
        //    else
        //    {


        //        i.eng.nar("Non posso andare lì.".tr()));




        //    }



        //    if (i.timeMustAdvance)
        //    {


        //        eng.increaseTimeAndUpdateNpcSchedules(i, w); // modifica la cutcene e potrebbe anche modificare il dialogo dopo.

        //    }





        //    //
        //    gameStateShowingQuestions gameStateTalkDopoLaCutScene;
        //    if (i.dialogToStart != null)
        //    {

        //        gameStateTalkDopoLaCutScene = new gameStateShowingQuestions
        //        {
        //            dialog = i.dialogToStart
        //        };
        //    }
        //    else
        //    {
        //        gameStateTalkDopoLaCutScene = null;
        //    }

        //    //




        //    if (i.cs.Any())
        //    {





        //        w.gs = new gameStateCutScene
        //        {
        //            cs = i.cs.ToArray(),
        //            iCurToken = 0,
        //            afterCutSceneShowDialog = gameStateTalkDopoLaCutScene,

        //        };

        //        return new actionRes2
        //        {
        //            nextCutSceneToken = new cutSceneTokenWithTitle { cutSceneToken = i.cs.First(), actionReadable = fraseCompleta },
        //        };
        //    }
        //    else
        //    {
        //        // siamo già dopo la cut scene. Ora, vedi se deve passare a room o a dialogo.


        //        var retv = calcolaActionResTalkORoom(w, gameStateTalkDopoLaCutScene);

        //        return retv;

        //    }
        //}

















        //internal static seg_action_res waitOneTurn(world_base w)
        //{
        //    Debug.Assert(w.gs is game_state_viewing_room);


        //    var i = new handler_input { };
        //    var cs = new cut_scene();

        //    eng.nar("Passa del tempo.".tr(), cs);



        //    eng.increaseTimeAndExecuteAfterActionScript(cs, w);


        //    game_state_showing_questions gameStateTalkDopoLaCutScene;
        //    if (i.dialogToStart != null)
        //    {
        //        gameStateTalkDopoLaCutScene = new game_state_showing_questions
        //        {
        //            dialog = i.dialogToStart
        //        };
        //    }
        //    else
        //    {
        //        gameStateTalkDopoLaCutScene = null;
        //    }

        //    //



        //    w.gs = new game_state_cut_scene
        //    {
        //        cs = cs.ToArray(),
        //        iCurToken = 0,
        //        afterCutSceneShowDialog = gameStateTalkDopoLaCutScene,

        //    };

        //    return new seg_action_res
        //    {
        //        nextCutSceneToken = new cut_scene_token_with_title { cutSceneToken = cs.First(), actionReadable = "Attendi".tr() },
        //    };


        //}


        /// <summary>
        /// needed for parseComplexScene.  todo fai in modo che non debba essere public.
        /// </summary>
        //public static Func<string, string, string> imgPathOfCharName;

        //public static void parseComplexCutScene(List<CutSceneToken> cs, string txt)
        //{


        //    var pars = txt.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

        //    var patternConEmozione = @"(^[A-Z ]+)(\(.*\))\.(.+)";
        //    var pattern = @"(^[A-Z ]+\.)(.+)";
        //    foreach (var pa in pars)
        //    {
        //        var parTrimmed = pa.Trim();
        //        var chEmo = Regex.Match(parTrimmed, patternConEmozione);
        //        if (chEmo.Groups.Count > 1)
        //        {
        //            var nomePer = chEmo.Groups[1].Value;
        //            var emoz = chEmo.Groups[2].Value;
        //            var testo = chEmo.Groups[3].Value;

        //            nomePer = nomePer.Trim() + ".";

        //            var imgPath = imgPathOfCharName(nomePer, emoz);
        //            cs.Add(new dialog_token { charName = nomePer, par = testo, img = imgPath });
        //        }
        //        else
        //        {
        //            var ch = Regex.Match(parTrimmed, pattern);

        //            if (ch.Groups.Count == 3)
        //            {
        //                var chname = ch.Groups[1].Value;
        //                var text = ch.Groups[2].Value;


        //                var imgPath = imgPathOfCharName(chname, null);
        //                cs.Add(new dialog_token { charName = chname, par = text, img = imgPath });

        //            }
        //            else
        //            {
        //                cs.Add(new nar_token { par = pa });
        //            }
        //        }

        //    }

        //}




        internal static RoomDataEditor ParseCoordFile(Room room, WorldBase wo, out bool roomDataFileNotPresent)
        {


            //double scale;

            //var scaleFilePath = System.Web.Hosting.HostingEnvironment.MapPath($"~\\img\\littlegirl\\{room.assetFolderName}\\scal-1900.txt");

            //var scaleTxt = System.IO.File.ReadAllText(scaleFilePath);
            //scale = double.Parse(scaleTxt, CultureInfo.InvariantCulture);


            var rootFolderForGraphicsFullPath = Utils.MapPathCrossHost($"~/{wo.graphicsRootFolderName()}");

            var folderForRoom = Path.Combine(rootFolderForGraphicsFullPath, room.assetFolderName);

            var fileNameEditor = Path.Combine(folderForRoom, "layer_data.json");
Console.WriteLine("[Segusum] ParseCoordFile " + fileNameEditor + " exists=" + File.Exists(fileNameEditor));



            //var fileNameHires = Path.Combine(folderForRoom, "layer_data_hires.txt");

            string fileTextJsonStr_editor;
            try
            {
                fileTextJsonStr_editor = File.ReadAllText(fileNameEditor);

            }
            catch (DirectoryNotFoundException)
            {
                roomDataFileNotPresent = true;
return null;
            }
            catch (FileNotFoundException)
            {
                // qui dovrei lanciare eccezione, ma non posso altrimenti sono costretto a mettere dall'inizio tutta la grafica di tutte le rooms.
                roomDataFileNotPresent = true;
return null;
            }
            var struEditor = JsonSerializer.Deserialize<RoomDataEditor>(fileTextJsonStr_editor);


            //KritaImgInfo? struHi;

            //if (fileTextJsonStrHires != null)
            //{
            //        struHi = DeserializeObject<KritaImgInfo>(fileTextJsonStrHires);
            //}
            //else
            //{
            //        struHi = null;
            //}

            //var posOfLayer = new Dictionary<string, LayerInfoParsed>();

            //foreach (var la in stru.layers)
            //{
            //    var layerFilename = Path.GetFileName(la.fullName);


            //    var pos = new RectSeg(la.x, la.y, la.wt, la.ht);
            //    posOfLayer[layerFilename] = new LayerInfoParsed(pos, false);



            //}



            //if (struHi != null)
            //{
            //        foreach (var la in struHi.layers)
            //        {
            //                var layerFilename = Path.GetFileName(la.fullName);



            //                var pos = new RectSeg(la.x / 3, la.y / 3, la.wt / 3, la.ht / 3);
            //                posOfLayer[layerFilename] = new LayerInfoParsed(pos, true);




            //        }
            //}
            //var coordsFileFinalPartOfPath = room.assetFolderName.Replace("-assets", "-coords.txt");  // witch-house-coords.txt

            //var fullpath = System.Web.Hosting.HostingEnvironment.MapPath($"~\\coords\\{coordsFileFinalPartOfPath}");

            //var testfullpath = System.Web.Hosting.HostingEnvironment.MapPath($"~\\img\\{coordsFileFinalPartOfPath}");

            //var lines = File.ReadAllLines(fullpath);



            //foreach (var ll in lines.add_indices())
            //{
            //        var line = ll.el;

            //        var spl = line.Split(new[] { "---" }, StringSplitOptions.None);
            //        var fileName = spl[0];
            //        if (fileName.EndsWith(".png") || fileName.EndsWith(".jpg")) // così mi costringo a dare nomi corretti in photoshop
            //        {
            //                var coords = spl[1];
            //                var c2 = coords.Split(',');
            //                var x0 = c2[0];
            //                var x = double.Parse(x0.Substring(1), CultureInfo.InvariantCulture) * scale;
            //                var y = double.Parse(c2[1].Substring(1), CultureInfo.InvariantCulture) * scale;
            //                var w = double.Parse(c2[2].Substring(1), CultureInfo.InvariantCulture) * scale;
            //                var h = double.Parse(c2[3].Substring(1), CultureInfo.InvariantCulture) * scale;

            //                var pos = new Rect(new Point(x, y), new Size(w, h));
            //                posOfLayer[fileName] = pos;
            //        }
            //}

            //room.LastTimeParsedCoordFile = DateTime.Now;

            //Utils.printToLogGeneric($"reparsed coord file for room {room.roomId}", "parseCoordFile");

            roomDataFileNotPresent = false;
            return struEditor;
        }
        //public static string strArrivederci(wo w)
        //{
        //    var arr = "Arrivederci, {1}.".tr().inst(w.)
        //}






        //void stats()
        //{
        //        var db = new segusumDb();
        //        var q = (from s in db.savegame
        //                 join u in db.user on s.idUser equals u.id

        //                 select new { user = u.uname, xml = s.savegameXml, date = s.dateModified }
        //        )
        //        .OrderByDescending(x => x.date)

        //        .Take(5)
        //        .ToList();
        //}




        public static EndGameStuffClient traduciEndGameStuff(WorldBase w, EndGameStuffClient untr)
        {
            var xdi = w.getXdocObjIndexedCached();
            return new EndGameStuffClient(
                untr.egsImg,
                untr.egsCredits.Select(x => w.translateDialogOrNarOrAnnotated(x, xdi)).ToArray());
        }

    } // class



    //public class resultOnlyRoomDesc
    //{


    //    public getRoomRes roomDesc;


    //}


    //public class verbInfo
    //{
    //    public string verbId;

    //    public bool isUnary;

    //    public bool invertObjectOrder;

    //    public string secondPart;
    //    public string firstPartForSentence;
    //    public string stringForContextMenu;

    //}

    //public class topicInfo
    //{
    //    public string topicId;
    //    public string questionText;
    //}

    //public class objTopicInfo
    //{
    //    //public string loId; // se parli con questo personaggio, allora i topic sono questi:


    //    public string topicId;

    //    public string questionText; // the text is different for every character you ask. Example: "{1}, how are you?"

    //    public override bool Equals(object obj)
    //    {
    //        var ot = obj as objTopicInfo;
    //        if (ot != null)
    //        {
    //            return ot.topicId == topicId && ot.questionText == questionText;
    //        }
    //        return false;
    //    }

    //    public override int GetHashCode()
    //    {
    //        return
    //            //(loId.GetHashCode().ToString() + 
    //            (topicId.GetHashCode().ToString() + questionText.GetHashCode().ToString()).GetHashCode();
    //    }

    //} // end class




}
