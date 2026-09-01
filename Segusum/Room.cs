#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Seg
{
    public class Room
    {

        //public DateTime? LastTimeParsedCoordFile { get; set; }

        internal WorldBase wo;

        public string roomId; // serve per la serializzazione.

        public string toTheRoom;

        //public double ManualOffsetX;
        //public double ManualOffsetY;


        //public double? ManualPosX;
        //public double? ManualPosY;

        public override string ToString()
        {
            return nameForMap;
        }

        internal string dynamicNameForMapTranslated(XDocIndexed xdocObj)
        {

            var dyn = wo.dynamicRoomName(this);
            if (dyn == null)
            {
                var tra1 = wo.translateDialogOrNarOrAnnotatedAux(nameForMap, xdocObj, out bool? found);

                if (found != false)
                {
                    return tra1;
                }
                else
                {
                    // falback old

                    return translatedNameForMap_old(xdocObj);
                }
            }
            else
            {
                return wo.translateDialogOrNarOrAnnotated(dyn, xdocObj);
            }

        }



        public double? map_x;
        public double? map_y;

        //public virtual void onBeforeLeavingRoom(eventArg ea)
        //{

        //}


        //public virtual void onBeforeLook(eventArg ea)
        //{

        //}


        public string whatToSayWhenEnteringRoom;

        //public string whatToSayWhenEnteringRoomFollowedFemale;



        //public string whatToSayWhenEnteringRoomAlone;

        //public string whatToSayWhenObjectIsInThisRoom;

        //public virtual void onEnteringRoom(eventArg ea, roomE previousRoom)
        //{

        //}


        internal HashSet<LogicObj> objectsInRoom = new HashSet<LogicObj>();

        //public List<LogicObj> pickablesOnFloor()
        //{
        //    return objectsInRoom.Where(p => p.appearsInInvIfNotPicked).ToList();
        //}

        //public int howManyTimesYouHaveVisitedThisRoom = 0;

        internal Dictionary<LogicObj, int> howManyTimesVisited = new Dictionary<LogicObj, int>();


        //public bool alreadyVisitedOnce()
        //{
        //    return howManyTimesYouHaveVisitedThisRoom > 0;
        //}

        public string translatedNameForMap_old(XDocIndexed xdocObj)
        {
            return translatedNameForMapAuxRoom_old(nameForMap, xdocObj);
        }

        public string translatedEntenceEntering(XDocIndexed xdocObj)
        {
            if (whatToSayWhenEnteringRoom != null)
            {
                var parzialmenteIstanza = translatedNameForMapAuxRoomEnter(whatToSayWhenEnteringRoom, xdocObj);
                return parzialmenteIstanza; // manca
            }
            else
            {
                var strArriviTemplUntransl = "Arrivi in {1}{2}.".translatable();

                var strArriviTemplTransl = wo.translateDialogOrNarOrAnnotated(strArriviTemplUntransl, xdocObj); // wo.translateSentenceWithIdFromObjfile(str, "arrivi_in_room_fallback", xdocObj?.Xdoc);


                var roomNameTra = dynamicNameForMapTranslated(xdocObj);
                var strFull = strArriviTemplTransl.inst(roomNameTra); // ora è parzialmente instanziata: è così: "arrivi in casa tua{2}"
                return strFull;
            }

        }


        public string translatedNameForMapAuxRoom_old(string untransl, XDocIndexed xdocObj)
        {
            if (untransl == null)
            {
                throw new ArgumentNullException(nameof(untransl));
            }

            if (wo.CurLang == null)
            {
                return untransl;
            }


            string nameTransl;


            //var xmlPath = WorldBase.getPathXmlTranslationObjs(wo.curLang);
            //var xdoc = XDocument.Load(xmlPath);
            if (xdocObj.roomOfRoomId.ContainsKey(roomId))
            {
                var el = xdocObj.roomOfRoomId[roomId]; // Root.Elements(tagname).Where(lel => lel.Attribute("room_id").Value == this.roomId).FirstOrDefault();
                if (el != null && el.Attribute("transl").Value != "+")
                {
                    nameTransl = el.Attribute("transl").Value.Replace("''", "\"");

                }
                else
                {
                    nameTransl = untransl;
                }
            }
            else
            {
                nameTransl = untransl;
            }

            return nameTransl;
        }

        public string translatedNameForMapAuxRoomEnter(string untransl, XDocIndexed xdi)
        {
            if (untransl == null)
            {
                throw new ArgumentNullException(nameof(untransl));
            }

            if (wo.CurLang == null)
            {
                return untransl;
            }

            var tra1 = wo.translateDialogOrNarOrAnnotatedAux(untransl, xdi, out bool? found);

            if (found != false)
            {
                return tra1;
            }
            else
            {
                // vecchio sistema
                string nameTransl;


                //var xmlPath = WorldBase.getPathXmlTranslationObjs(wo.curLang);
                //var xdoc = XDocument.Load(xmlPath);
                if (xdi.roomEnterOfRoomId.ContainsKey(roomId))
                {
                    var el = xdi.roomEnterOfRoomId[roomId]; // Root.Elements(tagname).Where(lel => lel.Attribute("room_id").Value == this.roomId).FirstOrDefault();
                    if (el != null && el.Attribute("transl").Value != "+")
                    {
                        nameTransl = el.Attribute("transl").Value.Replace("''", "\"");

                    }
                    else
                    {
                        nameTransl = untransl;
                    }
                }
                else
                {
                    nameTransl = untransl;
                }

                return nameTransl;
            }
        }

        //public CycleMemory cycle_mem = new CycleMemory { };


        public virtual void serialize(XElement xelRoom)
        {

            xelRoom.Add(new XAttribute("roomId", roomId));


            //if (this.LastTimeParsedCoordFile != null)
            //{


            //        xelRoom.Add(new XAttribute("last_time_parsed_coord", this.LastTimeParsedCoordFile.Value.ToString(CultureInfo.InvariantCulture)));
            //}




            //xelRoom.Add(new XAttribute("nextSentenceCounter", next_sentence_counter));
            //this.cycle_mem.serialize(xelRoom);


            foreach (var o in objectsInRoom)
            {
                var xelO = new XElement("objOnFloor");
                xelRoom.Add(xelO);

                xelO.Add(new XAttribute("loId", o.loId));
            }


            foreach (var x in howManyTimesVisited)
            {
                var xel = new XElement("howManyTimesYouHaveVisitedThisRoom");
                xelRoom.Add(xel);

                xel.Add(new XAttribute("loId", x.Key.loId));

                if (x.Value > 0)
                {
                    var tt = 4;
                }
                xel.Add(new XAttribute("times", x.Value));


            }

        }



        internal virtual void deserialize(XElement xelRoom)
        {


            //var xatlLastTimePa = xelRoom.Attribute("last_time_parsed_coord");
            //if (xatlLastTimePa != null)
            //{
            //        LastTimeParsedCoordFile = 
            //}

            //next_sentence_counter = int.Parse(xelRoom.Attribute("nextSentenceCounter").Value);
            //cycle_mem.deserialize(xelRoom.Element("cycle_memory"));

            howManyTimesVisited.Clear();
            foreach (var elHowm in xelRoom.Elements("howManyTimesYouHaveVisitedThisRoom"))
            {
                var chId = elHowm.Attribute("loId").Value;
                var times = int.Parse(elHowm.Attribute("times").Value);
                if (times > 0)
                {
                    //var yy = 4;
                }
                var ch = wo.loOfId[chId];
                howManyTimesVisited.Add(ch, times);

            }


            objectsInRoom.Clear();
            foreach (var elInv in xelRoom.Elements("objOnFloor"))
            {
                var loId = elInv.Attribute("loId").Value;
                if (wo.loOfId.ContainsKey(loId))
                {
                    var lo = wo.loOfId[loId];
                    objectsInRoom.Add(lo);
                }
                else
                {
                    // l'oggetto è nell'xml ma è stato tolto dal mondo. lo ignoro
                }
            }


        }


        public bool isFirstVisitWith(LogicObj loChar)
        {
            if (!loChar.isHere())
            {

                return false; // altrimenti è scomodo. voglio che isfirst-visit-with-camilla sia false se lei non l'ho ancora presa
            }

            if (howManyTimesVisited.ContainsKey(loChar))
            {
                return howManyTimesVisited[loChar] == 0;
            }
            else
            {
                return true;
            }
        }


        public bool wasEverVisitedBy(LogicObj loChar)
        {
            if (howManyTimesVisited.ContainsKey(loChar))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        ///  called by the engine
        /// </summary>
        /// <param name="w"></param>
        internal void registerInWorld(WorldBase w)
        {

            if(roomId == "roomOutsideHome")
            {
                var t = 4;
            }
            Debug.Assert(!w.roomOfId.ContainsKey(this.roomId));
            w.roomOfId[this.roomId] = this;


            if (assetFolderName != null)
            {
                coordFileEditor = eng.ParseCoordFile(this, w, out bool roomDataFileNotPresent); // bm_r48jr4j8r8r48
            }

        }


        ///// <summary>
        ///// usalo ad esempio per settare la parent room.
        ///// </summary>
        //public virtual void initRoom() { }

        //public class objVerbInfo
        //{
        //    public string loId;
        //    public string[] verbIds;
        //}
        ///// <summary>
        /////  can be null
        ///// </summary>
        //public abstract roomRepresentsContainer roomRepresentsContainer { get; }

        ///// <summary>
        ///// abbiamo stanze fasulle, che sono solo viste in una stanza padre
        ///// </summary>
        //public roomE parentRoom;




        public string nameForMap;

        public string assetFolderName;



        // è null se mancava il file di coordinate per quella room. in teoria potrei dare eccezione e terminare, ma volevo testare con una sola room.
        //internal Dictionary<string, LayerInfoParsed>? coordFile;

        internal RoomDataEditor coordFileEditor;
public string? imgPath()
        {

            //return coordFile.BackgroundPath;

            if (assetFolderName == null)
            {
                return null;
            }
            return $"{wo.graphicsRootFolderName()}/{assetFolderName}/bg.png";
        }

        // I narRoom possono usare anche asset grafici legacy, che non hanno
        // layer_data.json e vivono sotto img/littlegirl con un bg.jpg.
        // La stanza giocabile continua invece a usare imgPath() e il formato
        // corrente img/littlegirl_pixel/.../bg.png.
        public string? imgPathForNarRoom()
        {
            if (assetFolderName == null)
            {
                return null;
            }

            if (coordFileEditor == null)
            {
                var legacyGraphicsRoot = wo.graphicsRootFolderName().Replace("_pixel", "");
                return $"{legacyGraphicsRoot}/{assetFolderName}/bg.jpg";
            }

            return imgPath();
        }
        //public string img;

        //public HashSet<roomChar> roomChars = new HashSet<roomChar>();


        //public abstract void descWithMarkup(List<parHtmlClient> pars);

        //public abstract List<Obj> objectsInRoom();


        /// <summary>
        /// extracts the data to send to the client
        /// </summary>
        /// <param name="ret">input</param>
        /// <param name="pars">output</param>
        /// <param name="verbsOfObj">output</param>
        /// <param name="verbs">output</param>
        /// <param name="topicsOfChar">output</param>
        //public void convertServerParsToClientParsAndVerbsOfObj(List<parHtmlServer> ret, List<parHtmlClient> pars)
        //{





        //    foreach (var ps in ret)
        //    {
        //        var parc = new parHtmlClient();

        //        foreach (var el in ps.elements)
        //        {
        //            var simpl = el as simpleText;
        //            var keyw = el as keywordServer;
        //            if (simpl != null)
        //            {
        //                parc.elements.Add(new simpleTextClient { s = simpl.s });
        //            }
        //            else
        //            {


        //                //var verbIds = keyw.lo.verbs.Select(v => v.verbId).ToArray();
        //                //var verbInfos = keyw.lo.verbs.Select(v => new verbInfo
        //                //{
        //                //    verbId = v.verbId,
        //                //    firstPartForSentence = v.firstPartForSentence.flu(),
        //                //    stringForContextMenu = v.stringForContextMenu.flu(),

        //                //    isUnary = v is unaryVerb,
        //                //    invertObjectOrder = v.invertObjectOrder,
        //                //    secondPart = v.secondPart
        //                //}).ToList();

        //                //foreach (var vi in verbInfos)
        //                //{
        //                //    verbs.Add(vi);
        //                //}
        //                //verbsOfObj.Add(new objVerbInfo { loId = keyw.lo.loId, verbIds = verbIds });


        //                var uiName = keyw.lo.name.flu();

        //                parc.elements.Add(new keywordClient
        //                {
        //                    text = keyw.text,
        //                    loId = keyw.lo.loId,
        //                    loUiName = uiName,
        //                    imgForVerbMenu = keyw.lo.imgForVerbMenu(),
        //                    isVerb = keyw.lo.logicObjType == LogicObjType.VerbNormal || keyw.lo.logicObjType == LogicObjType.VerbPinned
        //                    //hasTalkNotUse = keyw.lo is characterE,
        //                });


        //            }
        //        }

        //        pars.Add(parc);
        //    }



        //}

        //public abstract string floorDescSing2 { get; }
        //public abstract string floorDescPlur2 { get; }



        //public Character[] charsHere
        //{
        //    get
        //    {
        //        {
        //            return objectsInRoom.Select(o => o as Character).SelectSome().ToArray(); //   wo.allChars.Where(ch => ch.roomWithThisObjOnTheFloor == this).ToArray();
        //        }
        //    }
        //}


        //public List<parHtmlServer> describeAllChars3()
        //{
        //    var ret = new List<parHtmlServer>();

        //    var personaggi = eng.buildDescrizioniDeiPersonaggi2(charsHere);
        //    ret.AddRange(personaggi);
        //    return ret;
        //}

        //public bool itIsFirstTimeYouSeeThisRoom2()
        //{
        //    return howManyTimesYouHaveVisitedThisRoom == 0;
        //}

        //bool thereAreNpcsInThisRoom()
        //{

        //    return wo.allChars.Where(ch => ch.roomWithThisObjOnTheFloor == this).Any(ch => !wo.curParty.Contains(ch));
        //}

        //public bool theCharsDescriptionShouldBeBeforeTheObjDesc()
        //{
        //    return (!itIsFirstTimeYouSeeThisRoom2() && thereAreNpcsInThisRoom());
        //}
    }

    //public class roomCharSlot
    //{

    //    public double posx;
    //    public bool isTaken;
    //}

}
