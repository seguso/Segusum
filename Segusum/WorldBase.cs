
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;


// ReSharper disable ConvertClosureToMethodGroup
// ReSharper disable RedundantStringInterpolation
// ReSharper disable ReplaceWithSingleCallToSingleOrDefault
#pragma warning disable 219

// ReSharper disable PossibleNullReferenceException
// ReSharper disable ReplaceWithSingleCallToFirstOrDefault
// ReSharper disable InvertIf
// ReSharper disable AssignNullToNotNullAttribute

namespace Seg
{


    public enum NarSize
    {
        Small = 0,
        Medium = 1,
        Large = 2,
        FullScreen = 3
    }
    //public class DynamicExclusionClient
    //{
    //        public string dec_objSerId;
    //        public string[] dec_qtoksChosen;
    //        public string dec_qtokToExclude;
    //}

    public class AspectTemp
    {
        public AspectTemp(Aspect aspect)
        {
            Aspect = aspect;
        }

        public Aspect Aspect { get; set; }
    }

    public abstract class WorldBase
    {



        /// <summary>
        /// normalmente questo non sarebbe una proprietà del mondo ma dell'utente, così come i savegame names. Ma lo copio nel mondo perche' l'engine lo deve sapere ma l'utente dell'engine non lo può passare sempre all'engine.
        /// </summary>
        public bool IsTextMode { get; protected internal set; }

        public bool IsCasualMode { get; protected internal set; }
        public bool IsTutorialMode { get; protected internal set; }
        public bool IsCasual() => IsCasualMode;
        public virtual bool CasualModeKeepsExplanation(LogicObj first, LogicObj second) => false;
        public virtual Cycle CasualGenericFailureCycle() => null;

        // Hook per i messaggi tutorial prima dell'apertura delle modal.
        // Il gioco principale restituisce null e non esegue alcun preflight.
        public virtual Cycle tutorialBeforeUseWithPrompt(TutorialPromptContext context) => null;
        public virtual Cycle tutorialBeforeUseForPrompt(TutorialPromptContext context) => null;
        public virtual Cycle tutorialBeforeIsActuallyPrompt(TutorialPromptContext context) => null;
        public virtual Cycle tutorialBeforeHideInsidePrompt(TutorialPromptContext context) => null;
        public virtual Cycle tutorialBeforeDisguiseAsPrompt(TutorialPromptContext context) => null;

        // Testi della schermata iniziale di scelta dell'interfaccia. Sono
        // overrideabili dall'autore del gioco e vengono tradotti dal server
        // insieme agli altri testi del mondo.
        public virtual string ProInterfaceTitle() => "Interfaccia Pro";
        public virtual string ProInterfaceSubtitle() => "Il gioco ti chiede di spiegare cosa pensi succederà, così non rischi di risolvere puzzle per caso mentre sperimenti. Adatta ai puristi dei puzzle.";
        public virtual string CasualInterfaceTitle() => "Interfaccia casual";
        public virtual string CasualInterfaceSubtitle() => "Simile alle interfacce tradizionali. Scegli questa se ti interessa soprattutto la storia e non ti importa se risolverai dei puzzle per caso mentre sperimenti.";




        public abstract EndGameStuffClient getEndGameData();

        //public WorldBase(string curlang)
        //{
        //        curLang = curlang;
        //}
        //public abstract         Qtok YouToken();

        //public abstract Qtok UseToken();

        //public abstract Qtok SayToken();


        public abstract Explanation[] getGlobalExplanations();


        public abstract bool fillerIsVisible(Filler fi);

        public abstract bool templateIsVisible(Template te);

        public abstract bool explanationIsVisibleForTextInput(TextInput ti, Explanation e);

        public abstract bool explanationIsVisible(Explanation e);

        /// <summary>
        /// useful because sometimes you only need to be able to talk to a character if a clue has been seen
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        //public abstract bool canTalkToCharacterNow(Character c);


        // cose da serializzare


        /// <summary>
        /// la scena giocabile attuale
        /// </summary>
        //public int curScene;
        public List<Objective> curObjectives = new List<Objective>();

        private readonly HashSet<Objective> allSeenObjectives = new HashSet<Objective>();

        protected internal List<PastAction> pastActions = new List<PastAction>();


        internal List<NamedCutScene> namedCutScenesSeen = new List<NamedCutScene>();

        // Central clock used by semantic timestamps.  Games may override it
        // with a deterministic test clock without changing the engine's other
        // DateTime.Now uses.
        protected virtual DateTime EngineNow => DateTime.Now;

        // Internal infrastructure access keeps the clock overridable by a
        // derived World without widening the public API surface.
        internal DateTime EngineNowForInfrastructure => EngineNow;

        internal ObjectiveAndHints[] hints = new ObjectiveAndHints[] { };
        public void setHints(ObjectiveAndHints[] hints)
        {
            this.hints = hints;
        }

        //public abstract bool youHaveAllTheElementsToSolvePuzzle(Objective o);

        /// <summary>
        /// i dialoghi toplevel, cioè non sono specifici di un personaggio. il gioco si può considerare come un'alternanza di dialoghi toplevel e scene giocabili.
        /// </summary>
        public abstract List<Dialog> dialogsToSerialize();



        //public abstract void fallback_chat_inter_pc(cut_scene cs);

        //public abstract List<DynamicExclusionClient> dynamicExclusions();

        public virtual void invariantConditions()
        {

        }


        public bool hasTriedToCombine(LogicObj lo1, LogicObj lo2)
        {

            return pastActions.Any(pa => pa is PastActionUseWith pw && pw.lo1 == lo1 && pw.lo2 == lo2);

        }

        public bool hasTriedToUse(LogicObj lo)
        {

            return pastActions.Any(pa => pa is PastActionUseHere pw && pw.lo == lo);

        }

        public abstract void after_action_executed(CutScene cs, ActionContext actionContext);


        //public abstract void processWrongSolution(Objective pu, string translatedNameOfObjective, PuzzleToken[] wrongSolution, XDocIndexed xdocObjects);

        /// <summary>
        /// Called when some custom AutoSolveHandler returns makesNoSenseAtThisTime = true, or when the process to auto solve does not fund any solution that removes the objective.
        /// </summary>
        /// <param name="pu"></param>
        /// <param name="translatedNameOfObjective"></param>
        /// <param name="xdocObjects"></param>
        //public abstract void processWrongSolutionAuto(Objective pu, string translatedNameOfObjective, XDocIndexed xdocObjects);

        public abstract string dynamicObjectName(LogicObj lo, bool withArticle, bool isForDialog);

        public abstract string dynamicRoomName(Room ro);

        //public abstract void gameTitleParagraphs();

        public abstract void startGameCutScene();

        //public abstract string gameTitle();


        public void changeRoomManualAdjacent(Room roomTarget, bool alsoShowGraphicsInTextMode = false)
        {

            var xdocObj = getXdocObjIndexedCached();
            var i = new BeforeRoomChangeInput { };

            var curSegment = new WalkPath { locations = new[] { curRoom, roomTarget }.ToList() };

            beforeRoomChangeManual(curRoom, roomTarget, curSegment, curSegment, i); // puo' annullare il cambio di room


            if (i.canChangeRoom)
            {



                changeRoomAux(roomTarget, out TextInput _textInputToShow
                     , addSentenceYouArriveAt: true,
                     callRoomChangedHandler: true
                     , xdocObj: xdocObj

                     , customSentenceYouArriveAt: null
                     , alsoShowGraphicsInTextMode: alsoShowGraphicsInTextMode
                        );


            }

        }

        public abstract void beforeWalkPathResetVariables();

        public abstract void beforeRoomChangeManual(Room from, Room to, WalkPath pathFromTo, WalkPath completePath, BeforeRoomChangeInput i);

        /// <summary>
        /// needed to set the character aspects in the location
        /// </summary>
        /// <param name="roomTarget"></param>
        public abstract void beforeRoomChangeManualAndAutoSetRoomAspects(Room roomTarget);


        public abstract void beforeExecuteDialogSetAspects();

        //public abstract string quickMove(Room roomTarget, List<cutSceneToken> cs);


        public Dictionary<string, List<Template>> templatesToExcludeOfObj = new Dictionary<string, List<Template>>();

        public Dictionary<string, List<string>> explanationsToExcludeOfObjective = new Dictionary<string, List<string>>();

        public Dictionary<string, List<string>> explanationsToExcludeOfLo = new Dictionary<string, List<string>>();

        // Esclusioni aggiunte automaticamente per limitare il numero di
        // explanation. Sono separate logicamente da quelle dichiarate dal
        // gioco, perché la visibilità può cambiare con room e stato.
        private readonly Dictionary<string, HashSet<string>> generatedExplanationExclusionsOfObjective = new();
        private readonly Dictionary<string, HashSet<string>> generatedExplanationExclusionsOfLo = new();


        //public virtual bool qtokIsVisibleNow(Objective curObj, Qtok tok)
        //{

        //        return true;
        //}

        //public virtual bool qtokIsEnabledNow(Qtok tok)
        //{

        //        return true;
        //}


        //public virtual bool verbIsVisibleNow(Verb v)
        //{
        //        return true;
        //}


        public bool namedCutSceneIsSeen(NamedCutSceneId ncsId)
        {
            return namedCutScenesSeen.Any(ncs => ncs.id.serId == ncsId.serId);
        }

        // Derived games sometimes need the timestamp of a named scene to
        // maintain their own legacy timing rules. Keep the serialized scene
        // collection encapsulated while exposing the narrow operation they
        // actually need.
        protected internal DateTime? namedCutSceneFirstSeenAt(NamedCutSceneId id)
            => namedCutScenesSeen.SingleOrDefault(ncs => ncs.id.serId == id.serId)?.FirstSeenAt;

        protected internal void setNamedCutSceneFirstSeenAtIfMissing(
            NamedCutSceneId id,
            DateTime firstSeenAt)
        {
            var scene = namedCutScenesSeen.SingleOrDefault(ncs => ncs.id.serId == id.serId);
            if (scene is not null) scene.FirstSeenAt ??= firstSeenAt;
        }

        // These protected seams support derived game test fixtures without
        // exposing the serialized named-cutscene implementation publicly.
        protected internal void clearNamedCutScenesForTesting() => namedCutScenesSeen.Clear();

        protected internal DateTime? namedCutSceneFirstSeenAtForTesting(NamedCutSceneId id)
            => namedCutSceneFirstSeenAt(id);

        protected internal void markNamedCutSceneSeenForTesting(
            NamedCutSceneId id,
            Room room,
            DateTime? firstSeenAt = null)
        {
            // Keep this fixture operation equivalent to the historical test
            // seam: callers decide whether an entry already exists.
            namedCutScenesSeen.Add(new NamedCutScene(id)
            {
                cs = new CutScene(canBeSkipped: true),
                oggettiMenzionati = new(),
                roomDoveEri = room,
                FirstSeenAt = firstSeenAt
            });
        }

        //public virtual bool verbIsHighlightedNow(Verb v)
        //{
        //        return false;
        //}

        /// <summary>
        /// lo stato del gioco. il gioco può trovarsi in uno di questi 3 stati: sto leggendo una cutscene (che può essere una cutscene dentro un dialogo o no), 
        /// sto visualizzando le domande di dialogo da chiedere, oppure sto guardando la stanza.
        /// </summary>
        protected internal GameState gs;

        /// <summary>Transient admin narration loaded by the web/persistence integration.</summary>
        public List<AdminNarrativeMessageClient> adminNarrativeMessagesPending = new();

        /// <summary>Message IDs whose tokens were appended to a natural cutscene in this world.</summary>
        public List<long> adminNarrativeMessagesDelivered = new();

        public void AppendAdminNarrativeMessages(CutScene cutScene, GameStateShowingQuestions? afterDialog,
                GameStateWaitingForText? afterText, GameStateFinished? afterFinished)
        {
                if (afterDialog != null || afterText != null || afterFinished != null || adminNarrativeMessagesPending.Count == 0)
                        return;
                foreach (var message in adminNarrativeMessagesPending)
                        foreach (var text in message.NarTexts.Where(x => !string.IsNullOrWhiteSpace(x)))
                        {
                                var token = new NarToken(false, null, text, cutScene.Count > 0,
                                        Array.Empty<LayerForClient>(), false, NarSize.Small)
                                { adminNarrativeMessageId = message.Id };
                                cutScene.Add(token);
                                if (!adminNarrativeMessagesDelivered.Contains(message.Id))
                                        adminNarrativeMessagesDelivered.Add(message.Id);
                        }
                adminNarrativeMessagesPending.Clear();
        }

        /// <summary>
        /// il tempo attuale. il gioco è a turni. ad ogni azione che compi (diversa da look e talk e poche altre), il tempo avanza di uno.
        /// </summary>
        internal ulong cur_time; // NON RINOMINARE, viene serializzato automaticamente essendo ulong

        /// <summary>
        /// il personaggio attivo in questo momento
        /// </summary>
        internal Character activeChar { get; set; }

        //internal string iqLevel { get; set; }

        internal bool StoryMode { get; set; }


        public bool IsStoryMode() { return StoryMode; }

        public abstract bool rebuildXmlToTranslateObjects(out string lang);

        // quando cambia stanza, la logica del gioco potrebbe: far partire descrizioni, far partire eventi o incidenti; 
        // far partire dialoghi. vediamo i dialoghi che devono avvenire. dipendono da tante cose: se è la prima volta che visiti, se
        // ci sono condizioni di pericolo, se hai addosso qualche oggetto appariscente, 
        // se hai appena completato una quest e devi raccontarlo a qualcuno,
        // se devono partire dei saluti perché non vedi qualcuno da tanto tempo, se devono fare esclamazioni sparse context-aware.
        //public abstract void onRoomChanged(bool hoAppenaCambiatoStanza, RandomInputs randomInputs);


        private string curLang;


        //public string IQLevel { set => iqLevel = value; }


        public Character ActiveChar
        {
            get => activeChar;
            set => activeChar = value;
        }


        public void addToParty(Character ch)
        {
            curParty.Add(ch);
        }

        public void removeFromParty(Character ch)
        {
            curParty.Remove(ch);
        }


        //public List<Character> allNpcsHere()
        //{
        //    return curRoom.charsHere.Where(ch => ch.asNpc != null).ToList();
        //}

        //public List<Character> allNpcs()
        //{
        //    return allChars.Where(ch => ch.asNpc != null).ToList();
        //}


        public ulong getCurTime()
        {
            return cur_time;
        }


        // cose non da serializzare, non cambiano mai durante il gioco

        internal Dictionary<string, LogicObj> loOfId = new Dictionary<string, LogicObj>();
        //internal Dictionary<string, Qtok> qtokOfId = new Dictionary<string, Qtok>();
        internal Dictionary<string, Filler> fillerOfId = new Dictionary<string, Filler>();
        internal Dictionary<string, Template> templateOfId = new Dictionary<string, Template>();
        internal Dictionary<string, UnVerb> unVerbOfId = new Dictionary<string, UnVerb>();
        internal Dictionary<string, BinVerb> binVerbOfId = new Dictionary<string, BinVerb>();
        //internal Dictionary<string, ZeroVerb> zeroVerbOfId = new Dictionary<string, ZeroVerb>();
        internal Dictionary<string, Objective> objectiveOfId = new Dictionary<string, Objective>();
        internal Dictionary<string, Room> roomOfId = new Dictionary<string, Room>();
        //internal Dictionary<string, DynLine> dynLineOfId = new Dictionary<string, DynLine>();
        //internal List<Qtok> allQtoks = new List<Qtok> { };

        internal List<Aspect> allAspects = new List<Aspect> { };
        internal List<AlternatePosition> allAlternatePositions = new List<AlternatePosition> { };

        public List<CombineHandler> combineHandlers = new List<CombineHandler>();
        private Dictionary<LogicObj, List<CombineHandler>> combineHandlersByFirst;
        private readonly Dictionary<Explanation, Explanation[]> explanationGroupsByMember = new();
        private readonly Dictionary<Explanation, string> explanationGroupIntrosByMember = new();
        private readonly List<CombineExplanationFamily> combineExplanationFamilies = new();
        private readonly List<CombineExplanationContextRule> combineExplanationContextRules = new();
        private readonly List<UseForExplanationContextRule> useForExplanationContextRules = new();

        private sealed class CombineExplanationFamily
        {
            public LogicObj[] Members { get; init; }
        }

        internal sealed class CombineExplanationContextRule
        {
            public LogicObj[] Members { get; init; }
            public LogicObj Target { get; init; }
            public Func<bool> IsActive { get; init; }
            public Explanation[] Group { get; init; }
            public string CustomExplanationIntro { get; init; }
        }

        internal sealed class UseForExplanationContextRule
        {
            public LogicObj Tool { get; init; }
            public Objective Objective { get; init; }
            public Func<bool> IsActive { get; init; }
            public Explanation[] Group { get; init; }
            public string CustomExplanationIntro { get; init; }
        }
        public List<IsActuallyHandler> isActuallyHandlers = new List<IsActuallyHandler>();
        public List<UseForHandler> useForHandlers = new List<UseForHandler>();
        internal List<PickUpHandler> pickUpHandlers = new List<PickUpHandler>();
        internal List<LookHandler> lookHandlers = new List<LookHandler>();
        internal List<LookHandler> useHereHandlers = new List<LookHandler>();
        //internal List<PuzzleSolvedHandlerOldUi> puzzleSolvedHandlersOldUi = new List<PuzzleSolvedHandlerOldUi>();

        public List<UseInComposerHandler> deduceHandlers = new List<UseInComposerHandler>();
        internal List<AutoSolvePuzzleHandler> autoSolvePuzzleHandlers = new List<AutoSolvePuzzleHandler>();
        //internal List<UnaryVerbNoObjectiveHandler> unVerbNoObjectiveHandlers = new List<UnaryVerbNoObjectiveHandler>();
        internal List<CancelTextInputHandler> cancelTextInputHandlers = new List<CancelTextInputHandler>();
        internal List<SubmitTextInputHandler> submitTextInputHandlers = new List<SubmitTextInputHandler>();
        //internal List<UseInLocationHandler> useInLocationHandlers = new List<UseInLocationHandler>();
        //internal List<ZeroHandler> zeroHandlers = new List<ZeroHandler>();
        internal List<TalkHereHandler> talkHereHandlers = new List<TalkHereHandler>();

        internal List<RoomChangedHandler> roomChangedHandlers = new List<RoomChangedHandler>();

        internal List<ObjAndVerb> disableVerbForObj = new List<ObjAndVerb>();
        internal List<Objective> disabledObjectives = new List<Objective>();

        internal List<exit> exits = new List<exit>();

        public ReadOnlyCollection<exit> Exits => exits.AsReadOnly();

        internal HashSet<Character> curParty = new HashSet<Character>();

        //public List<PuzzleSolution> getAllPuzzleSolutions()
        //{
        //        var x = (from psh in puzzleSolvedHandlersOldUi
        //                 select psh.puzzleSolution).ToList();
        //        return x;
        //}




        //public List<exit> Exits { get { return exits; } }

        ///// <summary>
        ///// i trigger sono quelle scene che non devono succedere in seguito ad una precisa azione del PC, ma ad esempio in seguito
        ///// a una MANCATA azione. Ad esempio: non ti siedi, e ogni tanto ti dicono "perché non ti siedi?". Oppure: sei bloccata da tempo,
        ///// e un personaggio ti dice "penso che possiamo fare qualcosa qui".
        ///// </summary>
        ///// <param name="i"></param>
        ///// <param name="aisJustChanged"></param>
        //public abstract void runTriggers(handlerInput i, out bool aisJustChanged);


        public abstract void onWalkPathNotFound(Room roomTarget);

        internal WalkPath findShortestPath(Room from, Room dest)
        {

            if (from == dest)
            {
                return null;
                //throw new Exception($"You called find_shortest path with from = desc = {from}");
            }
            //Debug.Assert(from != dest);

            var fr = new List<WalkPath> { new WalkPath { locations = new List<Room> { from } } };


        again:

            //var frOrdered = fr.OrderBy(n => n.locations.Count).ToList();

            if (!fr.Any())
            {
                // non c'è nupercorso
                return null;
            }
            else
            {
                var curPath = fr.First();

                fr = fr.Skip(1).ToList();


                var whereAmI = curPath.locations.Last();
                if (whereAmI == dest)
                {
                    return curPath;
                }
                else
                {

                    var adiacenti = exits.Where(e => e.From == whereAmI).Select(e => e.To).ToList();

                    // creo tanti percorsi figli e li metto in coda
                    foreach (var ad in adiacenti)
                    {
                        if (!curPath.locations.Contains(ad))
                        {
                            var newPath = new WalkPath
                            {
                                locations = new List<Room>(curPath.locations) // shallow copy. è una copia identica del curpath...

                            };

                            newPath.locations.Add(ad); // tranne che ha una locazione in più

                            fr.Add(newPath);
                        }

                    }

                    goto again;
                }
            }

        }


        public List<LogicObj> allChars()
        {
            return loOfId.Values.Where(lo => lo is Character).ToList();
        }

        public void disableVerbFor(Verb v, LogicObj o)
        {
            disableVerbForObj.Add(new ObjAndVerb { ovObj = o, ovVerb = v });
        }

        public void disableObjective(Objective obv)
        {
            if (!disabledObjectives.Contains(obv))
            {
                disabledObjectives.Add(obv);
            }
        }

        public void enableObjective(Objective obv)
        {
            disabledObjectives.Remove(obv);
        }

        public void addExit(Room from, Room to)
        {
            if (from.roomId == "roomDesert" || to.roomId == "roomDesert")
            {
                var exits2 = (from e in exits where e.From.roomId == "roomDesertCaves" || e.To.roomId == "roomDesertCaves" select e).ToList();
                var y = 4;
            }
            if (!exits.Any(e => e.From == from && e.To == to))
            {

                exits.Add(new exit { From = from, To = to });
            }

            if (!exits.Any(e => e.From == to && e.To == from))
            {
                exits.Add(new exit { From = to, To = from });
            }

            if (from.roomId == "roomDesert" || to.roomId == "roomDesert")
            {
                var exits2 = (from e in exits where e.From.roomId == "roomDesertCaves" || e.To.roomId == "roomDesertCaves" select e).ToList();
                var y = 4;
            }
        }


        public bool existsExit(Room r1, Room r2)
        {
            return (from e in exits where e.From == r1 && e.To == r2 select e).Any();
        }
        public void removeExit(Room from, Room to)
        {

            var toRem = exits.Where(e => e.From == from && e.To == to).SingleOrDefault();
            var toRem2 = exits.Where(e => e.From == to && e.To == from).SingleOrDefault();

            if (toRem != null)
            {
                exits.Remove(toRem);
            }

            if (toRem2 != null)
            {
                exits.Remove(toRem2);
            }
        }

        public void addRoomChangedHandler(Room room, Action<RoomChangedInput> handler)
        {
            if (roomChangedHandlers.Any(ha => ha.roomEntered == room))
            {
                throw new Exception($"A RoomChangedHandler already exists for room {room.roomId}");
            }

            roomChangedHandlers.Add(new RoomChangedHandler { handler = handler, roomEntered = room });
        }

        //public void addQuatHandler(BinVerb binVerb, LogicObj lo1, LogicObj lo2, Objective pu, Action<HandlerInput> handler)
        //{
        //    //Debug.Assert(!quatHandlers.Any(h => h.binVerb == binVerb && h.containsObj(lo1) && h.containsObj(lo2) && h.puzzle == uv));

        //    if (lo1.useWith == UseWith.CantBeUsed)
        //    {
        //        throw new Exception($"{lo1} cannot be selected. can't use it in a handler");
        //    }

        //    if (lo2.useWith == UseWith.CantBeUsed)
        //    {
        //        throw new Exception($"{lo2} cannot be selected. can't use it in a handler");
        //    }

        //    if (lo1.useWith != UseWith.UseBinaryAsTool)
        //    {
        //        throw new Exception($"{lo1} is declared as usewith = {lo1.useWith}, not as tool");
        //    }

        //    if (lo2.useWith != UseWith.UseBinaryAsTarget && lo2.useWith != UseWith.UseUnaryOrBinaryAsTarget)
        //    {
        //        throw new Exception($"{lo2} is declared as usewith = {lo1.useWith}, not as target");
        //    }

        //    if (lo1.useWith == UseWith.UseUnaryOrBinaryAsTarget && binVerb.canBeUnaryOrBinaryDependingOnObj)
        //    {
        //        throw new Exception($"{lo1} is declared as usewith = false, and {binVerb} can be unary or binary depending on context, so {lo1} cannot have a quat handler as first element. binverb = {binVerb}, puzzle = {pu}");
        //    }

        //    if (quatHandlers.Any(h => h.containsObj(lo1) && h.containsObj(lo2) && h.binVerb == binVerb && h.puzzle == pu))
        //    {
        //        throw new Exception($"already exists quat handler for {lo1} and {lo2} and {pu} and {binVerb}");
        //    }

        //    quatHandlers.Add(new QuatHandler (binVerb : binVerb, lo1 : lo1, lo2 : lo2, puzzle : pu, handler : handler));
        //}


        public void addHandlerCombine(LogicObj lo1, LogicObj lo2, string fullSentenceUntransl, Action<HandlerInput> handler = null, Explanation explanation = null, Func<bool> isPossibleNow = null)
        {

            // La coppia è orientata: lo1 è l'oggetto usato e lo2 il target.
            // Target diversi possono quindi coesistere anche se usano lo
            // stesso lo1, con o senza explanation.
            if (combineHandlers.Any(h => h.lo1 == lo1 && h.lo2 == lo2))
            {
                throw new Exception($"already exists useWithHandler for {lo1} and {lo2} ");
            }

            combineHandlers.Add(new CombineHandler(lo1: lo1, lo2: lo2, handler: handler, sentenceUntransl: fullSentenceUntransl, isPossibleNow: isPossibleNow, explanation: explanation));
            combineHandlersByFirst = null;
        }


        public void addHandlerIsActually(LogicObj lo, Explanation ex1, Explanation ex2, Action<HandlerInput> handler)
        {

            if (isActuallyHandlers.Any(h => h.Lo == lo && h.Explanation1 == ex1 && h.Explanation2 == ex2))
            {
                throw new Exception($"already exists isActuallyHandlers for {lo} and {ex1} and {ex2} ");
            }

            isActuallyHandlers.Add(new IsActuallyHandler(lo: lo, explanation1: ex1, explanation2: ex2, handler: handler));
        }

        //public void addHandlerCombine(LogicObj lo1, LogicObj lo2, string fullSentenceUntransl, Action<HandlerInput> handler = null, Explanation explanation = null)
        //{

        //        if (combineHandlers.Any(h => h.containsObj(lo1) && h.containsObj(lo2)))
        //        {
        //                throw new Exception($"already exists useWithHandler for {lo1} and {lo2} ");
        //        }

        //        combineHandlers.Add(new CombineHandler(lo1: lo1, lo2: lo2, handler: handler, sentenceUntransl: fullSentenceUntransl, isPossibleNow: null, explanation: explanation));
        //}


        public void addHandlerUseFor(LogicObj lo, Objective ob, Explanation ex, Action<HandlerInput> handler)
        {
            if (useForHandlers.Any(h => h.Lo == lo && h.Objective == ob))
            {
                throw new Exception($"already exists useforhandler for {lo} and {ob.serId}");
            }

            useForHandlers.Add(new UseForHandler(lo: lo, explanation: ex, objective: ob, handler: handler));
        }

        public void addHandlerUseFor(LogicObj lo, Objective ob, Action<HandlerInput> handler)
        {


            if (useForHandlers.Any(h => h.Lo == lo && h.Objective == ob))
            {
                throw new Exception($"already exists useforhandler for {lo} and {ob.serId} ");
            }

            useForHandlers.Add(new UseForHandler(lo: lo, explanation: null, objective: ob, handler: handler));
        }

