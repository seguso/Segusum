using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Transactions;
using Microsoft.AspNetCore.Mvc;
using System.Xml;
using System.Xml.Linq;
#pragma warning disable 219

namespace Seg
{
    public abstract class ApiBase : ControllerBase
    {

        private static readonly AsyncLocal<bool> currentTutorialMode = new();

        protected virtual WorldBase buildEmptyWorld(string lang, bool tutorialMode) => buildEmptyWorld(lang);

        private static string scenarioSaveTitle(string title, bool tutorialMode) =>
            tutorialMode ? "__tutorial__" + title : title;

        private static string displayScenarioSaveTitle(string title, bool tutorialMode) =>
            tutorialMode && title.StartsWith("__tutorial__")
                ? title.Substring("__tutorial__".Length)
                : title;

        public abstract WorldBase buildEmptyWorld(string lang);

        //protected IActionResult unaryActionImpl([FromBody] UnaryActionInput i)
        //{
        //    try
        //    {

        //        var db = new segusumDb();
        //        var user = auth(i, db);
        //        if (user == null)
        //            return Ok(new ReturnVal { errore = "noauth" });

        //        worldE w = restoreWorldFromMemoryOrDisk(user.id, db);


        //        var lo = w.loOfloId[i.loId];

        //        var actionRes = eng.executeUnaryAction(lo, w);


        //        // salvataggio automatico! non deve poter tornare indietro caricando un salvataggio
        //        autosave(db, user, w);


        //        return Ok(new ReturnVal
        //        {
        //            ret = actionRes
        //        });
        //    }
        //    catch (Exception e)
        //    {
        //        return Ok(new ReturnVal { errore = UtilsW.stringOfException(e) });
        //    }
        //}


