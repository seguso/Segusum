using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Linq;


// ReSharper disable ReplaceWithSingleCallToFirstOrDefault

namespace Seg
{
        //public class charCurrentAspect
        //{
        //    public pose Pose;
        //    public bool hasHat;
        //    public bool hasSwimsuit;
        //    public string filePath;
        //}
        //public class howsItGoingInput
        //{
        //    public List<cutSceneToken> cs;
        //    public Character whosAsking;
        //}



        public class Character : LogicObj
        {

                public string nameForDialog;

                //public string imgDefault;

              

                public bool isMale;



                public Character()
                {
                        //useWith = UseMode.UseWith;  // characters hanno un default diverso, use with

                        UseKindWhenInRoom = UseKindForRoomObjects.Deduce; // per default i personaggi hanno deduce, non use for o use here
                }


                //public bool isInCurParty =>

                //    wo.curParty.Contains(this);




                //public override string objDet => wo.ac.howHeCallsSomeoneElseAsSubject(this, det: true);
                //public override string subjDet => wo.ac.howHeCallsSomeoneElseAsSubject(this, det: true);


                //public override string subjInd()
                //{
                //    return wo.ac.howHeCallsSomeoneElseAsSubject(this, det: false);
                //}


                /// <summary>
                /// cosa deve succedere se un npc ti chiede come stai in un dato momento.
                /// </summary>
                /// <param name="i"></param>
                //public abstract void howsItGoing(howsItGoingInput i);


                // cose da serializzare


                //internal int howManyTimesYouLookedAtHim = 0;

                //internal Dictionary<strangeObjectCarriedSeen, strangeObjectData> memoryStrangeObjects = new Dictionary<strangeObjectCarriedSeen, strangeObjectData>();

                internal List<LogicObj> inv = new();
                //internal List<LogicObj> mind = new List<LogicObj>();

                ///// <summary>
                ///// if it is a playing character at this time, this must not be null
                ///// </summary>
                //public pc asPc;
                ///// <summary>
                ///// non null if it is a npc at this time
                ///// </summary>
                //public npc asNpc;


                public bool hasObject(LogicObj lo)
                {
                        return inv.Contains(lo);
                }

                public int? positionOfObjectInInv(LogicObj lo)
                {
                        if (hasObject(lo))
                        {
                                return inv.IndexOf(lo);
                        }
                        else
                        {
                                return null;
                        }

                }












                /// <summary>
                ///  you need to provide an array of all dialogs, in order for the engine to serialize and restore their state
                /// </summary>
                //public abstract dialog[] dialogsToSerialize { get; }




                internal override void serialize(XElement xelch)
                {

                        //var strangeList = memoryStrangeObjects.ToList();
                        //foreach (var pair in strangeList)
                        //{
                        //    var elStrange = new XElement("strangeObjSeen");
                        //    xelch.Add(elStrange);

                        //    var strangeObj = pair.Key;
                        //    var strangeObjData = pair.Value;
                        //    elStrange.Add(new XAttribute("obj", strangeObj.pi.loId));
                        //    elStrange.Add(new XAttribute("whoWasCarryingIt", strangeObj.whoWasCarryingIt.loId));
                        //    elStrange.Add(new XAttribute("timeLastSeen", strangeObjData.timeLastSeen));
                        //}




                        //xelch.Add(new XAttribute("howManyTimesYouLookedAtHim", howManyTimesYouLookedAtHim));

                        //if (chairWhereHeIsSitting != null)
                        //    xelch.Add(new XAttribute("chairWhereHeIsSitting", chairWhereHeIsSitting.loId));


                        foreach (var i in inv)
                        {
                                var elInv = new XElement("inv");
                                xelch.Add(elInv);


                                elInv.Add(new XAttribute("loId", i.loId));
                        }

                        // lo stato dei dialoghi va serializzato. quali domande sono state chieste e quali no.
                        //foreach (var d in dialogsToSerialize)
                        //{
                        //    eng.serializzaDialogoToXml(xelch, d);
                        //}


                        //if (asPc != null)
                        //{
                        //    var elPc = new XElement("pc");
                        //    xelch.Add(elPc);

                        //foreach (var conc in mind)
                        //{
                        //        var elConc = new XElement("mind");
                        //        xelch.Add(elConc);

                        //        elConc.Add(new XAttribute("loId", conc.loId));
                        //}


                        // todo serializza obiettivi
                        //}


                        //if (asNpc != null)
                        //{
                        //    // serializzo lo stato 



                        //    var elNpc = new XElement("npc");
                        //    xelch.Add(elNpc);




                        //    foreach(var p in asNpc.timeILastSawHim)
                        //    {
                        //        var elTime = new XElement("timeILastSawHim");
                        //        elNpc.Add(elTime);

                        //        elTime.Add(new XAttribute("charLoId", p.Key.loId));
                        //        elTime.Add(new XAttribute("time", p.Value));

                        //    }




                        //    // serializzo la schedule



                        //    //var sched = asNpc.schedule;

                        //    //Debug.Assert(sched != null); // è obbligatoria la schedule, altrimenti non puoi neanche guardarlo...

                        //    //var assembly = sched.GetType().Assembly;
                        //    //var fullname = sched.GetType().FullName;
                        //    //elNpc.Add(new XAttribute("assembly", assembly));
                        //    //elNpc.Add(new XAttribute("fullname", fullname));

                        //    //sched.serialize(elNpc);

                        //}

                        base.serialize(xelch);
                }