        public void addHandlerCombine(LogicObj lo1, LogicObj lo2, Func<string> dynamicSentenceUntransl, Action<HandlerInput> handler = null, Explanation explanation = null, Func<bool> isPossibleNow = null)
        {
            if (combineHandlers.Any(h => h.lo1 == lo1 && h.lo2 == lo2))
            {
                throw new Exception($"already exists useWithHandler for {lo1} and {lo2} ");
            }

            if (handler == null)
            {
                throw new Exception("handler is null");
            }
            //Debug.Assert(!quatHandlers.Any(h => h.binVerb == binVerb && h.containsObj(lo1) && h.containsObj(lo2) && h.puzzle == uv));

            //if (lo1.useWith == UseMode.CantBeUsed)
            //{
            //        throw new Exception($"{lo1} cannot be selected. can't use it in a handler");
            //}

            //if (lo2.useWith == UseMode.CantBeUsed)
            //{
            //        throw new Exception($"{lo2} cannot be selected. can't use it in a handler");
            //}

            //if (lo1.useWith != UseMode.UseWith)
            //{
            //        throw new Exception($"{lo1} is declared as usewith = {lo1.useWith}, not as UseWith");
            //}

            //// il secondo oggetto può essere sia unary che binaryastarget
            //if (lo2.useWith != UseMode.UseWith && lo2.useWith != UseMode.UseFor)
            //{
            //        throw new Exception($"{lo2} is declared as usewith = {lo2.useWith}");
            //}

            //// il primo oggetto deve essere use with (se il verbo è usa)
            //if (lo1.useWith != UseMode.UseWith && binVerb.canBeUnaryOrBinaryDependingOnObj)
            //{
            //        throw new Exception($"{lo1} is declared as usewith = false, and {binVerb} can be unary or binary depending on context, so {lo1} cannot have a quat handler as first element. binverb = {binVerb}, ");
            //}

            if (combineHandlers.Any(h => h.lo1 == lo1 && h.lo2 == lo2))
            {
                throw new Exception($"already exists useWithHandler for {lo1} and {lo2} ");
            }

            combineHandlers.Add(new CombineHandler(lo1: lo1, lo2: lo2, handler: handler, dynamicSentence: dynamicSentenceUntransl, isPossibleNow: isPossibleNow, explanation: explanation));
            combineHandlersByFirst = null;
        }

        public void addHandlerPickUp(LogicObj lo1, Action<PickUpHandlerInput> handler)
        {

            if (pickUpHandlers.Any(h => h.containsObj(lo1)))
            {
                throw new Exception($"already exists pickuphandler for {lo1}  ");
            }

            pickUpHandlers.Add(new PickUpHandler(lo1: lo1, handler: handler));
        }
        //public void addHandlerLook(LogicObj lo1, Action<HandlerInput> handler)
        //{

        //        if (lookHandlers.Any(h => h.containsObj(lo1)))
        //        {
        //                throw new Exception($"already exists look handler for {lo1}  ");
        //        }

        //        lookHandlers.Add(new LookHandler(lo1: lo1, handler: handler, isLookableNow: null, dynamicSentence: null));
        //}
        //public void addHandlerLook(LogicObj lo1, Func<string> dynamicSentence, Action<HandlerInput> handler)
        //{

        //        if (lookHandlers.Any(h => h.containsObj(lo1)))
        //        {
        //                throw new Exception($"already exists look handler for {lo1}  ");
        //        }

        //        lookHandlers.Add(new LookHandler(lo1: lo1, handler: handler, isLookableNow: null, dynamicSentence: dynamicSentence));
        //}
        //public void addHandlerLook(LogicObj lo1, string dynamicSentence, Action<HandlerInput> handler)
        //{

        //        if (lookHandlers.Any(h => h.containsObj(lo1)))
        //        {
        //                throw new Exception($"already exists look handler for {lo1}  ");
        //        }

        //        lookHandlers.Add(new LookHandler(lo1: lo1, handler: handler, isLookableNow: null, dynamicSentence: () => dynamicSentence));
        //}
        //public void addHandlerLook(LogicObj lo1, string dynamicSentence, Func<bool> isLookableNow, Action<HandlerInput> handler)
        //{

        //        if (lookHandlers.Any(h => h.containsObj(lo1)))
        //        {
        //                throw new Exception($"already exists look handler for {lo1}  ");
        //        }

        //        lookHandlers.Add(new LookHandler(lo1: lo1, handler: handler, isLookableNow: isLookableNow, dynamicSentence: () => dynamicSentence));
        //}
        //public void addHandlerLook(LogicObj lo1, Func<bool> isLookableNow, Action<HandlerInput> handler)
        //{

        //        if (lookHandlers.Any(h => h.containsObj(lo1)))
        //        {
        //                throw new Exception($"already exists look handler for {lo1}  ");
        //        }

        //        lookHandlers.Add(new LookHandler(lo1: lo1, handler: handler, isLookableNow: isLookableNow, dynamicSentence: null));
        //}

        public void addHandlerUseHere(LogicObj lo1, Action<HandlerInput> handler)
        {

            if (useHereHandlers.Any(h => h.containsObj(lo1)))
            {
                throw new Exception($"already exists use-here handler for {lo1}  ");
            }

            useHereHandlers.Add(new LookHandler(lo1: lo1, handler: handler, isLookableNow: null, dynamicSentence: null));
        }

        public void addHandlerUseHere(LogicObj lo1, string fullSentenceUntransl, Action<HandlerInput> handler)
        {
            if (useHereHandlers.Any(h => h.containsObj(lo1)))
            {
                throw new Exception($"already exists use-here handler for {lo1}  ");
            }

            useHereHandlers.Add(new LookHandler(
                lo1: lo1,
                handler: handler,
                isLookableNow: null,
                dynamicSentence: () => fullSentenceUntransl));
        }


        public void markAllRoomsVisitedDebug(Character ch)
        {
            foreach (var r in roomOfId.Values)
            {
                markRoomVisited(r, ch);
            }
        }

        //public void addHandlerUseWithNoOb(BinVerb binVerb, LogicObj lo1, LogicObj lo2, Action<HandlerInput> handler)
        //{

        //        if (lo1.useWith == UseMode.CantBeUsed)
        //        {
        //                throw new Exception($"{lo1} cannot be selected. can't use it in a handler");
        //        }

        //        if (lo2.useWith == UseMode.CantBeUsed)
        //        {
        //                throw new Exception($"{lo2} cannot be selected. can't use it in a handler");
        //        }

        //        if (lo1.useWith != UseMode.UseWith)
        //        {
        //                throw new Exception($"{lo1} is declared as usewith = {lo1.useWith}, not as UseWith");
        //        }

        //        // il secondo oggetto può essere sia unary che binaryastarget
        //        if (lo2.useWith != UseMode.UseWith && lo2.useWith != UseMode.UseFor && lo2.useWith != UseMode.UseZeroDependsOnLocation)
        //        {
        //                throw new Exception($"{lo2} is declared as usewith = {lo2.useWith}");
        //        }

        //        // il primo oggetto deve essere use with (se il verbo è usa)
        //        if (lo1.useWith != UseMode.UseWith && binVerb.canBeUnaryOrBinaryDependingOnObj)
        //        {
        //                throw new Exception($"{lo1} is declared as usewith = false, and {binVerb} can be unary or binary depending on context, so {lo1} cannot have a  handler as first element. binverb = {binVerb}, ");
        //        }

        //        if (useWithNoObHandlers.Any(h => h.containsObj(lo1) && h.containsObj(lo2) && h.binVerb == binVerb ))
        //        {
        //                throw new Exception($"already exists UseWithNoObHandler for {lo1} and {lo2} and {binVerb}");
        //        }

        //        useWithNoObHandlers.Add(new UseWithNoObHandler(binVerb: binVerb, lo1: lo1, lo2: lo2, handler: handler));
        //}

        //public void addQuatHandler(IEnumerable<BinVerb> binVerbs, LogicObj lo1, LogicObj lo2, Objective pu, Action<HandlerInput> handler)
        //{
        //    //Debug.Assert(!quatHandlers.Any(h => h.binVerb == binVerb && h.containsObj(lo1) && h.containsObj(lo2) && h.puzzle == uv));

        //    foreach (var binVerb in binVerbs)
        //    {
        //        addQuatHandler(binVerb, lo1, lo2, pu, handler);
        //    }

        //}

        public void addHandlerCancelTextInput(TextInput ti, Action<HandlerInput> handler)
        {


            if (cancelTextInputHandlers.Any(h => h.ti == ti))
            {
                throw new Exception($"already exists cancel text input handler for {ti.serId}");
            }

            cancelTextInputHandlers.Add(new CancelTextInputHandler(ti, handler));

        }

        public void addHandlerSubmitTextInput(TextInput ti, Action<TextHandlerInput> handler)
        {


            if (submitTextInputHandlers.Any(h => h.ti == ti))
            {
                throw new Exception($"already exists submit  text input handler for {ti.serId}");
            }

            submitTextInputHandlers.Add(new SubmitTextInputHandler(ti, handler));

        }

        //public void addTerHandlerUn(UnVerb unVerb, LogicObj lo, Objective pu, Action<HandlerInput> handler)
        //{

        //        if (lo.useWith == UseWith.CantBeUsed)
        //        {
        //                throw new Exception($"{lo} cannot be selected. can't use it in a handler");
        //        }

        //        if (terHandlersUn.Any(h => h.containsObj(lo) && h.unVerb == unVerb && h.puzzle == pu))
        //        {
        //                throw new Exception($"already exists bin handler for {lo} and {unVerb} and {pu}");
        //        }

        //        terHandlersUn.Add(new TerHandlerUn { unVerb = unVerb, lo = lo, puzzle = pu, handler = handler });
        //}

        //public void addHandlerUseFor(IEnumerable<BinVerb> binVerbs, LogicObj lo, Objective pu, Action<HandlerInput> handler)
        //{
        //        var binvs = binVerbs.ToList();
        //        foreach (var binv in binvs)
        //        {
        //                addHandlerUseFor(binv, lo, pu, handler);
        //        }
        //}

        //public void addHandlerPuzzleOldUi(PuzzleSolution puzzleSolution, Action<HandlerInput> handler)
        //{


        //        puzzleSolvedHandlersOldUi.Add(new PuzzleSolvedHandlerOldUi(puzzleSolution: puzzleSolution, handler: handler));
        //}


        public void addHandlerAutoSolvePuzzle(Objective pu, Action<HandlerInput> handler)
        {


            autoSolvePuzzleHandlers.Add(new AutoSolvePuzzleHandler(objective: pu, handler: handler));
        }




        //public ObjInRoomToken ort(LogicObj lo)
        //{
        //        return new ObjInRoomToken(lo);
        //}


        //public PuzzleSolution puzSol(Objective objective, params PuzzleToken[] solution)
        //{
        //        return new PuzzleSolution(objective, solution);
        //}

        //public EnumeratedToken ert(Qtok correct, params Qtok[] choices)
        //{
        //        return new EnumeratedToken(correct, choices);
        //}
        //public EnumeratedToken ent(Qtok[] correct, Qtok[] choices)
        //{
        //        return new EnumeratedToken(correct, choices);
        //}

        //public void addHandlerUseInLocation(BinVerb binVerb, LogicObj lo,  Action<HandlerInput> handler)
        //{
        //        if (lo.useWith == UseMode.CantBeUsed)
        //        {
        //                throw new Exception($"{lo} cannot be selected. can't use it in a handler");
        //        }

        //        if (lo.useWith != UseMode.UseZeroDependsOnLocation)
        //        {
        //                throw new Exception($"{lo} is not declared as \"use zero\", so it cannot have a \"use with location\" handler.");
        //        }

        //        if (useInLocationHandlers.Any(h => h.containsObj(lo) && h.binVerb == binVerb /*&& h.room == ro*/))
        //        {
        //                throw new Exception($"already exists use-in-location-handler for {lo} and {binVerb} ");
        //        }


        //        useInLocationHandlers.Add(new UseInLocationHandler(binVerb: binVerb, lo: lo, handler: handler/*, room: ro*/));
        //}

        //public void addHandlerUnVerbNoObject(UnVerb unVerb, LogicObj lo, Action<HandlerInput> handler)
        //{
        //        if (lo.useWith == UseMode.CantBeUsed)
        //        {
        //                throw new Exception($"{lo} cannot be selected. can't use it in a handler");
        //        }


        //        if (unVerbNoObjectiveHandlers.Any(h => h.containsObj(lo) && h.unVerb == unVerb ))
        //        {
        //                throw new Exception($"already exists use-in-location-handler for {lo} and {unVerb} ");
        //        }


        //        //if (unVerb.isPickup && !lo.makesSenseToPickItUp)
        //        //{
        //        //        throw new Exception($"{lo} cannot have a pickup handler because it is declared as not makesSenseToPickItUp");
        //        //}

        //        unVerbNoObjectiveHandlers.Add(new UnaryVerbNoObjectiveHandler(unVerb: unVerb, lo: lo, handler: handler));
        //}

        //public void addHandlerZeroVerb(ZeroVerb zeroVerb, Action<HandlerInput> handler)
        //{
        //        if (zeroHandlers.Any(h => h.zeroVerb == zeroVerb))
        //        {
        //                throw new Exception($"already exists zero handler for {zeroVerb} ");
        //        }

        //        zeroHandlers.Add(new ZeroHandler { zeroVerb = zeroVerb, handler = handler });
        //}


        public void addHandlerTalkHere(Room r, Action<HandlerInput> handler)
        {
            if (talkHereHandlers.Any(h => h.room == r))
            {
                throw new Exception($"already exists talk here handler for room {r.roomId} ");
            }

            talkHereHandlers.Add(new TalkHereHandler { room = r, handler = handler });
        }


        public void addHandlerDeduce(LogicObj lo, Template template, Filler filler, Action<HandlerInput> handler)
        {
            if (deduceHandlers.Any(h => h.lo == lo && h.template == template && h.fillers.Contains(filler)))
            {
                throw new Exception($"already exists zero handler for lo = {lo.loId} and{template.teId} and filler {filler.FilId} ");
            }

            deduceHandlers.Add(new UseInComposerHandler(lo: lo, template: template, filler: filler, handler: handler));
        }

        public void addHandlerDeduce(LogicObj lo, Template template, Filler filler1, Filler filler2, Action<HandlerInput> handler)
        {
            if (deduceHandlers.Any(h => h.lo == lo && h.template == template && h.fillers.Contains(filler1) && h.fillers.Contains(filler2)))
            {
                throw new Exception($"already exists zero handler for lo = {lo.loId} and {template.teId} and filler {filler1.FilId} and {filler2.FilId}");
            }

            deduceHandlers.Add(new UseInComposerHandler(lo: lo, template: template, filler1: filler1, filler2: filler2, handler: handler));
        }

        //public void addUnaryHandler(logicObjE lo, Action<handlerInput> handler)
        //{
        //    Debug.Assert(!unaryHandlers.Any(h => h.containsObj(lo) ));

        //    unaryHandlers.Add(new UnaryActionHandler { lo = lo, handler = handler });
        //}


        //protected uint counterInstanceNameGenerator;

        //public uint getNextIntForNewInstance()
        //{
        //    return counterInstanceNameGenerator++;
        //}

        public Room curRoom => activeChar.roomWithThisObjOnFloor;