        public IActionResult replay_cut_scene_impl([FromBody] ReplayCutSceneInput i)
        {
            try
            {
                var db = new segusumDb();
                var user = auth(i, db, out bool isTextMode);
                if (user == null)
                {
                    return Ok(new ApiReturnVal { errore = "noauth" });
                }

                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid, out string[] saveNames, i.lang, isTextMode);

                if (i.curTime != w.cur_time)
                {
                    // l'utente sta cercando di giocare su una finestra che è rimasta indietro come tempo... deve fare take over
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(i.curTime /* gli dico il tempo sbagliato non quello giusto, se nola volta dopo dice di avere quello*/)
                        {
                            ar_oldSessionMustTakeOver = true
                        }
                    });
                }
                else if (savegameInvalid)
                {
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(w.cur_time)
                        {
                            savegame_invalid = true
                        }
                    });
                }



                w.CurLang = i.lang;

                var xdocObj = w.getXdocObjIndexedCached();
                var ret = eng.replay_cut_scene(i.cut_scene_title, w, xdocObj, saveNames, isTextMode);



                // riapro tutti gli obiettivi bloccati
                foreach (var ob in w.objectiveOfId.Values)
                {
                    ob.how_many_times_tried = 0;
                }


                // salvataggio automatico! non deve poter tornare indietro caricando un salvataggio
                autosave(db, user, w);







                return Ok(new ApiReturnVal
                {
                    ret = ret
                });

            }
            catch (Exception e)
            {
                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
            }
        }

        public IActionResult lookPickupImpl([FromBody] LookPickupRememberInput i, bool ispickup, bool isUseHere, bool isLook, bool isRemember)
        {
            try
            {
                var db = new segusumDb();
                var user = auth(i, db, out bool isTextMode);
                if (user == null)
                {
                    return Ok(new ApiReturnVal { errore = "noauth" });
                }

                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid, out string[] saveNames, i.lang, isTextMode);
                if (i.curTime != w.cur_time)
                {
                    // l'utente sta cercando di giocare su una finestra che è rimasta indietro come tempo... deve fare take over
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(i.curTime /* gli dico il tempo sbagliato non quello giusto, se nola volta dopo dice di avere quello*/)
                        {
                            ar_oldSessionMustTakeOver = true
                        }
                    });
                }
                else if (savegameInvalid)
                {
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(w.cur_time)
                        {
                            savegame_invalid = true
                        }
                    });
                }

                w.CurLang = i.lang;

                var xdocObj = w.getXdocObjIndexedCached();


                SegActionRes ret;

                if (ispickup)
                {


                    var lo = w.loOfId[i.lo_id];


                    ret = eng.executeActionPickup(lo, w, saveNames, xdocObj, isTextMode);
                }
                else if (isUseHere)
                {
                    var lo = w.loOfId[i.lo_id];


                    ret = eng.executeActionUseHere(lo, w, saveNames, xdocObj, isTextMode);
                }
                else if (isLook)
                {
                    ret = eng.executeActionLook(i.lo_id, w, xdocObj, saveNames, isTextMode);
                }
                else
                {
                    //is remember
                    ret = eng.executeActionRemember(i.lo_id, w, xdocObj, saveNames, isTextMode);
                }




                // salvataggio automatico! non deve poter tornare indietro caricando un salvataggio
                autosave(db, user, w);







                return Ok(new ApiReturnVal
                {
                    ret = ret
                });

            }
            catch (Exception e)
            {
                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
            }
        }




        public IActionResult getNextHintImpl([FromBody] GetNextHintInput i)
        {
            try
            {
                var db = new segusumDb();
                var user = auth(i, db, out bool isTextMode);
                if (user == null)
                {
                    return Ok(new ApiReturnVal { errore = "noauth" });
                }

                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid, out string[] saveNames, i.lang, isTextMode);


                if (i.curTime != w.cur_time)
                {
                    // l'utente sta cercando di giocare su una finestra che è rimasta indietro come tempo... deve fare take over
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(i.curTime /* gli dico il tempo sbagliato non quello giusto, se nola volta dopo dice di avere quello*/)
                        {
                            ar_oldSessionMustTakeOver = true
                        }
                    });
                }
                else if (savegameInvalid)
                {
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(w.cur_time)
                        {
                            savegame_invalid = true
                        }
                    });
                }

                w.CurLang = i.lang;


                // todo salva pastActionHint



                var cs = new CutScene(canBeSkipped: false);

                w.setCurrentCs(cs);



                if (!w.objectiveOfId.ContainsKey(i.gnh_objId))
                {
                    // ho rimosso l'obiettivo penso. forse non succ mai
                    w.narText("Non ci sono suggerimenti per questo enigma.".translatable());

                }
                else
                {
                    var obj = w.objectiveOfId[i.gnh_objId];

                    w.pastActions.Add(new PastActionAskForHint { dateTime = DateTime.Now, pu = obj });

                    var xdi = w.getXdocObjIndexedCached();

                    var hintsForThisObjective = w.hints.Where(h => h.ob == obj).SingleOrDefault()?.hints;

                    if (hintsForThisObjective == null || hintsForThisObjective.isEmpty())
                    {
                        w.narText(w.translateDialogOrNarOrAnnotated("Non ci sono suggerimenti per questo enigma.".translatable(), xdi));
                    }
                    else
                    {
                        var nextUnseen = hintsForThisObjective

                                .Where(x => x.OnlyShowIf == null || x.OnlyShowIf() == true) // filtra quelli che ora non hanno senso

                                .Where(x => w.neverSeen(x.id)).FirstOrDefault();

                        if (nextUnseen == null)
                        {
                            w.narText(w.translateDialogOrNarOrAnnotated("Hai visto tutti i suggerimenti per questo enigma.".translatable(), xdi));
                        }
                        else
                        {


                            var now = DateTime.Now;
                            var elementiETempi = hintsForThisObjective
                                            .Where(h => /*w.seenAtLeastOnce(h.id)  && */ w.lastTimeElementExecuted.ContainsKey(h.id))
                                            .Select(h => new { h, tempoPassato = now.Subtract(w.lastTimeElementExecuted[h.id]) })
                                            .ToList();

                            bool passaCheckTempo;
                            if (nextUnseen.minutesToWait != null)
                            {


                                passaCheckTempo = elementiETempi.All(te => te.tempoPassato.TotalMinutes > nextUnseen.minutesToWait);

                            }
                            else
                            {
                                passaCheckTempo = true;

                            }

                            if (!passaCheckTempo)
                            {
                                var elementoPiuVicino = elementiETempi.OrderBy(x => x.tempoPassato).First();
                                var minutiDaAspettare = nextUnseen.minutesToWait.Value - elementoPiuVicino.tempoPassato.TotalMinutes;
                                int secondiDaAspettare = (int)(minutiDaAspettare * 60);
                                var timespanDaAspettare = new TimeSpan(0, 0, secondiDaAspettare);
                                var str = w.translateDialogOrNarOrAnnotated("Devi aspettare ancora {1} minuti e {2} secondi per il prossimo suggerimento.".translatable(), xdi);
                                str = str.inst(timespanDaAspettare.Minutes.ToString()).inst(timespanDaAspettare.Seconds.ToString());
                                w.narText(str);
                            }
                            else
                            {
                                // vediamo se può vedere il prossimo hint
                                var hc = nextUnseen.f();

                                if (hc.canContinue)
                                {
                                    // sto per mostrare la frase di successo.
                                    // ricorda che adesso è stata vista
                                    w.rememberYouHaveJustSeenCycleElement(nextUnseen.id);
                                }
                                else
                                {
                                    // sto mostrando la frase di fallimento, quindi non devo marcare visto l'hint.
                                }

                                // mostra la frase di successo o fallimento
                                foreach (var htmlTokenStr in hc.htmls)
                                {
                                    w.narText(htmlTokenStr);

                                }

                            }


                            //{
                            //        var now = DateTime.Now;
                            //        var elementiETempi = hintsForThisObjective
                            //                        .Where(h => /*w.seenAtLeastOnce(h.id)  && */ w.lastTimeElementExecuted.ContainsKey(h.id))
                            //                        .Select(h => new { h, tempoPassato = now.Subtract(w.lastTimeElementExecuted[h.id]) })
                            //                        .ToList();


                            //        bool passaCheckTempo;
                            //        if (nextUnseen.minutesToWait != null)
                            //        {


                            //                passaCheckTempo = elementiETempi.All(te => te.tempoPassato.TotalMinutes > nextUnseen.minutesToWait);

                            //        }
                            //        else
                            //        {
                            //                passaCheckTempo = true;

                            //        }

                            //        if (!passaCheckTempo)
                            //        {
                            //                var elementoPiuVicino = elementiETempi.OrderBy(x => x.tempoPassato).First();
                            //                var minutiDaAspettare = nextUnseen.minutesToWait.Value - elementoPiuVicino.tempoPassato.TotalMinutes;
                            //                int secondiDaAspettare = (int)(minutiDaAspettare * 60);
                            //                var timespanDaAspettare = new TimeSpan(0, 0, secondiDaAspettare);
                            //                var str = "Devi aspettare ancora {1} minuti e {2} secondi per il prossimo suggerimento.".translatable();
                            //                str = str.inst(timespanDaAspettare.Minutes.ToString()).inst(timespanDaAspettare.Seconds.ToString());
                            //                w.narText(str);
                            //        }
                            //        else
                            //        {
                            //                // ricorda che adesso è stata vista
                            //                w.rememberYouHaveJustSeenCycleElement(nextUnseen.id);





                            //                // qui crea la cutscene a partire dall'handler
                            //                foreach (var htmlTokenStr in nextUnseen.htmls)
                            //                {
                            //                        w.narText(htmlTokenStr);

                            //                }
                            //        }
                            //}

                        }
                    }
                }

                w.clearCurrentCs();



                w.gs = new GameStateCutScene
                (
                        cs: cs,
                        iCurToken: 0,
                        afterCutSceneShowDialog: null,
                        afterCutSceneWaitForTextInput: null
                        , afterCutSceneGameFinished: null

                );





                // salvataggio automatico! ricorda che hai visto quell'hint e quando. e il nuovo gamestate
                autosave(db, user, w);







                return Ok(new ApiReturnVal
                {
                    ret = new SegActionRes(w.cur_time)
                    {
                        nextCutSceneToken = new CutSceneTokenWithTitle { cutSceneToken = cs.First() },
                    }
                });

            }
            catch (Exception e)
            {
                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
            }
        }


        public class HintVistoClient
        {
            public string[] hvcPieces { get; set; }
        }

        public class ObiettivoEHintClient
        {
            public string ohcObjSerId { get; set; }

            public HintVistoClient[] ohcHintsSeen { get; set; }
        }

        public IActionResult getCurrentHintsImpl([FromBody] Credentials i)
        {
            try
            {
                var db = new segusumDb();
                var user = auth(i, db, out bool isTextMode);
                if (user == null)
                {
                    return Ok(new ApiReturnVal { errore = "noauth" });
                }

                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid, out string[] saveNames, i.lang, isTextMode);


                if (i.curTime != w.cur_time)
                {
                    // l'utente sta cercando di giocare su una finestra che è rimasta indietro come tempo... deve fare take over
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(i.curTime /* gli dico il tempo sbagliato non quello giusto, se nola volta dopo dice di avere quello*/)
                        {
                            ar_oldSessionMustTakeOver = true
                        }
                    });
                }
                else if (savegameInvalid)
                {
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(w.cur_time)
                        {
                            savegame_invalid = true
                        }
                    });
                }

                w.CurLang = i.lang;

                var xdi = w.getXdocObjIndexedCached();

                var hintVisti = w.curObjectives.Select(co =>

                {

                    var hintVistiDiQuellObiettivo = w.hints.SingleOrDefault(x => x.ob == co)?.hints

                                            .Where(x => w.wasSeenAtLeastOnce(x.id))
                                            .Select(h =>
                                            {

                                    var xx = h.f();
                                    return new HintVistoClient { hvcPieces = xx.htmls.Select(ht => w.translateDialogOrNarOrAnnotated(ht, xdi)).ToArray() };
                                })
                                            .ToArray();

                    if (hintVistiDiQuellObiettivo == null)
                    {
                        return null;
                    }
                    else
                    {
                        return new ObiettivoEHintClient
                        {
                            ohcObjSerId = co.serId

                                           ,
                            ohcHintsSeen = hintVistiDiQuellObiettivo
                        };
                    }
                })
                .SelectSome()
                .ToArray();







                //w.gs = new GameStateCutScene
                //{
                //        cs = cs,
                //        iCurToken = 0,
                //        afterCutSceneShowDialog = null,
                //        afterCutSceneWaitForTextInput = null

                //};





                //// salvataggio automatico! ricorda che hai visto quell'hint e quando. e il nuovo gamestate
                //autosave(db, user, w);




                var dic = hintVisti.ToDictionary(x => x.ohcObjSerId);


                return Ok(new ApiReturnVal
                {
                    ret = dic
                });

            }
            catch (Exception e)
            {
                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
            }
        }




        public IActionResult talkActionImpl([FromBody] AskQuestionInput i)
        {
            try
            {
                var db = new segusumDb();
                var user = auth(i, db, out bool isTextMode);
                if (user == null)
                {
                    return Ok(new ApiReturnVal { errore = "noauth" });
                }

                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid, out string[] saveNames, i.lang, isTextMode);


                if (i.curTime != w.cur_time)
                {
                    // l'utente sta cercando di giocare su una finestra che è rimasta indietro come tempo... deve fare take over
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(i.curTime /* gli dico il tempo sbagliato non quello giusto, se nola volta dopo dice di avere quello*/)
                        {
                            ar_oldSessionMustTakeOver = true
                        }
                    });
                }
                else if (savegameInvalid)
                {
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(w.cur_time)
                        {
                            savegame_invalid = true
                        }
                    });
                }

                w.CurLang = i.lang;




                var gst = (GameStateShowingQuestions)w.gs;
                var curDialog = gst.dialog;


                var question = gst.dialog.questions.Single(q => q.id == i.questionId);
                var ret = eng.askQuestion(w, curDialog, question, saveNames, isTextMode);




                // salvataggio automatico! non deve poter tornare indietro caricando un salvataggio
                autosave(db, user, w);







                return Ok(new ApiReturnVal
                {
                    ret = "ok_objectives"
                });

            }
            catch (Exception e)
            {
                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
            }
        }

        public IActionResult objectivesSeenImpl([FromBody] ObjectivesSeenInput i)
        {
            try
            {
                var db = new segusumDb();
                var user = auth(i, db, out bool isTextMode);
                if (user == null)
                {
                    return Ok(new ApiReturnVal { errore = "noauth" });
                }

                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid, out string[] saveNames, i.lang, isTextMode);


                if (i.curTime != w.cur_time)
                {
                    // l'utente sta cercando di giocare su una finestra che è rimasta indietro come tempo... deve fare take over
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(i.curTime /* gli dico il tempo sbagliato non quello giusto, se nola volta dopo dice di avere quello*/)
                        {
                            ar_oldSessionMustTakeOver = true
                        }
                    });
                }
                else if (savegameInvalid)
                {
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(w.cur_time)
                        {
                            savegame_invalid = true
                        }
                    });
                }

                w.CurLang = i.lang;




                foreach (var oid in i.osiObjectivesSeen)
                {
                    if (w.objectiveOfId.ContainsKey(oid))
                    {
                        var ob = w.objectiveOfId[oid];
                        ob.howManyTimesSeen++;

                    }
                }



                // salvataggio automatico! non deve poter tornare indietro caricando un salvataggio
                autosave(db, user, w);







                return Ok(new ApiReturnVal
                {
                    ret = "ok"
                });

            }
            catch (Exception e)
            {
                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
            }
        }


        //public IActionResult registerImpl([FromBody] InputAssociateUserPwdWithToken i)
        //{
        //        try
        //        {

        //                if (string.IsNullOrWhiteSpace(i.uname) || string.IsNullOrWhiteSpace(i.pwd) /*|| string.IsNullOrWhiteSpace(i.token)*/)
        //                {
        //                        return Ok(new ApiReturnVal { errore = "invalid-credentials-null" });
        //                }

        //                if (i.pwd != i.pwd2)
        //                {
        //                        return Ok(new ApiReturnVal { errore = "passwords-not-equal" });
        //                }

        //                using (var tr = new TransactionScope())
        //                {

        //                        var db = new segusumDb();
        //                        var user = (from u in db.user
        //                                    where u.uname.ToLower().Trim() == i.uname.ToLower().Trim()
        //                                    select u).FirstOrDefault();

        //                        if (user != null)
        //                        {
        //                                return Ok(new ApiReturnVal { errore = "username-already-taken" });
        //                        }

        //                        var newu = new user
        //                        {

        //                        }

        //                        //if (user.uname.is_not_null_or_white() || user.pwd.is_not_null_or_white())
        //                        //{
        //                        //        return Ok(new ApiReturnVal { errore = "user-id-and-pwd-already-set" });
        //                        //}



        //                        //var userWithSameName = (from u in db.user
        //                        //                        where u.uname == i.uname
        //                        //                        select u).Any();
        //                        //if (userWithSameName)
        //                        //{
        //                        //        return Ok(new ApiReturnVal { errore = "username-already-taken" });
        //                        //}
        //                        //else
        //                        {

        //                                user.uname = i.uname;
        //                                user.pwd = i.pwd;

        //                                db.SaveChanges();


        //                        }

        //                        tr.Complete();
        //                }

        //                return Ok(new ApiReturnVal
        //                {
        //                        ret = "ok",
        //                });
        //        }
        //        // todo vedi che eccezione arriva se inserisco nome utente che esiste gia
        //        catch (Exception e)
        //        {
        //                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
        //        }
        //}


        public IActionResult loadGameImpl([FromBody] Credentials i)
        {
            try
            {

                var db = new segusumDb();
                var user = auth(i, db, out bool isTextMode);
                if (user == null)
                {
                    return Ok(new ApiReturnVal { errore = "noauth" });
                }

                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid, out string[] saveNames, i.lang, isTextMode);
                // loadgame fa eccezione! non deve controllare che il time sia diverso! 
                //if (i.curTime != w.cur_time)
                //{
                //        // l'utente sta cercando di giocare su una finestra che è rimasta indietro come tempo... deve fare take over
                //        return Ok(new ApiReturnVal
                //        {
                //                ret = new SegActionRes(w.cur_time)
                //                {
                //                        ar_oldSessionMustTakeOver = true
                //                }
                //        });
                //}
                //else
                if (savegameInvalid)
                {
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(w?.cur_time ?? 0 /* w è null se savegame invalid*/)
                        {
                            savegame_invalid = true
                        }
                    });
                }

                w.CurLang = i.lang;

                // ora non sappiamo in che stato è il gioco: cut scene, show questions, o vieweing room.

                SegActionRes ret;

                if (w.gs is GameStateViewingRoom)
                {
                    w.beforeRoomChangeManualAndAutoSetRoomAspects(w.curRoom); // risetto gli aspect prima di darli al client. necessario se no restano gli aspect dell'ultimo dialogo

                    var roomDesc = w.getRoomDescForClient(saveNames, isTextMode);

                    ret = new SegActionRes(w.cur_time)
                    {
                        room = roomDesc,

                    };

                }
                else if (w.gs is GameStateCutScene gscEmpty && (gscEmpty.cs == null || gscEmpty.cs.Count == 0))
                {
                    // Alcuni mondi costruiscono la cutscene iniziale soltanto
                    // durante la richiesta di avvio. Se un autosave viene
                    // ricaricato dopo che i token non sono più ricostruibili,
                    // non possiamo indicizzare una lista vuota: la room è lo
                    // stato sicuro e coerente da mostrare al client.
                    w.gs = new GameStateViewingRoom();
                    w.beforeRoomChangeManualAndAutoSetRoomAspects(w.curRoom);
                    ret = new SegActionRes(w.cur_time)
                    {
                        room = w.getRoomDescForClient(saveNames, isTextMode)
                    };
                }
                else if (w.gs is GameStateCutScene gscCurrent)
                {

                    // devo mandare la cutscene attuale, senza avanzare il passo come nell'altro metodo 

                    var tokenIndex = Math.Clamp(gscCurrent.iCurToken, 0, gscCurrent.cs.Count - 1);
                    gscCurrent.iCurToken = tokenIndex;
                    ret = new SegActionRes(w.cur_time)
                        {
                        nextCutSceneToken = new CutSceneTokenWithTitle
                        {
                            actionReadable = null,
                            cutSceneToken = gscCurrent.cs[tokenIndex],
                        }

                            ,
                        room = w.getRoomDescForClient(saveNames, isTextMode)
                    };
                }
                else if (w.gs is GameStateShowingQuestions gsq)
                {

                    ret = eng.startDialogOrAskFirstQuestion(w, gsq.dialog, saveNames, isTextMode);

                }
                else if (w.gs is GameStateWaitingForText gswt)
                {

                    ret = eng.startTextInput(w, gswt.textInput);

                }
                else if (w.gs is GameStateFinished gsf)
                {

                    var untr = w.getEndGameData();

                    EndGameStuffClient tr = traduciEndGameStuff(w, untr);
                    ret = new SegActionRes(w.cur_time)
                    {
                        arEndGame = tr
                            ,
                        room = w.getRoomDescForClient(saveNames, isTextMode) // per i salvataggi a gioco finioto
                    };
                }
                else
                {
                    throw new Exception("game state unhandled in load vfkjsw9u92");
                }

                return Ok(new ApiReturnVal
                {
                    ret = ret
                });
            }
            catch (Exception e)
            {
                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
            }
        }

        public static EndGameStuffClient traduciEndGameStuff(WorldBase w, EndGameStuffClient untr)
        {
            var xdi = w.getXdocObjIndexedCached();
            var tr = new EndGameStuffClient(
                    untr.egsImg
                    , untr.egsCredits.Select(x =>

                                                            w.translateDialogOrNarOrAnnotated(x, xdi)).ToArray()
                    );
            return tr;
        }

        public IActionResult getNextArImpl([FromBody] Credentials i)
        {
            try
            {
                var phaseStopwatch = Stopwatch.StartNew();

                var db = new segusumDb();
                var user = auth(i, db, out bool isTextMode);
                phaseStopwatch.Stop();
                SegusumProfiler.Log($"getNextAr phase=auth elapsed_ms={phaseStopwatch.Elapsed.TotalMilliseconds:F1}");
                if (user == null)
                {
                    return Ok(new ApiReturnVal { errore = "noauth" });
                }

                phaseStopwatch.Restart();
                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid, out string[] saveNames, i.lang, isTextMode);
                phaseStopwatch.Stop();
                SegusumProfiler.Log($"getNextAr phase=restore elapsed_ms={phaseStopwatch.Elapsed.TotalMilliseconds:F1}");
                if (i.curTime != w.cur_time)
                {
                    // l'utente sta cercando di giocare su una finestra che è rimasta indietro come tempo... deve fare take over
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(i.curTime /* gli dico il tempo sbagliato non quello giusto, se nola volta dopo dice di avere quello*/)
                        {
                            ar_oldSessionMustTakeOver = true
                        }
                    });
                }
                else if (savegameInvalid)
                {
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(w.cur_time)
                        {
                            savegame_invalid = true
                        }
                    });
                }

                w.CurLang = i.lang;




                var gsCs = (GameStateCutScene)w.gs;

                if (gsCs.iCurToken < gsCs.cs.Count - 1)
                {
                    gsCs.iCurToken++;



                    // salvataggio automatico! altrimenti se esco durante la cut scene iniziale, arrivo in uno stato inconsistente: esiste un utente nel db senza savegame.
                    phaseStopwatch.Restart();
                    autosaveCutScenePosition(db, user, w);
                    phaseStopwatch.Stop();
                    SegusumProfiler.Log($"getNextAr phase=cutscene_position elapsed_ms={phaseStopwatch.Elapsed.TotalMilliseconds:F1}");







                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(w.cur_time)
                        {
                            nextCutSceneToken = new CutSceneTokenWithTitle { actionReadable = null, cutSceneToken = gsCs.cs[gsCs.iCurToken] },
                        }
                    });
                }
                else
                {


                    // la cut scene è finita. vedi cosa devo fare dopo:


                    phaseStopwatch.Restart();
                    var ret = eng.calcolaActionResTalkORoom(w, gsCs.afterCutSceneShowDialog, gsCs.afterCutSceneWaitForTextInput,
                            gsCs.afterCutSceneGameFinished,
                            saveNames, isTextMode);
                    phaseStopwatch.Stop();
                    SegusumProfiler.Log($"getNextAr phase=calculate_room elapsed_ms={phaseStopwatch.Elapsed.TotalMilliseconds:F1}");







                    // salvataggio automatico! altrimenti se esco durante la cut scene iniziale, arrivo in uno stato inconsistente: esiste un utente nel db senza savegame.
                    autosaveInBackground(user.uname, w);






                    return Ok(new ApiReturnVal { ret = ret });

                }

            }
            catch (Exception e)
            {
                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
            }
        }


        public IActionResult getPreviousCutSceneElementImpl([FromBody] Credentials i)
        {
            try
            {

                var db = new segusumDb();
                var user = auth(i, db, out bool isTextMode);
                if (user == null)
                {
                    return Ok(new ApiReturnVal { errore = "noauth" });
                }

                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid, out string[] saveNames, i.lang, isTextMode);
                if (i.curTime != w.cur_time)
                {
                    // l'utente sta cercando di giocare su una finestra che è rimasta indietro come tempo... deve fare take over
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(i.curTime /* gli dico il tempo sbagliato non quello giusto, se nola volta dopo dice di avere quello*/)
                        {
                            ar_oldSessionMustTakeOver = true
                        }
                    });
                }
                else if (savegameInvalid)
                {
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(w.cur_time)
                        {
                            savegame_invalid = true
                        }
                    });
                }

                w.CurLang = i.lang;




                var gsCs = (GameStateCutScene)w.gs;

                if (gsCs.iCurToken > 0)
                {
                    gsCs.iCurToken--;



                    // salvataggio automatico! altrimenti se esco durante la cut scene iniziale, arrivo in uno stato inconsistente: esiste un utente nel db senza savegame.
                    autosaveCutScenePosition(db, user, w);







                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(w.cur_time)
                        {
                            nextCutSceneToken = new CutSceneTokenWithTitle { actionReadable = null, cutSceneToken = gsCs.cs[gsCs.iCurToken] },
                        }
                    });
                }
                else
                {

                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(w.cur_time)
                        {
                            nextCutSceneToken = new CutSceneTokenWithTitle { actionReadable = null, cutSceneToken = gsCs.cs[gsCs.iCurToken] },
                            errorCannotGoBackInCutsceneAlreadyBeginning = true
                        }
                    });

                }

            }
            catch (Exception e)
            {
                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
            }
        }




        public IActionResult skipToEndOfCutSceneImpl([FromBody] Credentials i)
        {
            try
            {

                var db = new segusumDb();
                var user = auth(i, db, out bool isTextMode);
                if (user == null)
                {
                    return Ok(new ApiReturnVal { errore = "noauth" });
                }

                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid, out string[] saveNames, i.lang, isTextMode);
                if (i.curTime != w.cur_time)
                {
                    // l'utente sta cercando di giocare su una finestra che è rimasta indietro come tempo... deve fare take over
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(i.curTime /* gli dico il tempo sbagliato non quello giusto, se nola volta dopo dice di avere quello*/)
                        {
                            ar_oldSessionMustTakeOver = true
                        }
                    });
                }
                else if (savegameInvalid)
                {
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(w.cur_time)
                        {
                            savegame_invalid = true
                        }
                    });
                }

                w.CurLang = i.lang;




                var gsCs = (GameStateCutScene)w.gs;



                // la cut scene è finita. vedi cosa devo fare dopo:


                var ret = eng.calcolaActionResTalkORoom(w, gsCs.afterCutSceneShowDialog, gsCs.afterCutSceneWaitForTextInput

                        , gsCs.afterCutSceneGameFinished
                        , saveNames, isTextMode);



                // salvataggio automatico! altrimenti se esco durante la cut scene iniziale, arrivo in uno stato inconsistente: esiste un utente nel db senza savegame.
                autosaveInBackground(user.uname, w);



                return Ok(new ApiReturnVal { ret = ret });


            }
            catch (Exception e)
            {
                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
            }
        }



        //public IActionResult waitOneTurnImpl([FromBody] credentials i)
        //{
        //    try
        //    {


        //        var db = new segusumDb();
        //        var user = auth(i, db);
        //        if (user == null)
        //            return Ok(new ApiReturnVal { errore = "noauth" });

        //        world_base w = restoreWorldFromMemoryOrDisk(user.id, db);


        //        var actionRes = eng.waitOneTurn(w);


        //        // salvataggio automatico! non deve poter tornare indietro caricando un salvataggio
        //        autosave(db, user, w);


        //        return Ok(new ApiReturnVal
        //        {
        //            ret = actionRes,
        //        });
        //    }
        //    catch (Exception e)
        //    {
        //        return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
        //    }
        //}


        //public IActionResult zeroVerbImpl([FromBody] UnActionInput i)
        //{
        //        try
        //        {

        //                var db = new segusumDb();
        //                var user = auth(i, db);
        //                if (user == null)
        //                        return Ok(new ApiReturnVal { errore = "noauth" });

        //                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid, out string[] saveNames, i.lang);
        //                if (i.curTime != w.cur_time)
        //                {
        //                        // l'utente sta cercando di giocare su una finestra che è rimasta indietro come tempo... deve fare take over
        //                        return Ok(new ApiReturnVal
        //                        {
        //                                ret = new SegActionRes(i.curTime /* gli dico il tempo sbagliato non quello giusto, se nola volta dopo dice di avere quello*/)
        //                                {
        //                                        ar_oldSessionMustTakeOver = true
        //                                }
        //                        });
        //                }
        //                else if (savegameInvalid)
        //                {
        //                        return Ok(new ApiReturnVal
        //                        {
        //                                ret = new SegActionRes(w.cur_time)
        //                                {
        //                                        savegame_invalid = true
        //                                }
        //                        });
        //                }

        //                w.curLang = i.lang;



        //                // esegui azione


        //                var unVerb = w.zeroVerbOfId[i.uaiZeroVerbId];


        //                var actionRes = eng.executeZeroVerb(unVerb, w, saveNames);





        //                // salvataggio automatico! non deve poter tornare indietro caricando un salvataggio
        //                autosave(db, user, w);






        //                return Ok(new ApiReturnVal
        //                {
        //                        ret = actionRes
        //                });
        //        }
        //        catch (Exception e)
        //        {
        //                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
        //        }
        //}


        public IActionResult talkHereImpl([FromBody] Credentials i)
        {
            try
            {

                var db = new segusumDb();
                var user = auth(i, db, out bool isTextMode);
                if (user == null)
                {
                    return Ok(new ApiReturnVal { errore = "noauth" });
                }

                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid, out string[] saveNames, i.lang, isTextMode);
                if (i.curTime != w.cur_time)
                {
                    // l'utente sta cercando di giocare su una finestra che è rimasta indietro come tempo... deve fare take over
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(i.curTime /* gli dico il tempo sbagliato non quello giusto, se nola volta dopo dice di avere quello*/)
                        {
                            ar_oldSessionMustTakeOver = true
                        }
                    });
                }
                else if (savegameInvalid)
                {
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(w.cur_time)
                        {
                            savegame_invalid = true
                        }
                    });
                }

                w.CurLang = i.lang;



                // esegui azione





                var actionRes = eng.executeTalkHere(w, saveNames, isTextMode);





                // salvataggio automatico! non deve poter tornare indietro caricando un salvataggio
                autosave(db, user, w);






                return Ok(new ApiReturnVal
                {
                    ret = actionRes
                });
            }
            catch (Exception e)
            {
                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
            }
        }


        public IActionResult useWithImpl([FromBody] UseWithActionInput i)
        {
            try
            {

                var db = new segusumDb();
                var user = auth(i, db, out bool isTextMode);
                if (user == null)
                {
                    return Ok(new ApiReturnVal { errore = "noauth" });
                }

                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid, out string[] saveNames, i.lang, isTextMode);
                if (i.curTime != w.cur_time)
                {
                    // l'utente sta cercando di giocare su una finestra che è rimasta indietro come tempo... deve fare take over
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(i.curTime /* gli dico il tempo sbagliato non quello giusto, se nola volta dopo dice di avere quello*/)
                        {
                            ar_oldSessionMustTakeOver = true
                        }
                    });
                }
                else if (savegameInvalid)
                {
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(w.cur_time)
                        {
                            savegame_invalid = true
                        }
                    });
                }

                w.CurLang = i.lang;



                // esegui azione

                var xdocObj = w.getXdocObjIndexedCached();

                var lo1 = w.loOfId[i.uwaLoId1];
                var lo2 = w.loOfId[i.uwaLoId2];

                Explanation explanation;
                if (i.uwaExplanationId.isNullOrWhite())
                {
                    explanation = null;
                }
                else
                {
                    explanation = w.getAllExplanations().Single(ex => ex.expId == i.uwaExplanationId);
                }

                var actionRes = eng.executeActionUseWith(lo1, lo2, explanation, i.uwaAlreadyKnowItFails, w, saveNames, xdocObj, isTextMode);





                // salvataggio automatico! non deve poter tornare indietro caricando un salvataggio
                autosave(db, user, w);






                return Ok(new ApiReturnVal
                {
                    ret = actionRes
                });
            }
            catch (Exception e)
            {
                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
            }
        }


        public IActionResult useForImpl([FromBody] UseForInput i)
        {
            try
            {

                var db = new segusumDb();
                var user = auth(i, db, out bool isTextMode);
                if (user == null)
                {
                    return Ok(new ApiReturnVal { errore = "noauth" });
                }

                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid, out string[] saveNames, i.lang, isTextMode);
                if (i.curTime != w.cur_time)
                {
                    // l'utente sta cercando di giocare su una finestra che è rimasta indietro come tempo... deve fare take over
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(i.curTime /* gli dico il tempo sbagliato non quello giusto, se nola volta dopo dice di avere quello*/)
                        {
                            ar_oldSessionMustTakeOver = true
                        }
                    });
                }
                else if (savegameInvalid)
                {
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(w.cur_time)
                        {
                            savegame_invalid = true
                        }
                    });
                }

                w.CurLang = i.lang;



                // esegui azione

                var xdocObj = w.getXdocObjIndexedCached();

                var lo = w.loOfId[i.ufiLoId];
                var obj = w.objectiveOfId[i.ufiObjId];

                Explanation explanation;
                if (i.ufiExpId.isNullOrWhite())
                {
                    explanation = null;
                }
                else
                {
                    explanation = w.getAllExplanations().Single(ex => ex.expId == i.ufiExpId);
                }

                var actionRes = eng.executeActionUseFor(lo, obj, explanation, w, saveNames, xdocObj, isTextMode);





                // salvataggio automatico! non deve poter tornare indietro caricando un salvataggio
                autosave(db, user, w);






                return Ok(new ApiReturnVal
                {
                    ret = actionRes
                });
            }
            catch (Exception e)
            {
                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
            }
        }


        public IActionResult isActuallyImpl([FromBody] IsActuallyInput i)
        {
            try
            {

                var db = new segusumDb();
                var user = auth(i, db, out bool isTextMode);
                if (user == null)
                {
                    return Ok(new ApiReturnVal { errore = "noauth" });
                }

                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid, out string[] saveNames, i.lang, isTextMode);
                if (i.curTime != w.cur_time)
                {
                    // l'utente sta cercando di giocare su una finestra che è rimasta indietro come tempo... deve fare take over
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(i.curTime /* gli dico il tempo sbagliato non quello giusto, se nola volta dopo dice di avere quello*/)
                        {
                            ar_oldSessionMustTakeOver = true
                        }
                    });
                }
                else if (savegameInvalid)
                {
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(w.cur_time)
                        {
                            savegame_invalid = true
                        }
                    });
                }

                w.CurLang = i.lang;



                // esegui azione

                var xdocObj = w.getXdocObjIndexedCached();

                var lo = w.loOfId[i.iaLoId];
                var exp1 = w.getAllExplanationsWithCont().Where(ex => ex.expId == i.iaExp1Id).Single();

                var exp2 = exp1.Continuations.Where(ex => ex.expId == i.iaExp2Id).Single();


                var actionRes = eng.executeActionIsActually(lo, exp1, exp2, w, saveNames, xdocObj, isTextMode);





                // salvataggio automatico! non deve poter tornare indietro caricando un salvataggio
                autosave(db, user, w);






                return Ok(new ApiReturnVal
                {
                    ret = actionRes
                });
            }
            catch (Exception e)
            {
                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
            }
        }


        public IActionResult useInComposerImpl([FromBody] UseInComposerInput i)
        {
            try
            {

                var db = new segusumDb();
                var user = auth(i, db, out bool isTextMode);
                if (user == null)
                {
                    return Ok(new ApiReturnVal { errore = "noauth" });
                }

                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid, out string[] saveNames, i.lang, isTextMode);
                if (i.curTime != w.cur_time)
                {
                    // l'utente sta cercando di giocare su una finestra che è rimasta indietro come tempo... deve fare take over
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(i.curTime /* gli dico il tempo sbagliato non quello giusto, se nola volta dopo dice di avere quello*/)
                        {
                            ar_oldSessionMustTakeOver = true
                        }
                    });
                }
                else if (savegameInvalid)
                {
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(w.cur_time)
                        {
                            savegame_invalid = true
                        }
                    });
                }

                w.CurLang = i.lang;



                // esegui azione

                var xdocObj = w.getXdocObjIndexedCached();

                LogicObj lo;

                if (i.uwcLoId != null)
                {
                    lo = w.loOfId[i.uwcLoId];
                }
                else
                {
                    lo = null;
                }
                var te = w.templateOfId[i.uwcTemplateId];
                var fi1 = w.fillerOfId[i.uwcFillerId1];

                Filler fi2;

                if (i.uwcFillerId2.is_not_null_or_white())
                {
                    fi2 = w.fillerOfId[i.uwcFillerId2];
                }
                else
                {
                    fi2 = null;
                }



                var actionRes = eng.executeUseInComposerAction(lo, i.uwcPezzi, te, fi1, fi2, w, saveNames, xdocObj, isTextMode);





                // salvataggio automatico! non deve poter tornare indietro caricando un salvataggio
                autosave(db, user, w);






                return Ok(new ApiReturnVal
                {
                    ret = actionRes
                });
            }
            catch (Exception e)
            {
                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
            }
        }


        //public IActionResult autoSolvePuzzleImpl([FromBody] AutoSolvePuzzleInput i)
        //{
        //        try
        //        {

        //                var db = new segusumDb();
        //                var user = auth(i, db);
        //                if (user == null)
        //                        return Ok(new ApiReturnVal { errore = "noauth" });

        //                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid, out string[] saveNames, i.lang);
        //                if (i.curTime != w.cur_time)
        //                {
        //                        // l'utente sta cercando di giocare su una finestra che è rimasta indietro come tempo... deve fare take over
        //                        return Ok(new ApiReturnVal
        //                        {
        //                                ret = new SegActionRes(i.curTime /* gli dico il tempo sbagliato non quello giusto, se nola volta dopo dice di avere quello*/)
        //                                {
        //                                        ar_oldSessionMustTakeOver = true
        //                                }
        //                        });
        //                }
        //                else if (savegameInvalid)
        //                {
        //                        return Ok(new ApiReturnVal
        //                        {
        //                                ret = new SegActionRes(w.cur_time)
        //                                {
        //                                        savegame_invalid = true
        //                                }
        //                        });
        //                }

        //                w.curLang = i.lang;






        //                var objective = w.objectiveOfId[i.psi_objective.ser_id];

        //                var xdocI = w.getXdocObjIndexedCached();

        //                SegActionRes actionRes;


        //                // prima di tutto vedo se c'è un custom handler
        //                var hand = w.autoSolvePuzzleHandlers.Where(ha => ha.objective == objective).SingleOrDefault();

        //                if (hand != null)
        //                {
        //                        actionRes = eng.executePuzzleSolutionAuto(objective, w, saveNames, xdocI);
        //                }
        //                else
        //                {


        //                        //// poi 
        //                        //w.beforeActionExecuted(objective, w.curRoom, out bool canceled);

        //                        //if (canceled) // ha scritto nella cut scene il testo
        //                        //{

        //                        //}
        //                        //else
        //                        {


        //                                var solution = w.autoFindSolutionForPuzzle(objective, this);
        //                                if (solution == null)
        //                                {

        //                                        var xdocObj = w.getXdocObjIndexedCached();
        //                                        var hi = new HandlerInput
        //                                        {

        //                                        };
        //                                        var cs = new CutScene(canBeSkipped: false);
        //                                        string fraseCompleta;

        //                                        var inOrderTo = w.translateSentenceWithIdFromObjfile(strToTranslate: "Fai qualcosa per ", xelementName: "do_something_to", xdocObj: xdocObj?.Xdoc);
        //                                        fraseCompleta = "{1} {2}".inst(inOrderTo).inst(objective.translated_name(xdocObj));

        //                                        w.setCurrentCs(cs);


        //                                        // vedo se per caso devo impedire di farlo perche' on e' il momento
        //                                        w.beforeActionExecuted(objective, w.curRoom, out bool canceled);
        //                                        if (!canceled)
        //                                        {
        //                                                w.processWrongSolutionAuto(objective, objective.translated_name(xdocObj), xdocObj);
        //                                        }

        //                                        w.clearCurrentCs();

        //                                        eng.vediStatoGameTalkOText(hi, out GameStateShowingQuestions gameStateTalkDopoLaCutScene, out GameStateWaitingForText gameStateWaitingTextDopoCutScene);

        //                                        w.gs = new GameStateCutScene
        //                                        {
        //                                                cs = cs,
        //                                                iCurToken = 0,
        //                                                afterCutSceneShowDialog = gameStateTalkDopoLaCutScene,
        //                                                afterCutSceneWaitForTextInput = gameStateWaitingTextDopoCutScene,
        //                                        };

        //                                        actionRes = new SegActionRes(w.cur_time)
        //                                        {
        //                                                nextCutSceneToken = new CutSceneTokenWithTitle
        //                                                {
        //                                                        cutSceneToken = cs.First(),


        //                                                        actionReadable = fraseCompleta
        //                                                },
        //                                        };
        //                                }
        //                                else
        //                                {

        //                                        PuzzleSolutionPieceSentByClient[] solutionClient = WorldBase.convertSolutionIntoUserSolution(xdocI, solution.solution);
        //                                        actionRes = eng.executePuzzleSolution(objective, solutionClient, w, saveNames, xdocI);
        //                                }
        //                        }
        //                }








        //                // salvataggio automatico! non deve poter tornare indietro caricando un salvataggio
        //                autosave(db, user, w);






        //                return Ok(new ApiReturnVal
        //                {
        //                        ret = actionRes
        //                });
        //        }
        //        catch (Exception e)
        //        {
        //                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
        //        }
        //}



        //public IActionResult submitPuzzleSolutionImpl([FromBody] PuzzleSolutionInput i)
        //{
        //        try
        //        {

        //                var db = new segusumDb();
        //                var user = auth(i, db);
        //                if (user == null)
        //                        return Ok(new ApiReturnVal { errore = "noauth" });

        //                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid, out string[] saveNames, i.lang);
        //                if (i.curTime != w.cur_time)
        //                {
        //                        // l'utente sta cercando di giocare su una finestra che è rimasta indietro come tempo... deve fare take over
        //                        return Ok(new ApiReturnVal
        //                        {
        //                                ret = new SegActionRes(i.curTime /* gli dico il tempo sbagliato non quello giusto, se nola volta dopo dice di avere quello*/)
        //                                {
        //                                        ar_oldSessionMustTakeOver = true
        //                                }
        //                        });
        //                }
        //                else if (savegameInvalid)
        //                {
        //                        return Ok(new ApiReturnVal
        //                        {
        //                                ret = new SegActionRes(w.cur_time)
        //                                {
        //                                        savegame_invalid = true
        //                                }
        //                        });
        //                }

        //                w.curLang = i.lang;



        //                // esegui azione


        //                var objective = w.objectiveOfId[i.psi_objective.ser_id];

        //                var xdocI = w.getXdocObjIndexedCached();


        //                var test = w.autoFindSolutionForPuzzle(objective, this);

        //                var actionRes = eng.executePuzzleSolution(objective, i.psi_solutionSent, w, saveNames, xdocI);

        //                objective.how_many_times_tried++;



        //                // salvataggio automatico! non deve poter tornare indietro caricando un salvataggio
        //                autosave(db, user, w);






        //                return Ok(new ApiReturnVal
        //                {
        //                        ret = actionRes
        //                });
        //        }
        //        catch (Exception e)
        //        {
        //                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
        //        }
        //}


        //public IActionResult useInLocationImpl([FromBody] UseInLocationInput i)
        //{
        //        try
        //        {

        //                var db = new segusumDb();
        //                var user = auth(i, db);
        //                if (user == null)
        //                        return Ok(new ApiReturnVal { errore = "noauth" });

        //                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid);
        //                if (savegameInvalid)
        //                {
        //                        return Ok(new ApiReturnVal
        //                        {
        //                                ret = new SegActionRes
        //                                {
        //                                        savegame_invalid = true
        //                                }
        //                        });
        //                }

        //                w.curLang = i.lang;



        //                // esegui azione


        //                var binVerb = w.binVerbOfId[i.uilBinVerbId];
        //                var lo = w.loOfId[i.uilLoId];
        //                var ro = w.roomOfId[i.uilRoomId];
        //                var pu = w.objectiveOfId[i.uilPuzId];

        //                var actionRes = eng.executeUseInLocationAction(binVerb, lo, ro, pu, w);





        //                // salvataggio automatico! non deve poter tornare indietro caricando un salvataggio
        //                autosave(db, user, w);






        //                return Ok(new ApiReturnVal
        //                {
        //                        ret = actionRes
        //                });
        //        }
        //        catch (Exception e)
        //        {
        //                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
        //        }
        //}


        //public IActionResult terActionUnImpl([FromBody] TerActionUnInput i)
        //{
        //        try
        //        {

        //                var db = new segusumDb();
        //                var user = auth(i, db);
        //                if (user == null)
        //                        return Ok(new ApiReturnVal { errore = "noauth" });

        //                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid);
        //                if (savegameInvalid)
        //                {
        //                        return Ok(new ApiReturnVal
        //                        {
        //                                ret = new SegActionRes
        //                                {
        //                                        savegame_invalid = true
        //                                }
        //                        });
        //                }

        //                w.curLang = i.lang;



        //                // esegui azione


        //                var unVerb = w.unVerbOfId[i.taiuUnVerbId];
        //                var lo = w.loOfId[i.taiuLoId];
        //                var pu = w.objectiveOfId[i.taiuPuzId];

        //                var actionRes = eng.executeTerActionUn(unVerb, lo, pu, w);


        //                // incremento contatore dell uso dell obiettivo
        //                pu.how_many_times_tried++;


        //                // salvataggio automatico! non deve poter tornare indietro caricando un salvataggio
        //                autosave(db, user, w);






        //                return Ok(new ApiReturnVal
        //                {
        //                        ret = actionRes
        //                });
        //        }
        //        catch (Exception e)
        //        {
        //                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
        //        }
        //}


        //public IActionResult terActionBinImpl([FromBody] UseForInput i)
        //{
        //        try
        //        {

        //                var db = new segusumDb();
        //                var user = auth(i, db);
        //                if (user == null)
        //                        return Ok(new ApiReturnVal { errore = "noauth" });

        //                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid);
        //                if (savegameInvalid)
        //                {
        //                        return Ok(new ApiReturnVal
        //                        {
        //                                ret = new SegActionRes
        //                                {
        //                                        savegame_invalid = true
        //                                }
        //                        });
        //                }

        //                w.curLang = i.lang;



        //                // esegui azione
        //                var binVerb = w.binVerbOfId[i.taiBinVerbId];
        //                var pu = w.objectiveOfId[i.taiPuzId];

        //                var lo = w.loOfId[i.taiLoId];

        //                var actionRes = eng.executeTerActionBin(binVerb, lo, pu, w);



        //                // incremento contatore dell uso dell obiettivo
        //                pu.how_many_times_tried++;




        //                // salvataggio automatico! non deve poter tornare indietro caricando un salvataggio
        //                autosave(db, user, w);






        //                return Ok(new ApiReturnVal
        //                {
        //                        ret = actionRes
        //                });
        //        }
        //        catch (Exception e)
        //        {
        //                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
        //        }
        //}

        //public IActionResult unNoObImpl([FromBody] UnNoObInput i)
        //{
        //        try
        //        {

        //                var db = new segusumDb();
        //                var user = auth(i, db);
        //                if (user == null)
        //                        return Ok(new ApiReturnVal { errore = "noauth" });

        //                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid);
        //                if (savegameInvalid)
        //                {
        //                        return Ok(new ApiReturnVal
        //                        {
        //                                ret = new SegActionRes { savegame_invalid = true }
        //                        });
        //                }

        //                w.curLang = i.lang;



        //                // esegui azione
        //                var unVerb = w.unVerbOfId[i.unoUnVerbId];


        //                var lo = w.loOfId[i.unoLoId];

        //                var actionRes = eng.executeActionUnNoOb(unVerb, lo, w);



        //                // incremento contatore dell uso dell obiettivo
        //                //pu.how_many_times_tried++;




        //                // salvataggio automatico! non deve poter tornare indietro caricando un salvataggio
        //                autosave(db, user, w);



        //                return Ok(new ApiReturnVal
        //                {
        //                        ret = actionRes
        //                });
        //        }
        //        catch (Exception e)
        //        {
        //                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
        //        }
        //}



        public IActionResult cancelTextInputImpl([FromBody] CancelTextInputInput i)
        {
            try
            {

                var db = new segusumDb();
                var user = auth(i, db, out bool isTextMode);
                if (user == null)
                {
                    return Ok(new ApiReturnVal { errore = "noauth" });
                }

                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid, out string[] saveNames, i.lang, isTextMode);

                if (i.curTime != w.cur_time)
                {
                    // l'utente sta cercando di giocare su una finestra che è rimasta indietro come tempo... deve fare take over
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(i.curTime /* gli dico il tempo sbagliato non quello giusto, se nola volta dopo dice di avere quello*/)
                        {
                            ar_oldSessionMustTakeOver = true
                        }
                    });
                }
                else if (savegameInvalid)
                {
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(w.cur_time) { savegame_invalid = true }
                    });
                }

                w.CurLang = i.lang;



                // esegui azione
                var ti = w.getAllTextInputs().Single(t => t.serId == i.ctiSerId);

                var xdocObj = w.getXdocObjIndexedCached();

                var actionRes = eng.executeCancelTextInput(ti, w, saveNames, xdocObj?.Xdoc, isTextMode);




                // salvataggio automatico! non deve poter tornare indietro caricando un salvataggio
                autosave(db, user, w);






                return Ok(new ApiReturnVal
                {
                    ret = actionRes
                });
            }
            catch (Exception e)
            {
                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
            }
        }

        public IActionResult submitTextInputImpl([FromBody] SubmitTextInputInput i)
        {
            try
            {

                var db = new segusumDb();
                var user = auth(i, db, out bool isTextMode);
                if (user == null)
                {
                    return Ok(new ApiReturnVal { errore = "noauth" });
                }

                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid, out string[] saveNames, i.lang, isTextMode);
                if (i.curTime != w.cur_time)
                {
                    // l'utente sta cercando di giocare su una finestra che è rimasta indietro come tempo... deve fare take over
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(i.curTime /* gli dico il tempo sbagliato non quello giusto, se nola volta dopo dice di avere quello*/)
                        {
                            ar_oldSessionMustTakeOver = true
                        }
                    });
                }
                else if (savegameInvalid)
                {
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(w.cur_time) { savegame_invalid = true }
                    });
                }

                w.CurLang = i.lang;



                // esegui azione
                var ti = w.getAllTextInputs().Single(t => t.serId == i.stiSerId);
                var xdocObj = w.getXdocObjIndexedCached();
                var actionRes = eng.executeSubmitTextInput(ti, i.stiText, i.stiText2, i.stiExplId, w, saveNames, xdocObj, isTextMode);




                // salvataggio automatico! non deve poter tornare indietro caricando un salvataggio
                autosave(db, user, w);



                return Ok(new ApiReturnVal
                {
                    ret = actionRes
                });
            }
            catch (Exception e)
            {
                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
            }
        }

        public IActionResult quickMoveImpl([FromBody] QuickMoveInput i)
        {
            try
            {

                var db = new segusumDb();
                var user = auth(i, db, out bool isTextMode);
                if (user == null)
                {
                    return Ok(new ApiReturnVal { errore = "noauth" });
                }

                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid, out string[] saveNames, i.lang, isTextMode);
                if (i.curTime != w.cur_time)
                {
                    // l'utente sta cercando di giocare su una finestra che è rimasta indietro come tempo... deve fare take over
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(i.curTime /* gli dico il tempo sbagliato non quello giusto, se nola volta dopo dice di avere quello*/)
                        {
                            ar_oldSessionMustTakeOver = true
                        }
                    });
                }
                else if (savegameInvalid)
                {
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(w.cur_time)
                        {
                            savegame_invalid = true
                        }
                    });
                }

                w.CurLang = i.lang;



                // esegui azione



                var room = w.roomOfId[i.qmiRoomId];

                var xdocObj = w.getXdocObjIndexedCached();
                var actionRes = eng.executeQuickMove(room, w, saveNames, xdocObj, isTextMode);



                // salvataggio automatico! non deve poter tornare indietro caricando un salvataggio
                autosave(db, user, w);






                return Ok(new ApiReturnVal
                {
                    ret = actionRes
                });
            }
            catch (Exception e)
            {
                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
            }
        }


        ///// <summary>
        ///// non piu usata perche ho deciso di non chiedere obiettivo se combini due oggetti. infatti o non serve, o l'enigma si può riformulare come usa unario + obiettivo
        ///// </summary>
        ///// <param name="i"></param>
        ///// <returns></returns>
        //public IActionResult quatActionImpl([FromBody] QuatActionInput i)
        //{
        //        try
        //        {

        //                var db = new segusumDb();
        //                var user = auth(i, db);
        //                if (user == null)
        //                        return Ok(new ApiReturnVal { errore = "noauth" });

        //                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid);
        //                if (savegameInvalid)
        //                {
        //                        return Ok(new ApiReturnVal
        //                        {
        //                                ret = new SegActionRes
        //                                {
        //                                        savegame_invalid = true
        //                                }
        //                        });
        //                }

        //                w.curLang = i.lang;



        //                // esegui azione
        //                var binVerb = w.binVerbOfId[i.qaiBinVerbId];
        //                var obj = w.objectiveOfId[i.qaiPuzId];

        //                var lo1 = w.loOfId[i.qaiLo1Id];
        //                var lo2 = w.loOfId[i.qaiLo2Id];

        //                var actionRes = eng.executeQuatAction(binVerb, lo1, lo2, obj, w);


        //                // incremento contatore dell uso dell obiettivo
        //                obj.how_many_times_tried++;


        //                // salvataggio automatico! non deve poter tornare indietro caricando un salvataggio
        //                autosave(db, user, w);






        //                return Ok(new ApiReturnVal
        //                {
        //                        ret = actionRes
        //                });
        //        }
        //        catch (Exception e)
        //        {
        //                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
        //        }
        //}

        //public IActionResult binaryNoObActionImpl([FromBody] BinaryNoObActionInput i)
        //{
        //        try
        //        {

        //                var db = new segusumDb();
        //                var user = auth(i, db);
        //                if (user == null)
        //                        return Ok(new ApiReturnVal { errore = "noauth" });

        //                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid);
        //                if (savegameInvalid)
        //                {
        //                        return Ok(new ApiReturnVal
        //                        {
        //                                ret = new SegActionRes
        //                                {
        //                                        savegame_invalid = true
        //                                }
        //                        });
        //                }

        //                w.curLang = i.lang;



        //                // esegui azione
        //                var binVerb = w.binVerbOfId[i.bnaiBinVerbId];


        //                var lo1 = w.loOfId[i.bnaiLo1Id];
        //                var lo2 = w.loOfId[i.bnaiLo2Id];

        //                var actionRes = eng.executeBinaryNoObAction(binVerb, lo1, lo2, w);


        //                // incremento contatore dell uso dell obiettivo
        //                //obj.how_many_times_tried++;


        //                // salvataggio automatico! non deve poter tornare indietro caricando un salvataggio
        //                autosave(db, user, w);






        //                return Ok(new ApiReturnVal
        //                {
        //                        ret = actionRes
        //                });
        //        }
        //        catch (Exception e)
        //        {
        //                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
        //        }
        //}


        public IActionResult saveGameWithNameImpl([FromBody] SaveGameWithNameInput i)
        {
            try
            {

                var db = new segusumDb();
                var user = auth(i, db, out bool isTextMode);
                if (user == null)
                {
                    return Ok(new ApiReturnVal { errore = "noauth" });
                }

                // carico in memoria ciò che voglio salvare con un certo nome.
                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid, out string[] saveNames, i.lang, isTextMode);

                if (i.curTime != w.cur_time)
                {
                    // l'utente sta cercando di giocare su una finestra che è rimasta indietro come tempo... deve fare take over
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(i.curTime /* gli dico il tempo sbagliato non quello giusto, se nola volta dopo dice di avere quello*/)
                        {
                            ar_oldSessionMustTakeOver = true
                        }
                    });
                }
                else if (savegameInvalid)
                {
                    return Ok(new ApiReturnVal
                    {
                        ret = new SegActionRes(w.cur_time)
                        {
                            savegame_invalid = true
                        }
                    });
                }

                w.CurLang = i.lang;



                // non c'è nessuna azione da eseguire, quindi neppure bisogno di fare autosave nel savegame di default. dobbiamo solo salvare
                // nel savegame col nome specificato
                autosave(db, user, w, i.savegameName);



                var newsavenames = Utils.retry(() => (from s in db.savegame
                                                      where s.idUser == user.id
                                                      where s.savegameTitle != "" // skippail default
                                                      where currentTutorialMode.Value
                                                        ? s.savegameTitle.StartsWith("__tutorial__")
                                                        : !s.savegameTitle.StartsWith("__tutorial__")
                                                      select s)
                                    .OrderByDescending(s => s.dateModified)

                                    .Select(s => displayScenarioSaveTitle(s.savegameTitle, currentTutorialMode.Value))
                                    .ToArray()
                                    );

                // non posso aggiungere una cutscene, perché se ha salvato durante una cutscene la perderei
                //var cs = new List<cutSceneToken>();
                //eng.nar($"[Partita salvata]".tr(), cs);


                //w.gs = new gameStateCutScene
                //{
                //    cs = cs.ToArray(),
                //    iCurToken = 0,
                //    afterCutSceneShowDialog = null,

                //};

                //var ar = new actionRes2
                //{
                //    nextCutSceneToken = new cutSceneTokenWithTitle
                //    {
                //        cutSceneToken = cs.First(),
                //        actionReadable = "salva partita".tr()
                //    },
                //};


                // restituisco uno stato identico
                //actionRes2 ar;
                //if (i.cs.Any())
                //{


                //    w.gs = new gameStateCutScene
                //    {
                //        cs = i.cs.ToArray(),
                //        iCurToken = 0,
                //        afterCutSceneShowDialog = gameStateTalkDopoLaCutScene,

                //    };

                //    ar = new actionRes2
                //    {
                //        nextCutSceneToken = new cutSceneTokenWithTitle { cutSceneToken = i.cs.First(), actionReadable = fraseCompleta },
                //    };
                //}
                //else
                //{
                //    // siamo già dopo la cut scene. Ora, vedi se deve passare a room o a dialogo.


                //    ar = calcolaActionResTalkORoom(w, gameStateTalkDopoLaCutScene);

                //}

                return Ok(new ApiReturnVal
                {
                    ret = new
                    {
                        res = "ok",
                        newsavenames = newsavenames
                    }
                });
            }
            catch (Exception e)
            {
                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
            }
        }

        public IActionResult loadGameWithNameImpl([FromBody] SaveGameWithNameInput i)
        {
            try
            {

                var db = new segusumDb();
                var user = auth(i, db, out bool isTextMode);
                if (user == null)
                {
                    return Ok(new ApiReturnVal { errore = "noauth" });
                }

                // carico in memoria ciò che voglio salvare con un certo nome.
                var w = restoreWorldFromDisk(user.id, db, i.savegameName, out var savegameInvalid, i.lang, isTextMode);


                if (w != null)
                {
                    w.invariantConditions(); // rilanciale se no il salvataggio viene senza gli oggetti nuovi... così almeno cerchi di recuperarlo
                }

                var saveNames = Utils.retry(() => (from s in db.savegame
                                                   where s.idUser == user.id
                                                   where s.savegameTitle != "" // skippail default
                                                   where currentTutorialMode.Value
                                                     ? s.savegameTitle.StartsWith("__tutorial__")
                                                     : !s.savegameTitle.StartsWith("__tutorial__")
                                                   select s)
                                    .OrderByDescending(s => s.dateModified)

                                    .Select(s => displayScenarioSaveTitle(s.savegameTitle, currentTutorialMode.Value))
                                    .ToArray()
                                    );

                if (savegameInvalid)
                {
                    return Ok(new ApiReturnVal { errore = "save_game_invalid" });
                }

                w.CurLang = i.lang;
                if (w == null)
                {
                    return Ok(new ApiReturnVal { errore = "save_game_not_found" });
                }


                // ora lui diventa il savegame di default, quindi salva il savegame di default
                autosave(db, user, w);




                // Il salvataggio nominato può contenere una cutscene in corso.
                // Riusa il percorso di loadGameImpl, che restituisce correttamente
                // il token corrente invece di forzare sempre la stanza.
                eng.worldOfUser[user.id] = w;
                return loadGameImpl(i);
            }
            catch (Exception e)
            {
                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
            }
        }


        public class InputCreateUserAndStartGame2
        {
            public string uname { get; set; }
            public string pwd1 { get; set; }

            public string pwd2 { get; set; }

            public string lang { get; set; }

            public int? gameId { get; set; }

            public bool tutorialMode { get; set; }
        }

        public ApiReturnVal createUserAndStartGameCore(InputCreateUserAndStartGame2 i, string ipAddr)
        {
            try
            {

                // lo sposto qui così posso testare questo anche senza db funzionante
                currentTutorialMode.Value = i.tutorialMode;
                var w = buildEmptyWorld(i.lang, i.tutorialMode);
                w.IsTutorialMode = i.tutorialMode;
                integrityCheckAndPostProcessingAfterWorldBuild(w);

                precomputationsAfterWorldBuildDeserialize(w);

                int idUser;
                //string token;
                user user;
                var db = new segusumDb();

                //retry:
                //try
                {
                    using (var tr = new TransactionScope())
                    {


                        int newInt;
                        if (db.user.Any())
                        {
                            newInt = (from u in db.user
                                      select u.id).Max() + 1;
                        }
                        else
                        {
                            newInt = 1;
                        }



                        //token = ipAddr + "_" + newInt;


                        var esistegia = (from u in db.user
                                         where u.uname.Trim() == i.uname.Trim()
                                         select 1).Any();


                        if (esistegia)
                        {
                            return new ApiReturnVal { errore = "username-already-taken" };
                        }


                        if (i.pwd1 != i.pwd2)
                        {
                            return new ApiReturnVal { errore = "passwords-not-equal" };
                        }


                        // aggiungo l'utente al db
                        var now = DateTime.Now;
                        var newUser = new user
                        {
                            uname = i.uname.Trim(),
                            pwd = i.pwd1,
                            dateCreated = now,
                            dateLastAccess = now,
                            tempToken = "n.a."  // non piu usato


                                ,
                            canPlayGraphicsMode = !StorageOptions.IsFile
                                ,
                            gameId = i.gameId,
                            isCasualMode = false
                        };
                        db.user.Add(newUser);
                        db.SaveChanges();

                        db.ips.Add(new ips { idUser = newUser.id, ip = ipAddr, dateLastUsed = now });
                        db.SaveChanges();

                        idUser = newUser.id;
                        user = newUser;

                        tr.Complete();
                    }
                }
                //catch (DbUpdateException e)
                //{
                //    // è successo chiamando new game
                //    goto retry;
                //}


                var actionRes = startNewGame(user, db, i.lang, w, saveNames: new string[] { } /* usati solo per precalcolare subito la room dopo la cutscene, non servono*/

                , isTextMode: true // sopra ho appena messo graphics mode = false

                );




                var debugTime = w.cur_time;
                // devo salvare il nuovo mondo in memoria, se no al prossimo actionres c'è una discordanza di curtime tra client e server
                autosave(db, user, w);


                return new ApiReturnVal
                {
                    ret = new getTokenResult
                    {
                        //token = token,
                        res = actionRes,
                    }
                };

            }
            catch (Exception e)
            {
                return new ApiReturnVal { errore = UtilsW.stringOfException(e) };
            }
        }

        protected IActionResult createUserAndStartGameImpl(InputCreateUserAndStartGame2 i)
        {
            var ipAddr = Request?.HttpContext?.Connection?.RemoteIpAddress?.ToString();
            return Ok(createUserAndStartGameCore(i, ipAddr));
        }


        protected IActionResult startNewGameImpl([FromBody] Credentials i)
        {
            try
            {

                // lo sposto qui così posso testare questo anche senza db funzionante

                var db = new segusumDb();
                var user = auth(i, db, out bool isTextMode);
                if (user == null)
                {
                    return Ok(new ApiReturnVal { errore = "noauth" });
                }

                currentTutorialMode.Value = i.tutorialMode;
                var w = buildEmptyWorld(i.lang, i.tutorialMode);
                w.IsTutorialMode = i.tutorialMode;
                w.IsCasualMode = user.isCasualMode == true;
                integrityCheckAndPostProcessingAfterWorldBuild(w);

                precomputationsAfterWorldBuildDeserialize(w);



                var saveNames = loadSavegameNamesFromDb(user.id, db);

                var actionRes = startNewGame(user, db, i.lang, w, saveNames, isTextMode);




                var debugTime = w.cur_time;
                // devo salvare il nuovo mondo in memoria, dato che è cambiato
                autosave(db, user, w);


                return Ok(new ApiReturnVal
                {
                    ret = new getTokenResult
                    {

                        res = actionRes,
                    }
                });

            }
            catch (Exception e)
            {
                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
            }
        }

        protected IActionResult setGameModeImpl(GameModeInput i)
        {
            try
            {
                using var db = new segusumDb();
                var user = auth(i, db, out bool isTextMode);
                if (user == null) return Ok(new ApiReturnVal { errore = "noauth" });
                user.isCasualMode = i.casualMode;
                db.SaveChanges();
                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var invalid, out var names, i.lang, isTextMode);
                if (invalid || w == null) return Ok(new ApiReturnVal { errore = "savegame-invalid" });
                w.IsCasualMode = i.casualMode;
                return Ok(new ApiReturnVal { ret = new SegActionRes(w.cur_time) { room = eng.creaRoomDaDareAlClient(w, names, isTextMode) } });
            }
            catch (Exception e)
            {
                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
            }
        }


        protected IActionResult checkLastUsersImpl()
        {
            try
            {

                // lo sposto qui così posso testare questo anche senza db funzionante

                var db = new segusumDb();

                var re = (from s in db.savegame
                          join u in db.user on s.idUser equals u.id

                          select new CheckUsers { DateModified = s.dateModified, SavegameXml = s.savegameXml, A_Uname = u.uname })
                         .OrderByDescending(x => x.DateModified)
                         .Take(5)
                         .ToArray()
                         ;



                return Ok(re);

            }
            catch (Exception e)
            {
                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
            }
        }


        public class getTokenResult
        {
            public string token;
            public SegActionRes res;
        }


        /// <summary>
        /// il savegame di default ha titolo = stringa vuota
        /// </summary>
        /// <param name="db"></param>
        /// <param name="user"></param>
        /// <param name="w"></param>
        /// <param name="savegameName"></param>
        // Durante una cutscene cambia solo il token visualizzato. Riscrivere
        // l'intero mondo a ogni click crea migliaia di oggetti temporanei e può
        // provocare pause del garbage collector. Aggiorniamo quindi solo quel
        // valore nel snapshot già persistito.
        protected static void autosaveInBackground(string userName, WorldBase w)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    // La richiesta che ha creato la room rilascia il gate al
                    // termine della risposta; da qui in poi nessuna API può
                    // modificare il mondo mentre lo serializziamo.
                    var gate = ApiSerializationGate.ForUser(userName);
                    gate.Wait();
                    try
                    {
                        using var db = new segusumDb();
                        var user = db.user.Include(u => u.ips).Single(u => u.uname == userName);
                        autosave(db, user, w);
                    }
                    finally
                    {
                        gate.Release();
                    }
                }
                catch (Exception e)
                {
                    SegusumProfiler.Log($"background_autosave user={userName} error={e.GetType().Name}: {e.Message}");
                }
            });
        }

        protected static void autosaveCutScenePosition(segusumDb db, user user, WorldBase w)
        {
            if (w.gs is not GameStateCutScene gsCs)
            {
                autosave(db, user, w);
                return;
            }

            var sav = Utils.retry(() => db.savegame.Where(s =>
                                            s.savegameTitle == scenarioSaveTitle("", w.IsTutorialMode)
                                            && s.idUser == user.id).FirstOrDefault());

            const string stateMarker = "<gameStateCutScene";
            const string tokenMarker = "iCurToken=\"";
            var stateStart = sav?.savegameXml?.IndexOf(stateMarker, StringComparison.Ordinal) ?? -1;
            var tokenStart = stateStart < 0
                ? -1
                : sav.savegameXml.IndexOf(tokenMarker, stateStart, StringComparison.Ordinal);

            if (sav == null || tokenStart < 0)
            {
                autosave(db, user, w);
                return;
            }

            var valueStart = tokenStart + tokenMarker.Length;
            var valueEnd = sav.savegameXml.IndexOf('"', valueStart);
            if (valueEnd < 0)
            {
                autosave(db, user, w);
                return;
            }

            sav.savegameXml = sav.savegameXml.Substring(0, valueStart)
                + gsCs.iCurToken
                + sav.savegameXml.Substring(valueEnd);
            sav.dateModified = DateTime.Now;

            var now = DateTime.Now;
            foreach (var ip in user.ips)
                ip.dateLastUsed = now;

            var stopwatch = Stopwatch.StartNew();
            Utils.retry(() => db.SaveChanges());
            stopwatch.Stop();
            SegusumProfiler.Log($"autosave user={user.id} title= phase=cutscene_position " +
                $"elapsed_ms={stopwatch.Elapsed.TotalMilliseconds:F1} token={gsCs.iCurToken} " +
                $"storage={(StorageOptions.IsFile ? "file" : "sql")}");
        }

        protected static void autosave(segusumDb db, user user, WorldBase w, string savegameName = "")
        {
            var serializeStopwatch = Stopwatch.StartNew();
            var xdoc = w.serialize();
            var xmlStr = xdoc.ToString();
            serializeStopwatch.Stop();
            SegusumProfiler.Log($"autosave user={user.id} title={savegameName} phase=serialize " +
                $"elapsed_ms={serializeStopwatch.Elapsed.TotalMilliseconds:F1} xml_chars={xmlStr.Length} " +
                $"past_actions={w.pastActions.Count} named_cutscenes={w.namedCutScenesSeen.Count}");

            var debugcurtime = w.cur_time;

            var sav = Utils.retry(() => db.savegame.Where(s =>
                                            s.savegameTitle == scenarioSaveTitle(savegameName, w.IsTutorialMode)
                                            && s.idUser == user.id).FirstOrDefault()
                                            );

            if (sav == null)
            {
                var newSav = new savegame
                {
                    idStory = 0, // non usato per ora
                    idUser = user.id,
                    savegameXml = xmlStr,
                    savegameTitle = scenarioSaveTitle(savegameName, w.IsTutorialMode),
                    dateModified = DateTime.Now
                };
                db.savegame.Add(newSav);
            }
            else
            {
                sav.dateModified = DateTime.Now;
                sav.savegameXml = xmlStr;
            }


            //db.SaveChanges();
            // aggiorna date last used

            var now = DateTime.Now;
            //user.dateLastAccess = now; // no, provoca deadlock! lo salvo già altrove.

            foreach (var ip in user.ips)
            {
                ip.dateLastUsed = now;
            }


            //user.savegame = xmlStr;

            var persistStopwatch = Stopwatch.StartNew();
            Utils.retry(() =>
                    db.SaveChanges()
            );
            persistStopwatch.Stop();
            SegusumProfiler.Log($"autosave user={user.id} title={savegameName} phase=persist " +
                $"elapsed_ms={persistStopwatch.Elapsed.TotalMilliseconds:F1} storage=" +
                (StorageOptions.IsFile ? "file" : "sql"));


        }

        /// <summary>
        /// serve per i mondi in memoria. se non leggo dal disco, non rilegge i coord file
        /// </summary>
        //protected IActionResult reparseCoordFile()
        //{

        //        //if (cr.uname == "maurizio" && cr.pwd == "manuelo")
        //        {
        //                foreach (var wo in eng.worldOfUser)
        //                {
        //                        foreach (var ro in wo.Value.roomOfId.Values)
        //                        {
        //                                if (ro.assetFolderName.is_not_null_or_white())
        //                                {
        //                                        var dic = eng.ParseCoordFile(ro, wo.Value, out bool dataNotPresent);
        //                                        if (dataNotPresent)
        //                                        {
        //                                                throw new Exception("room data file was not present {ro}");
        //                                        }
        //                                        ro.coordFile = dic;
        //                                }
        //                        }
        //                }

        //                Utils.printToLogGeneric("reparsed coord files", "coordFileParse");

        //                return Ok("done");
        //        }
        //        //else
        //        //{
        //        //        return Ok("wrong pwd");
        //        //}
        //}


        /// <summary>
        /// chiamare solo dopo aver autenticato l'utente
        /// </summary>
        /// <param name="idUser"></param>
        /// <param name="db"></param>
        /// <param name="savegame_invalid"></param>
        /// <returns></returns>
        protected WorldBase restoreWorldFromMemoryOrDisk(int idUser, segusumDb db, out bool savegameInvalid, out string[] saveNames, string lang, bool isTextMode)
        {


            var exists = eng.worldOfUser.TryGetValue(idUser, out var wo)
                && wo.IsTutorialMode == currentTutorialMode.Value;

            //exists = false; // TEMP debug per debuggare la serializ  bm_debug bm_testare bm_serializ
            // serve anche a forzare il reparse del coord file, ma non e' ideale

            saveNames = loadSavegameNamesFromDb(idUser, db);

            if (exists)
            {
                wo.IsTextMode = isTextMode; // lo copio nel mondo perchè serve a narRoom
                wo.IsCasualMode = userModeIsCasual(idUser, db);

                wo.invariantConditions();
                savegameInvalid = false;





                return wo;
            }
            else
            {
                // chiama automaticamente anche reparse_coord_file.

                // non giocava da troppo tempo. il suo mondo è stato salvato nel db
                var restored = restoreWorldFromDisk(idUser, db, savegameTitle: "", savegameInvalid: out savegameInvalid, lang: lang, isTextMode: isTextMode);

                if (savegameInvalid)
                {
                    return null;
                }

                restored.invariantConditions();

                return restored; // notare che carico il savegame di default, quello che ha nome stringa vuota.

            }


        }

        private static bool userModeIsCasual(int idUser, segusumDb db) =>
            db.user.Where(u => u.id == idUser).Select(u => u.isCasualMode).SingleOrDefault() == true;

        private static string[] loadSavegameNamesFromDb(int idUser, segusumDb db)
        {
            string[] saveNames;
            saveNames =
                    Utils.retry(() =>
                    (from s in db.savegame
                     where s.idUser == idUser
                     where s.savegameTitle != "" // skippail default
                     where currentTutorialMode.Value
                       ? s.savegameTitle.StartsWith("__tutorial__")
                       : !s.savegameTitle.StartsWith("__tutorial__")
                     select s)
                      .OrderByDescending(s => s.dateModified)

                      .Select(s => displayScenarioSaveTitle(s.savegameTitle, currentTutorialMode.Value))
                      .ToArray()

                      );
            return saveNames;
        }

        protected WorldBase restoreWorldFromDisk(int idUser, segusumDb db, string savegameTitle, out bool savegameInvalid, string lang, bool isTextMode)
        {
            var restoreStopwatch = Stopwatch.StartNew();

            var savegame = Utils.retry(() => (from u in db.savegame
                                              where u.idUser == idUser
                                              where u.savegameTitle == scenarioSaveTitle(savegameTitle, currentTutorialMode.Value)
                                              select u.savegameXml).FirstOrDefault()
                            );

            if (savegame != null)
            {
                XDocument xdoc;
                try
                {
                    xdoc = XDocument.Parse(savegame);
                }
                catch (XmlException e)
                {
                    // Savegames are currently XML. A savegame written by the
                    // incompatible JSON serializer (or otherwise malformed)
                    // must be treated as corrupted so the client can start a
                    // new game instead of exposing an internal exception.
                    savegameInvalid = true;
                    SegusumProfiler.Log($"restore user={idUser} title={savegameTitle} phase=invalid-format " +
                        $"elapsed_ms={restoreStopwatch.Elapsed.TotalMilliseconds:F1} xml_chars={savegame.Length} " +
                        $"exception={e.GetType().Name}");
                    return null;
                }

                var w = buildEmptyWorld(lang, currentTutorialMode.Value); // l'engine non conosce il tipo world

                w.IsTextMode = isTextMode; // lo metto nel mondo perché serve a narRoom
                w.IsTutorialMode = currentTutorialMode.Value;
                w.IsCasualMode = userModeIsCasual(idUser, db);

                integrityCheckAndPostProcessingAfterWorldBuild(w);

                w.deserialize(xdoc, out savegameInvalid); // modifica w.gs
                if (savegameInvalid)
                {
                    return null;
                }




                // devo precalcolare per ogni obiettivo quali qtok sono sempre nascosti
                precomputationsAfterWorldBuildDeserialize(w);



                eng.worldOfUser[idUser] = w;

                restoreStopwatch.Stop();
                SegusumProfiler.Log($"restore user={idUser} title={savegameTitle} phase=deserialize " +
                    $"elapsed_ms={restoreStopwatch.Elapsed.TotalMilliseconds:F1} xml_chars={savegame.Length} " +
                    $"past_actions={w.pastActions.Count} named_cutscenes={w.namedCutScenesSeen.Count}");

                return w;

            }
            else
            {

                //non mi pare che possa succedere. se lui è nel db, la sua partita deve essere o in memoria o su disco.
                // mi è successo se non ha trovato il salvataggio con quel nome
                savegameInvalid = true;
                restoreStopwatch.Stop();
                SegusumProfiler.Log($"restore user={idUser} title={savegameTitle} phase=missing " +
                    $"elapsed_ms={restoreStopwatch.Elapsed.TotalMilliseconds:F1}");
                return null;

            }
        }


        private static void precomputationsAfterWorldBuildDeserialize(WorldBase w)
        {
            Debug.Assert(w.activeChar != null); // altrimenti fallisce un pezzo di qtokIsVisibleNow. ecco perché questa proc va chiamata dopo deserialize o build







            // genero la tabella degli oggetti non gestiti


            var y = 4;

            rebuildGeneraOggettiNonGestiti(w);


            w.afterDeserializeComputeExclusions();




            //foreach (var ob in w.objectiveOfId.Values)
            //{
            //        ob.excludedQtoks.Clear();

            //        foreach (var qt in w.allQtoks)
            //        {
            //                var visib = w.qtokIsVisibleNow(ob, qt);
            //                if (!visib)
            //                {
            //                        ob.excludedQtoks.Add(qt);
            //                }
            //        }

            //}


            //var allExplanations = w.getAllExplanations().ToArray();

            var xdi = w.getXdocObjIndexedCached();

            foreach (var ti in w.getAllTextInputs())
            {
                if (ti.tiCorrectExplanation != null)
                {
                    //ti.tiCorrectExplanation.exName = w.translateDialogOrNarOrAnnotated(ti.tiCorrectExplanation.exName, xdi);

                    if (ti.tiVisibleExplanations == null)
                    {
                        throw new Exception($"You have set correct explanation but not visible explanations for {ti.serId}");
                    }

                    //foreach (var tiv in ti.tiVisibleExplanations)
                    //{
                    //        tiv.exName = w.translateDialogOrNarOrAnnotated(tiv.exName, xdi);
                    //}
                }
            }

        }

        private static void rebuildGeneraOggettiNonGestiti(WorldBase w)
        {
            var sb = new StringBuilder();


            // TODO scommenta per generare
            //faiCiclo(w, sb, chars: true);

            //faiCiclo(w, sb, chars: false);

            var daGestire = sb.ToString();
        }

        private static void faiCiclo(WorldBase w, StringBuilder sb, bool chars)
        {
            foreach (var loInv in w.loOfId.Values)
            {
                if (loInv.IsPickableHint && loInv.HoverActionWhenInInv == HoverActionWhenInInv.UseWith)
                {
                    foreach (var loTarget in w.loOfId.Values)
                    {
                        if (chars && loTarget is Character || !chars && !(loTarget is Character))
                        {

                            if (w.combineHandlers.Any(uwa => uwa.lo1 == loInv && uwa.lo2 == loTarget))
                            {

                            }
                            else
                            {
                                sb.AppendLine($"gestire: {loInv.loId} --> {loTarget.loId}");
                            }
                        }


                    }
                }
            }
        }

        internal static void integrityCheckAndPostProcessingAfterWorldBuild(WorldBase w)
        {


            //foreach (var qt in w.allQtoks)
            //{
            //        if (qt.serId == null)
            //        {
            //                throw new Exception("qtok null");
            //        }
            //}

            var combineHandlersOfLo1 = w.combineHandlers.GroupBy(ha => ha.lo1).ToDictionary(x => x.Key, x => x.ToList());

            var useForHandlersOfObj = w.useForHandlers.GroupBy(ha => ha.Objective).ToDictionary(x => x.Key, x => x.ToList());

            var useHereHandlersOfLo1 = w.useHereHandlers.GroupBy(ha => ha.lo1).ToDictionary(x => x.Key, x => x.ToList());

            foreach (var lo in w.loOfId.Values)
            {
                var templates = new[]
                {
                    lo.VerbWhenUseWithAsFirstObjectOnHoverNotSelected,
                    lo.VerbWhenUseWithAsFirstObjectSelectedWithPlaceHolder,
                    lo.VerbWhenUseWithAsFirstObjectSelectedWithPlaceHolderOnHoverSecond
                };
                if (templates.Any(x => x != null && x.Contains(WorldBase.TargetPossessivePlaceholder))
                    && lo.TargetPossessiveAgreement == null)
                {
                    throw new Exception($"LogicObj {lo.loId} uses {WorldBase.TargetPossessivePlaceholder} without TargetPossessiveAgreement");
                }
            }

            foreach (var ob in w.objectiveOfId.Values)
            {
                var handlers = useForHandlersOfObj.itemOrEmpty(ob); // w.useForHandlers.Where(ha => ha.Objective == ob).ToList();

                // Explanation/no-explanation is allowed to vary between objects
                // for the same objective.  The only invalid ambiguity is two
                // handlers for the same ordered pair (object, objective).
                var duplicate = handlers
                        .GroupBy(handler => handler.Lo)
                        .FirstOrDefault(group => group.Count() > 1);
                if (duplicate != null)
                {
                    throw new Exception($"objective {ob.serId} has more than one handler for object {duplicate.Key.loId}");
                }
            }

            //// post processing di nuovo: dopo define recognized action, se ancora non hanno associated qtok, devo dare errore
            foreach (var lo in w.loOfId.Values)
            {

                if (lo.UseKindWhenInRoom == UseKindForRoomObjects.UseHere || lo.HoverActionWhenInInv == HoverActionWhenInInv.UseHere)
                {
                    {
                        var ha = useHereHandlersOfLo1.itemOrEmpty(lo); // .Where(h => h.lo1 == lo).SingleOrDefault();
                        if (!ha.Any()) // == null)
                        {
                            throw new Exception($"missing use here handler for {lo.loId}");
                        }
                    }

                    {
                        var ha = combineHandlersOfLo1.itemOrEmpty(lo); //  .Where(h => h.lo1 == lo).SingleOrDefault();
                        if (ha.Any()) // != null)
                        {
                            throw new Exception($"object has use here but has combine handler: {lo.loId}");
                        }
                    }

                }


                // Duplicate ordered pairs are rejected when the handler is
                // registered; there are no target-specific explanation rules.
                //if (lo.HoverActionWhenInInv == HoverActionWhenInInv.UseWith)
                //{
                //        {
                //                var ha = w.useHereHandlers.Where(h => h.lo1 == lo).SingleOrDefault();
                //                if (ha != null)
                //                {
                //                        throw new Exception($"objectg has use with but has usehere handler : {lo.loId}");
                //                }
                //        }

                //        //{
                //        //        var ha = w.combineHandlers.Where(h => h.lo1 == lo).SingleOrDefault();
                //        //        if (ha == null)
                //        //        {
                //        //                throw new Exception($"object has use with but does not have combine handler: {lo.loId}");
                //        //        }
                //        //}

                //}

                //if (lo.associatedQToks.isEmpty())
                //{
                //        throw new Exception($"logic object {lo.loId} does not have any associated qtok");
                //}
                //if (lo.associatedQToks.Length == 1)
                //{
                //        lo.associatedQToks.Single().serId = $"{lo.loId}__qtok";
                //}

                if (lo.IsConcept && lo.IsExit)
                {
                    throw new Exception($"cannot be concept and exit: {lo}");
                }

                if (lo.IsExit && lo.UseKindWhenInRoom != UseKindForRoomObjects.UseHere)
                {
                    throw new Exception($"exit needs to be either use-here or show-ap: {lo}");
                }
            }


            foreach (var ha in w.useForHandlers)
            {
                var lo = ha.Lo;

                if (lo.UseKindWhenInRoom != UseKindForRoomObjects.UseFor && lo.UseKindWhenInRoom != UseKindForRoomObjects.Nothing)
                {
                    throw new Exception($"object has use for handler but when clicked in room it does not have useFor or nothing");
                }

            }


            foreach (var ha in w.isActuallyHandlers)
            {
                var lo = ha.Lo;

                if (lo.UseKindWhenInRoom != UseKindForRoomObjects.Deduce)
                {

                    throw new Exception($"object has isActually handler but when clicked in room it does not have isActually: {lo.loId}");
                }

            }
            foreach (var ha in w.useHereHandlers)
            {
                var lo = ha.lo1;

                if (lo.HoverActionWhenInInv != HoverActionWhenInInv.UseHere && lo.IsPickableHint /* altrimenti scatta errore con porta dalle 3 serrature*/)
                {

                    throw new Exception($"object has usehere handler , is pickable, but when clicked in inv it does not have usehere: {lo.loId}");
                }

            }
            foreach (var ha in w.combineHandlers)
            {

                if (ha.lo1.HoverActionWhenInInv == HoverActionWhenInInv.UseHere)
                {
                    throw new Exception($"you gave combine handler to object that has usehere: {ha.lo1.loId}");
                }


                if (ha.Explanation != null && ha.IsPossibleNow != null)
                {
                    throw new Exception($"ha {ha.lo1.loId}  , {ha.Explanation.expId}, cannot have explanation and ispossiblenow at the same time");

                }
            }



            var cancelMissing = new List<string>();
            var submitMissing = new List<string>();
            foreach (var ti in w.getAllTextInputs())
            {
                {
                    var ha = w.cancelTextInputHandlers.Where(h => h.ti == ti).SingleOrDefault();
                    if (ha == null)
                    {
                        cancelMissing.Add(ti.serId);

                    }
                }


                {
                    var ha = w.submitTextInputHandlers.Where(h => h.ti == ti).SingleOrDefault();
                    if (ha == null)
                    {
                        submitMissing.Add(ti.serId);

                    }
                }

                {
                    if (ti.tiPreamboloExplanation.is_not_null_or_white() && ti.tiCorrectExplanation == null)
                    {
                        throw new Exception($"text input {ti.serId} has preamble to explanation, but you didn't set the correct explanation");
                    }
                    if (ti.tiPreamboloExplanation.isNullOrWhite() && ti.tiCorrectExplanation != null)
                    {
                        throw new Exception($"text input {ti.serId} has no preamble to explanation, but you have set the correct explanation");
                    }

                }
            }


            if (cancelMissing.Any())
            {
                var str = cancelMissing.aggregateStringList();
                throw new Exception("missing cancel-text-input handler for: " + str);
            }

            if (submitMissing.Any())
            {
                var str = submitMissing.aggregateStringList();
                throw new Exception("missing submit-text-input handler for: " + str);
            }


            //foreach (var ob in w.objectiveOfId.Values)
            //{
            //        //var qtokAssociatiTranneYou = ob.associatedQToks.Where(q => q != w.YouToken()).ToList();

            //        if (ob.associatedQToks.isEmpty())
            //        {
            //                throw new Exception($"objective {ob.serId} does not have any associated qtok "); //except you
            //        }
            //}

            //foreach (var x in w.puzzleSolvedHandlersOldUi)
            //{
            //        if (x.puzzleSolution.solution.Length == 2) // 2 significa 3 incluso l'obiettivo
            //        {
            //                throw new Exception($"A puzzle can't have  3 elements as solution: {x.puzzleSolution.objective.serId}");
            //        }
            //}




            foreach (var lo in w.loOfId.Values)
            {
                var combineHandlersOfLo = combineHandlersOfLo1.itemOrEmpty(lo);
                lo.IsVerbThatRequiresExplanation = combineHandlersOfLo.Any(ha => ha.Explanation != null);
                //lo.IsVerbThatRequiresExplanation = (w.combineHandlers.Any(ha => ha.lo1 == lo && ha.Explanation != null));


            }


        }

        protected static user auth(Credentials cr, segusumDb db, out bool isTextMode)
        {
            currentTutorialMode.Value = cr.tutorialMode;
            user user;
            var retryCount = 0;
        retry:
            try
            {
                using (var tr = new TransactionScope())
                {

                    if (cr.uname.is_not_null_or_white() && cr.pwd.is_not_null_or_white())
                    {
                        user = (from u in db.user // non fare Utils.retry qui! c'è già
                                where u.uname == cr.uname
                                where u.pwd == cr.pwd
                                select u).SingleOrDefault();

                        if (user != null)
                        {


                            int? gameId = user.gameId;
                            int? credGameId = cr.cred_gameId;
                            if (gameId.GetValueOrDefault() == credGameId.GetValueOrDefault() & gameId.HasValue == credGameId.HasValue)
                            {
                                isTextMode = !user.canPlayGraphicsMode.Value;
                                user.dateLastAccess = new DateTime?(DateTime.Now);
                                db.SaveChanges();
                            }
                            else
                            {
                                isTextMode = !user.canPlayGraphicsMode.Value;
                                return (user)null;
                            }

                            //isTextMode = ! user.canPlayGraphicsMode.Value ;
                            //user.dateLastAccess = DateTime.Now;
                            //db.SaveChanges();
                        }
                        else
                        {
                            isTextMode = true;
                        }
                    }
                    //else if (cr.token.is_not_null_or_white())
                    //{
                    //        user = (from u in db.user
                    //                where u.tempToken == cr.token
                    //                select u).SingleOrDefault();

                    //        if (user != null)
                    //        {


                    //                user.dateLastAccess = DateTime.Now;
                    //                db.SaveChanges();
                    //        }

                    //}
                    else
                    {
                        user = null;
                        isTextMode = true;
                    }

                    tr.Complete();
                }
            }
            catch (SqlException e)
            {
                if (retryCount++ < 5)
                {
                    goto retry;
                }
                else
                {
                    throw;
                }
            }
            catch (DbUpdateException e)
            {
                // succedeva quando chiamavo in parallelo per errore getNext e savegame o loadgame. succederà quando tanti utenti andranno in contemporanea.
                if (retryCount++ < 5)
                {
                    goto retry;
                }
                else
                {
                    throw;
                }
            }


            return user;
        }


        //[HttpPost]
        //[Route("api/newGameForExistingUser")]
        //public IActionResult newGameForExistingUser([FromBody] credentials i)
        //{
        //    try
        //    {

        //        var db = new segusumDb();
        //        var user = auth(i, db);

        //        if (user == null)
        //        {
        //            return Ok(new ReturnVal { errore = "noauth" });
        //        }
        //        else
        //        {
        //            actionRes2 ret = startNewGame(user.id);

        //            return Ok(new ReturnVal
        //            {
        //                ret = ret
        //            });
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        return Ok(new ReturnVal { errore = Utils.stringOfException(e) });
        //    }
        //}

        protected SegActionRes startNewGame(user user, segusumDb db, string lang, WorldBase w, string[] saveNames, bool isTextMode)
        {




            w.CurLang = lang;
            w.setStartState();

            w.cur_time = 0;
            //var debugcu = w.cur_time;

            w.curRoom.howManyTimesVisited[w.activeChar] = 1; // altrimenti quando esci , la mappa ti dice che non l'hai mai vistata

            //gl.setupScene1(w);

            //gl.setupScene2(w); // debug
            //gl.setupScene3(w); // debug


            eng.worldOfUser[user.id] = w;




            var cs = new CutScene(canBeSkipped: false);


            w.setCurrentCs(cs);
            w.startGameCutScene();
            w.clearCurrentCs();





            w.gs = new GameStateCutScene(

                    cs: cs,



                    afterCutSceneShowDialog: null  // dopo la cutscene iniziale non devo mostrare dialoghi, ma la room
                    , afterCutSceneWaitForTextInput: null // dopo la cutscene iniziale non devo mostrare text input, ma la room
                    , afterCutSceneGameFinished: null
                    , iCurToken: 0

            );








            // lo metto anche su disco perché altrimenti crasha se disabilito la ram e faccio sempre da disco
            autosave(db, user, w);






            var ret = new SegActionRes(w.cur_time)
            {
                nextCutSceneToken = new CutSceneTokenWithTitle { /*actionReadable = w.gameTitle()*/ cutSceneToken = cs.First(), }
                    ,
                room = eng.creaRoomDaDareAlClient(w, saveNames, isTextMode)
            };
            return ret;
        }

        protected IActionResult tutorialPromptImpl(TutorialPromptInput i)
        {
            try
            {
                var db = new segusumDb();
                var user = auth(i, db, out bool isTextMode);
                if (user == null)
                    return Ok(new ApiReturnVal { errore = "noauth" });

                var w = restoreWorldFromMemoryOrDisk(user.id, db, out var savegameInvalid,
                    out string[] saveNames, i.lang, isTextMode);
                if (savegameInvalid)
                    return Ok(new ApiReturnVal { ret = new SegActionRes(w.cur_time) { savegame_invalid = true } });

                if (!w.IsTutorialMode)
                    return Ok(new ApiReturnVal { ret = new SegActionRes(w.cur_time)
                    { room = w.getRoomDescForClient(saveNames, isTextMode) } });

                if (!Enum.TryParse<TutorialPromptKind>(i.tpiKind, out var kind))
                    throw new Exception("tutorial prompt: unknown kind");

                if (!w.loOfId.TryGetValue(i.tpiFirstObjectId, out var first))
                    throw new Exception("tutorial prompt: first object not found");
                LogicObj second = null;
                if (i.tpiSecondObjectId != null)
                    w.loOfId.TryGetValue(i.tpiSecondObjectId, out second);

                var context = new TutorialPromptContext
                {
                    Kind = kind,
                    IsCasual = w.IsCasual(),
                    FirstObject = first,
                    SecondObject = second,
                    ExplanationWillBeRequested = kind == TutorialPromptKind.UseWith
                        || kind == TutorialPromptKind.HideInside
                        || kind == TutorialPromptKind.DisguiseAs
                };

                var cycle = kind switch
                {
                    TutorialPromptKind.UseWith => w.tutorialBeforeUseWithPrompt(context),
                    TutorialPromptKind.UseFor => w.tutorialBeforeUseForPrompt(context),
                    TutorialPromptKind.IsActually => w.tutorialBeforeIsActuallyPrompt(context),
                    TutorialPromptKind.HideInside => w.tutorialBeforeHideInsidePrompt(context),
                    TutorialPromptKind.DisguiseAs => w.tutorialBeforeDisguiseAsPrompt(context),
                    _ => null
                };

                var cs = new CutScene(canBeSkipped: false);
                w.setCurrentCs(cs);
                try
                {
                    if (cycle != null)
                        w.execNextInCycle(cycle);
                }
                finally
                {
                    w.clearCurrentCs();
                }

                SegActionRes actionRes;
                if (cs.Count == 0)
                {
                    actionRes = new SegActionRes(w.cur_time)
                    { room = w.getRoomDescForClient(saveNames, isTextMode) };
                }
                else
                {
                    w.gs = new GameStateCutScene(cs, 0, null, null, null);
                    actionRes = new SegActionRes(w.cur_time)
                    {
                        nextCutSceneToken = new CutSceneTokenWithTitle
                        { actionReadable = null, cutSceneToken = cs.First() },
                        room = w.getRoomDescForClient(saveNames, isTextMode)
                    };
                }

                autosave(db, user, w);
                return Ok(new ApiReturnVal { ret = actionRes });
            }
            catch (Exception e)
            {
                return Ok(new ApiReturnVal { errore = UtilsW.stringOfException(e) });
            }
        }

    }
}