                internal override void deserialize(XElement el, out bool saveGameInvalid)
                {



                        //memoryStrangeObjects.Clear();
                        //foreach (var xelStrangeObj in el.Elements("strangeObjSeen"))
                        //{
                        //    var piLoId = xelStrangeObj.Attribute("obj").Value;
                        //    var whoCarriedItLoId = xelStrangeObj.Attribute("whoWasCarryingIt").Value;
                        //    var timeLastSeen = ulong.Parse(xelStrangeObj.Attribute("timeLastSeen").Value);

                        //    var pi = wo.loOfloId[piLoId];
                        //    var whoCarriedIt = (Character)wo.loOfloId[whoCarriedItLoId];

                        //    var key = new strangeObjectCarriedSeen
                        //    {
                        //        pi = pi,
                        //        whoWasCarryingIt = whoCarriedIt,
                        //    };
                        //    var data = new strangeObjectData
                        //    {
                        //        timeLastSeen = timeLastSeen,
                        //    };

                        //    memoryStrangeObjects.Add(key, data);
                        //}








                        //howManyTimesYouLookedAtHim = int.Parse(el.Attribute("howManyTimesYouLookedAtHim").Value);



                        inv.Clear();
                        foreach (var elInv in el.Elements("inv"))
                        {
                                var loIdOgg = elInv.Attribute("loId").Value;
                                if (wo.loOfId.ContainsKey(loIdOgg))
                                {
                                        var lo = wo.loOfId[loIdOgg];
                                        inv.Add(lo);
                                }
                                else
                                {
                                        // ho cambiato il loid di qualcuno, quindi nel resx non c'e'... speriamo che una invariant condiion lo rimetta.
                                }
                        }




                        //foreach(var elDial in el.Elements("dialog"))
                        //{
                        //    var dialogId = elDial.Attribute("id").Value;
                        //    var dial = dialogsToSerialize.Single(d => d.id == dialogId);

                        //    eng.deserializzaDialogoDaXel(elDial, dial);
                        //}


                        //var elPc = el.Element("pc");
                        //if (elPc != null)
                        //{
                        //    asPc = new pc(this);

                        //foreach (var elMind in el.Elements("mind"))
                        //{
                        //        var loId2 = elMind.Attribute("loId").Value;
                        //        var lo = wo.loOfId[loId2];

                        //        mind.Add(lo);
                        //}

                        // todo deserializza obiettivi
                        //}


                        //var elNpc = el.Element("npc");
                        //if (elNpc != null)
                        //{
                        //    //var fullname = elNpc.Attribute("fullname").Value;
                        //    //var assembly = elNpc.Attribute("assembly").Value;

                        //    asNpc = new npc(this);





                        //    asNpc.timeILastSawHim.Clear();
                        //    foreach(var elT in elNpc.Elements("timeILastSawHim"))
                        //    {
                        //        var loId = elT.Attribute("charLoId").Value;
                        //        var ch = wo.loOfloId[loId] as Character;


                        //        var time = ulong.Parse(elT.Attribute("time").Value);

                        //        asNpc.timeILastSawHim.Add(ch, time);
                        //    }









                        //    //var schedule = (npcSchedule)   Activator.CreateInstance(assemblyName: assembly, typeName: fullname).Unwrap();
                        //    //schedule._npc = asNpc;

                        //    //schedule.deserialize(elNpc);

                        //    //asNpc.schedule = schedule;

                        //    //Debug.Assert(asNpc.schedule.heIsDoingThis() != null);



                        //}


                        //Debug.Assert(!(asNpc != null && asPc != null));

                        base.deserialize(el, out saveGameInvalid);
                }


                //public dialog_token dial(string s, string idForDup = null)
                //{
                //    return s.todial(this, idForDup);
                //}


                //public abstract relationBetweenChars relationWith(characterE c);

                //public abstract string descObjectHeldPlacehFormal { get; }
                //public abstract string descObjectHeldPlacehInformal { get; }
                //public abstract string descObjectHeldPlacehYou { get; }

                //public string subj { get { return wo.ac.howHeCallsSomeoneElseAsSubject(this, det: true); } } // attenzione, ho dubbi se det vada sempre bene. testare.
                //public string subju { get { return subj.flu(); } }

                //public abstract Func<character, Task> greeting { get; }





                //public bool isThisTheFirstTimeYouLookAtHim()
                //{
                //    return howManyTimesYouLookedAtHim == 1;
                //}

                //public abstract bool daDelLeiA(characterE other);











                public void pickUp(LogicObj lo)
                {

                        lo.isSeen = true;

                        if (lo.charWithThisObj == this)
                        {
                                // ce l'ha già
                        }
                        else
                        {
                                lo.removeFromWorld();



                                lo.charWithThisObj = this;
                                //this.inv.Add(lo);
                                this.inv.Insert(0, lo);
                        }
                }


                internal string translatedNameForDialog(XDocIndexed xdocObjs)
                {

                        if (wo.CurLang == null)
                        {
                                return this.nameForDialog;
                        }
                        string nameTransl;


                        //var xmlPath = WorldBase.getPathXmlTranslationObjs(wo.curLang);
                        //var xdoc = XDocument.Load(xmlPath);
                        if (xdocObjs.charNameForDialogOfLoId.ContainsKey(loId)) {
                                var el = xdocObjs.charNameForDialogOfLoId[loId]; // Root?.Elements("char_name_for_dialog").Where(lel => lel.Attribute("lo_id")?.Value == this.loId).FirstOrDefault();
                                if (el != null && el.Attribute("transl")?.Value != "+")
                                {
                                        nameTransl = el.Attribute("transl")?.Value.Replace("''", "\"");

                                }
                                else
                                {
                                        nameTransl = this.nameForDialog;
                                }
                        }
                        else
                        {
                                nameTransl = this.nameForDialog;
                        }

                        return nameTransl;
                }



        }
}