        public string CurLang
        {
            get => curLang; set
            {
                if (value == "it")
                {
                    curLang = null;
                }
                else if (value == "en")
                {
                    curLang = "en";
                }
                else if (value == "de")
                {
                    curLang = "de";
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }

        internal const string TargetPossessivePlaceholder = "{targetPossessive}";

        internal string targetPossessiveFor(LogicObj templateSource, GenderNumber targetGenderNumber, XDocIndexed xdi)
        {
            if (CurLang == null)
            {
                var italianForm = (templateSource.TargetPossessiveAgreement ?? PossessiveAgreement.MasculineSingular) switch
                {
                    PossessiveAgreement.MasculineSingular => "suo",
                    PossessiveAgreement.FeminineSingular => "sua",
                    PossessiveAgreement.MasculinePlural => "suoi",
                    PossessiveAgreement.FemininePlural => "sue",
                    _ => throw new NotImplementedException()
                };
                return targetGenderNumber == GenderNumber.They ? "loro" : italianForm;
            }

            var key = targetGenderNumber switch
            {
                GenderNumber.He => "__target_possessive_he__",
                GenderNumber.She => "__target_possessive_she__",
                GenderNumber.It => "__target_possessive_it__",
                GenderNumber.They => "__target_possessive_they__",
                _ => throw new NotImplementedException()
            };
            return translateDialogOrNarOrAnnotated(key, xdi);
        }

        internal string resolveTargetPossessiveTemplate(string translatedTemplate, LogicObj templateSource, LogicObj target, XDocIndexed xdi)
        {
            if (translatedTemplate == null || !translatedTemplate.Contains(TargetPossessivePlaceholder))
                return translatedTemplate;
            if (target == null)
                throw new InvalidOperationException("A target is required for {targetPossessive}.");
            return translatedTemplate.Replace(TargetPossessivePlaceholder, targetPossessiveFor(templateSource, target.genderNumber, xdi));
        }

        internal ObjForClient.TargetPossessiveFormsClient targetPossessiveForms(LogicObj templateSource, XDocIndexed xdi)
        {
            return new ObjForClient.TargetPossessiveFormsClient
            {
                he = targetPossessiveFor(templateSource, GenderNumber.He, xdi),
                she = targetPossessiveFor(templateSource, GenderNumber.She, xdi),
                it = targetPossessiveFor(templateSource, GenderNumber.It, xdi),
                they = targetPossessiveFor(templateSource, GenderNumber.They, xdi)
            };
        }

        public IEnumerable<Objective> getAllObjectives()
        {
            var fields = GetType().GetFields( /* dato che voglio anche i privati, devo aggiungere questo . altrimenti l utente è costretto a dichiarare tutto come public */ System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            foreach (var m in fields)
            {
                var q = m.GetValue(this);
                if (q is Objective o)
                {
                    yield return o;

                }
            }
        }

        public IEnumerable<TextInput> getAllTextInputs()
        {
            var fields = GetType().GetFields( /* dato che voglio anche i privati, devo aggiungere questo . altrimenti l utente è costretto a dichiarare tutto come public */ System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            foreach (var m in fields)
            {
                var q = m.GetValue(this);
                if (q is TextInput o)
                {
                    yield return o;

                }
            }
        }

        public IEnumerable<Explanation> getAllExplanations()
        {
            var fields = GetType().GetFields( /* dato che voglio anche i privati, devo aggiungere questo . altrimenti l utente è costretto a dichiarare tutto come public */ System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            foreach (var m in fields)
            {
                var q = m.GetValue(this);
                if (q is Explanation o)
                {
                    yield return o;

                }
            }
        }

        public IEnumerable<ExplanationWithCont> getAllExplanationsWithCont()
        {
            var fields = GetType().GetFields( /* dato che voglio anche i privati, devo aggiungere questo . altrimenti l utente è costretto a dichiarare tutto come public */ System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            foreach (var m in fields)
            {
                var q = m.GetValue(this);
                if (q is ExplanationWithCont o)
                {
                    yield return o;

                }
            }
        }


        /// <summary>
        /// Numero massimo standard di spiegazioni mostrate per obiettivo o
        /// oggetto. Il gioco può personalizzarlo con un override.
        /// </summary>
        protected virtual int maxExplanationsToShow => 6;

        public  void afterDeserializeComputeExclusions()
        {
            // Il risultato automatico precedente può essere obsoleto se nel
            // frattempo è cambiata la room o lo stato del puzzle. Rimuovo
            // soltanto ciò che aveva aggiunto questo algoritmo, preservando
            // le esclusioni dichiarate esplicitamente dal gioco.
            foreach (var generated in generatedExplanationExclusionsOfObjective)
            {
                if (explanationsToExcludeOfObjective.TryGetValue(generated.Key, out var exclusions))
                {
                    exclusions.RemoveAll(generated.Value.Contains);
                }
            }
            generatedExplanationExclusionsOfObjective.Clear();

            foreach (var generated in generatedExplanationExclusionsOfLo)
            {
                if (explanationsToExcludeOfLo.TryGetValue(generated.Key, out var exclusions))
                {
                    exclusions.RemoveAll(generated.Value.Contains);
                }
            }
            generatedExplanationExclusionsOfLo.Clear();

            var useForHandlersOfObj = useForHandlers.GroupBy(ha => ha.Objective).ToDictionary(x => x.Key, x => x.ToList());

            //// adesso, tra quei template che rimangono, lasciane al massimo 5 per ogni oggetto. ma sempre gli stessi!
            //var allExplanations = exf.Where(ha => ha.Explanation != null).Select(ha => ha.Explanation).Distinct().ToList();

            foreach (var ob in getAllObjectives())
            {
                Explanation[] explanationsDiPartenzaConQuestoOb;
                if (ob.CustomExplanations == null)
                {
                    explanationsDiPartenzaConQuestoOb = getGlobalExplanations();
                }
                else
                {
                    explanationsDiPartenzaConQuestoOb = ob.CustomExplanations;
                }

                var explanationsVisibleWithThisObjective = explanationsDiPartenzaConQuestoOb
                        .Where(te => explanationIsVisible(te))
                        .Where(te => !explanationsToExcludeOfObjective.itemOrEmpty(ob.serId).Contains(te.expId)).ToList();

                var expsNecessarieConQuestoOb =
                        useForHandlersOfObj.itemOrEmpty(ob).Select(ha => ha.Explanation).Distinct().ToHashSet();

                // La scelta delle explanation di disturbo deve essere
                // pseudo-casuale ma stabile per questo obiettivo. Se fosse
                // casuale a ogni apertura, l'utente vedrebbe una explanation
                // corretta fissa e un gruppo di alternative sempre diverso:
                // questo renderebbe sospetta proprio l'alternativa corretta.
                // Il seed derivato dall'ID produce quindi sempre lo stesso
                // insieme di alternative per lo stesso obiettivo.
                var hash = ob.serId.quickIntHashOfString();
                var rand = new Random(hash);
                var quanteExpVoglioAllaFine = maxExplanationsToShow;

                var expsVisibiliCur = new HashSet<Explanation>(explanationsVisibleWithThisObjective);
                if (expsVisibiliCur.Count > quanteExpVoglioAllaFine)
                {
                    while (expsVisibiliCur.Count > quanteExpVoglioAllaFine)
                    {
                        Explanation expCheScelgoPerEssNascosta;
                    again:
                        var iTemplateCheScelgo = rand.Next() % explanationsVisibleWithThisObjective.Count;
                        expCheScelgoPerEssNascosta = explanationsVisibleWithThisObjective[iTemplateCheScelgo];

                        if (expsNecessarieConQuestoOb.Contains(expCheScelgoPerEssNascosta))
                        {
                            goto again;
                        }
                        else if (!expsVisibiliCur.Contains(expCheScelgoPerEssNascosta))
                        {
                            goto again;
                        }
                        else
                        {
                            if (explanationsToExcludeOfObjective.ContainsKey(ob.serId))
                            {
                                explanationsToExcludeOfObjective[ob.serId].Add(expCheScelgoPerEssNascosta.expId);
                            }
                            else
                            {
                                explanationsToExcludeOfObjective[ob.serId] = new List<string> { expCheScelgoPerEssNascosta.expId };
                            }
                            if (!generatedExplanationExclusionsOfObjective.TryGetValue(ob.serId, out var generatedForObjective))
                            {
                                generatedForObjective = new HashSet<string>();
                                generatedExplanationExclusionsOfObjective[ob.serId] = generatedForObjective;
                            }
                            generatedForObjective.Add(expCheScelgoPerEssNascosta.expId);
                            expsVisibiliCur.Remove(expCheScelgoPerEssNascosta);
                        }
                    }
                }
            }

            var combineHandlersOfLo1 = combineHandlers.GroupBy(ha => ha.lo1).ToDictionary(x => x.Key, x => x.ToList());

            // ora riduco il numero di explanations per gli oggetti come il pennello che chiedono una spiegazione E non hanno spiegazione custom
            foreach (var lo in getAllLogicObjects())
            {
                if (lo.IsVerbThatRequiresExplanation)
                {
                    Explanation[] explanationsDiPartenzaConQuestoOb;
                    if (lo.CustomExplanations == null)
                    {
                        explanationsDiPartenzaConQuestoOb = getGlobalExplanations();
                    }
                    else
                    {
                        explanationsDiPartenzaConQuestoOb = lo.CustomExplanations;
                    }

                    var explanationsVisibleWithThisLo = explanationsDiPartenzaConQuestoOb
                            .Where(te => explanationIsVisible(te))
                            // Le esclusioni dichiarate esplicitamente devono
                            // essere applicate prima di calcolare quali altre
                            // explanation nascondere per arrivare al limite.
                            // Se le applichiamo dopo, un oggetto può finire
                            // con 5 explanation invece delle 6 previste.
                            .Where(te => !explanationsToExcludeOfLo.itemOrEmpty(lo.loId).Contains(te.expId))
                            .ToList();

                    var expsNecessarieConQuestoOb =
                            combineHandlersOfLo1.itemOrEmpty(lo).Select(ha => ha.Explanation).Distinct().ToHashSet();

                    // Anche per gli oggetti la casualità è deterministica:
                    // lo stesso oggetto deve proporre sempre le stesse
                    // alternative di disturbo. In caso contrario la
                    // explanation corretta, che resta fissa, risalterebbe
                    // ogni volta che le altre cambiano.
                    var hash = lo.loId.quickIntHashOfString();
                    var rand = new Random(hash);
                    var quanteExpVoglioAllaFine = maxExplanationsToShow;

                    var expsVisibiliCur = new HashSet<Explanation>(explanationsVisibleWithThisLo);
                    if (expsVisibiliCur.Count > quanteExpVoglioAllaFine)
                    {
                        while (expsVisibiliCur.Count > quanteExpVoglioAllaFine)
                        {
                            Explanation expCheScelgoPerEssNascosta;
                        again:
                            var iTemplateCheScelgo = rand.Next() % explanationsVisibleWithThisLo.Count;
                            expCheScelgoPerEssNascosta = explanationsVisibleWithThisLo[iTemplateCheScelgo];

                            if (expsNecessarieConQuestoOb.Contains(expCheScelgoPerEssNascosta))
                            {
                                goto again;
                            }
                            else if (!expsVisibiliCur.Contains(expCheScelgoPerEssNascosta))
                            {
                                goto again;
                            }
                            else
                            {
                                if (explanationsToExcludeOfLo.ContainsKey(lo.loId))
                                {
                                    explanationsToExcludeOfLo[lo.loId].Add(expCheScelgoPerEssNascosta.expId);
                                }
                                else
                                {
                                    explanationsToExcludeOfLo[lo.loId] = new List<string> { expCheScelgoPerEssNascosta.expId };
                                }
                                if (!generatedExplanationExclusionsOfLo.TryGetValue(lo.loId, out var generatedForLo))
                                {
                                    generatedForLo = new HashSet<string>();
                                    generatedExplanationExclusionsOfLo[lo.loId] = generatedForLo;
                                }
                                generatedForLo.Add(expCheScelgoPerEssNascosta.expId);
                                expsVisibiliCur.Remove(expCheScelgoPerEssNascosta);
                            }
                        }
                    }
                }
            }
        }

        public IEnumerable<LogicObj> getAllLogicObjects()
        {
            var fields = GetType().GetFields( /* dato che voglio anche i privati, devo aggiungere questo . altrimenti l utente è costretto a dichiarare tutto come public */ System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            foreach (var m in fields)
            {
                var q = m.GetValue(this);
                if (q is LogicObj o)
                {
                    yield return o;

                }
            }
        }

        //public IEnumerable<Qtok> getAllChiusure()
        //{
        //        foreach (var qt in allQtoks)
        //        {
        //                if (qt.failureContinuationKind == ContinuationKind.EndsSentence && !qt.ExcludeFromChiusure)
        //                        yield return qt;
        //        }

        //        //if (getAllPuzzleSolutions().isEmpty())
        //        //{
        //        //        throw new Exception("non ancora calcolate");
        //        //}
        //        //foreach (var ps in getAllPuzzleSolutions())
        //        //{
        //        //        if (ps.solution.Length == 5)
        //        //        {
        //        //                var ultimoTok = ps.solution.Last();
        //        //                if (ultimoTok is EnumeratedToken etUltimo)
        //        //                {
        //        //                        if (etUltimo.correct.failureContinuationKind == ContinuationKind.EndsSentence)
        //        //                        {
        //        //                                yield return etUltimo.correct;
        //        //                        }
        //        //                }
        //        //        };

        //        //}

        //}

        protected virtual string mapFileName => null;
        /// <summary>Web-root-relative folder containing exported map images.</summary>
        protected virtual string mapImageFolder => "img";

        private string mapImageFileName;
        private double mapImageX;
        private double mapImageY;
        private double mapImageWidth;
        private double mapImageHeight;

        protected virtual Character initialActiveCharacter => null;

        protected virtual void configureObjectLinks()
        {
        }

        protected void registerExplanationGroup(Explanation[] explanations, string customExplanationIntro)
        {
            foreach (var explanation in explanations.Distinct())
            {
                explanationGroupsByMember[explanation] = explanations;
                explanationGroupIntrosByMember[explanation] = customExplanationIntro;
            }
        }

        internal Explanation[] getExplanationGroup(Explanation explanation)
        {
            return explanationGroupsByMember.TryGetValue(explanation, out var group)
                ? group
                : null;
        }

        protected internal string getExplanationGroupIntro(Explanation explanation)
        {
            if (explanation == null)
            {
                return null;
            }

            return explanationGroupIntrosByMember.TryGetValue(explanation, out var intro)
                ? intro
                : null;
        }

        protected void registerUseWithExplanationFamily(params LogicObj[] members)
        {
            combineExplanationFamilies.Add(new CombineExplanationFamily
            {
                Members = members.Distinct().ToArray()
            });
        }

        protected void registerUseWithExplanationContext(
                LogicObj target,
                Func<bool> isActive,
                Explanation[] group,
                string customExplanationIntro,
                params LogicObj[] members)
        {
            combineExplanationContextRules.Add(new CombineExplanationContextRule
            {
                Members = members.Distinct().ToArray(),
                Target = target,
                IsActive = isActive,
                Group = group,
                CustomExplanationIntro = customExplanationIntro
            });
        }

        /// <summary>
        /// Associa in modo esplicito a un'azione Use For la situazione, il
        /// gruppo di Explanation e il relativo preambolo. È l'equivalente
        /// per gli obiettivi di registerUseWithExplanationContext.
        /// </summary>
        protected void registerUseForExplanationContext(
                LogicObj tool,
                Objective objective,
                Func<bool> isActive,
                Explanation[] group,
                string customExplanationIntro)
        {
            useForExplanationContextRules.Add(new UseForExplanationContextRule
            {
                Tool = tool,
                Objective = objective,
                IsActive = isActive,
                Group = group,
                CustomExplanationIntro = customExplanationIntro
            });
        }

        internal IEnumerable<UseForExplanationContextRule> getActiveUseForExplanationContexts(LogicObj tool)
        {
            return useForExplanationContextRules
                .Where(rule => rule.Tool == tool && rule.IsActive())
                .ToList();
        }

        internal bool hasActiveUseForExplanationContext(Objective objective)
        {
            return useForExplanationContextRules.Any(rule =>
                rule.Objective == objective && rule.IsActive());
        }

        internal HashSet<string> explicitExplanationExclusionsOfLo(string loId)
        {
            var explicitExclusions = explanationsToExcludeOfLo
                .itemOrEmpty(loId)
                .ToHashSet();
            if (generatedExplanationExclusionsOfLo.TryGetValue(loId, out var generated))
            {
                explicitExclusions.ExceptWith(generated);
            }
            return explicitExclusions;
        }

        internal LogicObj[] getCombineExplanationFamily(LogicObj first)
        {
            return combineExplanationFamilies
                .Where(family => family.Members.Contains(first))
                .SelectMany(family => family.Members)
                .Distinct()
                .ToArray();
        }

        internal IEnumerable<CombineExplanationContextRule> getActiveCombineExplanationContexts(LogicObj first)
        {
            var activeRules = combineExplanationContextRules
                .Where(rule => rule.Members.Contains(first)
                    && rule.IsActive())
                .ToList();

            foreach (var rulesForTarget in activeRules.GroupBy(rule => rule.Target))
            {
                var firstRule = rulesForTarget.First();
                var hasConflict = rulesForTarget.Skip(1).Any(rule =>
                    rule.CustomExplanationIntro != firstRule.CustomExplanationIntro
                    || !(rule.Group ?? Array.Empty<Explanation>()).Select(ex => ex.expId)
                        .SequenceEqual((firstRule.Group ?? Array.Empty<Explanation>()).Select(ex => ex.expId)));

                if (hasConflict)
                {
                    throw new InvalidOperationException(
                        $"Conflicting active UseWith explanation contexts for first object {first.loId} and target {rulesForTarget.Key.loId}. " +
                        "The active rules specify different explanation groups or introductions.");
                }
            }

            return activeRules;
        }

        // In Casual l'handler esatto viene scelto automaticamente. Se la coppia
        // appartiene a un contesto di explanation, deve però essere considerata
        // disponibile solo quando quel contesto è ancora attivo: è la stessa
        // condizione che impedisce di riproporre un'explanation ormai narrativa-mente
        // superata nella modalità normale.
        internal bool isCombineExplanationAvailableNow(LogicObj first, LogicObj target, Explanation explanation)
        {
            var matchingRules = combineExplanationContextRules
                .Where(rule => rule.Members.Contains(first) && rule.Target == target)
                .ToList();

            if (matchingRules.Count == 0)
            {
                return true;
            }

            return matchingRules.Any(rule => rule.IsActive()
                && (rule.Group ?? Array.Empty<Explanation>()).Contains(explanation));
        }

        internal IReadOnlyList<CombineHandler> getCombineHandlersForFirst(LogicObj first)
        {
            if (combineHandlersByFirst == null)
            {
                combineHandlersByFirst = combineHandlers
                    .GroupBy(handler => handler.lo1)
                    .ToDictionary(group => group.Key, group => group.ToList());
            }

            return combineHandlersByFirst.TryGetValue(first, out var handlers)
                ? handlers
                : Array.Empty<CombineHandler>();
        }

        protected virtual void configureActionHandlers()
        {
        }

        protected virtual void configureRoomHandlers()
        {
        }

        protected virtual void configureGameRestrictions()
        {
        }

        protected virtual void configureCycles()
        {
        }

        /// <summary>
        /// Runs the standard initialization pipeline. It is called by the
        /// engine constructor so a derived game cannot forget it.
        /// </summary>
        private void initializeGame()
        {
            configureObjectLinks();

            if (mapFileName != null)
            {
                readMapJsonAndSetRoomCoords(mapFileName);
            }

            configureActionHandlers();
            configureRoomHandlers();
            configureGameRestrictions();
            configureCycles();

            if (initialActiveCharacter != null)
            {
                ActiveChar = initialActiveCharacter;
            }
        }

        protected WorldBase(string lang)
        {

            CurLang = lang;

            // il mondo registra tutti i suoi membri che sono logicObj e room e objective
            var fields = GetType().GetFields( /* dato che voglio anche i privati, devo aggiungere questo . altrimenti l utente è costretto a dichiarare tutto come public */ System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            foreach (var m in fields)
            {
                var q = m.GetValue(this);
                if (q is LogicObj lo)
                {


                    lo.registerInWorld(this);

                    //foreach (var qt in lo.associatedQToks)
                    //{
                    //        qtokOfId.Add(qt.serId, qt);
                    //        allQtoks.Add(qt);
                    //}

                    // init non serve più con il refactoring. gli event handlers ora sono globali, non negli oggetti.
                    //lo.init(); // il costruttore non poteva usare il world, quindi se ti serve il world, devi usare init.
                    // questo tra l'altro crea i contenitori !

                    // registro nel world i contenitori appena creati
                    //foreach (var conta in lo.containers)
                    //{
                    //    conta.registerInWorld(this);
                    //}



                }
                else if (q is Filler f)
                {
                    fillerOfId.Add(f.FilId, f);
                }
                else if (q is Template t)
                {
                    templateOfId.Add(t.teId, t);
                }
                else if (q is Room r)
                {
                    r.wo = this;


                    r.registerInWorld(this);



                }
                //else if (q is DynLine li)
                //{


                //    li.register_in_world(this);



                //}
                else if (q is Objective o)
                {
                    objectiveOfId.Add(o.serId, o);

                }
                else if (q is UnVerb unVerb)
                {
                    unVerbOfId.Add(unVerb.verbId, unVerb);

                }
                //else if (q is ZeroVerb zVerb)
                //{
                //        zeroVerbOfId.Add(zVerb.verbId, zVerb);

                //}
                else if (q is BinVerb binVerb)
                {
                    binVerbOfId.Add(binVerb.verbId, binVerb);

                }
                //else if (q is Qtok qt)
                //{
                //        qtokOfId.Add(qt.serId, qt);

                //        allQtoks.Add(qt);
                //}
                else if (q is Aspect a)
                {
                    allAspects.Add(a);
                }
                else if (q is AlternatePosition p)
                {
                    allAlternatePositions.Add(p);
                }
                //else 
                //{
                //    throw new Exception("unhandled");

                //}



            }



            ValidateAlternatePositionDefinitions();

            maybeRebuildXmlForTranslation();

            initializeGame();





        }


        //public virtual void serialize(XElement xelWorld)
        //{

        //}


        //public abstract Qtok[] allClosingQtoks();

        public virtual void deserializeMembersCreatedByUsers(XElement xelWorld)
        {
            foreach (var el in xelWorld.Elements("boolVariable"))
            {
                var name = el.Attribute("name").Value;
                var val = bool.Parse(el.Attribute("value").Value);

                System.Reflection.FieldInfo fieldInfo = GetType().GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (fieldInfo != null)
                {
                    fieldInfo.SetValue(this, val);
                }
                else
                {
                    // ho eliminato una var, non serve invalidare il salvataggio
                }
            }

            foreach (var el in xelWorld.Elements("Int32"))
            {
                var name = el.Attribute("name").Value;
                var curVal = int.Parse(el.Attribute("curVal").Value);


                var ob = GetType().GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (ob != null)
                {
                    ob.SetValue(this, curVal);
                }
                // else ho eliminato una var, non fa nietne

            }

            foreach (var el in xelWorld.Elements("UInt64"))
            {
                var name = el.Attribute("name").Value;
                var curVal = ulong.Parse(el.Attribute("curVal").Value);



                GetType().GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(this, curVal);
            }

            foreach (var el in xelWorld.Elements("DateTime"))
            {
                var name = el.Attribute("name").Value;
                var curVal = DateTime.Parse(el.Attribute("curVal").Value, CultureInfo.InvariantCulture);

                var obj = GetType().GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (obj != null)
                {

                    obj.SetValue(this, curVal);
                }
                else
                {
                    // ho eliminato una variabile lasttime dal mondo
                }
            }

            //foreach (var el in xelWorld.Elements("cycle_memory"))
            //{
            //        var cyc = new CycleMemory();
            //        var fieldname = cyc.deserialize(el);

            //        GetType().GetField(fieldname, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(this, cyc);

            //}

        }


        public Dictionary<CycleElemId, int> howManyTimesElementExecuted = new Dictionary<CycleElemId, int>();
        /// <summary>
        /// serve il datetime perché ciò che conta è la sensazione che è passato molto tempo dall'ultima volta che hai detto questa battuta
        /// </summary>
        public Dictionary<CycleElemId, DateTime> lastTimeElementExecuted = new Dictionary<CycleElemId, DateTime>();


        public XDocument serialize()
        {
            var xd = new XDocument();
            var xelRoot = new XElement("root");
            xd.Add(xelRoot);

            xelRoot.Add(new XAttribute("curTime", cur_time));


            xelRoot.Add(new XAttribute("version", 2));



            //serialize(xelRoot); // così il designer può salvare le variabili globail dentro world, tipo curscene




            //xelRoot.Add(new XAttribute("iq_level", iqLevel ?? "" /* non sarà mai null tranne salvataggi vecchi*/));

            xelRoot.Add(new XAttribute("story_mode", StoryMode ? "Y" : "N"));

            xelRoot.Add(new XAttribute("activeChar", activeChar.loId));
            //xelRoot.Add(new XAttribute("curScene", curScene));


            if (CurLang != null)
            {
                xelRoot.Add(new XAttribute("lang", CurLang));
            }





            // serializza automaticamente tutte le var bool , int , e cyclemem
            foreach (var fi in GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
            {
                //if (fi.Name == "tempoNuvoloso")
                //{
                //    var y = 4;

                //    var t = fi.GetType();
                //}
                if (fi.FieldType.Name == "Boolean")
                {
                    var val = fi.GetValue(this);

                    var xel = new XElement("boolVariable");
                    xelRoot.Add(xel);

                    xel.Add(new XAttribute("name", fi.Name));
                    xel.Add(new XAttribute("value", val));
                }
                else if (fi.FieldType.Name == "Int32")
                {
                    var xel = new XElement("Int32");
                    xelRoot.Add(xel);

                    var val = (int)fi.GetValue(this);

                    xel.Add(new XAttribute("name", fi.Name));
                    xel.Add(new XAttribute("curVal", val));


                }
                else if (fi.FieldType.Name == "UInt64")
                {
                    var xel = new XElement("UInt64");
                    xelRoot.Add(xel);

                    var val = (ulong)fi.GetValue(this);

                    xel.Add(new XAttribute("name", fi.Name));
                    xel.Add(new XAttribute("curVal", val));


                }
                else if (fi.FieldType.Name == "DateTime")
                {
                    var xel = new XElement("DateTime");
                    xelRoot.Add(xel);

                    var val = (DateTime)fi.GetValue(this);

                    xel.Add(new XAttribute("name", fi.Name));
                    xel.Add(new XAttribute("curVal", val.ToString(CultureInfo.InvariantCulture)));


                }
                //else if (fi.FieldType.Name == "CycleMemory")
                //{
                //        var cyc = (CycleMemory)fi.GetValue(this);

                //        cyc?.serialize(xelRoot, name: fi.Name);
                //}


            }



            foreach (var fi in GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
            {
                var sy = 4;
                if (fi.PropertyType.Name == "CycleElemId")
                {
                    var cyc = (CycleElemId)fi.GetValue(this);

                    var xel = new XElement("cycleElem");
                    xelRoot.Add(xel);

                    xel.Add(new XAttribute("name", fi.Name));
                    if (howManyTimesElementExecuted.ContainsKey(cyc))
                    {
                        xel.Add(new XAttribute("howMany", howManyTimesElementExecuted[cyc]));
                    }
                    if (lastTimeElementExecuted.ContainsKey(cyc))
                    {
                        xel.Add(new XAttribute("lastTime", lastTimeElementExecuted[cyc].ToString(CultureInfo.InvariantCulture)));
                    }
                }
            }

            foreach (var cyc in howManyTimesElementExecuted.Keys
                         .Concat(lastTimeElementExecuted.Keys)
                         .Where(c => c?.StableId != null)
                         .GroupBy(c => c.StableId, StringComparer.Ordinal)
                         .Select(g => g.First()))
            {
                var xel = new XElement("cycleElem");
                xelRoot.Add(xel);
                xel.Add(new XAttribute("id", cyc.StableId!));
                if (howManyTimesElementExecuted.ContainsKey(cyc))
                    xel.Add(new XAttribute("howMany", howManyTimesElementExecuted[cyc]));
                if (lastTimeElementExecuted.ContainsKey(cyc))
                    xel.Add(new XAttribute("lastTime", lastTimeElementExecuted[cyc].ToString(CultureInfo.InvariantCulture)));
            }
















            // salvo il game state

            if (gs is GameStateShowingQuestions gsq)
            {

                var xelGsq = new XElement("gameStateShowingQuestions");
                xelRoot.Add(xelGsq);

                xelGsq.Add(new XAttribute("dialogId", gsq.dialog.id));



            }
            else if (gs is GameStateWaitingForText gst)
            {
                var xelGst = new XElement("GameStateWaitingForText");
                xelRoot.Add(xelGst);

                xelGst.Add(new XAttribute("serId", gst.textInput.serId));

            }
            else if (gs is GameStateCutScene gsc)
            {

                var xelGsc = new XElement("gameStateCutScene");
                xelRoot.Add(xelGsc);


                if (gsc.afterCutSceneShowDialog != null)
                {
                    xelGsc.Add(new XAttribute("afterCutSceneShowDialog", gsc.afterCutSceneShowDialog.dialog.id));
                }

                if (gsc.afterCutSceneWaitForTextInput != null)
                {
                    xelGsc.Add(new XAttribute("afterCutSceneWaitForTextInput", gsc.afterCutSceneWaitForTextInput.textInput.serId));
                }


                if (gsc.afterCutSceneGameFinished != null)
                {
                    xelGsc.Add(new XAttribute("afterCutSceneGameFinished", "yes"));
                }

                xelGsc.Add(new XAttribute("iCurToken", gsc.iCurToken));

                //xelGsc.Add(new XAttribute("gcsCanBeSkipped", gsc.gcsCanBeSkipped));

                var csToSer = gsc.cs;

                serializeCutScene(xelGsc, csToSer);

            }
            else if (gs is GameStateFinished)
            {
                var xelGsr = new XElement("gameStateFinished");
                xelRoot.Add(xelGsr);

            }
            else if (gs is GameStateViewingRoom)
            {
                var xelGsr = new XElement("gameStateViewingRoom");
                xelRoot.Add(xelGsr);

            }
            else
            {
                throw new NotImplementedException();
            }












            foreach (var d in dialogsToSerialize())
            {
                eng.serializzaDialogoToXml(xelRoot, d);
            }



            foreach (var cp in curParty)
            {
                var elc = new XElement("curParty");
                xelRoot.Add(elc);
                elc.Add(new XAttribute("loId", cp.loId));
            }




            //salvo le exit perché ora sono dinamiche
            foreach (var exit in exits)
            {
                var el = new XElement("exit");
                xelRoot.Add(el);
                exit.serialize(el);
            }


            // salvo lo stato di tutti i logicobj definiti nel world. anche quelli creati dniamicamente.

            foreach (var lo in loOfId.Values)
            {
                var el = new XElement("logicObj");
                xelRoot.Add(el);

                //var lo = (logicObjE)q;
                lo.serialize(el);

            }

            // salvo lo stato di tutti i verbi --- perché i verbi hanno uno stato, perché potrebbero essere abilità non ancora acquisite.
            foreach (var lo in unVerbOfId.Values)
            {
                var el = new XElement("unVerb");
                xelRoot.Add(el);


                lo.serialize(el);

            }

            //foreach (var lo in zeroVerbOfId.Values)
            //{
            //        var el = new XElement("zeroVerb");
            //        xelRoot.Add(el);


            //        lo.serialize(el);

            //}

            foreach (var lo in binVerbOfId.Values)
            {
                var el = new XElement("binVerb");
                xelRoot.Add(el);


                lo.serialize(el);

            }









            // salvare lo stato delle room( se ce l'hanno)


            foreach (var ro in roomOfId.Values)
            {
                var el = new XElement("room");
                xelRoot.Add(el);

                ro.serialize(el);

            }




            // salvo lo stato degli obiettivi
            foreach (var o in objectiveOfId.Values)
            {
                o.serialize(xelRoot);
            }


            // gli obiettivi attuali
            foreach (var o in curObjectives)
            {
                var el = new XElement("cur_objective");
                xelRoot.Add(el);

                el.Add(new XAttribute("id", o.serId));
            }

            foreach (var o in allSeenObjectives)
            {
                var el = new XElement("seen_objective");
                xelRoot.Add(el);

                el.Add(new XAttribute("id", o.serId));
            }

            //foreach (var o in curDangerSituations)
            //{
            //    var el = new XElement("cur_danger_situation");
            //    xelRoot.Add(el);

            //    el.Add(new XAttribute("id", o.serId));
            //}

            //foreach (var o in allSeenDangerSituations)
            //{
            //    var el = new XElement("seen_danger_situation");
            //    xelRoot.Add(el);

            //    el.Add(new XAttribute("id", o.serId));
            //}



            // XML persistence does not assign meaning to the physical order of
            // past actions. Administrative consumers sort by dateTime when
            // presenting a timeline, so avoid allocating an ordered copy here.
            foreach (var pa in pastActions)
            {
                if (pa is PastActionTerBin pat)
                {
                    var el = new XElement("past_action_ter_bin");
                    xelRoot.Add(el);


                    el.Add(new XAttribute("binVerbId", pat.binVerb.verbId));
                    el.Add(new XAttribute("loId", pat.lo.loId));

                    el.Add(new XAttribute("objectiveId", pat.puzzle.serId));
                    el.Add(new XAttribute("time", pa.dateTime.ToString(CultureInfo.InvariantCulture)));
                }
                else if (pa is PastActionTerUn patu)
                {
                    var el = new XElement("past_action_ter_un");
                    xelRoot.Add(el);


                    el.Add(new XAttribute("unVerbId", patu.unVerb.verbId));
                    el.Add(new XAttribute("loId", patu.lo.loId));

                    el.Add(new XAttribute("objectiveId", patu.puzzle.serId));
                    el.Add(new XAttribute("time", pa.dateTime.ToString(CultureInfo.InvariantCulture)));
                }
                else if (pa is PastActionMove patm)
                {
                    var el = new XElement("past_action_move");
                    xelRoot.Add(el);


                    el.Add(new XAttribute("roomId", patm.room.roomId));

                    el.Add(new XAttribute("time", pa.dateTime.ToString(CultureInfo.InvariantCulture)));
                }

                else if (pa is PastActionQuat paq)
                {
                    var el = new XElement("past_action_qua");
                    xelRoot.Add(el);


                    el.Add(new XAttribute("binVerbId", paq.binVerb.verbId));
                    el.Add(new XAttribute("lo1Id", paq.lo1.loId));
                    el.Add(new XAttribute("lo2Id", paq.lo2.loId));

                    el.Add(new XAttribute("objectiveId", paq.puzzle.serId));
                    el.Add(new XAttribute("time", pa.dateTime.ToString(CultureInfo.InvariantCulture)));
                }

                //else if (pa is PastActionUn pau)
                //{
                //        var el = new XElement("past_action_un");
                //        xelRoot.Add(el);




                //        el.Add(new XAttribute("zeroVerbId", pau.zeroVerb.verbId));
                //        el.Add(new XAttribute("time", pa.dateTime.ToString(CultureInfo.InvariantCulture)));
                //}
                else if (pa is PastActionSolvePuzzle pas)
                {
                    var el = new XElement("past_action_solve_puzzle");
                    xelRoot.Add(el);




                    el.Add(new XAttribute("solution", pas.Solution));
                    el.Add(new XAttribute("time", pas.dateTime.ToString(CultureInfo.InvariantCulture)));
                }
                else if (pa is PastActionSubmitText patt)
                {
                    var el = new XElement("past_action_submit_text");
                    xelRoot.Add(el);




                    el.Add(new XAttribute("textTyped", patt.TextTyped));
                    if (patt.TextTyped2.is_not_null_or_white())
                    {
                        el.Add(new XAttribute("textTyped2", patt.TextTyped2));
                    }
                    el.Add(new XAttribute("time", patt.dateTime.ToString(CultureInfo.InvariantCulture)));

                    if (patt.explId.is_not_null_or_white())
                    {
                        el.Add(new XAttribute("expl_id", patt.explId));
                    }
                }
                else if (pa is PastActionCancelText paca)
                {
                    var el = new XElement("past_action_cancel_text");
                    xelRoot.Add(el);


                    el.Add(new XAttribute("time", paca.dateTime.ToString(CultureInfo.InvariantCulture)));
                }
                else if (pa is PastActionUseWith pauw)
                {
                    var el = new XElement("past_action_use_with");
                    xelRoot.Add(el);

                    el.Add(new XAttribute("full_text", pauw.FullText));

                    el.Add(new XAttribute("lo1Id", pauw.lo1.loId));
                    el.Add(new XAttribute("lo2Id", pauw.lo2.loId));


                    if (pauw.exp != null)
                    {
                        el.Add(new XAttribute("expl", pauw.exp.expId));
                    }




                    if (pauw.handlerCalled != null)
                    {
                        el.Add(new XAttribute("handler_called", pauw.handlerCalled.Value ? "Y" : "N"));
                    }


                    el.Add(new XAttribute("time", pa.dateTime.ToString(CultureInfo.InvariantCulture)));
                }
                else if (pa is PastActionUseFor pauf)
                {
                    var el = new XElement("past_action_use_for");
                    xelRoot.Add(el);



                    el.Add(new XAttribute("loId", pauf.lo.loId));
                    el.Add(new XAttribute("objId", pauf.ob.serId));


                    if (pauf.exp != null)
                    {
                        el.Add(new XAttribute("expl", pauf.exp.expId));
                    }

                    if (pauf.handlerCalled != null)
                    {
                        el.Add(new XAttribute("handler_called", pauf.handlerCalled.Value ? "Y" : "N"));
                    }




                    el.Add(new XAttribute("time", pa.dateTime.ToString(CultureInfo.InvariantCulture)));
                }
                else if (pa is PastActionIsActually pais)
                {
                    var el = new XElement("past_action_is_actually");
                    xelRoot.Add(el);


                    el.Add(new XAttribute("sentence", pais.completeSentence));

                    el.Add(new XAttribute("loId", pais.lo.loId));




                    el.Add(new XAttribute("exp1", pais.exp1.expId));
                    el.Add(new XAttribute("exp2", pais.exp2.expId));





                    el.Add(new XAttribute("time", pa.dateTime.ToString(CultureInfo.InvariantCulture)));
                }
                else if (pa is PastActionPickup ppi)
                {
                    var el = new XElement("past_action_pick_up");
                    xelRoot.Add(el);



                    el.Add(new XAttribute("lo", ppi.lo.loId));


                    el.Add(new XAttribute("time", pa.dateTime.ToString(CultureInfo.InvariantCulture)));
                }
                else if (pa is PastActionLookRemember pl)
                {
                    var el = new XElement("past_action_look");
                    xelRoot.Add(el);


                    el.Add(new XAttribute("lo", pl.lo.loId));
                    el.Add(new XAttribute("full_text", pl.fullText));

                    el.Add(new XAttribute("time", pa.dateTime.ToString(CultureInfo.InvariantCulture)));
                }
                else if (pa is PastActionUseHere puh)
                {
                    var el = new XElement("past_action_use_here");
                    xelRoot.Add(el);


                    el.Add(new XAttribute("lo", puh.lo.loId));
                    el.Add(new XAttribute("full_text", puh.fullText));

                    el.Add(new XAttribute("time", pa.dateTime.ToString(CultureInfo.InvariantCulture)));
                }
                else if (pa is PastActionAskForHint puhi)
                {
                    var el = new XElement("past_action_ask_hint");
                    xelRoot.Add(el);


                    el.Add(new XAttribute("obj", puhi.pu.serId));

                    el.Add(new XAttribute("time", pa.dateTime.ToString(CultureInfo.InvariantCulture)));
                }
            }







            foreach (var n in namedCutScenesSeen)
            {
                var el = new XElement("named_cut_scene");
                xelRoot.Add(el);

                el.Add(new XAttribute("ser_id", n.id.serId));
                el.Add(new XAttribute("title", n.id.titleUntranslated));

                n.serialize(el);

                el.Add(new XAttribute("room_id", n.roomDoveEri.roomId));

                Debug.Assert(n.cs.canBeSkipped);
                serializeCutScene(el, n.cs);

                foreach (var mo in n.oggettiMenzionati)
                {
                    switch (mo)
                    {
                        case LogicObj lo:
                            {
                                var moel = new XElement("oggetto_menzionato");
                                el.Add(moel);

                                moel.Add(new XAttribute("lo_id", lo.loId));
                                break;
                            }
                        case Objective ob:
                            {
                                var moel = new XElement("mentioned_objective");
                                el.Add(moel);

                                moel.Add(new XAttribute("ser_id", ob.serId));
                                break;
                            }
                        default:
                            throw new Exception("vfiovfijk");
                    }
                }
            }


            return xd;
        }

        private static void serializeCutScene(XElement xelCutScene, CutScene csToSer)
        {
            xelCutScene.Add(new XAttribute("canBeSkipped", csToSer.canBeSkipped));

            foreach (var tok in csToSer)
            {


                if (tok is NarToken nt)
                {


                    nt.serialize(xelCutScene);
                }
                else if (tok is DialogToken dt)
                {


                    var xelDial = new XElement("cutSceneToken", new XAttribute("type", "dialog"));
                    xelCutScene.Add(xelDial);


                    xelDial.Add(new XAttribute("size", (int)dt.ntSize));

                    xelDial.Add(new XAttribute("charName", dt.dtCharName));

                    if (dt.img != null)
                    {
                        xelDial.Add(new XAttribute("img", dt.img));
                    }
                    xelDial.Add(new XAttribute("par", dt.dtPar));

                    //xelDial.Add(new XAttribute("canBeSkipped", dt.cstCanGoBackToPrevious));

                }
                else if (tok is NarTokenMultipar tokmp)
                {

                    var xelMulti = new XElement("cutSceneToken", new XAttribute("type", "narMultipar"));
                    xelCutScene.Add(xelMulti);

                    if (tokmp.img != null)
                    {
                        xelMulti.Add(new XAttribute("img", tokmp.img));
                    }

                    foreach (var par in tokmp.pars)
                    {
                        var xelPar = new XElement("par", new XAttribute("par", par));
                        xelMulti.Add(xelPar);
                    }

                    //xelMulti.Add(new XAttribute("canBeSkipped", tokmp.cstCanBeSkipped));
                }
                else
                {
                    throw new Exception("gr8u8j2");
                }
            }
        }

        public void execNextInCycle(IEnumerable<CycleElement> cycl/*, ref CycleMemory mem*/)
        {
            ////inizializzo se necessario

            //var cycleElements = cycl.ToList();
            //if (mem == null)
            //{
            //        // succede per le memory non delle stanza, ma dichiarate come var globali. inizialmente sono null.

            //        mem = new CycleMemory();

            //        //mem = new cycle_memory
            //        //{
            //        //    next_element_to_try = 0,
            //        //    how_many_times_element_executed = new Dictionary<int, int>()

            //        //};
            //        //for (var i = 0; i < cycl.Count(); i++)
            //        //{
            //        //    mem.how_many_times_element_executed.Add(i, 0);
            //        //}

            //}


            execNextInCycleAux(cycl/*, mem*/);

        }

        private void execNextInCycleAux(IEnumerable<CycleElement> cycl/*, CycleMemory mem*/)
        {





            //inizializzo se necessario
            var cycleElements = cycl.ToList();


            // non serve piu dopo il redesign copn gli id
            //if (mem.howManyTimesElementExecuted.Count != cycleElements.Count)
            //{
            //        // ricostruisco il dizionario. succede per le memory delle stanze, che non sono null ma non sono neppure inizializzate correttamente
            //        mem.howManyTimesElementExecuted.Clear();
            //        for (var i = 0; i < cycleElements.Count; i++)
            //        {
            //                mem.howManyTimesElementExecuted.Add(i, 0);
            //        }

            //}




            var cyc = cycleElements.ToList();
            // se c'è un elemento del ciclo la cui condiz è verificata
            if (CycleMemory.wouldSaySomething(cyc, this)) // se c'è qualcosa da dire...
            {


                int indexOfNextElementToTry;

                // se c'è qualcosa DI NUOVO da dire, ignora tutte le cose non nuove! altrimenti rischi di ripetere cose vecchie prima di arrivare alle nuove.

                var c_e_qualcosaDiNuovoDaDire = CycleMemory.wouldSaySomethingNew(cyc, this);
                var ignoraLeCoseNonNuove = c_e_qualcosaDiNuovoDaDire;
                // se c'è qualcosa di nuovo, devo anche resettare il contatore a zero, perché le cose nuove devono essere dette in ordine di importanza
                if (c_e_qualcosaDiNuovoDaDire)
                {
                    indexOfNextElementToTry = 0;
                }
                else
                {
                    // niente di nuovo da dire. devo trovare la prossima cosa da dire, ciclando.
                    var elementiDetti = cyc.Where(x => lastTimeElementExecuted.ContainsKey(x.Id) && lastTimeElementExecuted[x.Id] != default(DateTime)).ToList();
                    if (elementiDetti.isEmpty())
                    {
                        indexOfNextElementToTry = 0;
                    }
                    else
                    {
                        // trova quello detto più di recente
                        var maxTime = elementiDetti.Select(x => lastTimeElementExecuted[x.Id]).Max();
                        var lastElementSaid = elementiDetti.Where(el => lastTimeElementExecuted[el.Id] == maxTime).Last();

                        var indexOfLastSaid = cyc.IndexOf(lastElementSaid);
                        indexOfNextElementToTry = indexOfLastSaid + 1;

                        if (indexOfNextElementToTry >= cyc.Count)
                        {
                            indexOfNextElementToTry = 0;
                        }
                    }
                }

            riprova:

                var curEl = cyc[indexOfNextElementToTry];

                DateTime? lastTimeExecuted;
                if (lastTimeElementExecuted.ContainsKey(curEl.Id))
                {
                    lastTimeExecuted = lastTimeElementExecuted[curEl.Id];
                }
                else
                {
                    lastTimeExecuted = null;
                }




                if ( /* se la condizione è verificata */curEl.cond(lastTimeExecuted)
                                                        && /* e questo elemento non è stato ripetuto troppe volte */(curEl.repeat == Repeat.Forever || howManyTimesElementExecuted[curEl.Id] == 0)
                                                        && /* e non è da saltare perché non nuovo e ci sono cose nuove da dire */
                                                        (howManyTimesElementExecuted[curEl.Id] == 0 || !ignoraLeCoseNonNuove)
                )
                {
                    curEl.action(lastTimeExecuted);
                    howManyTimesElementExecuted[curEl.Id] = howManyTimesElementExecuted[curEl.Id] + 1;
                    lastTimeElementExecuted[curEl.Id] = DateTime.Now;

                    //indexOfNextElementToTry++;

                    //if (indexOfNextElementToTry >= cyc.Count)
                    //{
                    //        indexOfNextElementToTry = 0;
                    //} 

                }
                else
                {
                    indexOfNextElementToTry++;

                    if (indexOfNextElementToTry >= cyc.Count)
                    {
                        indexOfNextElementToTry = 0;
                    }

                    goto riprova;
                }
            }


        }

        //public void execNextInCycle(IEnumerable<CycleElement> priorityListWithConditions)
        //{
        //        execNextInCycle(priorityListWithConditions/*, ref curRoom.cycle_mem*/);

        //}


        /// <summary>
        /// when an important event occurs, you need to reset all room cycles, so the next time the player enters that room, players will immediately talk about that event. If you don't call this,
        /// he may first talk about less important things, which would be unrealistic.
        /// </summary>
        public void importantEventHappenedResetAllRoomCycles()
        {
            // non dovrebbe servire più, perche tanto se ci sono cose nuove, mai dette, ora prevalgono, anche se il cursore le ha superate
            //foreach (var r in roomOfId.Values)
            //{
            //        r.cycle_mem.nextElementToTry = 0;
            //}
        }

        public List<LogicObj> mergedInvOfAllCharsInParty()
        {
            return curParty.Select(ch => ch.inv.ToList()).Aggregate((i1, i2) =>
            {

                var r = i1.Concat(i2).ToList();

                return r;
            }).ToList();
        }


        public abstract void setStartState();

        //public abstract Dialog getInitialDialog();



        public void deserialize(XDocument xd, out bool savegameInvalid)
        {

            var xelRoot = xd.Root;


            var xatVer = xelRoot.Attribute("version");

            if (xatVer == null || xatVer.Value != "2")
            {
                savegameInvalid = true;
                return;
            }

            deserializeMembersCreatedByUsers(xelRoot); // così carica le variabili globali dentro world, tipo curscene


            //iqLevel = xelRoot.Attribute("iq_level")?.Value;

            XAttribute atStoryMode = xelRoot.Attribute("story_mode");
            StoryMode = atStoryMode?.Value == "N" ? false : true;

            var acId = xelRoot.Attribute("activeChar").Value;
            activeChar = loOfId[acId] as Character;


            if (xelRoot.Attribute("lang") != null)
            {
                CurLang = xelRoot.Attribute("lang").Value;
            }

            //curScene = int.Parse( xd.Root.Attribute("curScene").Value);

            cur_time = ulong.Parse(xd.Root.Attribute("curTime").Value);









            // carico il game state
            var xelGsq = xelRoot.Element("gameStateShowingQuestions");
            var xelGsc = xelRoot.Element("gameStateCutScene");
            var xelGsr = xelRoot.Element("gameStateViewingRoom");
            var xelGst = xelRoot.Element("GameStateWaitingForText");
            var xelGsf = xelRoot.Element("gameStateFinished");

            if (xelGsr != null)
            {
                gs = new GameStateViewingRoom();
            }
            else if (xelGsf != null)
            {
                gs = new GameStateFinished();
            }
            else if (xelGsq != null)
            {
                var dialId = xelGsq.Attribute("dialogId").Value;

                var allDialogs = getListOfAllDialogsInWorldAndInChars();

                var dial = allDialogs.Single(d => d.id == dialId);

                gs = new GameStateShowingQuestions
                {
                    dialog = dial
                };
            }
            else if (xelGsc != null)
            {
                var iCurToken = int.Parse(xelGsc.Attribute("iCurToken").Value);

                var afterCutSceneShowDialog = deserEventualeDialogoDopoLaCutScene(xelGsc);

                var afterCutSceneWaitForText = deserEventualeWaitingForTextDopoLaCutScene(xelGsc);

                var cs = deserializeCutScene(xelGsc);

                gs = new GameStateCutScene(

                        iCurToken: iCurToken,
                        afterCutSceneShowDialog: afterCutSceneShowDialog,
                        afterCutSceneWaitForTextInput: afterCutSceneWaitForText,
                        afterCutSceneGameFinished: deserEventualeEndGameDopoLaCutScene(xelGsc),
                        cs: cs

                );
            }
            else if (xelGst != null)
            {
                var serId = xelGst.Attribute("serId").Value;

                var allTextInputs = getAllTextInputs();

                var ti = allTextInputs.Single(d => d.serId == serId);

                gs = new GameStateWaitingForText(ti);

            }
            else
            {
                throw new Exception("dfgfg34f");
            }













            foreach (var elDial in xelRoot.Elements("dialog"))
            {
                var dialogId = elDial.Attribute("id").Value;
                var dial = dialogsToSerialize().SingleOrDefault(d => d.id == dialogId);

                if (dial != null)
                {

                    eng.deserializzaDialogoDaXel(elDial, dial);
                }
                else
                {
                    // ho elilminato un dialogo dal gioco
                }
            }







            curParty.Clear();
            var party = xelRoot.Elements("curParty");
            foreach (var elp in party)
            {
                var loId = elp.Attribute("loId").Value;
                var lo = loOfId[loId] as Character;
                curParty.Add(lo);
            }








            //seconda passata: ora tutti gli ogg sono istanziati: deserializzo


            foreach (var xelLo in xelRoot.Elements("logicObj"))
            {
                var loId = xelLo.Attribute("loId").Value;

                if (loOfId.ContainsKey(loId))
                {

                    var lo = loOfId[loId];

                    lo.deserialize(xelLo, out savegameInvalid); // adesso tutti i continaer dovrebbero essere stati creati. se crasha qui, probabilmente nell'oggetto ti sei scordato di fare containers.add(mContainer)
                    if (savegameInvalid)
                    {
                        return;
                    }

                }
                else
                {
                    // l'oggetto  nell xml non è più nel mondo , l'ho tolto. lo ignoro
                }

            }


            // restore dello stato dei verbi 
            foreach (var xelLo in xelRoot.Elements("unVerb"))
            {
                var loId = xelLo.Attribute("serId").Value;

                var lo = unVerbOfId[loId];

                lo.deserialize(xelLo);

            }

            //foreach (var xelLo in xelRoot.Elements("zeroVerb"))
            //{
            //        var loId = xelLo.Attribute("serId").Value;

            //        if (zeroVerbOfId.ContainsKey(loId))
            //        {
            //                var lo = zeroVerbOfId[loId];

            //                lo.deserialize(xelLo);
            //        }
            //        else
            //        {
            //                // ho tolto il verbo dal mondo ma è rimasto nell'xml, salvataggio vecchio. lo ignoro
            //        }

            //}

            foreach (var xelLo in xelRoot.Elements("binVerb"))
            {
                var loId = xelLo.Attribute("serId").Value;

                var lo = binVerbOfId[loId];

                lo.deserialize(xelLo);

            }



            // deserializza lo stato degli obiettivi
            foreach (var xelOb in xelRoot.Elements("objective"))
            {
                var oid = xelOb.Attribute("ser_id").Value;
                if (objectiveOfId.ContainsKey(oid))
                {
                    var ob = objectiveOfId[oid];
                    ob.deserialize(xelOb);
                }
                else
                {
                    // obietivo rimosso nel mondo.. possiamo continuare.
                }



            }





            // ora deserializza gli obiettivi correnti
            curObjectives.Clear();
            foreach (var xelOb in xelRoot.Elements("cur_objective"))
            {
                var oid = xelOb.Attribute("id").Value;

                if (objectiveOfId.ContainsKey(oid))
                {
                    var o = objectiveOfId[oid];
                    curObjectives.Add(o);
                }
                else
                {
                    // ho eliminato un obiettivo... non serve invalidare il salvataggio
                }
            }

            // ora deserializza gli obiettivi visti
            allSeenObjectives.Clear();
            foreach (var xelOb in xelRoot.Elements("seen_objective"))
            {
                var oid = xelOb.Attribute("id").Value;

                if (objectiveOfId.ContainsKey(oid))
                {
                    var o = objectiveOfId[oid];
                    allSeenObjectives.Add(o);
                }
            }





            // uscite (che ora sono dinamiche)
            var exits0 = (from e in exits where e.From.roomId == "roomDesertCaves" || e.To.roomId == "roomDesertCaves" select e).ToList();
            exits.Clear();
            foreach (var elExit in xelRoot.Elements("exit"))
            {
                var from = elExit.Attribute("from").Value;


                var to = elExit.Attribute("to").Value;

                if (roomOfId.ContainsKey(from) && roomOfId.ContainsKey(to))
                {
                    var fromR = roomOfId[from];
                    var toR = roomOfId[to];
                    addExit(fromR, toR);
                }
                else
                {
                    // ho tolto una room  a salvataggio iniziato.
                }


            }
            var exits2 = (from e in exits where e.From.roomId == "roomDesertCaves" || e.To.roomId == "roomDesertCaves" select e).ToList();








            var lAllExpl_ = getAllExplanations();
            var dicExplOfId = lAllExpl_.ToDictionary(x => x.expId);

            var lAllExpWithCont_ = getAllExplanationsWithCont();
            var dicExplWithContOfid = lAllExpWithCont_.ToDictionary(x => x.expId);

            pastActions.Clear();

            foreach (var xelPa in xelRoot.Elements("past_action_use_with"))
            {

                var lo1id = xelPa.Attribute("lo1Id").Value;
                var lo2id = xelPa.Attribute("lo2Id").Value;
                if (loOfId.ContainsKey(lo1id) && loOfId.ContainsKey(lo2id))
                {
                    var lo1 = loOfId[lo1id];
                    var lo2 = loOfId[lo2id];


                    Explanation exp;
                    var xatExp = xelPa.Attribute("expl");
                    if (xatExp != null)
                    {
                        //exp = lAllExpl.SingleOrDefault(ex => ex.expId == xatExp.Value);

                        exp = dicExplOfId.itemOrDefault(xatExp.Value);
                    }
                    else
                    {
                        exp = null;
                    }

                    var fulltext = xelPa.Attribute("full_text").Value;

                    var handlerCalledAt = xelPa.Attribute("handler_called");
                    bool? handlerCalled;
                    if (handlerCalledAt == null)
                    {
                        handlerCalled = null;
                    }
                    else if (handlerCalledAt.Value == "Y")
                    {
                        handlerCalled = true;
                    }
                    else
                    {
                        handlerCalled = false;
                    }

                    var time = DateTime.Parse(xelPa.Attribute("time").Value, CultureInfo.InvariantCulture);

                    var pa = new PastActionUseWith(handlerCalled, lo1, lo2, exp, fulltext, time);
                    pastActions.Add(pa);
                }
            }







            foreach (var xelPa in xelRoot.Elements("past_action_use_for"))
            {

                var loId = xelPa.Attribute("loId").Value;
                var objId = xelPa.Attribute("objId").Value;
                if (loOfId.ContainsKey(loId) && objectiveOfId.ContainsKey(objId))
                {
                    var lo = loOfId[loId];
                    var obj = objectiveOfId[objId];


                    Explanation exp;
                    var xatExp = xelPa.Attribute("expl");
                    if (xatExp != null)
                    {
                        //exp = lAllExpl.SingleOrDefault(ex => ex.expId == xatExp.Value);
                        exp = dicExplOfId.itemOrDefault(xatExp.Value);
                    }
                    else
                    {
                        exp = null;
                    }

                    var handlerCalledAt = xelPa.Attribute("handler_called");
                    bool? handlerCalled = handlerCalledAt == null ? null : handlerCalledAt.Value == "Y";
                    var time = DateTime.Parse(xelPa.Attribute("time").Value, CultureInfo.InvariantCulture);

                    var pa = new PastActionUseFor(lo, obj, exp, time, handlerCalled);
                    pastActions.Add(pa);
                }
            }

            foreach (var xelPa in xelRoot.Elements("past_action_is_actually"))
            {

                var loId = xelPa.Attribute("loId").Value;

                if (loOfId.ContainsKey(loId))
                {
                    var lo = loOfId[loId];

                    var exp1Id = xelPa.Attribute("exp1").Value;
                    var exp2Id = xelPa.Attribute("exp2").Value;

                    var sentence = xelPa.Attribute("sentence").Value;




                    try
                    {
                        //var exp1 = lAllExpWithCont.SingleOrDefault(ex => ex.expId == exp1Id);
                        var exp1 = dicExplWithContOfid.itemOrDefault(exp1Id);

                        var exp2 = exp1?.Continuations.SingleOrDefault(co => co.expId == exp2Id);

                        if (exp2 == null || exp1 == null)
                        {
                            // ho eliminato una exp. ignoro
                        }
                        else
                        {
                            var time = DateTime.Parse(xelPa.Attribute("time").Value, CultureInfo.InvariantCulture);

                            var pa = new PastActionIsActually(sentence, lo, exp1, exp2, time);
                            pastActions.Add(pa);
                        }
                    }
                    catch
                    {
                        // eliminato una exp. ignoro
                    }
                }
            }






            foreach (var xelPa in xelRoot.Elements("past_action_pick_up"))
            {

                var lo = loOfId[xelPa.Attribute("lo").Value];




                var time = DateTime.Parse(xelPa.Attribute("time").Value, CultureInfo.InvariantCulture);

                var pa = new PastActionPickup { lo = lo, dateTime = time };
                pastActions.Add(pa);
            }



            foreach (var xelPa in xelRoot.Elements("past_action_look"))
            {

                var lo = loOfId[xelPa.Attribute("lo").Value];
                var fulltext = xelPa.Attribute("full_text").Value;


                var time = DateTime.Parse(xelPa.Attribute("time").Value, CultureInfo.InvariantCulture);

                var pa = new PastActionLookRemember(lo, fulltext, time);
                pastActions.Add(pa);
            }



            foreach (var xelPa in xelRoot.Elements("past_action_use_here"))
            {

                string loId = xelPa.Attribute("lo").Value;
                if (loOfId.ContainsKey(loId)) // altrimenti ho eliminato un oggetto
                {
                    var lo = loOfId[loId];
                    var fulltext = xelPa.Attribute("full_text").Value;


                    var time = DateTime.Parse(xelPa.Attribute("time").Value, CultureInfo.InvariantCulture);

                    var pa = new PastActionUseHere(lo, fulltext, time);
                    pastActions.Add(pa);
                }
            }


            foreach (var xelPa in xelRoot.Elements("past_action_ask_hint"))
            {

                string puId = xelPa.Attribute("obj").Value;
                if (objectiveOfId.ContainsKey(puId)) // altrimenti ho eliminato un obiettivo
                {
                    var obj = objectiveOfId[puId];



                    var time = DateTime.Parse(xelPa.Attribute("time").Value, CultureInfo.InvariantCulture);

                    var pa = new PastActionAskForHint { pu = obj, dateTime = time };
                    pastActions.Add(pa);
                }
            }

            foreach (var xelPa in xelRoot.Elements("past_action_ter_un"))
            {

                var lo = loOfId[xelPa.Attribute("loId").Value];
                var unVerb = unVerbOfId[xelPa.Attribute("unVerbId").Value];
                var obje = objectiveOfId[xelPa.Attribute("objectiveId").Value];
                var time = DateTime.Parse(xelPa.Attribute("time").Value, CultureInfo.InvariantCulture);

                var pa = new PastActionTerUn { unVerb = unVerb, lo = lo, puzzle = obje, dateTime = time };
                pastActions.Add(pa);
            }

            foreach (var xelPa in xelRoot.Elements("past_action_qua"))
            {

                var lo1 = loOfId[xelPa.Attribute("lo1Id").Value];
                var lo2 = loOfId[xelPa.Attribute("lo2Id").Value];
                var binVerb = binVerbOfId[xelPa.Attribute("binVerbId").Value];
                var obje = objectiveOfId[xelPa.Attribute("objectiveId").Value];
                var time = DateTime.Parse(xelPa.Attribute("time").Value, CultureInfo.InvariantCulture);

                var pa = new PastActionQuat { binVerb = binVerb, lo1 = lo1, lo2 = lo2, puzzle = obje, dateTime = time };
                pastActions.Add(pa);
            }


            //foreach (var xelPa in xelRoot.Elements("past_action_un"))
            //{
            //        string verbid = xelPa.Attribute("zeroVerbId").Value;
            //        if (zeroVerbOfId.ContainsKey(verbid))
            //        {

            //                var zeroVerb = zeroVerbOfId[verbid];


            //                var time = DateTime.Parse(xelPa.Attribute("time").Value, CultureInfo.InvariantCulture);

            //                var pa = new PastActionUn { zeroVerb = zeroVerb, dateTime = time };
            //                pastActions.Add(pa);
            //        }
            //        else
            //        {
            //                // posso ignorare errore
            //        }
            //}

            foreach (var xelPa in xelRoot.Elements("past_action_solve_puzzle"))
            {
                string solution = xelPa.Attribute("solution").Value;


                var time = DateTime.Parse(xelPa.Attribute("time").Value, CultureInfo.InvariantCulture);

                var pa = new PastActionSolvePuzzle { Solution = solution, dateTime = time };
                pastActions.Add(pa);

            }

            foreach (var xelPa in xelRoot.Elements("past_action_submit_text"))
            {
                string tex = xelPa.Attribute("textTyped").Value;


                var time = DateTime.Parse(xelPa.Attribute("time").Value, CultureInfo.InvariantCulture);


                var atExplId = xelPa.Attribute("expl_id");
                string explId;
                if (atExplId != null)
                {
                    explId = atExplId.Value;
                }
                else
                {
                    explId = null;
                }




                var attext2 = xelPa.Attribute("textTyped2");
                string text2;
                if (attext2 != null)
                {
                    text2 = attext2.Value;
                }
                else
                {
                    text2 = null;
                }



                var pa = new PastActionSubmitText { TextTyped = tex, TextTyped2 = text2, dateTime = time, explId = explId };
                pastActions.Add(pa);

            }

            foreach (var xelPa in xelRoot.Elements("past_action_cancel_text"))
            {



                var time = DateTime.Parse(xelPa.Attribute("time").Value, CultureInfo.InvariantCulture);

                var pa = new PastActionCancelText { dateTime = time };
                pastActions.Add(pa);

            }

            foreach (var xelPa in xelRoot.Elements("past_action_move"))
            {
                var roomId = xelPa.Attribute("roomId").Value;
                if (roomOfId.ContainsKey(roomId))
                {
                    var room = roomOfId[roomId];
                    var time = DateTime.Parse(xelPa.Attribute("time").Value, CultureInfo.InvariantCulture);

                    var pa = new PastActionMove { room = room, dateTime = time };
                    pastActions.Add(pa);
                }
            }





            foreach (var xelRo in xelRoot.Elements("room"))
            {
                var roomId = xelRo.Attribute("roomId").Value;

                if (roomOfId.ContainsKey(roomId))
                {
                    var room = roomOfId[roomId];

                    room.deserialize(xelRo);

                }
                else
                {
                    // ho tolto una room a salvataggio iniziato. ignoro.
                }
            }





            // debug per testare i lsalvataggio di tutte le past actions
            //var debugPastActions = pastActions.OrderByDescending(pa => pa.dateTime).ToList();
            //var y = 4;




            namedCutScenesSeen.Clear();
            foreach (var xelNcs in xelRoot.Elements("named_cut_scene"))
            {
                var serId = xelNcs.Attribute("ser_id").Value;
                var nameUntransl = xelNcs.Attribute("title").Value;

                var id = new NamedCutSceneId
                {
                    serId = serId
                ,
                    titleUntranslated = nameUntransl
                };
                var ncs = new NamedCutScene(id)
                {
                    cs = deserializeCutScene(xelNcs),
                    oggettiMenzionati = new List<Mentionable>()
                }; // non so se posso istanziare qui, o devo trovare il membro con stesso ser_id
                   //ncs.title = ;


                Debug.Assert(ncs.cs.canBeSkipped);
                ncs.deserialize(xelNcs);

                foreach (var xelMenz in xelNcs.Elements("oggetto_menzionato"))
                {
                    var serIdOgg = xelMenz.Attribute("lo_id").Value;
                    if (loOfId.ContainsKey(serIdOgg))
                    {
                        var lo = loOfId[serIdOgg];
                        ncs.oggettiMenzionati.Add(lo);
                    }
                    else
                    {
                        // l'oggetto menzionato è stato rimosso perché hai cambiato il mondo a salvataggio iniziato. ignora
                    }
                }

                foreach (var xelMenz in xelNcs.Elements("mentioned_objective"))
                {
                    string oid = xelMenz.Attribute("ser_id").Value;
                    if (objectiveOfId.ContainsKey(oid))
                    {
                        var ob = objectiveOfId[oid];
                        ncs.oggettiMenzionati.Add(ob);
                    }
                    else
                    {
                        // ho eliminato un obiettivo
                    }
                }


                var roomId = xelNcs.Attribute("room_id").Value;
                if (roomOfId.ContainsKey(roomId))
                {
                    var room = roomOfId[roomId];
                    ncs.roomDoveEri = room;

                    namedCutScenesSeen.Add(ncs);
                }
            }

            RepairLegacySemanticTimestamps();




            foreach (var xelCycleEl in xelRoot.Elements("cycleElem"))
            {
                var explicitId = xelCycleEl.Attribute("id")?.Value;
                var name = xelCycleEl.Attribute("name")?.Value;
                var ce = explicitId == null ? null : new CycleElemId(explicitId);
                System.Reflection.PropertyInfo? cycleElType = name == null ? null : GetType().GetProperty(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

                if (ce != null || cycleElType != null)
                {

                    ce ??= (CycleElemId)cycleElType!.GetValue(this)!;

                    var atHowMany = xelCycleEl.Attribute("howMany");
                    if (atHowMany != null)
                    {
                        var howmany = int.Parse(atHowMany.Value);
                        howManyTimesElementExecuted.Add(ce, howmany);
                    }


                    var atLastTime = xelCycleEl.Attribute("lastTime");
                    if (atLastTime != null)
                    {
                        var lastt = DateTime.Parse(atLastTime.Value, CultureInfo.InvariantCulture); ;
                        lastTimeElementExecuted.Add(ce, lastt);
                    }

                    //cycleEl.SetValue(this, val);
                }
                else
                {
                    // ho eliminato una var, non serve invalidare il salvataggio
                }
            }





            savegameInvalid = false;
        }

        private static CutScene deserializeCutScene(XElement xelCutScene)
        {

            var canBeSkipped = bool.Parse(xelCutScene.Attribute("canBeSkipped").Value);

            var cs = new CutScene(canBeSkipped);
            foreach (var xelTokAndInd in xelCutScene.Elements("cutSceneToken").add_indices())
            {

                var xelTok = xelTokAndInd.el;

                //string idForDuplicates = null;
                //if (xelTok.Attribute("idForDuplicates") != null)
                //{
                //    idForDuplicates = xelTok.Attribute("idForDuplicates").Value;
                //}



                var tokenType = xelTok.Attribute("type").Value;
                if (tokenType == "dialog")
                {

                    var charName = xelTok.Attribute("charName").Value;



                    var img = xelTok.Attribute("img")?.Value;
                    var par = xelTok.Attribute("par").Value;

                    var size = xelTok.Attribute("size")?.Value ?? "0"; // può non esserci nel nar

                    var sizei = (NarSize)(int.Parse(size));

                    cs.Add(

                           new DialogToken(
                                            charName: charName,
                                            img: img,
                                            par: par,
                                            canBeSkipped: canBeSkipped,
                                            canGoBackToPrev: xelTokAndInd.i > 0
                                            , size: sizei
                                           )
                          );
                }
                else if (tokenType == "nar")
                {
                    var nt = NarToken.deserialize(xelTok: xelTok, cutsceneCanBeSkipped: canBeSkipped, canGoBackToPrev: xelTokAndInd.i > 0);


                    cs.Add(nt);


                }
                else if (tokenType == "narMultipar")
                {

                    var pars = new List<string>();
                    foreach (var xelPar in xelTok.Elements("par"))
                    {
                        var par = xelPar.Attribute("par").Value;
                        pars.Add(par);
                    }

                    var img = xelTok.Attribute("img").Value;
                    cs.Add(
                           new NarTokenMultipar
                               (canBeSkipped,
                                img,
                                pars,
                                canGoBackToPrev: xelTokAndInd.i > 0)
                          );
                }
                else
                {
                    throw new Exception("dfjkjjfk");
                }
            }

            return cs;
        }



        private GameStateShowingQuestions deserEventualeDialogoDopoLaCutScene(XElement xelGsc)
        {
            GameStateShowingQuestions afterCutSceneShowDialog;



            var xatAfterCutSceneShowDialog = xelGsc.Attribute("afterCutSceneShowDialog");

            if (xatAfterCutSceneShowDialog != null)
            {
                var dialId = xatAfterCutSceneShowDialog.Value;
                var allDialogs = getListOfAllDialogsInWorldAndInChars();

                var dial = allDialogs.Single(d => d.id == dialId);
                afterCutSceneShowDialog = new GameStateShowingQuestions
                {
                    dialog = dial,
                };
            }
            else
            {
                afterCutSceneShowDialog = null;
            }

            return afterCutSceneShowDialog;
        }

        private GameStateWaitingForText deserEventualeWaitingForTextDopoLaCutScene(XElement xelGsc)
        {
            GameStateWaitingForText afterCutSceneWaitingForText;



            var xatafterCutSceneWaitForTextInput = xelGsc.Attribute("afterCutSceneWaitForTextInput");

            if (xatafterCutSceneWaitForTextInput != null)
            {
                var tiId = xatafterCutSceneWaitForTextInput.Value;
                var allTextInputs = getAllTextInputs();
                var ti = allTextInputs.Single(d => d.serId == tiId);
                afterCutSceneWaitingForText = new GameStateWaitingForText(ti);
            }
            else
            {
                afterCutSceneWaitingForText = null;
            }

            return afterCutSceneWaitingForText;
        }


        private GameStateFinished deserEventualeEndGameDopoLaCutScene(XElement xelGsc)
        {
            GameStateFinished ret;



            var xatafterCutSceneGameFin = xelGsc.Attribute("afterCutSceneGameFinished");

            if (xatafterCutSceneGameFin != null)
            {
                //var tiId = xatafterCutSceneGameFin.Value;
                //var allTextInputs = getAllTextInputs();
                //var ti = allTextInputs.Single(d => d.serId == tiId);
                ret = new GameStateFinished();
            }
            else
            {
                ret = null;
            }

            return ret;
        }

        private List<Dialog> getListOfAllDialogsInWorldAndInChars()
        {
            var allDialogs = new List<Dialog>();
            allDialogs.AddRange(dialogsToSerialize());

            //foreach (var ch in this.allChars)
            //{
            //    allDialogs.AddRange(ch.dialogsToSerialize);
            //}

            return allDialogs;
        }

        //const int quantiTurniPrimaCheQualcunoTiSaluti = 4;

        //const int quantiTurniPrimaCheQualcunoTiRimproveriPerOggAppariscente = 7;


        //public List<cutSceneToken> seNecessarioFaiPartireCutSceneOggettoAppariscente(out bool qualcunoHaRimproverato)
        //{
        //    var ret = Utils.cs;

        //    qualcunoHaRimproverato = false;


        //    var cutSceneDegliOggettiAppariscentiCheTuHai = cutSceneWhenTheySeeYouCarryingThis.Where(csw =>
        //        ac.inv.Contains(csw.lo)).ToList();


        //    var personaggiCheTiVedono = allChars.Where(ch => ch != ac && ch.isInRoomIndirectly(curRoom)).ToList();

        //    if (personaggiCheTiVedono.Any())
        //    {


        //        foreach (var csapp in cutSceneDegliOggettiAppariscentiCheTuHai)
        //        {
        //            var strangeObj = new strangeObjectCarriedSeen { pi = csapp.lo, whoWasCarryingIt = ac };


        //            var personaggiCheNonTiHannoMaiVistoConQuell = personaggiCheTiVedono.Where(pe => !pe.memoryStrangeObjects.ContainsKey(strangeObj)).ToList();

        //            var personaggiCheTiHannoVistoConQuellMaNonRecentem = personaggiCheTiVedono.Where(pe =>
        //                        pe.memoryStrangeObjects.ContainsKey(strangeObj)
        //                        && curTime - pe.memoryStrangeObjects[strangeObj].timeLastSeen > quantiTurniPrimaCheQualcunoTiRimproveriPerOggAppariscente).ToList();


        //            characterE personaggioCheTiRimprovera = personaggiCheNonTiHannoMaiVistoConQuell.randomOrDefault();
        //            if (personaggioCheTiRimprovera == null)
        //                personaggioCheTiRimprovera = personaggiCheTiHannoVistoConQuellMaNonRecentem.randomOrDefault();

        //            if (personaggioCheTiRimprovera != null)
        //            {
        //                qualcunoHaRimproverato = true;

        //                // vieni rimproverato, e tutti lo ricordano
        //                var isPrimaVoltaCheTiRimproveraSuQuesto = !personaggioCheTiRimprovera.memoryStrangeObjects.ContainsKey(strangeObj);

        //                csapp.buildCutScene(
        //                    new InputWhenTheySeeYou
        //                    {
        //                        cs = ret,
        //                        chWhoSeesYou = personaggioCheTiRimprovera,
        //                        isFirstTimeTheySeeYou = isPrimaVoltaCheTiRimproveraSuQuesto,

        //                    }
        //                    ); // scrive ret

        //                // tutti ricordano che sei stato rimprovarato da poco. non solo i personaggi che ti vedono. altrimenti succede che uno ti rimprovera, poi un altro entra
        //                // nella stanza e ti rimprovera di nuovo, a raffica.
        //                foreach (var pe in allChars) // non personaggiCheTiVedono, ma tutti.
        //                {
        //                    if (pe.memoryStrangeObjects.ContainsKey(strangeObj))
        //                    {
        //                        pe.memoryStrangeObjects[strangeObj].timeLastSeen = curTime;
        //                    }
        //                    else
        //                    {
        //                        pe.memoryStrangeObjects.Add(strangeObj, new strangeObjectData { timeLastSeen = curTime });
        //                    }
        //                }
        //            }
        //            else
        //            {
        //                // non vieni rimproverato, ma TUTTI aumentano il contatore del tempo da cui sei stato rimproverato
        //                foreach (var pe in allChars)
        //                {
        //                    if (pe.memoryStrangeObjects.ContainsKey(strangeObj))
        //                    {
        //                        // niente
        //                    }
        //                    else
        //                    {
        //                        pe.memoryStrangeObjects.Add(strangeObj, new strangeObjectData { timeLastSeen = curTime });
        //                    }
        //                }
        //            }

        //        }

        //    }

        //    return ret;
        //}


        //public List<cutSceneToken> seNecessarioFaiPartireSaluti()
        //{
        //    var ret = Utils.cs;


        //    var allNpcs = allChars.Select(ch => ch.asNpc).Where(n => n != null).ToList();

        //    // INIT : quelli che non ti hanno mai visto, ipotizzo che ti abbiano visto adesso. E non tantissimo tempo fa, altrimenti ti salutano anche se partite insieme. ti hanno visto da poco.
        //    // così posso supporre che tutti ti abbiano visto.
        //    foreach (var pe in allNpcs)
        //    {
        //        foreach (var pa in curParty)
        //        {
        //            if (!pe.timeILastSawHim.ContainsKey(pa))
        //            {
        //                pe.timeILastSawHim.Add(pa, curTime);
        //            }
        //        }
        //    }


        //    var npcsCheTiVedono = allChars.Select(ch => ch.asNpc).Where(x => x != null).Where(npc => npc.asChar.isInRoomIndirectly(curRoom)).ToList();
        //    var npcsCheNonTiVedono = allChars.Select(ch => ch.asNpc).Where(x => x != null).Where(npc => !npc.asChar.isInRoomIndirectly(curRoom)).ToList();


        //    if (npcsCheTiVedono.Any())
        //    {

        //        var pcCheVieneSalutato = curParty.randomOrDefault();


        //        //var personaggiCheNonTiHannoMaiVisto = personaggiCheTiVedono.Where(pe => !pe.memoryHowLongSinceISawHim.ContainsKey(eng.activeChar)).ToList();

        //        var npcsCheTiHannoVistoMaNonRecentem = npcsCheTiVedono.Where(npc => curTime - npc.timeILastSawHim[pcCheVieneSalutato] > quantiTurniPrimaCheQualcunoTiSaluti).ToList();


        //        var npcCheTiSaluta = npcsCheTiHannoVistoMaNonRecentem.randomOrDefault();

        //        if (npcCheTiSaluta != null)
        //        {
        //            // vieni salutato, e tutti lo ricordano
        //            //var isPrimaVoltaCheTiSaluta = !npcCheTiSaluta.memoryHowLongSinceISawHim.ContainsKey(pcCheVieneSalutato.asChar);

        //            // ora il saluto. ti chiede come va e cosa hai fatto dall'ultima volta. per ora solo come va.


        //            howsItGoing(new howsItGoingInput { cs = ret, whosAsking = npcCheTiSaluta.asChar });


        //            // TUTTI sanno che sei stato salutato, non solo quelli che ti hanno visto! altrimenti se entrano in successione, vieni salutato a raffica

        //            foreach (var npc in allNpcs)
        //            {
        //                foreach (var pc in curParty)
        //                    npc.timeILastSawHim[pc] = curTime;

        //            }
        //        }
        //        else
        //        {
        //            // non vieni salutato. ora, quelli che ti hanno visto resettano il tempo da cui non ti hanno visto .
        //            // invece quelli che non ti hanno visto non fanno niente, quindi aumentano il tempo da cui non ti hanno visto.
        //            foreach (var npc in npcsCheTiVedono)
        //            {
        //                foreach (var pc in curParty)
        //                    npc.timeILastSawHim[pc] = curTime;

        //            }


        //        }


        //        // per quelli che ti hanno visto, resetta il contatore, o crealo se non ti avevano mai visto.


        //    }
        //    else
        //    {
        //        // nessun oti vede. tutti aumentano però il contatore


        //    }


        //    return ret;


        //}

        //public abstract string defaultIqLevel();

        public void changeRoom(Room roomTarget, bool callRoomChangedHandler = false, bool addSentenceYouArriveAt = true, string customSentenceYouArriveAt = "", bool alsoShowGraphicsInTextMode = false)
        {

            var xdocObj = getXdocObjIndexedCached();

            changeRoomAux(roomTarget, out TextInput _, addSentenceYouArriveAt: addSentenceYouArriveAt, callRoomChangedHandler: callRoomChangedHandler, xdocObj: xdocObj
                    , customSentenceYouArriveAt: customSentenceYouArriveAt
                    , alsoShowGraphicsInTextMode: alsoShowGraphicsInTextMode);
        }


        //public void removeInvObjectAndInsertInPlace(Character ch, LogicObj toRemove, LogicObj toAdd)
        //{
        //        var pos = ch.positionOfObjectInInv(toRemove);
        //        toRemove.removeFromWorld();
        //        if (pos != null)
        //        {
        //                ch.pickUp(toAdd, positionOfObjectInInv: pos.Value); // se crasha, sono in debug e ho saltato un pezzo. fixare piu sopra in debugmode
        //        }
        //        else
        //        {
        //                ch.pickUp(toAdd);
        //        }
        //}
        internal void changeRoomAux(Room roomTarget, out TextInput textInputToShow, bool addSentenceYouArriveAt, bool callRoomChangedHandler
                , XDocIndexed xdocObj, string customSentenceYouArriveAt, bool alsoShowGraphicsInTextMode)
        {

            //textInputToShow = null;

            //var path = find_shortest_path(curRoom, roomTarget);

            CutScene cs;
            {
                if (curCs.isEmpty())
                {
                    throw new Exception("cur_cs isnull");
                }

                cs = curCs.Peek();

            }


            var whoIsMoving = curParty;

            //var w = whoIsMoving.First().wo;

            if (curRoom != roomTarget)
            {

                //var hoAppenaCambiatoStanza = curRoom != roomTarget;





                foreach (var ch in whoIsMoving)
                {

                    // setto la nuova stanza
                    //ch.roomWithThisObjOnTheFloor = roomTarget;
                    ch.putInRoom(roomTarget);
                }









                // prima dei rimproveri, devi dire "arrivi nella stanza x" oppure "tizio arriva nella stanza dove sei tu". e questo fa vedere la locazione. quindi gli aspects corretti sono da settare prima, 
                //perche' possono ess diversi da quelli dei dialoghi.

                beforeRoomChangeManualAndAutoSetRoomAspects(roomTarget); // setta gli aspect

                if (addSentenceYouArriveAt)
                {
                    setCurrentCs(cs);

                    if (customSentenceYouArriveAt.is_not_null_or_white())
                    {
                        narRoom(customSentenceYouArriveAt, roomTarget, removeIfLast: true, alsoShowGraphicsInTextMode: alsoShowGraphicsInTextMode);
                    }
                    else
                    {
                        //var whoFollows = curParty.Where(c => c != activeChar).ToList();



                        //if (whoFollows.Count == 0)
                        //{
                        var parzIstanz = roomTarget.translatedEntenceEntering(xdocObj); // è del tipo "arrivi in casa{2}"
                        var arriviInCasa = parzIstanz.inst(""); // {2} va sostituito con stringa vuota, perché sei solo

                        narRoom(arriviInCasa, roomTarget, removeIfLast: true, alsoShowGraphicsInTextMode: alsoShowGraphicsInTextMode);
                        //}
                        //else
                        //{
                        //        var strFollowers = whoFollows.toStr();


                        //        // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                        //        if (activeChar.isMale)
                        //        {

                        //                var pezzoSeguitoDa = ", seguito da {1}".inst(strFollowers); // todo traduci

                        //                narRoom(roomTarget.translatedEntenceEntering(xdocObj).inst(pezzoSeguitoDa), roomTarget, removeIfLast: true);
                        //        }
                        //        else
                        //        {
                        //                var pezzoSeguitoDa = ", seguita da {1}".inst(strFollowers); // todo traduci
                        //                narRoom(roomTarget.translatedEntenceEntering(xdocObj).inst(pezzoSeguitoDa), roomTarget, removeIfLast: true);
                        //        }
                        //}

                    }

                    clearCurrentCs();

                }





                if (whoIsMoving.Contains(activeChar))
                {
                    // fai partire le cutscene quando entri nella stanza specifica, incluse le cutscene di quando vedi una stanza per la prima volta.
                    //roomTarget.onEnteringRoom(enteringRoomArgs, previousRoom);

                    // marca visti gli oggetti
                    foreach (var pi in roomTarget.objectsInRoom)
                    {
                        pi.isSeen = true;
                    }


                }




                if (callRoomChangedHandler) // a volte, se chiamo changeroom dentro una cut scene, non voglio scatenare i dialoghi di ingresso nella location, quindi non setto questo
                {

                    // non va bene usare curTime con % perché a volte entri in sincronia, quindi se esci e rientri becchi sempre %2 ==0 e non vedi mai una frase

                    var r = eng.rnd.Next();
                    var rnd = new RandomInputs
                    {
                        rnd10 = r % 10,
                        rnd2 = r % 2,
                        rnd3 = r % 3,
                        rnd4 = r % 4,
                        rnd5 = r % 5,
                    };




                    var handler = roomChangedHandlers.SingleOrDefault(ha => ha.roomEntered.roomId == roomTarget.roomId);

                    if (handler != null)
                    {
                        setCurrentCs(cs);

                        var i = new RoomChangedInput(/*justChangedRoom: hoAppenaCambiatoStanza,*/
                                                              randomInputs: rnd);


                        beforeExecuteDialogSetAspects(); // all'inizio di ogni dialogo, devo mettere aspect = null, se no partono con l'aspect della room, e ogni dialogo deve sempre dire aspect = null

                        handler.handler(i);
                        //w.onRoomChanged(hoAppenaCambiatoStanza, rnd);

                        textInputToShow = i.textInputToShow;


                        clearCurrentCs(); // qui se era nested la mette erroneamente a null
                    }
                    else
                    {
                        textInputToShow = null;
                    }

                }
                else
                {
                    textInputToShow = null;
                }

                // solo ora che ho chiamato onRoomChanged possoincrementare il contatore delle volte che hai visto la stanza
                foreach (var ch in curParty)
                {
                    markRoomVisited(roomTarget, ch);

                }

            }
            else
            {
                textInputToShow = null;
            }
        }

        public static void markRoomVisited(Room roomTarget, Character ch)
        {
            if (roomTarget.howManyTimesVisited.ContainsKey(ch))
            {
                roomTarget.howManyTimesVisited[ch] = roomTarget.howManyTimesVisited[ch] + 1;
            }
            else
            {
                roomTarget.howManyTimesVisited[ch] = 1; // non zero, altrimenti devi visitare 2 volte ogni stanza prima che cambi
            }
        }


        //public bool alreadyDone(LogicObj lo1, LogicObj lo2, Puzzle obj)
        //{
        //    var exi = pastActions.Any(pa =>
        //    {
        //        if (pa is PastActionTernary pat)
        //        {
        //            return pa.containsObj(lo1) && pa.containsObj(lo2) && pat.objective == obj;
        //        }
        //        else
        //        {
        //            return false;
        //        }
        //    });
        //    return exi;
        //}

        public void addObjective(Objective o)
        {

            if (o.objFirstTimeSeen == null)
            {
                o.objFirstTimeSeen = EngineNow;
            }

            if (!curObjectives.Contains(o))
            {
                curObjectives.Insert(0, o);
            }

            if (!allSeenObjectives.Contains(o))
            {
                allSeenObjectives.Add(o);
            }
        }

        public void removeObjective(Objective o, bool solved = true)
        {
            addObjective(o); // così se vuoi simulare di aver risolto l'obiettivo, basta chiamare removeObjectives, non serve add e remove

            curObjectives.Remove(o);

            if (solved && o.SolvedAt == null)
            {
                o.SolvedAt = EngineNow;
            }

            // invece rimane in allseenobj. a meno che non lo dici:
            if (!solved)
            {
                allSeenObjectives.Remove(o);
            }

        }

        /// <summary>
        /// Repairs only missing timestamps for semantic states already proven
        /// by the save.  It never infers an event from a weak secondary state.
        /// </summary>
        protected virtual void RepairLegacySemanticTimestamps()
        {
            foreach (var objective in allSeenObjectives)
            {
                if (!curObjectives.Contains(objective) && objective.SolvedAt == null)
                {
                    objective.SolvedAt = EngineNow;
                }
            }

            foreach (var cutScene in namedCutScenesSeen)
            {
                cutScene.FirstSeenAt ??= EngineNow;
            }
        }


        //public void addDangerSituation(DangerSituation o)
        //{
        //    curDangerSituations.Add(o);
        //    allSeenDangerSituations.Add(o);
        //}

        //public void removeDangerSituation(DangerSituation o)
        //{
        //    curDangerSituations.Remove(o);
        //    // invece rimane in allseenobj
        //}

        //public DangerSituation firstDangerSituation()
        //{
        //    return curDangerSituations.FirstOrDefault();
        //}


        public bool there_is_nobody_in_cur_room()
        {
            return curRoom.objectsInRoom.Where(lo => lo is Character && lo != ActiveChar).isEmpty();
        }

        public bool objectiveIsCurrent(Objective o)
        {
            return curObjectives.Contains(o);
        }


        public bool objectiveIsSolved(Objective o)
        {
            return !curObjectives.Contains(o) && allSeenObjectives.Contains(o);
        }

        public bool objectiveIsSeen(Objective o)
        {
            return allSeenObjectives.Contains(o);
        }


        abstract public LogicObj loHideInside();
        abstract public LogicObj loClimb();
        abstract public LogicObj loDisguiseAs();

        /// <summary>
        /// Enumerates only combinations available in the current game state.
        /// Persistence integrations can audit them without coupling the core to SQL.
        /// </summary>
        internal IReadOnlyList<UnhandledCombinationCandidate> GetUnhandledCombinationCandidates() =>
            UnhandledCombinationCandidates.Find(this);

        //public bool dangerIsPast(DangerSituation o)
        //{
        //    return !curDangerSituations.Contains(o) && allSeenDangerSituations.Contains(o);
        //}

        public abstract string graphicsRootFolderName();


        public GetRoomRes getRoomDescForClient(string[] grrSaveNamesOrdered, bool isTextMode)
        {

            // La visibilità delle explanation può dipendere dalla room e
            // dallo stato corrente del gioco. Ricalcolo quindi le esclusioni
            // automatiche prima di costruire la risposta, mantenendo stabile
            // la scelta finché queste condizioni non cambiano.
            afterDeserializeComputeExclusions();

            //ottieni nuova desc stanza
            //var parsClient = new List<parHtmlClient>();

            //w.curRoom.descWithMarkup(parsClient);



            var xdocI = getXdocObjIndexedCached();





            // La disponibilità di "parla" viene calcolata per la stanza
            // corrente più avanti. Non interroghiamo i cicli di tutte le
            // stanze: la costruzione di un ciclo può avere effetti narrativi
            // e non deve essere eseguita durante la serializzazione.
            /*
                        //objectsInRoom è un hashset quindi l'ordine dei putInRoom è andato perso. riordino
                        var charsInRoom = ro.objectsInRoom
                                                 .Where(o => o is Character)
                                                 .Where(o => !o.isInCurParty())
                                                 .ToList();


                        var firstChar = (Character)charsInRoom.FirstOrDefault();

                        if (firstChar != null)
                        {
                            charsToTalkTo.Add(firstChar);
                        }
            */







            var dicRooms = new Dictionary<string, RoomForClient>();

            foreach (var kv in roomOfId)
            {
                if (curRoom.roomId == kv.Key) // nuovo - adesso serve solo la room attuale
                {
                    var ro = kv.Value;


                    //objectsInRoom è un hashset quindi l'ordine dei putInRoom è andato perso. riordino
                    var objectsInRoomOrd = ro.objectsInRoom
                                             .OrderBy(o => curParty.Contains(o) ? 0 : 1)
                                             .ThenBy(o => o is Character ? 0 : 1)
                                             .ThenBy(o => o.orderForTextMode)
                                             .ToList();


                    foreach (var lo in objectsInRoomOrd)
                    {
                        Debug.Assert(lo.wo != null);
                    }
                    //var party = ro.objectsInRoom.Where(o => curParty.Contains(o)).ToList();

                    //var charsInRoom = ro.objectsInRoom.Where(o => o is Character).Where(o => o.notIn(party));

                    //var nonCharsHere = ro.objectsInRoom.Where(o => !(o is Character)).Where(o => o.notIn(party) );

                    //var orderedObjectsInRoom = nonSelectable.Concat(charsInRoom).Concat(nonCharsHere).ToList();



                    var rfc_objects = objectsInRoomOrd

                                    .Where(lo => !lo.IsExit) // tolgo le uscite perche' ora ho di nuovo la mappa
                                  .Select(lo => ofcOfLo(lo, xdocI)).ToList();


                    dicRooms[kv.Key] = new RoomForClient
                    (
                            rfc_objects: rfc_objects,
                            rfc_img: ro.imgPath() ?? imgNotAvailable()
                            , nameTextMode: ro.dynamicNameForMapTranslated(xdocI),
                            rfc_bg_wt: ro.coordFileEditor?.BackgroundWidth ?? 1920,
                            rfc_bg_ht: ro.coordFileEditor?.BackgroundHeight ?? 1080

                    //rfc_layers: rfc_layers
                    );
                }
            }



            // devo aggiungere l'inv
            var grrInvObjects = eng.computeParsOfInv(activeChar, xdocI)

                    .OrderBy(x => x.ofcIsConcept ? 0 : 1)

                    .Reverse() // i più recenti in cima
                    .ToList(); // prima gli oggetti, così non combini van helsing con valigia. sbagliato, perche' i verbi vannoo prima degli oggetti. e in ogni caso io segnalo con la freccia l'oggetto van helsing, quindi non risolverei.


            //// devo aggiungere mind
            //var grrInvConcepts = eng.computeParsOfMindOfActiveChar(this, xdocI);






            //var zeroVerbs = zeroVerbOfId.Values
            //                            .Where(v => verbIsVisibleNow(v))
            //                            .Select(zverb => new VerbForClient
            //                            (
            //                                    vfcCanOnlyBeUsedWithObjsInRoomNotInv: false,
            //                                    vfc_is_remember: false,
            //                                    vfcSerId: zverb.verbId,
            //                                    vfcCanBeUnaryOrBinaryDependingOnObject: false,
            //                                    vfcIsZeroVerb: true,
            //                                    vfcIsBinary: false,
            //                                    vfcName: zverb.translated_name(xdocI?.Xdoc),
            //                                    vfcSecondPart: null,
            //                                    vfcRequiresPuzzle: false,
            //                                    vfcPriority: zverb.priority,
            //                                    vfcCharIsAlwaysFirst: false,
            //                                    vfcCharIsAlwaysLast: false,
            //                                    vfcIsHighlighted: verbIsHighlightedNow(zverb),
            //                                    vfcIsUnary: false,
            //                                    vfcIsAskForHints: zverb.is_ask_for_hint,
            //                                    vfcIsPickup: false
            //                            )).ToList();




            //var unVerbs = unVerbOfId.Values
            //                        .Where(v => verbIsVisibleNow(v))
            //                        .Select(unverb => new VerbForClient
            //                        (
            //                                vfcCanOnlyBeUsedWithObjsInRoomNotInv: unverb.canOnlyBeUsedWithRoomObjectsNotInv,
            //                                vfcCanBeUnaryOrBinaryDependingOnObject: false,
            //                                vfcSerId: unverb.verbId,
            //                                vfcIsBinary: false,
            //                                vfcName: unverb.translated_name(xdocI?.Xdoc),
            //                                vfcSecondPart: null,
            //                                vfcRequiresPuzzle: unverb.requires_objective,
            //                                vfcPriority: unverb.priority,
            //                                vfcCharIsAlwaysFirst: false,
            //                                vfcCharIsAlwaysLast: false,
            //                                vfcIsHighlighted: verbIsHighlightedNow(unverb),
            //                                vfcIsUnary: true,
            //                                vfcIsAskForHints: false,
            //                                vfcIsZeroVerb: false,
            //                                vfc_is_remember: unverb.is_remember,
            //                                vfcIsPickup: unverb.isPickup

            //                        )).ToList();


            //var binVerbs = binVerbOfId.Values
            //                          .Where(v => verbIsVisibleNow(v))
            //                          .Select(binverb => new VerbForClient
            //                          (
            //                                  vfcSerId: binverb.verbId,
            //                                  vfcCanBeUnaryOrBinaryDependingOnObject: binverb.canBeUnaryOrBinaryDependingOnObj,
            //                                  vfcIsBinary: true,
            //                                  vfcName: binverb.translated_name(xdocI?.Xdoc),
            //                                  vfcSecondPart: binverb.translated_second_part(xdocI?.Xdoc),
            //                                  vfcRequiresPuzzle: binverb.requiresPuzzle,
            //                                  vfcPriority: binverb.priority,
            //                                  vfcCharIsAlwaysLast: binverb.charIsAlwaysLast,
            //                                  vfcCharIsAlwaysFirst: binverb.charIsAlwaysFirst,
            //                                  vfcIsHighlighted: verbIsHighlightedNow(binverb),
            //                                  vfcIsUnary: false,
            //                                  vfcIsAskForHints: false,
            //                                  vfcCanOnlyBeUsedWithObjsInRoomNotInv: false,
            //                                  vfc_is_remember: false,
            //                                  vfcIsZeroVerb: false,
            //                                  vfcIsPickup: false
            //                          )).ToList();





            var visitedRooms = roomOfId.Values.Where(r => r.wasEverVisitedBy(activeChar) || r == curRoom).ToList();



            var grrMustShowYouSeeNothingSpecialHere = curRoom.objectsInRoom.All(ob => ob.isInCurParty());

            List<RoomCoords> roomCoords;

            //if (isTextMode)
            //{
            //        roomCoords = null;
            //}
            //else
            {
                roomCoords = roomOfId.Keys.Select(roomId =>
                {



                    var room = roomOfId[roomId];


                    //if (room.roomId == "roomDesertCaves")
                    //{
                    //        var exits2 = (from e in exits where e.From.roomId == "roomDesertCaves" || e.To.roomId == "roomDesertCaves" select e).ToList();
                    //        var exitsde = (from e in exits where e.From.roomId == "roomDesert" || e.To.roomId == "roomDesert" select e).ToList();
                    //        var y = 4;
                    //}

                    var isAdjacentToAVisitedRoom = visitedRooms.Any(vr => exits.Any(e => e.From == vr && e.To == room));

                    var isAlreadyVisited = room.wasEverVisitedBy(activeChar) || room == curRoom;

                    var isAccessibleFromHere = isAdjacentToAVisitedRoom || isAlreadyVisited;

                    return new RoomCoords
                    {
                        rcRoomId = roomId,
                        rcX = room.map_x,
                        rcY = room.map_y,
                        rcRoomName = room.dynamicNameForMapTranslated(xdocI),
                        rcAlreadyVisitedOnce = isAlreadyVisited,
                        rcAdjacent = isAdjacentToAVisitedRoom,
                        rcIsCurRoom = curRoom == room,
                        rcIsAccessibleFromHere = isAccessibleFromHere,
                    };
                }).OrderBy(rc =>
                {
                    if (rc.rcIsCurRoom) return 0d;
                    if (!rc.rcX.HasValue || !rc.rcY.HasValue || !curRoom.map_x.HasValue || !curRoom.map_y.HasValue)
                        return double.MaxValue;
                    var dx = rc.rcX.Value - curRoom.map_x.Value;
                    var dy = rc.rcY.Value - curRoom.map_y.Value;
                    return dx * dx + dy * dy;
                }).ToList();
            }
            //var allVerbs = zeroVerbs.Concat(binVerbs).Concat(unVerbs).OrderBy(v => v.vfcPriority).ToList();

            var namedCutScenesSerIds = namedCutScenesSeen.Select(n => new NamedCutSceneClient
            {
                ncsc_ser_id = n.id.serId,
                ncsc_title_translated = translateDialogOrNarOrAnnotated(n.id.titleUntranslated, xdocI)
            }).ToList();

            var grrObjectives = curObjectives

                    // non piu perche ora sono tutti non cliccabili
                    //.OrderBy(o =>
                    //{
                    //        if (disabledObjectives.Any(dio => dio.serId == o.serId))
                    //        {
                    //                return 1;
                    //        }
                    //        else
                    //        {
                    //                return 2;
                    //        }

                    //})
                    .Select(o =>
                    {
                        return objectiveClientOfObjective(o, xdocI);
                    })

            .ToList();

            //foreach (var ob in grr_objectives)
            //{
            //    ob.readable_name = ob.translated_name(cur_lang);
            //}






            var walkTo = translateSentenceWithIdFromObjfile("cammina verso", "walk_to", xdocI?.Xdoc);
            var walk = translateSentenceWithIdFromObjfile("cammina", "walk", xdocI?.Xdoc);
            var inOrderTo = translateSentenceWithIdFromObjfile("per", "in_order_to", xdocI?.Xdoc);
            var hereYouSee = translateSentenceWithIdFromObjfile("qui vedi", "here_you_see", xdocI?.Xdoc);
            var yourObjects = translateSentenceWithIdFromObjfile("i tuoi oggetti", "your_objects", xdocI?.Xdoc);
            var oggettiCheVediQui = translateSentenceWithIdFromObjfile("oggetti che vedi qui", "objects_you_see_here", xdocI?.Xdoc);
            var clickObjectToRemember = translateSentenceWithIdFromObjfile("Seleziona...", "click_an_object", xdocI?.Xdoc);
            //var oggettiChePortiConTe = translateSentenceWithIdFromObjfile("oggetti che porti con te", "objects_you_are_carrying", xdocI?.Xdoc);
            var objectsSeenSomewhere = translateSentenceWithIdFromObjfile("cose che hai visto da qualche parte", "objects_seen_somewhere", xdocI?.Xdoc);
            //var possibleActions = translateSentenceWithIdFromObjfile("azioni rapide", "actions", xdocI?.Xdoc);
            //var yourObjectives = translateSentenceWithIdFromObjfile("cose che devi fare", "things_to_do", xdocI?.Xdoc);
            var ricordaUnOggetto = translateSentenceWithIdFromObjfile("ricorda un oggetto", "remember_some_object", xdocI?.Xdoc);
            //var areYouStuck = translateSentenceWithIdFromObjfile("sei bloccato?", "are_you_stuck", xdocI?.Xdoc);
            var other = translateSentenceWithIdFromObjfile("altro", "other", xdocI?.Xdoc);
            var options = translateSentenceWithIdFromObjfile("opzioni", "options", xdocI?.Xdoc);
            var nothingSpecial = translateSentenceWithIdFromObjfile("niente di particolare", "nothing_special", xdocI?.Xdoc);

            var cancel = translateSentenceWithIdFromObjfile("annulla", "cancel", xdocI?.Xdoc);
            var back = translateSentenceWithIdFromObjfile("chiudi", "back", xdocI?.Xdoc);
            var rereadClues = translateSentenceWithIdFromObjfile("rileggi gli indizi", "reread_clues", xdocI?.Xdoc);
            //var pressToContinue = translateSentenceWithIdFromObjfile("fai clic per continuare", "press_to_continue", xdocI?.Xdoc);
            //var you_dont_see_how_this_can_help = translated_el_generic("non vedi come questo possa aiutarti a {1}.", "you_dont_see_how_this_can_help");

            //var dynLines = dynLineOfId.Values.Select(x => new DynLineClient
            //{
            //    dlcEndPoint = x.endPoint,
            //    dlcSerId = x.serId,
            //    dlcStartPoint = x.startPoint,
            //    dlcIsVisibleNow = isDynLineVisible(x)
            //}).ToList();

            //if (dynLines.Any(dl => dl.dlcStartPoint == null, out DynLineClient debug))
            //{
            //    throw new Exception($"in the xml map  ,  line {debug} is missing, or it is present but snapped to the boxes, so it does not have absolute x and y.");
            //}

            //var gfjkgjf = 4;

            //var dfdfd = (dicRooms[curRoom.roomId]);

            //var grrPickupVerbId = (from uv in unVerbOfId.Values

            //                       where uv.isPickup
            //                        select uv.verbId).Single();

            //var grrPickupReadableNameTransl = (from uv in unVerbOfId.Values

            //                                   where uv.isPickup
            //                       select uv.translated_name(curLang).firstLetterToUpper()).Single();

            //var grrUseVerb = (from binverb in binVerbOfId.Values
            //                  where binverb.canBeUnaryOrBinaryDependingOnObj
            //                  select new VerbForClient
            //                          (
            //                                  vfcSerId: binverb.verbId,
            //                                  vfcCanBeUnaryOrBinaryDependingOnObject: binverb.canBeUnaryOrBinaryDependingOnObj,
            //                                  vfcIsBinary: true,
            //                                  vfcName: binverb.translated_name(curLang),
            //                                  vfcSecondPart: binverb.translated_second_part(curLang),
            //                                  vfcRequiresPuzzle: binverb.requiresPuzzle,
            //                                  vfcPriority: binverb.priority,
            //                                  vfcCharIsAlwaysLast: binverb.charIsAlwaysLast,
            //                                  vfcCharIsAlwaysFirst: binverb.charIsAlwaysFirst,
            //                                  vfcIsHighlighted: verbIsHighlightedNow(binverb),
            //                                  vfcIsUnary: false,
            //                                  vfcIsAskForHints: false,
            //                                  vfcCanOnlyBeUsedWithObjsInRoomNotInv: false,
            //                                  vfc_is_remember: false,
            //                                  vfcIsZeroVerb: false,
            //                                  vfcIsPickup: false
            //                          )).Single();




            //var grrPuzzleSolutions = (from ps in puzzleSolvedHandlersOldUi

            //                          select new PuzzleSolutionClient
            //                          {
            //                                  pscObjective = objectiveClientOfObjective(ps.puzzleSolution.objective, xdocI),
            //                                  pscSolution = ps.puzzleSolution.solution.Select(tok =>
            //                                 {
            //                                         PuzzleTokenClient ret;
            //                                         if (tok is EnumeratedToken et)
            //                                         {
            //                                                 ret = new EnumeratedTokenClient
            //                                                 {
            //                                                         etc_choices = et.choices

            //                                                                .Where(qt => qtokIsVisibleNow(ps.puzzleSolution.objective, qt)
            //                                                                                        //&& ObjQtokIsVisibleNowForWorldState(qt) 
            //                                                                                        )
            //                                                                .Select(qt => qt.serId)
            //                                                                //new QtokClient(qt, co => VerbQTokIsVisibleNowForSelectedObjective(ps.puzzleSolution.objective, co)
            //                                                                //                        &&ObjQtokIsVisibleNowForWorldState(co) )
            //                                                                .ToArray(),
            //                                                         etc_qtokCorrect = et.correct.serId

            //                                                         //new QtokClient(et.correct, co => VerbQTokIsVisibleNowForSelectedObjective(ps.puzzleSolution.objective, co)
            //                                                         //                               && ObjQtokIsVisibleNowForWorldState(co))
            //                                                 };
            //                                         }
            //                                         else if (tok is ObjInRoomToken ort)
            //                                         {
            //                                                 ret = new ObjInRoomTokenClient { oir_loIdCorrect = ort.correct.loId };
            //                                         }
            //                                         else
            //                                         {
            //                                                 throw new NotImplementedException();
            //                                         }

            //                                         return ret;
            //                                 }).ToArray()

            //                          }).ToList();



            //var loOfQt = new Dictionary<Qtok, LogicObj>();
            //foreach(var lo in getAllLogicObjects())
            //{
            //        foreach(var qt in lo.associatedQToks)
            //        {
            //                if (!loOfQt.ContainsKey(qt)) // solo il primo
            //                {
            //                        loOfQt.Add(qt, lo);
            //                }
            //        }
            //}

            //var qtoksAll = allQtoks

            //        // non filtrare più, ma mostrare col punto interrogrativo
            //        //.Where(qt => ObjQtokIsVisibleNowForWorldState(qt) /* non posso chiamare l'altra funzione, perché non è noto l'obiettivo, ma non serve*/ )

            //        .Select(qt => new QtokClient(qt, loOfQt.itemOrDefault(qt)/*, x => ObjQtokIsVisibleNowForWorldState(x) */, xdocI)).ToArray();
            ////{

            ////        if (qt is EnumeratedToken ent)
            ////        {
            ////                return ent.choices.Select(qt0 => new QtokClient(qt0) );
            ////        }
            ////        else
            ////        {
            ////                return null;
            ////        }
            ////}).select_some().Flatten() ).Flatten().Distinct().ToArray();

            ////var qtsOfObjectiveId = qtoksAll.GroupBy(x => x.oqt_ob.serId).ToDictionary(x => x.Key, x=> x.ToArray());

            LayerForClient[] rfc_layers;

            if (isTextMode)
            {
                rfc_layers = null;
            }
            else
            {
                rfc_layers = buildLayersOfRoom(curRoom);
            }

            var grrLayersOfCurRoom = rfc_layers;


            //var dicQtokOfSerId = qtoksAll.ToDictionary(x => x.qt_serId);


            var grrFillers = fillerOfId.Values
                    .Where(fi => fillerIsVisible(fi))
                    .Select(fi =>
                    {
                        // devo tradurre prima di passare al client
                        var transl = translateDialogOrNarOrAnnotated(fi.Name, xdocI);
                        return new Filler(fi.FilId, transl, fi.Icon, IsForSayVerb: fi.IsForSayVerb);
                    }).ToList();

            var grrTemplates = templateOfId.Values
                    .Where(te => templateIsVisible(te))
                    .Select(te =>
            {
                // devo tradurre prima di passare al client
                var translhe = translateDialogOrNarOrAnnotated(te.heShe, xdocI);
                var translThey = translateDialogOrNarOrAnnotated(te.they, xdocI);
                return new Template(te.teId, translhe, translThey, IsForSayVerb: te.IsForSayVerb, isForChars: te.isForChars);
            }).ToList();


            var grrTemplatesToExcludeOfObj = new Dictionary<string, string[]>();
            foreach (var x in templatesToExcludeOfObj)
            {
                grrTemplatesToExcludeOfObj.Add(x.Key, x.Value.Select(q => q.teId).ToArray());
            }


            var grrExplanationsToExcludeOfObj = new Dictionary<string, string[]>();
            foreach (var x in explanationsToExcludeOfObjective)
            {
                grrExplanationsToExcludeOfObj.Add(x.Key, x.Value.ToArray());
            }


            var grrExplanationsToExcludeOfLo = new Dictionary<string, string[]>();
            foreach (var x in explanationsToExcludeOfLo)
            {
                grrExplanationsToExcludeOfLo.Add(x.Key, x.Value.ToArray());
            }


            var globalExps = getGlobalExplanations().ToList(); // in qualche modo fissando questo sparisce il problema che in italiano a volte appare in inglese
            var grrExplanationsGlobal = globalExps
                    .Where(ex => explanationIsVisible(ex))
                    .Select(e =>
                    {
                        //if (e.expId == "exQualcunoMangeraQualcosa")
                        //{
                        //        var y = 4;
                        //}
                        var tra = translateDialogOrNarOrAnnotated(e.exName, xdocI);
                        return new ExplanationClient(e.expId, tra);
                    }).ToArray();




            var grrExpCont = getAllExplanationsWithCont()
                    .ToList()
                    .Select(e => new ExplanationWithContClient(e.expId

                    , translateDialogOrNarOrAnnotated(e.exName, xdocI)
                    , e.Continuations
                            .Where(co => explanationIsVisible(co))
                            .Select(co => new ExplanationClient(co.expId
                                                                                                                                    , translateDialogOrNarOrAnnotated(co.exName, xdocI))).ToArray())).ToArray();



            // La costruzione della descrizione può avvenire fuori da una
            // CutScene, ma il gioco può avere del codice nei factory dei
            // cicli che prepara testo narrativo mentre costruisce il ciclo.
            // Manteniamo quindi il calcolo di grrTalkNow usando una CutScene
            // tecnica, senza eseguire il ciclo: in questo modo il gioco non
            // deve conoscere né gestire lo stack interno dell'engine e non si
            // perde la disponibilità del pulsante Parla.
            Cycle roomCycle;
            if (curCs.isEmpty())
            {
                var inspectionCs = new CutScene(canBeSkipped: false);
                setCurrentCs(inspectionCs);
                try
                {
                    roomCycle = getRoomCycle(curRoom);
                }
                finally
                {
                    clearCurrentCs();
                }
            }
            else
            {
                roomCycle = getRoomCycle(curRoom);
            }
            var grrTalkNow = roomCycle != null && CycleMemory.wouldSaySomethingNewAndImportant(roomCycle, this);

            var roomDesc = new GetRoomRes
            {
                grrHideInsideLoId = loHideInside()?.loId,
                grrClimbLoId = loClimb()?.loId,
                grrTravestitiLoId = loDisguiseAs()?.loId,
                grrExplanationsWithCont = grrExpCont,
                grrIsTextMode = isTextMode,
                grrExplanationsToExcludeOfObjective = grrExplanationsToExcludeOfObj,
                grrExplanationsToExcludeOfLo = grrExplanationsToExcludeOfLo,
                grrExplanationsGlobal = grrExplanationsGlobal,
                grrFillers = grrFillers,
                grrTemplates = grrTemplates,
                grrTalkNow = grrTalkNow,
                grrProInterfaceTitle = translateDialogOrNarOrAnnotated(ProInterfaceTitle(), xdocI),
                grrProInterfaceSubtitle = translateDialogOrNarOrAnnotated(ProInterfaceSubtitle(), xdocI),
                grrCasualInterfaceTitle = translateDialogOrNarOrAnnotated(CasualInterfaceTitle(), xdocI),
                grrCasualInterfaceSubtitle = translateDialogOrNarOrAnnotated(CasualInterfaceSubtitle(), xdocI),
                //grrCannotTalkNow = cannotTalkNow(curRoom),
                grrSaveNames = grrSaveNamesOrdered,
                grrMustShowYouSeeNothingSpecialHere = grrMustShowYouSeeNothingSpecialHere,
                grrStrClickAnObjectToRemember = clickObjectToRemember,

                //grrDynLines = dynLines,
                grrLayersOfCurRoom = grrLayersOfCurRoom,
                grrCurRoomId = curRoom.roomId,
                grrRoomCoords = roomCoords,
                grrMapImage = mapImageFileName == null ? null : $"{mapImageFolder}/{mapImageFileName}",
                grrMapImageX = mapImageX,
                grrMapImageY = mapImageY,
                grrMapImageWidth = mapImageWidth,
                grrMapImageHeight = mapImageHeight,
                grrRooms = dicRooms,
                grrObjectives = grrObjectives,
                grrTemplatesToExcludeOfObj = grrTemplatesToExcludeOfObj,
                //grrPuzzleSolutions = grrPuzzleSolutions,
                roomName = curRoom.dynamicNameForMapTranslated(xdocI).ToUpper(),
                grrInvObjects = grrInvObjects,
                //grrDynamicExclusions = dynamicExclusions(),
                //grrInvConcepts = grrInvConcepts,
                //grrVerbs = allVerbs,
                //grrUseVerb = grrUseVerb,
                roomImg = curRoom.imgPath() ?? imgNotAvailable(),


                grrDisabledVerbs = disableVerbForObj.Select(x => new ObjAndVerbClient { ovcObj = x.ovObj.loId, ovcVerb = x.ovVerb.verbId }).ToList(),
                grrDisabledObjectives = disabledObjectives.Select(x => new VerbAndObjectiveClient
                {
                    vocObjective = x.serId,
                    //vocVerb = x.voVerb.verbId
                }).ToList(),

                mindTitle = objectsSeenSomewhere,

                invTitle = yourObjects,
                grrStrOggettiCheVediQui = oggettiCheVediQui,
                //grrStrOggettiChePortiConTe = oggettiChePortiConTe,

                //verbsTitle = possibleActions,

                optionsTitle = options,

                //verbExitThrough = verbExitThrough,

                activeChar = new ObjForClient(activeChar, xdocI),


                grr_named_cut_scenes = namedCutScenesSerIds,

                grr_walk_to_translated = walkTo,
                //grrPickupVerbId = grrPickupVerbId,
                //grrPickupReadableNameTransl = grrPickupReadableNameTransl,
                grr_in_order_to_translated = inOrderTo,
                grr_walk_translated = walk,
                //grr_useTokenSerId = UseToken().serId,
                //grr_are_you_stuck = areYouStuck,
                grr_here_you_see = hereYouSee,
                grr_objects_seen_somewhere = objectsSeenSomewhere,
                grr_options = options,
                grr_other = other,
                //grr_possible_actions = possibleActions,
                //grr_your_objectives = yourObjectives,
                grrRememberAnObject = ricordaUnOggetto,
                grr_your_objects = yourObjects,
                grr_back = back,
                grr_cancel = cancel,
                grr_reread_clues = rereadClues,
                //grr_press_to_continue = pressToContinue,
                grr_nothing_special = nothingSpecial
                    //grr_IQLevel = iqLevel
                    ,
                grrStoryMode = StoryMode,
                grrCasualMode = IsCasualMode
                //grr_you_dont_see_how_this_can_help = you_dont_see_how_this_can_help,

            };

            return roomDesc;
        }



        //internal PuzzleSolution autoFindSolutionForPuzzle(Objective ob, ApiBase ab)
        //{





        //        var firstSolutionThatWorks = (from so in getAllPuzzleSolutions()

        //                                      where so.objective == ob
        //                                      where solutionSolvesPuzzle(so, ab)
        //                                      select so
        //                     ).FirstOrDefault();


        //        if (firstSolutionThatWorks == null)
        //                return null;


        //        // controllo anche se l'oggetto richiesto è nel mondo ED è stato visto. 
        //        var passed = firstSolutionThatWorks.solution.Select(x =>
        //       {
        //               if (x is EnumeratedToken et)
        //               {
        //                       return true;
        //               }
        //               else if (x is ObjInRoomToken ot)
        //               {
        //                       var lo = loOfId[ot.correct.loId];
        //                       return lo.is_in_world() && lo.isSeen;

        //               }
        //               else
        //               {
        //                       throw new NotImplementedException();
        //               }
        //       })
        //                .All(x => x);





        //        // controllo anche se il verbo è visibile. no, non possso perché non sarebbe visibile in story mode se non sei dal golem. invece ci deve andare.
        //        //bool passed2;
        //        //var verbo = firstSolutionThatWorks.solution.First();
        //        //if (verbo is EnumeratedToken etv)
        //        //{
        //        //        var qt = qtokOfId[etv.correct.serId];
        //        //        if (qtokIsVisibleNow(ob, qt))
        //        //        {
        //        //                passed2 = true;
        //        //        }
        //        //        else
        //        //        {
        //        //                passed2 = false;
        //        //        }
        //        //}
        //        //else
        //        //        passed2 = false;




        //        if (passed)
        //        {
        //                return firstSolutionThatWorks;
        //        }
        //        else
        //        {
        //                return null;
        //        }




        //}
        //bool solutionSolvesPuzzle(PuzzleSolution sol, ApiBase ab)
        //{
        //        // devo capire se in questo momento posso risolvere il puzzle, cioè eseguire l'handler di questo.
        //        // quindi quale è la stanz ain cui agire. lo posso capire adll oggetto, se è in una room. ma se non lo è? purtroppo c'è l'implementazione che fa changeroom (ma in quel caso la risposta è sì, è fattibile).
        //        // solo raramente, il beforechangeroom blocca... ma si può gestire ad hoc.

        //        //ma se questo ha due handler, quale  quello giusto? devo duplicare il mondo ed eseguire l'handler

        //        Debug.Assert(!objectiveIsSolved(sol.objective));

        //        var xdoc = serialize();


        //        // creo un clone del mondo
        //        var wcopy = ab.buildEmptyWorld(this.curLang); // l'engine non conosce il tipo world
        //        ApiBase.integrityCheckAfterWorldBuild(wcopy);

        //        wcopy.deserialize(xdoc, out bool savegameInvalid);




        //        // ora in qesto clone eseguo l'azione
        //        var xdocI = wcopy.getXdocObjIndexedCached();
        //        var saveNames = new string[] { };


        //        PuzzleToken[] solutionToConvert = sol.solution;

        //        PuzzleSolutionPieceSentByClient[] solutionClient = convertSolutionIntoUserSolution(xdocI, solutionToConvert);

        //        var objInNewWorld = wcopy.objectiveOfId[sol.objective.serId];

        //        var actionRes = eng.executePuzzleSolution(objInNewWorld, solutionClient, wcopy, saveNames, xdocI);


        //        var isCorrectSolution = (wcopy.objectiveIsSolved(objInNewWorld));
        //        return isCorrectSolution;


        //}

        //internal static PuzzleSolutionPieceSentByClient[] convertSolutionIntoUserSolution(XDocIndexed xdocI, PuzzleToken[] solutionToConvert)
        //{
        //        return solutionToConvert.Select(x =>
        //        {
        //                if (x is EnumeratedToken et)
        //                {
        //                        return new PuzzleSolutionPieceSentByClient
        //                        {
        //                                isEnu = true
        //                        ,
        //                                oir_loIdCorrect = null
        //                        ,
        //                                qt_serId = et.correct.serId
        //                        ,
        //                                psi_readableName = et.correct.translatedNameHeShe(xdocI)
        //                        };
        //                }
        //                else if (x is ObjInRoomToken ot)
        //                {
        //                        return new PuzzleSolutionPieceSentByClient
        //                        {
        //                                isEnu = false
        //                        ,
        //                                oir_loIdCorrect = ot.correct.loId
        //                        ,
        //                                qt_serId = null
        //                        ,
        //                                psi_readableName = ot.correct.dynamicNameTranslated(xdocI, withArticle: true)
        //                        };
        //                }
        //                else
        //                {
        //                        throw new NotImplementedException();
        //                }
        //                 ;
        //        }).ToArray();
        //}

        private LayerForClient[] buildLayersOfRoom(Room room)
        {
            if (room.assetFolderName == null || room.coordFileEditor == null)
            {
                return new LayerForClient[] { };
            }

            ValidateAlternatePositions(room);

            LayerForClient[] rfc_layers;
            //var dicCoordsOfNomeFileLayer = room.coordFile; // eng.ParseCoordFile(curRoom);

            //if (dicCoordsOfNomeFileLayer == null)
            //{
            //    throw new Exception($"room {room.roomId} does not have dictionary. You need to put a file called {room.assetFolderName}\\layer-data.txt");
            //}

            var rfc_layersColor = room.coordFileEditor.Layers.Select(la => // se coordFile e' null, vedi bm_r48jr4j8r8r48. non ha trovato il file txt
            {


                bool loIsHereWithCorrectAspect;
                LogicObj lo;

                //if (filename == "olivia-po.png")
                //{
                //        var gfj = 4;
                //}
                //parseFileNameDaCoordFile(layerFileName, out string loId, out string[] aspects, out bool isPortrait, out bool isOutline, out bool isStatic);

                if (la.LogicalObj == null)
                {
                    throw new Exception("logicalobj null");
                }

                if (loOfId.ContainsKey(la.LogicalObj))
                {
                    lo = loOfId[la.LogicalObj];
                    if (!lo.isIn(room)) // e non isHere, se no se lo chiami da beforeroomchanged non funge
                    {
                        loIsHereWithCorrectAspect = false;
                    }
                    else
                    {
                        //if (lo.loId == "olivia")
                        //{
                        //        var y = 4;
                        //}

                        bool aspettoMatcha;
                        if (lo.Aspect == null /*&& aspects.isEmpty()*/) // i color layer non hanno aspects
                        {
                            aspettoMatcha = true;
                        }
                        //else if (lo.Aspect != null && aspects.Any() && aspects.Single() == lo.Aspect.serId) //se crasha qui, controlla gli aspects sopra. se c'e' dentro default , toglilo dal file coords a mano. se c'è mod, rinomina da -dracula-mod a draculaMod
                        //{
                        //    aspettoMatcha = true;
                        //}
                        else
                        {
                            aspettoMatcha = false;
                        }

                        loIsHereWithCorrectAspect = aspettoMatcha;
                    }
                }
                else
                {

                    loIsHereWithCorrectAspect = false;
                    lo = null;
                }



                //if (layerFileName == "bg-png" || (loIsHereWithCorrectAspect && !isPortrait) || (lo == null && !isPortrait))
                {



                    //var filename = layerFileName.Replace("-png", "") + ".png";

                    var path = $"{graphicsRootFolderName()}/{room.assetFolderName}/{la.FileName}";
                    LayerInfoParsed rect = new LayerInfoParsed(new RectSeg((int)la.X, (int)la.Y, (int)la.Width, (int)la.Height)); // isHiRes dicCoordsOfNomeFileLayer[layerFileName];

                    return new LayerForClient
                    {
                        lfc_imgPath = path,
                        lfc_x = rect.rect.x,
                        lfc_y = rect.rect.y,
                        lfc_ht = rect.rect.ht,
                        lfc_wt = rect.rect.wt,
                        //lfc_isHires = rect.isHiRes,
                        lfc_loId = la.LogicalObj,
                        lfcIsOutline = true, // stiamo parlando di color layers // isOutline
                        lfc_zIndex = la.ZIndex
                                            //lfc_nameMustAppearInGraphics = lo?.nameMustAppearInGraphics ?? false
                    };
                }

            })
            .SelectSome()
            //.Reverse() // in photoshop il primo è più sopra, ma nel browser il primo va sotto
            .ToArray();

            LayerForClient[] rfc_sprites = room.coordFileEditor.Sprites.Select(sp =>
                    {


                        bool loIsHereWithCorrectAspect;
                        LogicObj lo;

                        //if (filename == "olivia-po.png")
                        //{
                        //        var gfj = 4;
                        //}
                        //parseFileNameDaCoordFile(layerFileName, out string loId, out string[] aspects, out bool isPortrait, out bool isOutline, out bool isStatic);

                        if (sp.LogicalObj == null)
                        {
                            throw new Exception("logicalobj null"); // nell'editor ti sei dimenticato di impostare il logicobj per uno sprite
                        }

                        if (loOfId.ContainsKey(sp.LogicalObj))
                        {
                            lo = loOfId[sp.LogicalObj];

                            if (!lo.isIn(room)) // e non isHere, se no se lo chiami da beforeroomchanged non funge
                            {
                                loIsHereWithCorrectAspect = false;
                            }
                            else
                            {
                                //if (lo.loId == "olivia")
                                //{
                                //        var y = 4;
                                //}

                                bool aspettoMatcha;
                                if (lo.Aspect == null && sp.Aspect == null) // i color layer non hanno aspects
                                {
                                    aspettoMatcha = true;
                                }
                                else if (lo.Aspect != null && lo.Aspect.serId == sp.Aspect) // && aspects.Any() && aspects.Single() == lo.Aspect.serId) //se crasha qui, controlla gli aspects sopra. se c'e' dentro default , toglilo dal file coords a mano. se c'è mod, rinomina da -dracula-mod a draculaMod
                                {
                                    aspettoMatcha = true;
                                }
                                else
                                {
                                    aspettoMatcha = false;
                                }

                                loIsHereWithCorrectAspect = aspettoMatcha && string.Equals(lo.AlternatePos?.serId, sp.PositionName, StringComparison.Ordinal);
                            }
                        }
                        else
                        {

                            loIsHereWithCorrectAspect = false;
                            lo = null;
                        }




                        //if (layerFileName == "bg-png" || (loIsHereWithCorrectAspect && !isPortrait) || (lo == null && !isPortrait))
                        if (loIsHereWithCorrectAspect || lo == null) // temp finche non ho di nuovo gli apsect
                        {



                            //var filename = layerFileName.Replace("-png", "") + ".png";

                            var path = $"{graphicsRootFolderName()}/{room.assetFolderName}/{sp.SpriteScaledAdjustedFileName}";
                            LayerInfoParsed rect = new LayerInfoParsed(new RectSeg((int)sp.XOrig, (int)sp.YOrig, (int)sp.Width, (int)sp.Height)); // isHiRes dicCoordsOfNomeFileLayer[layerFileName];

                            return new LayerForClient
                            {
                                lfc_imgPath = path,
                                lfc_x = rect.rect.x,
                                lfc_y = rect.rect.y,
                                lfc_ht = rect.rect.ht,
                                lfc_wt = rect.rect.wt,
                                lfc_loId = sp.LogicalObj,
                                lfcIsOutline = sp.IsOutline,
                                lfc_zIndex = sp.ZIndex

                            };
                        }
                        else
                        {
                            return null;
                        }

                    })
                    .SelectSome()
                    //.Reverse() // in photoshop il primo è più sopra, ma nel browser il primo va sotto
                    .ToArray();
            return rfc_layersColor.Concat(rfc_sprites)
                .OrderBy(layer => layer.lfc_zIndex)
                .ToArray();
        }

        private void ValidateAlternatePositions(Room room)
        {
            foreach (var lo in loOfId.Values.Distinct())
            {
                if (lo.AlternatePos == null || !lo.isIn(room))
                {
                    continue;
                }

                var aspectCandidates = room.coordFileEditor.Sprites
                    .Where(sp => string.Equals(sp.LogicalObj, lo.loId, StringComparison.Ordinal)
                        && string.Equals(lo.Aspect?.serId, sp.Aspect, StringComparison.Ordinal))
                    .ToList();

                if (aspectCandidates.Count > 0 && !aspectCandidates.Any(sp =>
                    string.Equals(sp.PositionName, lo.AlternatePos.serId, StringComparison.Ordinal)))
                {
                    var aspectName = lo.Aspect?.serId ?? "default";
                    throw new InvalidOperationException(
                        $"Il LogicObj '{lo.loId}' richiede la posizione alternativa '{lo.AlternatePos.serId}' " +
                        $"nella room '{room.assetFolderName}', ma layer_data.json non contiene un PNG per la " +
                        $"combinazione aspect '{aspectName}' + posizione '{lo.AlternatePos.serId}'. " +
                        "Apri la room nello Scene Editor e crea/esporta la posizione alternativa per ogni aspect " +
                        "del bundle, oppure imposta AlternatePos a null per usare la posizione di default.");
                }
            }
        }

        //internal static void parseFileNameDaCoordFile(string filename, out string loId, out string[] aspects, out bool isPortrait, out bool isOutline, out bool isStatic)
        //{

        //    var loIdAndAspect = System.IO.Path.GetFileNameWithoutExtension(filename);

        //    //if (loIdAndAspect.Contains("sedia_elettrica"))
        //    //{
        //    //        var y = 4;
        //    //}
        //    var spl = loIdAndAspect.Split('-');
        //    loId = spl.First();

        //    // portrait, hires, outline vengono ignorati. non sono aspects. static significa che lo mette automaticamente nella room, invariante
        //    aspects = spl.Skip(1).Where(x => x != "po" && x != "ou" && x != "png" && x != "hi" && x != "static").ToArray();

        //    //isPortrait = loIdAndAspect.EndsWith("-po");
        //    //isOutline = loIdAndAspect.EndsWith("-ou");
        //    isPortrait = spl.Contains("po");
        //    isOutline = spl.Contains("ou");
        //    isStatic = spl.Contains("static");
        //}

        private ObjectiveClient objectiveClientOfObjective(Objective o, XDocIndexed xdocObj)
        {

            if (o.serId == "puEntrareNelCastelloCamillaSuperandoLaGuardia")
            {
                var y = 4;
            }
            else if (o.serId == "puTrovareLaPrincipessaNelCastello")
            {
                var y = 4;
            }
            //var ebug = puzzleSolvedHandlers.GroupBy(x => x.puzzleSolution.objective).Where(gr => gr.Count() > 1).ToDictionary(x => x.Key, x => x.ToList());

            //var handlersDiO = (from ha in puzzleSolvedHandlersOldUi
            //                   where ha.puzzleSolution.objective.serId == o.serId
            //                   select ha).ToList();
            //var scelte = (from ha in handlersDiO
            //              let primoTokenVerbo = (EnumeratedToken)ha.puzzleSolution.solution.First()
            //              select primoTokenVerbo.choices);
            //var scelteFlatDistinct = scelte.Flatten().Distinct().ToList();
            //var oc_verbsToShow = scelteFlatDistinct
            //                      .Where(qt => qtokIsVisibleNow(o, qt) && qtokIsEnabledNow(qt))
            //                      .OrderBy(qt => qt.Priority)
            //                      .Select(qt =>
            //                      qt.serId)

            //                      //new QtokClient(qt, x => VerbQTokIsVisibleNowForSelectedObjective(o, x) && ObjQtokIsVisibleNowForWorldState(x)))
            //                      .ToList()

            //                      ;

            string customExplanationsIntro;
            var customExplanationIntro = o.CustomExplanationsIntro
                ?? (o.CustomExplanations?.FirstOrDefault() is Explanation explanation
                    ? getExplanationGroupIntro(explanation)
                    : null);
            if (customExplanationIntro != null)
            {
                customExplanationsIntro = translateDialogOrNarOrAnnotated(customExplanationIntro, xdocObj);
            }
            else
            {
                customExplanationsIntro = null;
            }




            string customExplanationsFailureTemplate;
            if (o.CustomExplanationsFailureTemplate != null)
            {
                customExplanationsFailureTemplate = translateDialogOrNarOrAnnotated(o.CustomExplanationsFailureTemplate, xdocObj);
            }
            else
            {
                customExplanationsFailureTemplate = null;
            }

            ExplanationClient[] customExplanations;
            if (o.CustomExplanations != null)
            {
                customExplanations = o.CustomExplanations.Select(x => new ExplanationClient(x.expId, translateDialogOrNarOrAnnotated(x.exName, xdocObj))).ToArray();
            }
            else
            {
                customExplanations = null;
            }
            // l'obiettivo mostra usa se esiste almeno una soluzione che inizia per usa o "dì"
            //var showsUse =
            //        handlersDiO.Any(ha => ha.puzzleSolution.solution.First() is EnumeratedToken et && (et.correct == UseToken() || et.correct == SayToken()));

            // This is only the fallback rule, used when there is no exact
            // (object, objective) handler.  Different objects may legitimately
            // have different explanation requirements for the same objective.
            var handlersForObjective = useForHandlers.Where(ha => ha.Objective == o).ToList();
            var obcDoNotShowExplanations = !handlersForObjective.Any(ha => ha.Explanation != null)
                && !hasActiveUseForExplanationContext(o);

            return new ObjectiveClient
                    (
                            readable_name: o.translated_name(this, xdocObj),
                            ser_id: o.serId
                            , wasSeen: o.IsSeen()
                            , obcDoNotShowExplanations: obcDoNotShowExplanations
                            , customExplanationsIntro: customExplanationsIntro
                            , customExplanations: customExplanations
                            , customExplanationsFailureTemplate: customExplanationsFailureTemplate
                            , containedSubject: o.ContainedSubject == null ? null : translateDialogOrNarOrAnnotated(o.ContainedSubject, xdocObj)
                    //oc_verbsToShow: oc_verbsToShow
                    //, requiresBecause: o.requiresBecause
                    //, associatedQtoks: o.associatedQToks.Select(q => q.serId).ToArray()
                    //, excludedQtoks: o.excludedQtoks.Select(q => q.serId).ToArray()
                    //, showsUse: showsUse
                    //oc_is_temp_disabled_for_random_trials = o.must_be_disabled_now()
                    );
        }

        private ObjForClient ofcOfLo(LogicObj lo, XDocIndexed xdocObjs)
        {
            if (lo.loId == "innOwner")
            {
                var x = 4;
            }

            var ofcCanBeRemembered = namedCutScenesSeen.Any(nc => nc.oggettiMenzionati.Contains(lo), out NamedCutScene trovato);

            return new ObjForClient(lo, xdocObjs);

        }

        public string translateSentenceWithIdFromObjfile(string strToTranslate, string xelementName, XDocument xdocObj)
        {
            if (CurLang == null)
            {
                return strToTranslate;
            }

            string nameTransl;


            //var xmlPath = getPathXmlTranslationObjs(curLang);
            //var xdoc = XDocument.Load(xmlPath);
            var el = xdocObj.Root.Element(xelementName);
            if (el != null && el.Attribute("transl").Value != "+")
            {
                nameTransl = el.Attribute("transl").Value.Replace("''", "\"");
            }
            else
            {
                nameTransl = strToTranslate;
            }

            return nameTransl;
        }


        /// <summary>
        /// non serve serializzarla. serve solo a tener traccia all'interno dello stesso metodo, per evitare di appendere sempre ", cs" .
        /// è uno stack, e non una variabile, per gestire le chiamate nidificate.
        /// </summary>
        internal Stack<CutScene> curCs = new Stack<CutScene>();

        public CutScene curCutScene()
        {
            return curCs.Peek();
        }

        public void setCurrentCs(CutScene cs)
        {
            curCs.Push(cs);
        }

        public void clearCurrentCs()
        {
            curCs.Pop();
        }

        internal NamedCutScene cur_named_cs;

        private void addTokenToCutSceneAndNamedCutScene(List<CutSceneToken> cs, CutSceneToken token)
        {
            cs.Add(token);

            // qui devo aggiungere il token alla named cut scene, se è aperta
            if (cur_named_cs != null)
            {
                CutSceneToken clone;
                if (token is DialogToken dt)
                {
                    clone = new DialogToken(canBeSkipped: true, img: dt.img, charName: dt.dtCharName, par: dt.dtPar, canGoBackToPrev: dt.cstCanGoBackToPrevious, size: dt.ntSize);
                }
                else if (token is NarToken nt)
                {
                    clone = new NarToken(canBeSkipped: true, img: nt.img, par: nt.ntPar, canGoBackToPrev: nt.cstCanGoBackToPrevious, ntLayers: nt.ntLayers, removeIfLast: nt.removeIfLast, size: nt.ntSize);
                }
                else if (token is NarTokenMultipar mp)
                {
                    clone = new NarTokenMultipar(canBeSkipped: true, img: mp.img, pars: mp.pars, canGoBackToPrev: mp.cstCanGoBackToPrevious);

                }
                else
                {

                    throw new Exception();
                }


                cur_named_cs.cs.Add(clone);
            }

        }

        public void dial(Character c, string testo, NarSize size = NarSize.Small, string insta = null)
        {

            if (c.customNarSizeForDialog != null)
            {
                size = c.customNarSizeForDialog.Value;
            }

            if (curCs.isEmpty())
            {
                throw new Exception($"cur_cs is null");
            }
            //eng.dial(c, s, cur_cs.Peek());


            var cs = curCs.Peek();



            //Aspect oldAspect;
            //if (aspectTemp != null)
            //{
            //        oldAspect = c.Aspect;
            //        c.Aspect = aspectTemp.Aspect;
            //}
            //else
            //{
            //        oldAspect = null;
            //}

            var xdocI = getXdocObjIndexedCached();
            var testoTrad = translateDialogOrNarOrAnnotated(testo, xdocI);

            if (insta != null)
            {
                string instaTrad = translateDialogOrNarOrAnnotated(insta, xdocI);
                testoTrad = testoTrad.inst(instaTrad).firstLetterToUpper();
            }


            //var ac = c.wo.ac;
            var imgToSet = c.calcImgPortrait();


            string charName = $"{c.dynamicNameTranslated(xdocI, false, isForDialog: true)}. ";
            //if (c.customNameForDialog != null)
            //{
            //        charName = translateDialogOrNarOrAnnotated(c.customNameForDialog(), xdocI);
            //}
            //else {


            //charName = $"{c.translatedNameForDialog(xdocI) ?? c.dynamicNameTranslated(xdocI, withArticle: false)}. ";
            //}

            var token = new DialogToken
                (canBeSkipped: cs.canBeSkipped,
                 par: testoTrad,
                 img: imgToSet,
                 charName: charName,
                 canGoBackToPrev: !cs.isEmpty()
                 , size: size
                );

            addTokenToCutSceneAndNamedCutScene(cs, token);

            //if (aspectTemp != null)
            //{
            //        c.Aspect = oldAspect;
            //}
            //cs.Add(s.todial(c));
        }

        //internal XDocument getXdocObj()
        //{
        //        XDocument xdocObj;
        //        if (curLang != null)
        //        {
        //                var xmlPath = WorldBase.getPathXmlTranslationObjs(curLang);
        //                xdocObj = XDocument.Load(xmlPath);
        //        }
        //        else
        //        {
        //                xdocObj = null;
        //        }

        //        return xdocObj;
        //}

        // Regression fixtures in a derived game assembly use the same engine
        // execution pipeline as the web integration. Keep these seams
        // protected instead of making the low-level static engine methods
        // public API.
        protected internal SegActionRes executeUseWithForTesting(
            LogicObj first,
            LogicObj target,
            Explanation explanation,
            bool youAlreadyKnowItWillFail,
            string[] saveNames,
            XDocIndexed xdi,
            bool isTextMode)
            => eng.executeActionUseWith(first, target, explanation,
                youAlreadyKnowItWillFail, this, saveNames, xdi, isTextMode);

        protected internal SegActionRes executeUseForForTesting(
            LogicObj tool,
            Objective objective,
            Explanation explanation,
            string[] saveNames,
            XDocIndexed xdi,
            bool isTextMode)
            => eng.executeActionUseFor(tool, objective, explanation,
                this, saveNames, xdi, isTextMode);

        protected internal XDocIndexed getXdocObjIndexedCached()
        {



            XDocIndexed xdocI;
            if (CurLang != null)
            {
                var cached = eng.xdocIndexedCached.GetOrAdd(CurLang, x =>
                {
                    var xmlPath = WorldBase.getPathXmlTranslationObjs(CurLang);
                    var xdocObj = XDocument.Load(xmlPath);



                    var fullpathXmlGen = get_path_xml_translation_dialogs(CurLang);
                    var xdocGen = XDocument.Load(fullpathXmlGen);

                    xdocI = new XDocIndexed(xdocObj, xdocGen);
                    return xdocI;

                });

                xdocI = cached;


            }
            else
            {
                xdocI = null;
            }

            return xdocI;

        }

        public static string removeTranslationHint(string t)
        {
            if (t == null)
            {
                throw new Exception("t is null");
            }
            var longest_match = Regex.Matches(t, @"\[\[.*\]\]").Cast<Match>().Select(ma => new { ma, le = ma.Value.Length }).OrderByDescending(x => x.le).FirstOrDefault();
            if (longest_match == null)
            {
                return t;
            }
            else
            {
                var t2 = t.Replace(longest_match.ma.Value, "");
                return t2;
            }

        }

        public string translateDialogOrNarOrAnnotated(string testo, XDocIndexed xdi)
        {
            //if (testo.Contains("mangerà"))
            //{
            //        var y = 4;
            //}
            return translateDialogOrNarOrAnnotatedAux(testo, xdi, out bool? _found);
        }

        public string translateDialogOrNarOrAnnotatedAux(string testo, XDocIndexed xdi, out bool? found)
        {

            if (testo == null)
            {
                found = null;
                return null;
            }

            if (CurLang == null)
            {
                found = null;
                return removeTranslationHint(testo);
            }
            else
            {
                var testoQuotes = testo.Replace("\"", "''"); // nell'xml sono salvati con '' per leggibilità
                var testoTrad = xdi.translate(testoQuotes, out bool found2);
                found = found2;


                //var fullpathXml = get_path_xml_translation_dialogs(curLang);
                //var xdoc = XDocument.Load(fullpathXml);
                //var testoQuotes = testo.Replace("\"", "''"); // nell'xml sono salvati con '' per leggibilità
                //var el = xdoc.Root.Elements("str").Where(ele => ele.Attribute("orig").Value == testoQuotes).FirstOrDefault();
                //string testoTrad;
                //if (el != null)
                //{
                //        // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                //        if (el.Attribute("transl").Value != "+")
                //        {
                //                testoTrad = el.Attribute("transl").Value;
                //        }
                //        else
                //        {
                //                testoTrad = removeTranslationHint(testo);
                //        }
                //}
                //else
                //{
                //        testoTrad = removeTranslationHint(testo);
                //}

                return testoTrad.Replace("''", "\"") /* inverti la sostituzione fatta dal tool */;
            }
        }

        public static string get_path_xml_translation_dialogs(string lang)
        {
            return Utils.MapPathCrossHost($"~/transl_{lang}.xml");
        }

        public static string getPathXmlTranslationObjs(string lang)
        {
            return Utils.MapPathCrossHost($"~/objects_transl_{lang}.xml");
        }

        public void narText(string s, bool removeIfLast = false)
        {
            if (curCs.isEmpty())
            {
                throw new Exception($"cur_cs is null");
            }

            var cs = curCs.Peek();

            var xdi = getXdocObjIndexedCached();

            var testoTrad = translateDialogOrNarOrAnnotated(s, xdi);

            var tok = new NarToken(canBeSkipped: cs.canBeSkipped, par: testoTrad, img: null, canGoBackToPrev: !cs.isEmpty(), ntLayers: new LayerForClient[] { },
                    removeIfLast: removeIfLast, size: NarSize.Small);


            addTokenToCutSceneAndNamedCutScene(cs, tok);

        }
        public void narImg(string s, string img, NarSize size = NarSize.FullScreen, bool removeIfLast = false, bool alsoShowGraphicsInTextMode = false)
        {
            if (curCs.isEmpty())
            {
                throw new Exception($"cur_cs is null");
            }

            var cs = curCs.Peek();

            var xdi = getXdocObjIndexedCached();
            var testoTrad = translateDialogOrNarOrAnnotated(s, xdi);

            var tok = new NarToken(canBeSkipped: cs.canBeSkipped, par: testoTrad
                    , img: IsTextMode && !alsoShowGraphicsInTextMode ? null : img
                    , canGoBackToPrev: !cs.isEmpty(), ntLayers: new LayerForClient[] { },
                    removeIfLast: removeIfLast

                    , size: IsTextMode && !alsoShowGraphicsInTextMode ? NarSize.Small : size);


            addTokenToCutSceneAndNamedCutScene(cs, tok);

        }
        public void narRoom(string s, Room room, bool removeIfLast, bool alsoShowGraphicsInTextMode = false)
        {
            if (curCs.isEmpty())
            {
                throw new Exception($"cur_cs is null");
            }

            var cs = curCs.Peek();
            var xdi = getXdocObjIndexedCached();
            var testoTrad = translateDialogOrNarOrAnnotated(s, xdi);


            LayerForClient[] ntLayers;

            if (IsTextMode && !alsoShowGraphicsInTextMode)
            {
                ntLayers = new LayerForClient[] { }; // se null, crasha serialize
            }
            else
            {
                ntLayers = buildLayersOfRoom(room);
            }

            var tok = new NarToken(canBeSkipped: cs.canBeSkipped, par: testoTrad

                    , img: IsTextMode && !alsoShowGraphicsInTextMode ? null : room.imgPathForNarRoom()

                    , canGoBackToPrev: !cs.isEmpty(),
                    ntLayers: ntLayers, removeIfLast: removeIfLast
                    , size: IsTextMode && !alsoShowGraphicsInTextMode ? NarSize.Small : NarSize.FullScreen
                    );


            addTokenToCutSceneAndNamedCutScene(cs, tok);

        }



        public bool wasSeenAtLeastOnce(CycleElemId el)
        {
            return howManyTimesElementExecuted.ContainsKey(el) && howManyTimesElementExecuted[el] > 0;
        }

        public bool neverSeen(CycleElemId el)
        {
            return !wasSeenAtLeastOnce(el);
        }

        /// <summary>
        /// if some element has never been seen, returns false, because certainly no more than N minutes passed
        /// </summary>
        /// <param name="minutes"></param>
        /// <param name="elems">they can be CycleElemIds or DateTime?</param>
        /// <returns></returns>
        public bool moreThanNMinutesPassedFromAll__old(double minutes, params object[] elems)
        {
            // return PEROGNI (el :  [el non e' mai stato detto  ] OR [el e' stato detto da piu' di N minuti] )

            var result = elems.All(el =>
            {
                if (el is CycleElemId cy)
                {
                    if (!lastTimeElementExecuted.ContainsKey(cy))
                    {
                        // se non sono mai stati visti, certamente non sono passati più di 5 minuti
                        return false;


                    }
                    else
                    {
                        var lastTime = lastTimeElementExecuted[cy];

                        if (DateTime.Now.Subtract(lastTime).TotalMinutes > minutes)
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
                else if (el is DateTime da)
                {
                    if (da == default(DateTime))
                    {
                        return false; // se mai visto, false
                    }
                    else
                    {
                        if (DateTime.Now.Subtract(da).TotalMinutes > minutes)
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            });

            return result;


        }


        public bool noneOfThemWasSeenRecently(double minutes, params object[] elems)
        {

            // il senso è che gli elementi sono equivalenti. quindi se almeno uno è stato visto da meno di tot tempo, non devo mostrare nessuno di loro.

            // quinidi faccio: se esiste uno che è stato visto da meno di tot

            // e poi nego il tutto, ottenendo "nessno di loro è stato visto da meno di tot"

            var result = elems.Any(el =>
            {
                if (el is CycleElemId cy)
                {
                    if (!lastTimeElementExecuted.ContainsKey(cy))
                    {
                        // non è stato visto, quindi è falso che è stato visto da meno di tot
                        return false;


                    }
                    else
                    {
                        var lastTime = lastTimeElementExecuted[cy];

                        if (DateTime.Now.Subtract(lastTime).TotalMinutes > minutes)
                        {
                            return false; // non è vero che è stato visto da meno di tot
                        }
                        else
                        {
                            return true; // è stato visto da meno di tot
                        }
                    }
                }
                else if (el is DateTime da)
                {
                    if (da == default(DateTime))
                    {
                        return false;
                    }
                    else
                    {
                        if (DateTime.Now.Subtract(da).TotalMinutes > minutes)
                        {
                            return false;
                        }
                        else
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            });

            return !result; // devo negare, vedi sopra spiegazione


        }


        //public void dial(string s, List<cut_scene_token> cs)
        //{
        //    eng.nar(s, cs);
        //}

        public void readMapXmlAndSetRoomCoords(string xmlPath)
        {
            var fullpath = Utils.MapPathCrossHost($"~/{xmlPath}");
            var xdoc = XDocument.Load(fullpath);

            XElement xelroot = xdoc.Root.Element("diagram").Element("mxGraphModel").Element("root");
            var cells = xelroot.Elements("mxCell")
                            //.Where(c => c.Attribute("value") != null)
                            //.Where(c => c.Value.Contains("--"))
                            .ToList();


            var cellsWithDynName = xelroot.Elements("object")
                            .Where(c => c.Attribute("dynName") != null)
                            .Select(o =>

                           {
                               var cell = o.Element("mxCell");
                               cell.Add(new XAttribute("value", $"--dyn{o.Attribute("dynName").Value}"));
                               return cell;

                           })


                            .ToList();


            cells = cells.Concat(cellsWithDynName).ToList();

            //var cellsInObjects = xdoc.Root.Element("root").Elements("object").toList();

            // trova le room da xmlid
            var roomOfXmlId = trovaRoomDictionaryXmlId(cells);



            // setta le posizioni delle room

            var cellsWithValue = (from ce in cells
                                  where ce.Attribute("value") != null
                                  where ce.Attribute("value").Value.StartsWith("--dyn")
                                  select ce).ToList();


            foreach (var el in cells)
            {
                var atval = el.Attribute("value");
                if (atval != null)
                {
                    if (atval.Value.Contains("--room") || atval.Value.Contains("--\nroom") || atval.Value.Contains("-- room"))
                    {
                        // trovo roomId
                        var roomId = findRoomIdOrDynId(atval.Value);

                        // adesso trovo x e y
                        var elG = el.Element("mxGeometry");
                        if (elG != null)
                        {
                            double x;

                            var atx = elG.Attribute("x");
                            if (atx == null)
                            {
                                x = 0;
                            }
                            else
                            {
                                x = double.Parse(atx.Value, CultureInfo.InvariantCulture);
                            }






                            double y;

                            var aty = elG.Attribute("y");
                            if (aty == null)
                            {
                                y = 0;
                            }
                            else
                            {
                                y = double.Parse(aty.Value, CultureInfo.InvariantCulture);
                            }



                            var room = roomOfId[roomId];
                            //if (room.ManualPosX != null)
                            //{
                            //    room.map_x = room.ManualPosX.Value;
                            //}
                            //else
                            {
                                room.map_x = x;
                            }

                            //if (room.ManualPosY != null)
                            //{
                            //    room.map_y = room.ManualPosY.Value;
                            //}
                            //else
                            {
                                room.map_y = y;
                            }



                        }
                    }
                    else if (atval.Value.Contains("--dyn"))
                    {
                        // attenzione, questo viene riletto solo quando riavvii il server, cioè quando costruisci u nuovo world() (oppure forse anche se hai il debug di ricaricare il mondo da db ogni volta).
                        // quindi se non vedi modifiche, termina dalla taskbar iis express

                        // trovo roomId
                        var dynId = findRoomIdOrDynId(atval.Value);

                        // adesso trovo x e y
                        var elG = el.Element("mxGeometry");

                        var elPoints = elG.Elements("mxPoint").Select(elp => new MapPoint
                        {
                            x = double.Parse(elp.Attribute("x").Value, CultureInfo.InvariantCulture),
                            y = double.Parse(elp.Attribute("y").Value, CultureInfo.InvariantCulture)
                        }).ToArray();


                        if (elPoints[0] == null)
                        {
                            throw new Exception("null");
                        }

                        //if (dynLineOfId.ContainsKey(dynId))
                        //{
                        //    var dyn = dynLineOfId[dynId];
                        //    dyn.startPoint = elPoints[0];
                        //    dyn.endPoint = elPoints[1];

                        //}
                        //else
                        //{
                        //    throw new Exception($"In the xml you defined a dynLine called {dynId}, but in the csharp code you didn't define a dynline");
                        //}



                    }
                }
            }



            // setta le exit
            foreach (var el in cells)
            {

                //if (el.Attribute("id").Value == "5390bec1807b25bf-35")
                //{
                //    var tt = 4;
                //}
                var atsource = el.Attribute("source");
                var attarget = el.Attribute("target");
                if (atsource != null && attarget != null)
                {
                    var sourceId = atsource.Value;
                    var targId = attarget.Value;

                    if (sourceId == "5390bec1807b25bf-35" || targId == "5390bec1807b25bf-35")
                    {
                        var y = 4;
                    }

                    if (roomOfXmlId.ContainsKey(sourceId) && roomOfXmlId.ContainsKey(targId)) // può darsi che sia falso, se hai messo stanze temporanee non implementate ma già nella mappa
                    {
                        var sourceRoom = roomOfXmlId[sourceId];



                        var targRoom = roomOfXmlId[targId];



                        addExit(sourceRoom, targRoom);
                    }
                }
            }
        }
        public void readMapJsonAndSetRoomCoords(string jsonPath)
        {
            var fullpath = Utils.MapPathCrossHost($"~/{jsonPath}");

            var text = System.IO.File.ReadAllText(fullpath);
            var cl = System.Text.Json.JsonSerializer.Deserialize<Seg.LocationData>(text);

            if (cl == null)
                throw new InvalidOperationException($"Il file mappa '{fullpath}' non contiene un JSON valido.");

            // The editor writes an absolute path, but the engine intentionally keeps
            // only the basename: the generated JPG is deployed under the web root.
            if (!string.IsNullOrWhiteSpace(cl.BackgroundImageExportedPath))
            {
                // Normalize Windows paths too, because the engine may run on Linux.
                mapImageFileName = System.IO.Path.GetFileName(
                    cl.BackgroundImageExportedPath.Replace('\\', '/'));
                var mapImagePath = Utils.MapPathCrossHost($"~/{mapImageFolder}/{mapImageFileName}");
                if (!System.IO.File.Exists(mapImagePath))
                    throw new System.IO.FileNotFoundException($"Immagine mappa non trovata nella cartella '{mapImageFolder}'.", mapImagePath);
                mapImageX = cl.BackgroundImageX;
                mapImageY = cl.BackgroundImageY;
                mapImageWidth = cl.BackgroundImageWidth;
                mapImageHeight = cl.BackgroundImageHeight;
            }

            foreach (var ro in cl.Locations)
            {
                var room = roomOfId[ro.Id];
                //if (room.ManualPosX != null)
                //{
                //    room.map_x = room.ManualPosX.Value;
                //}
                //else
                {
                    room.map_x = ro.X;
                }

                //if (room.ManualPosY != null)
                //{
                //    room.map_y = room.ManualPosY.Value;
                //}
                //else
                {
                    room.map_y = ro.Y;
                }


                foreach (var ex in ro.Connections)
                {
                    var targ = roomOfId[ex];

                    addExit(room, targ);
                }

            }

        }


        //public abstract bool isDynLineVisible(DynLine dyn);

        private Dictionary<string, Room> trovaRoomDictionaryXmlId(List<XElement> cells)
        {
            var roomOfXmlId = new Dictionary<string, Room>();

            foreach (var el in cells)
            {
                var atval = el.Attribute("value");
                if (atval != null)
                {
                    if (atval.Value.Contains("--room") || atval.Value.Contains("--\nroom") || atval.Value.Contains("-- room"))
                    {
                        var roomId = findRoomIdOrDynId(atval.Value);


                        var xmlId = el.Attribute("id").Value;
                        if (!roomOfId.ContainsKey(roomId))
                        {
                            throw new Exception($"In the xml you defined a room {roomId}, but in the .cs file you did not define it.");
                        }

                        var room = roomOfId[roomId];
                        roomOfXmlId[xmlId] = room;
                    }
                    //else if (atval.Value.Contains("--dyn"))
                    //{
                    //    string dynId = findRoomIdOrDynId(atval.Value);


                    //    var xmlId = el.Attribute("id").Value;
                    //    if (!dynLineOfId.ContainsKey(dynId))
                    //    {
                    //        throw new Exception($"In the xml you defined a dynLine {dynId}, but in the .cs file you did not define it.");
                    //    }
                    //    var dyn = dynLineOfId[dynId];
                    //    dynLineOfId[xmlId] = dyn;
                    //}
                }
            }

            return roomOfXmlId;
        }

        private static string findRoomIdOrDynId(string atval)
        {
            var spl = atval.Split(new[] { "--" }, StringSplitOptions.None);

            var second = spl[1];
            var roomId = second.Replace(" ", ""); // ho dovuto mett spazi per andare a capo

            roomId = roomId.Replace("<div/>", "");
            roomId = roomId.Replace("<div>", "");
            roomId = roomId.Replace("</div>", "");
            roomId = roomId.Replace("<div />", "");
            roomId = roomId.Replace("<br/>", "");
            roomId = roomId.Replace("<br>", "");
            roomId = roomId.Replace("\n", "");
            return roomId;
        }



        //public abstract bool mustTalkNow(Room curRoom);
        public abstract Cycle getRoomCycle(Room r);

        //public abstract bool cannotTalkNow(Room curRoom);

        //public abstract void cutSceneCannotTalkNow();

        public void rememberYouHaveJustSeenCycleElement(CycleElemId id)
        {
            lastTimeElementExecuted[id] = DateTime.Now;

            if (howManyTimesElementExecuted.ContainsKey(id))
            {
                howManyTimesElementExecuted[id] = howManyTimesElementExecuted[id] + 1;
            }
            else
            {
                howManyTimesElementExecuted[id] = 1;
            }
        }

        public static Cycle startCycle(CycleElemId Id, Importance isImportant, Repeat repeat, Func<DateTime?, bool> cond, Action<DateTime?> a)
        {
            var li = new Cycle { new CycleElement(Id) { repeat = repeat, cond = cond, action = a, IsImportant = isImportant == Importance.Important ? true : false } };

            return li;

        }

        private void ValidateAlternatePositionDefinitions()
        {
            var invalid = allAlternatePositions
                .FirstOrDefault(position => string.IsNullOrWhiteSpace(position.serId));
            if (invalid != null)
            {
                throw new InvalidOperationException("Ogni AlternatePosition deve avere un ID non vuoto.");
            }

            var duplicate = allAlternatePositions
                .GroupBy(position => position.serId, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null)
            {
                throw new InvalidOperationException(
                    $"Esistono più AlternatePosition con l'ID '{duplicate.Key}'. Gli ID devono essere univoci.");
            }
        }

        public static Cycle startCycle(string Id, Importance isImportant, Repeat repeat, Func<DateTime?, bool> cond, Action<DateTime?> a)
            => startCycle(new CycleElemId(Id), isImportant, repeat, cond, a);

        public static Cycle startCycle(string Id, Repeat repeat, Func<DateTime?, bool> cond, Action<DateTime?> a)
            => startCycle(new CycleElemId(Id), repeat, cond, a);

        public static Cycle startCycle(CycleElemId Id, Repeat repeat, Func<DateTime?, bool> cond, Action<DateTime?> a)
        {
            var li = new Cycle { new CycleElement(Id) { repeat = repeat, cond = cond, action = a, IsImportant = false } };

            return li;

        }

        public Cycle startCycle(CycleElemId Id, Action<DateTime?> a)
        {
            var li = new Cycle { new CycleElement(Id) { action = a } };

            return li;

        }

        public Cycle startCycle(string Id, Action<DateTime?> a) => startCycle(new CycleElemId(Id), a);
        public Cycle startCycle(string Id, params Action<DateTime?>[] actions) => startCycle(new CycleElemId(Id), actions);

        public Cycle startCycle(CycleElemId Id, params Action<DateTime?>[] actions)
        {
            return startCycle(Id, x =>
            {
                foreach (var action in actions)
                {
                    action(x);
                }
            });
        }

        //internal HashSet<string> DeclaredCycleIds = new HashSet<string>();

        //public Cycle startCycle(string Id, Action<DateTime?> a)
        //{

        //        var li = new Cycle { new CycleElement( new CycleElemId( Id, this) ) { action = a } };

        //        return li;

        //}


        public Cycle startCycle(CycleElemId Id, Func<DateTime?, bool> cond, Action<DateTime?> a)
        {
            var li = new Cycle { new CycleElement(Id) { cond = cond, action = a } };

            return li;

        }

        public Cycle startCycle(string Id, Func<DateTime?, bool> cond, Action<DateTime?> a)
            => startCycle(new CycleElemId(Id), cond, a);
        public Cycle startCycle(string Id, Func<DateTime?, bool> cond, params Action<DateTime?>[] actions)
            => startCycle(new CycleElemId(Id), cond, actions);

        public Cycle startCycle(CycleElemId Id, Func<DateTime?, bool> cond, params Action<DateTime?>[] actions)
        {
            return startCycle(Id, cond, x =>
            {
                foreach (var action in actions)
                {
                    action(x);
                }
            });
        }

        public Cycle startCycle(CycleElemId Id, Importance i, Func<DateTime?, bool> cond, Action<DateTime?> a)
        {
            var li = new Cycle { new CycleElement(Id) { cond = cond, action = a, IsImportant = i == Importance.Important } };

            return li;

        }
        public Cycle startCycle(string Id, Importance i, Func<DateTime?, bool> cond, Action<DateTime?> a)
            => startCycle(new CycleElemId(Id), i, cond, a);
        //public Cycle startCycle(string Id, Func<DateTime?, bool> cond, Action<DateTime?> a)
        //{
        //        var id2 = new CycleElemId(Id, this);
        //        var li = new Cycle { new CycleElement(id2) { cond = cond, action = a } };

        //        return li;

        //}


        public Cycle startCycle(CycleElemId Id, Repeat repeat, Action<DateTime?> a)
        {
            var li = new Cycle { new CycleElement(Id) { repeat = repeat, action = a } };

            return li;

        }
        public Cycle startCycle(string Id, Repeat repeat, Action<DateTime?> a)
            => startCycle(new CycleElemId(Id), repeat, a);
        public Cycle startCycle(string Id, Repeat repeat, params Action<DateTime?>[] actions)
            => startCycle(new CycleElemId(Id), repeat, actions);

        public Cycle startCycle(CycleElemId Id, Repeat repeat, params Action<DateTime?>[] actions)
        {
            return startCycle(Id, repeat, x =>
            {
                foreach (var action in actions)
                {
                    action(x);
                }
            });
        }

        public Cycle startCycle(CycleElemId Id, Importance importance, Repeat repeat, Action<DateTime?> a)
        {
            var li = new Cycle { new CycleElement(Id) { repeat = repeat, action = a, IsImportant = importance == Importance.Important } };

            return li;

        }
        public Cycle startCycle(string Id, Importance importance, Repeat repeat, Action<DateTime?> a)
            => startCycle(new CycleElemId(Id), importance, repeat, a);
        //public void named_cut_scene(string title, IEnumerable<logic_obj> oggetti_menzionati,  Action a)
        //{
        //    begin_named_cut_scene(title, oggetti_menzionati);

        //    a();

        //    end_named_cut_scene();

        //}

        //public void named_cut_scene(string title, Action a)
        //{
        //    begin_named_cut_scene(title, null);

        //    a();

        //    end_named_cut_scene();

        //}


        public NamedCutSceneDisposer namedCutScene(NamedCutSceneId id, Room roomWhereYouAre, params Mentionable[] objectsMentioned)
        {
            if (cur_named_cs != null)
            {
                throw new Exception($"There is already an open named cut scene: {cur_named_cs.id.serId}");
            }

            if (namedCutScenesSeen.Any(nc => nc.id.serId == id.serId))
            {
                // esiste già. niente
                return new NamedCutSceneDisposer { wo = null };
            }
            else
            {

                if (objectsMentioned == null)
                {
                    objectsMentioned = new Mentionable[] { };
                }

                cur_named_cs = new NamedCutScene(id)
                {
                    cs = new CutScene(canBeSkipped: true /*essendo named, la stai ricordando, quindi la puoi skippare */),
                    oggettiMenzionati = objectsMentioned.ToList(),
                    roomDoveEri = roomWhereYouAre,
                };
                return new NamedCutSceneDisposer { wo = this };
            }
        }

        //internal void end_named_cut_scene()
        //{
        //    cur_named_cs = null;
        //}
        //public static void addToCycle(List<CycleElement> cyc, Action a)
        //{
        //    cyc.Add(new CycleElement { action = a });

        //}

        private void maybeRebuildXmlForTranslation()
        {
            if (rebuildXmlToTranslateObjects(out var lang))
            {
                Debugger.Break();
                var fullpath = Utils.MapPathCrossHost($"~/objects_transl_{lang}.xml");
                XDocument xdoc;
                if (System.IO.File.Exists(fullpath))
                {
                    xdoc = XDocument.Load(fullpath);
                }
                else
                {
                    xdoc = new XDocument();
                    xdoc.Add(new XElement("root"));
                }

                //foreach (var qt in allQtoks)
                //{
                //        var xel = xdoc.Root.Elements("qtok").Where(el => el.Attribute("ser_id").Value == qt.serId).FirstOrDefault();
                //        if (xel == null)
                //        {
                //                xel = new XElement("qtok");
                //                xdoc.Root.Add(xel);

                //                xel.Add(new XAttribute("ser_id", qt.serId));
                //                xel.Add(new XAttribute("name_heShe", qt.readableName_heShe.Replace("\"", "''")));
                //                xel.Add(new XAttribute("name_they", qt.readableName_they.Replace("\"", "''")));
                //                //xel.Add(new XAttribute("name_you", qt.readableName_you.Replace("\"", "''")));
                //                xel.Add(new XAttribute("transl_heShe", "+"));
                //                xel.Add(new XAttribute("transl_they", "+"));
                //                xel.Add(new XAttribute("transl_you", "+"));
                //        }
                //        else
                //        {
                //                xel.Attribute("name_heShe").SetValue(qt.readableName_heShe.Replace("\"", "''"));
                //                xel.Attribute("name_they").SetValue(qt.readableName_they.Replace("\"", "''"));
                //                //xel.Attribute("name_you").SetValue(qt.readableName_you.Replace("\"", "''"));


                //        }
                //}

                foreach (var lo in loOfId.Values)
                {

                    var xel = xdoc.Root.Elements("logic_obj").Where(el => el.Attribute("lo_id").Value == lo.loId).FirstOrDefault();
                    if (xel == null)
                    {
                        xel = new XElement("logic_obj");
                        xdoc.Root.Add(xel);

                        xel.Add(new XAttribute("lo_id", lo.loId));
                        xel.Add(new XAttribute("orig_name", lo.name.Replace("\"", "''")));
                        xel.Add(new XAttribute("transl", "+"));
                    }
                    else
                    {
                        xel.Attribute("orig_name").SetValue(lo.name.Replace("\"", "''"));
                    }
                }

                foreach (var lo in loOfId.Values)
                {
                    if (lo.inTheHandOf != null)
                    {

                        var xel = xdoc.Root.Elements("logic_obj_in_the_hand_of").Where(el => el.Attribute("lo_id").Value == lo.loId).FirstOrDefault();
                        if (xel == null)
                        {
                            xel = new XElement("logic_obj_in_the_hand_of");
                            xdoc.Root.Add(xel);

                            xel.Add(new XAttribute("lo_id", lo.loId));
                            xel.Add(new XAttribute("orig_name", lo.inTheHandOf.Replace("\"", "''")));
                            xel.Add(new XAttribute("transl", "+"));
                        }
                        else
                        {
                            xel.Attribute("orig_name").SetValue(lo.inTheHandOf.Replace("\"", "''"));
                        }
                    }
                }



                foreach (var lo in loOfId.Values)
                {
                    if (lo is Character cha && cha.nameForDialog != null)
                    {

                        var xel = xdoc.Root.Elements("char_name_for_dialog").Where(el => el.Attribute("lo_id").Value == lo.loId).FirstOrDefault();
                        if (xel == null)
                        {
                            xel = new XElement("char_name_for_dialog");
                            xdoc.Root.Add(xel);

                            xel.Add(new XAttribute("lo_id", lo.loId));
                            xel.Add(new XAttribute("orig_name", cha.nameForDialog.Replace("\"", "''")));
                            xel.Add(new XAttribute("transl", "+"));
                        }
                        else
                        {
                            xel.Attribute("orig_name").SetValue(cha.nameForDialog.Replace("\"", "''"));
                        }
                    }

                }









                foreach (var lo in unVerbOfId.Values)
                {

                    var xel = xdoc.Root.Elements("un_verb").Where(el => el.Attribute("verb_id").Value == lo.verbId).FirstOrDefault();
                    if (xel == null)
                    {
                        xel = new XElement("un_verb");
                        xdoc.Root.Add(xel);

                        xel.Add(new XAttribute("verb_id", lo.verbId));
                        xel.Add(new XAttribute("orig_name", lo.name.Replace("\"", "''")));
                        xel.Add(new XAttribute("transl", "+"));
                    }
                    else
                    {
                        xel.Attribute("orig_name").SetValue(lo.name.Replace("\"", "''"));
                    }
                }

                foreach (var lo in binVerbOfId.Values)
                {

                    var xel = xdoc.Root.Elements("bin_verb").Where(el => el.Attribute("verb_id").Value == lo.verbId).FirstOrDefault();
                    if (xel == null)
                    {
                        xel = new XElement("bin_verb");
                        xdoc.Root.Add(xel);

                        xel.Add(new XAttribute("verb_id", lo.verbId));
                        xel.Add(new XAttribute("orig_name", lo.name.Replace("\"", "''")));
                        xel.Add(new XAttribute("transl", "+"));
                    }
                    else
                    {
                        xel.Attribute("orig_name").SetValue(lo.name.Replace("\"", "''"));
                    }
                }




                foreach (var lo in binVerbOfId.Values)
                {

                    var xel = xdoc.Root.Elements("bin_verb_second_part").Where(el => el.Attribute("verb_id").Value == lo.verbId).FirstOrDefault();
                    if (xel == null)
                    {
                        xel = new XElement("bin_verb_second_part");
                        xdoc.Root.Add(xel);

                        xel.Add(new XAttribute("verb_id", lo.verbId));
                        xel.Add(new XAttribute("orig_name", lo.secondPart.Replace("\"", "''")));
                        xel.Add(new XAttribute("transl", "+"));
                    }
                    else
                    {
                        xel.Attribute("orig_name").SetValue(lo.name.Replace("\"", "''"));
                    }
                }






                //foreach (var lo in zeroVerbOfId.Values)
                //{

                //        var xel = xdoc.Root.Elements("zero_verb").Where(el => el.Attribute("verb_id").Value == lo.verbId).FirstOrDefault();
                //        if (xel == null)
                //        {
                //                xel = new XElement("zero_verb");
                //                xdoc.Root.Add(xel);

                //                xel.Add(new XAttribute("verb_id", lo.verbId));
                //                xel.Add(new XAttribute("orig_name", lo.name.Replace("\"", "''")));
                //                xel.Add(new XAttribute("transl", "+"));
                //        }
                //        else
                //        {
                //                xel.Attribute("orig_name").SetValue(lo.name.Replace("\"", "''"));
                //        }
                //}

                foreach (var lo in objectiveOfId.Values)
                {

                    var xel = xdoc.Root.Elements("objective").Where(el => el.Attribute("ser_id").Value == lo.serId).FirstOrDefault();
                    if (xel == null)
                    {
                        xel = new XElement("objective");
                        xdoc.Root.Add(xel);

                        xel.Add(new XAttribute("ser_id", lo.serId));
                        xel.Add(new XAttribute("orig_name", lo.nameReadable.Replace("\"", "''")));
                        xel.Add(new XAttribute("transl", "+"));
                    }
                    else
                    {
                        xel.Attribute("orig_name").SetValue(lo.nameReadable.Replace("\"", "''"));
                    }
                }
                //foreach (var lo in templateOfId.Values)
                //{


                //        var xel = xdoc.Root.Elements("template").Where(el => el.Attribute("te_id").Value == lo.teId).FirstOrDefault();

                //        if (xel == null)
                //        {
                //                xel = new XElement("template");
                //                xdoc.Root.Add(xel);

                //                xel.Add(new XAttribute("te_id", lo.teId));
                //                xel.Add(new XAttribute("nameHeShe", lo.heShe.Replace("\"", "''")));
                //                xel.Add(new XAttribute("nameThey", lo.they.Replace("\"", "''")));
                //                xel.Add(new XAttribute("translHeShe", "+"));
                //                xel.Add(new XAttribute("translThey", "+"));
                //        }
                //        else
                //        {
                //                xel.Attribute("orig_name").SetValue(lo.nameForMap.Replace("\"", "''"));
                //        }
                //}





                foreach (var lo in roomOfId.Values)
                {


                    var xel = xdoc.Root.Elements("room").Where(el => el.Attribute("room_id").Value == lo.roomId).FirstOrDefault();

                    if (xel == null)
                    {
                        xel = new XElement("room");
                        xdoc.Root.Add(xel);

                        xel.Add(new XAttribute("room_id", lo.roomId));
                        xel.Add(new XAttribute("orig_name", lo.nameForMap.Replace("\"", "''")));
                        xel.Add(new XAttribute("transl", "+"));
                    }
                    else
                    {
                        xel.Attribute("orig_name").SetValue(lo.nameForMap.Replace("\"", "''"));
                    }
                }






                foreach (var lo in roomOfId.Values)
                {

                    if (lo.whatToSayWhenEnteringRoom != null)
                    {
                        var xel = xdoc.Root.Elements("room_enter").Where(el => el.Attribute("room_id").Value == lo.roomId).FirstOrDefault();

                        if (xel == null)
                        {
                            xel = new XElement("room_enter");
                            xdoc.Root.Add(xel);

                            xel.Add(new XAttribute("room_id", lo.roomId));
                            xel.Add(new XAttribute("orig_name", lo.whatToSayWhenEnteringRoom.Replace("\"", "''")));
                            xel.Add(new XAttribute("transl", "+"));
                        }
                        else
                        {
                            xel.Attribute("orig_name").SetValue(lo.whatToSayWhenEnteringRoom.Replace("\"", "''"));
                        }
                    }
                }










                addGenericElIfMissing(xdoc, "walk");



                addGenericElIfMissing(xdoc, "walk_to");







                addGenericElIfMissing(xdoc, "in_order_to");



                addGenericElIfMissing(xdoc, "here_you_see");
                addGenericElIfMissing(xdoc, "your_objects");
                addGenericElIfMissing(xdoc, "objects_seen_somewhere");
                addGenericElIfMissing(xdoc, "possible_actions");
                addGenericElIfMissing(xdoc, "your_objectives");
                addGenericElIfMissing(xdoc, "are_you_stuck");
                addGenericElIfMissing(xdoc, "other");
                addGenericElIfMissing(xdoc, "options");
                addGenericElIfMissing(xdoc, "nothing_special");
                addGenericElIfMissing(xdoc, "back");
                addGenericElIfMissing(xdoc, "cancel");
                addGenericElIfMissing(xdoc, "reread_clues");
                addGenericElIfMissing(xdoc, "press_to_continue");
                addGenericElIfMissing(xdoc, "you_dont_see_how_this_can_help");









                xdoc.Save(fullpath);
            }
        }

        private static void addGenericElIfMissing(XDocument xdoc, string elname)
        {
            var xelInOrderTo = xdoc.Root.Element(elname);
            if (xelInOrderTo == null)
            {
                xelInOrderTo = new XElement(elname);
                xelInOrderTo.Add(new XAttribute("transl", "+"));
                xdoc.Root.Add(xelInOrderTo);
            }
        }






        public abstract void beforeActionExecuted(LogicObj lo, Objective obj, Room ro, out bool cancel);

        public abstract string imgNotAvailable();

        //public abstract void unaryVerbFailedOnObject(UnVerb unVerb, LogicObj lo);

        public abstract void rememberFailedOnObject(LogicObj lo);
    }


}
