let thisVersion = "54"; // 52 = sushi, tasse , yrface , milan, someone, etc. 54: matt
const gClientScriptVersion = "88";

function replaceTargetPossessive(text, target, templateSource) {
    if (text == null || !text.includes("{targetPossessive}")) return text;
    if (target == null || templateSource == null || templateSource.ofcTargetPossessiveForms == null) {
        throw new Error("Missing target possessive configuration");
    }
    const form = templateSource.ofcTargetPossessiveForms[target.ofcGender];
    if (form == null) throw new Error("Missing target possessive form for " + target.ofcGender);
    return text.replaceAll("{targetPossessive}", form);
}

function updateAndroidBottomInset() {
    const visualViewport = window.visualViewport;
    let inset = 0;
    if (visualViewport) {
        inset = Math.max(0, window.innerHeight - visualViewport.height - visualViewport.offsetTop);
    }
    if (document.documentElement && document.documentElement.style) {
        document.documentElement.style.setProperty("--segusum-bottom-inset", `${Math.ceil(inset)}px`);
    }
}

updateAndroidBottomInset();
if (window.visualViewport) {
    window.visualViewport.addEventListener("resize", updateAndroidBottomInset);
    window.visualViewport.addEventListener("scroll", updateAndroidBottomInset);
}
// Stato della stanza: deve esistere prima che le callback asincrone possano
// eseguire handleAr/loadSavedGame.
var g_last_room_desc = null;
var gSelectedObj = null;
var gLo1ChosenWasInInv = null;
var gLoHover = null;
var gDatHover = null;
var gLayerHover = null;
var gBtnPushedObj = null;
var gBtnPushedObjective = null;
var gSelectedVerb = null;
var gVerbChosen = null;
var gObjectiveChosen = null;
var gBtnPushedVerb = null;
var gLastInvNonIgnorato = null;
var gTextModeRoomTargetChosen = false;

// Accesso esplicito usato dai callback asincroni, senza dipendere dalla
// risoluzione implicita dello scope della funzione legacy.
globalThis.deselectAll = function () {
    gSelectedObj = null;
    gSelectedVerb = null;
    if (gBtnPushedObj) {
        gBtnPushedObj.removeClass("active");
        gBtnPushedObj = null;
    }
    gVerbChosen = null;
    if (gBtnPushedVerb) {
        gBtnPushedVerb.removeClass("active");
        gBtnPushedVerb = null;
    }
    gObjectiveChosen = null;
    if (gBtnPushedObjective) {
        gBtnPushedObjective.removeClass("active");
        gBtnPushedObjective = null;
    }
    $(".txtBtnHighlighted, .textVerbHighlightedYellow").removeClass("txtBtnHighlighted textVerbHighlightedYellow");
    $(".textModeInvObject, .textModeRoomObj").removeClass("active highlighted");
    $(".textModeRoomObj").addClass("disabled").removeClass("highl");
    $(".textModeChildButton").removeClass("disabled");
    if (typeof updateToolbar === "function" && g_last_room_desc != null) {
        updateToolbar();
    }
    globalThis.updateActionBarAndSelectabilityOfObjects();
};

// Helper grafico disponibile anche durante il caricamento asincrono delle
// cutscene/narrazioni.
async function changeImgSrcAndWait(img, src) {
    img.hide();
    let outsideResolve;
    let pr = new Promise((resolve) => {
        outsideResolve = resolve;
    });

    img.off('error').on('error', function () {
        outsideResolve();
    });

    img.off("load").on("load", function () {
        outsideResolve();
    });

    img.attr("src", src);
    await pr;
    img.show();
}


// --- INIZIO AGGIUNTA: Funzioni di utilità mancanti (ex import) ---
function is_touch_device1() {
    return (('ontouchstart' in window) || (navigator.maxTouchPoints > 0) || (navigator.msMaxTouchPoints > 0));
}

function delay(ms) { return new Promise(resolve => setTimeout(resolve, ms)); }

function intersectRect(r1, r2) {
    return !(r2.left > r1.right || r2.right < r1.left || r2.top > r1.bottom || r2.bottom < r1.top);
}

function newPatternReplace(w1, w2, repl, matchStart = false) {
    return { Word1: w1, Word2: w2, Repl: repl, MatchStartOfSecondWord: matchStart };
}

function head(array) { return (array && array.length) ? array[0] : null; }
function tail(array) { return (array && array.length > 1) ? array.slice(1) : []; }
function last(array) { return (array && array.length) ? array[array.length - 1] : null; }

// Estensioni prototipi (necessarie perché usate nel codice come .firstLetterToUpper())
if (!String.prototype.firstLetterToUpper) {
    String.prototype.firstLetterToUpper = function () {
        return this.charAt(0).toUpperCase() + this.slice(1);
    };
}
if (!Array.prototype.any) {
    Array.prototype.any = function (predicate) {
        return this.some(predicate || (x => x));
    };
}
// --- FINE AGGIUNTA ---


function* te() {

    for (var i = 0; i < 5; i++) {
        yield i;
    }
}




var gAnimIntervals = {};

function ant(fr, t) {
    return { anim_name: fr, anim_time: t };
}

function engine_startAnim(uniqueId, a, msBase) {

    if (typeof gAnimIntervals[uniqueId] != 'undefined' && gAnimIntervals[uniqueId] != null) {
        clearInterval(gAnimIntervals[uniqueId]);
        gAnimIntervals[uniqueId] = null;
    }


    // nascondi tutti i frame tranne il primo
    let nameNotToHide = a[0].anim_name;

    for (let ifr = 0; ifr < a.length; ifr++) {
        let nameToHide = a[ifr].anim_name;
        if (nameToHide != nameNotToHide) {
            $(`.imlayer[filename='${nameToHide}'`).hide();
        }
    }

    let iCurFrame = 0;
    let timeOfCurFrame = 0;
    let interv = setInterval(function () {
        let curFrame = a[iCurFrame];
        if (timeOfCurFrame >= curFrame.anim_time) {
            // avanza il frame
            //console.log("incremento frame");
            let iOldFrame = iCurFrame;
            iCurFrame++;
            timeOfCurFrame = 0;
            if (iCurFrame >= a.length) {
                iCurFrame = 0;
            }

            // applica la modifica nascondendo il vecchio layer e mostrando il nuovo
            {

                let oldFilename = a[iOldFrame].anim_name;
                let curFilename = a[iCurFrame].anim_name;
                $(`.imlayer[filename='${oldFilename}'`).hide();
                $(`.imlayer[filename='${curFilename}'`).show();
            }

        }
        else {
            timeOfCurFrame++;
        }

    }
        , msBase);
    gAnimIntervals[uniqueId] = interv;

    return interv;
}






//debugger;
//var res = new Lazy(te());

//let primi = res.take(6);




function noImgMode() {

    if (typeof g_last_room_desc === "undefined" || g_last_room_desc == null) {
        return true;
    }
    return g_last_room_desc.grrIsTextMode;
}

jQuery.fn.reverse = [].reverse;

function isImgMode() {
    return !noImgMode();
}

function getLang() {
    return gLang;
}


var gGameId;

if (gGameIdStr == '') {
    gGameId = null;
}
else {
    gGameId = parseInt(gGameIdStr);
}

let credentialsId = `credentials_${gGameId}`;
let sessionTokenId = `session_token_${gGameId}`;
let g_tutorialMode = false;
let g_afterTutorialPrompt = null;
function setTutorialMode(mode) {
    g_tutorialMode = mode === true;
    $("#btnPlayTutorial").text(g_tutorialMode ? "Esci dal tutorial" : "Gioca tutorial");
    if (localStorage[credentialsId]) {
        const c = JSON.parse(localStorage[credentialsId]);
        c.tutorialMode = g_tutorialMode;
        localStorage[credentialsId] = JSON.stringify(c);
    }
}
if (localStorage[credentialsId]) {
    try { g_tutorialMode = JSON.parse(localStorage[credentialsId]).tutorialMode === true; } catch (_) { }
}
let versionId = `version_${gGameId}`;

console.log('credentials id = ', credentialsId);

var gStopEndTitles = null;

var itIsTheUserWhoClosedDialogTextInput = null;

var gIsNarrowScreen;
var gIsReadingHint = null;
var gQuantiHintEranoVisibiliUltimaVoltaPerEnigma = {};
var gCurScale = 1.0;

//var htBarraInv = 310; // 337 - 377 - oppure 448 deve coincidere col css. ho fatto così perché usando jquery non sottraeva la toolbar di chrome. su mobile penso.

var gLoHoverManualRect = null;
var gTemplateRadioExplanation;
var gTemplateImgTarget;
var gTemplateImgTargetExit;
var gTemplateImgTargetExitDown;
var gTemplateDialogChoice;
var gFrozenMouse = false;

console.log('prefisso = ', prefissoWebApi);








String.prototype.fondiParole = function () {
    let seqfrase = this;

    if (getLang() != "it") {
        return this;
    }
    //debugger;
    let spl = seqfrase.split(' '); // seqfrase.Split(new [] { " ", "\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
    let patterns = [
        newPatternReplace("in", "la", "nella"),
        newPatternReplace("in", "il", "nel"),
        newPatternReplace("in", "lo", "nello"),
        newPatternReplace("in", "le", "nelle"),
        newPatternReplace("in", "i", "nei"),
        newPatternReplace("in", "gli", "negli"),
        newPatternReplace("in", "l'", "nell'", true), // non funziona se nonmatcho l'inizio della seconda parola, la parola è    "l'albero"


        newPatternReplace("su", "la", "sulla"),
        newPatternReplace("su", "il", "sul"),
        newPatternReplace("su", "lo", "sullo"),
        newPatternReplace("su", "le", "sulle"),
        newPatternReplace("su", "i", "sui"),
        newPatternReplace("su", "gli", "sugli"),
        newPatternReplace("su", "l'", "sull'", true),

        newPatternReplace("da", "la", "dalla"),
        newPatternReplace("da", "il", "dal"),
        newPatternReplace("da", "lo", "dallo"),
        newPatternReplace("da", "le", "dalle"),
        newPatternReplace("da", "i", "dai"),
        newPatternReplace("da", "gli", "dagli"),
        newPatternReplace("da", "l'", "dall'", true),


        newPatternReplace("di", "la", "della"),


        newPatternReplace("a", "il", "al"),
        newPatternReplace("a", "gli", "agli"),
        newPatternReplace("a", "lo", "allo"),
        newPatternReplace("a", "la", "alla"),
        newPatternReplace("a", "le", "alle"),
        newPatternReplace("a", "i", "ai"),
        newPatternReplace("a", "l'", "all'", true)

    ];



    for (let j = 0; j < spl.length - 1; j++) {
        let cur = spl[j];
        let next = spl[j + 1]; // potrebbe essere   "l'albero"
        //debugger;
        let patterns1 = patterns.filter(p => !p.MatchStartOfSecondWord && p.Word1 == cur && p.Word2 == next);
        let patterns2 = patterns.filter(p => p.MatchStartOfSecondWord && p.Word1 == cur && next.startsWith(p.Word2));

        if (patterns1.length > 0) {
            let pa = patterns1[0];
            spl.splice(j, 1); // spl.RemoveAt(j);
            spl.splice(j, 1); // spl.RemoveAt(j);
            spl.splice(j, 0, pa.Repl);  //spl.Insert(j, pa.Repl);
        }
        else if (patterns2.length > 0) {
            let pa2 = patterns2[0];
            spl.splice(j, 1); // spl.RemoveAt(j);
            spl.splice(j, 1); // spl.RemoveAt(j);

            let nellalbero = next.replace(pa2.Word2, pa2.Repl);
            spl.splice(j, 0, nellalbero); //spl.Insert(j, nellalbero);
        }
    }

    seqfrase = spl.join(" "); // spl.aggregateStringList( " ");
    return seqfrase;
}


//let test2 = fondiParole("in la mamma");

//debugger;



function disabilitaTuttoTemporaneamenteMentreVediFrase() {
    $(".btnTextModeRight, .textModeRoomObj , .quivedi, .dovesei, .textModeIntroAzioniOgggetti.azioni").addClass("tempDisabled");
}
function riabilitaTuttoTemporaneamenteDisabilitatoFrase() {
    $(".btnTextModeRight, .textModeRoomObj , .quivedi, .dovesei, .textModeIntroAzioniOgggetti.azioni").removeClass("tempDisabled");
}
function calcolaTempoFrase(fullText) {
    let tempoFrasiBrevi = 1000;
    let tempoFrasiLunghe = 2400;
    // Frasi campione usate solo per calibrare il tempo di visualizzazione:
    // non devono dipendere dai contenuti di uno specifico gioco.
    let fraseBreve = "parla con qualcuno";
    let fraseLunga = "travestiti da qualcuno per superare l'ostacolo";

    let y0 = tempoFrasiBrevi;
    let y1 = tempoFrasiLunghe;
    let x = fullText.length;
    let x0 = fraseBreve.length;
    let x1 = fraseLunga.length;

    let m = (y1 - y0) / (x1 - x0);

    let tempo = y0 + m * (x - x0);
    return tempo;
}

function rebuildDialogChoicesMenu(loSecondo) {
    let wtInv = $(".invBar").width();
    $(".dialogChoicesOuter").width(wtInv);
    $(".dialogCloseOuter").width(wtInv);

    // La didascalia segue il mouse dentro la stanza, ma non deve restare
    // sotto il pannello nero delle scelte di dialogo.
    $(".btnOggettoInRoom").remove();
    $(".dialogChoiceOuterFullScreen").show();

    $(".dialogChoiceTemplate").remove();
    for (let io of g_last_room_desc.grrInvObjects) {
        // devo mostrare solo quelli che prima erano concetti, ma che non sono verbi custom. quindi erano per forza voci di dialogo
        if (io.ofcIsConversationTopic) {
            let ioFissato = io;
            let newel = gTemplateDialogChoice.clone();
            newel.appendTo(".dialogChoicesOuter");

            { // imposta il testo
                let nomeLoSecondo;
                if (loSecondo.ofcNameWithArticle != null && loSecondo.ofcNameWithArticle != "") {
                    nomeLoSecondo = loSecondo.ofcNameWithArticle;
                }
                else {
                    nomeLoSecondo = loSecondo.ofc_name;
                }
                let testo = replaceTargetPossessive(io.ofcVerbWhenUseWithAsFirstObjectSelectedWithPlaceHolderOnHoverSecond, loSecondo, io).replace("{1}", nomeLoSecondo);
                testo = testo.firstLetterToUpper().fondiParole();
                newel.find(".dialogText").text(testo);
            }

            { // imposta l'icona

                newel.find(".dialogImg").attr('src', `${prefissoWebApi}/${gInvIconsFolder}/${io.loId}.png`)

            }




            newel.click(async e => {
                //debugger;


                // devo fare finta che sia stato selezionato prima questo logicobject e poi lo.




                let mouseX = e.clientX;
                let mouseY = e.clientY;


                gSelectedObj = ioFissato; // non io, se no resta l'ultimo del ciclo

                // La scelta è stata fatta: il pannello non deve rimanere
                // sopra alla frase e al dialogo successivo.
                $(".dialogChoiceOuterFullScreen").hide();


                await onLoClickedRoom(loSecondo, mouseX, mouseY);
            });



        }
    }



    $(".dialogCloseInner").off('click').on('click', e => {
        e.preventDefault();
        e.stopPropagation();
        $(".dialogChoiceOuterFullScreen").hide();
    });
}

function updateToolbar(roomDesc = null) {
    if (gFrozenMouse) return;

    if (roomDesc == null) {
        roomDesc = g_last_room_desc;
    }

    $(".invIcon").removeClass('highlighted');
    $(".invIcon .selectorIcon").hide();
    if (gSelectedVerb == 'pickup') {
        $(".invIcon.pickUpIcon").addClass('highlighted');
        $(".invIcon.pickUpIcon .selectorIcon").show();
    }
    else if (gSelectedVerb == 'remember') {
        //$(".invIcon.rememberIcon").addClass('highlighted');
        //$(".invIcon.pickUpIcon .selectorIcon").show();
    }
    else if (gSelectedVerb == 'look') {
        $(".invIcon.eyeIcon").addClass('highlighted');
        $(".invIcon.eyeIcon .selectorIcon").show();
    }
    else if (gSelectedVerb == 'talk') {
        $(".invIcon.talkIcon").addClass('highlighted');
        $(".invIcon.talkIcon .selectorIcon").show();
    }
    else if (gSelectedVerb == 'deduce') {
        $(".invIcon.deduceIcon").addClass('highlighted');
        $(".invIcon.deduceIcon .selectorIcon").show();
    }
    else if (gSelectedVerb == 'use') {
        $(".invIcon.useIcon").addClass('highlighted');
        $(".invIcon.useIcon .selectorIcon").show();
    }
    else if (gSelectedVerb == null) {
        //$(".invIcon.eyeIcon").addClass('highlighted');
    }
    else {
        console.error('jhr');
        debugger;
    }





    if (noImgMode()) {


        function updateTextRoomIconsEnabled() {
            let ofcInCurRoom = g_last_room_desc.grrRooms[g_last_room_desc.grrCurRoomId].rfc_objects;




            $(".textModeRoomObj ").each((i, el0) => {
                let el = $(el0);
                let loId = el.attr("lo_id");

                let ofcs = ofcInCurRoom.filter(ofc => ofc.loId == loId);
                if (ofcs.length == 0) {
                    // questo succede se hai cliccato "risolvi mistero richiamo" e lei si è spostata cambiando locazione. quindi 
                    // ofcInCurRoom è cambiato, ma i pulsanti non si sono ancora aggiornati.
                    // ad esempio loId = "fango" o anche "diga", ma non c'è un pulsante per il fango o per la diga
                    // risolvo non facendo niente.
                    //debugger;
                }
                else {

                    let ofc = ofcs[0];
                    if (gSelectedVerb == 'pickup') {
                        if (ofc.ofcIsPickableNow) {
                            el.removeClass("disabled");
                            el.addClass('highl');
                        }
                        else {
                            el.addClass("disabled");
                            el.removeClass('highl');
                        }
                    }
                    else if (gSelectedVerb == 'remember') {
                        if (ofc.ofc_can_be_remembered) {
                            el.removeClass("disabled");
                            el.addClass('highl');
                        }
                        else {
                            el.addClass("disabled");
                            el.removeClass('highl');
                        }
                    }
                    else if (gSelectedVerb == null) {
                        if (gSelectedObj == null) {
                            el.addClass("disabled");
                            el.removeClass('highl');
                            //if (ofc.ofcVerbIdWhenInRoom == 'useFor' || ofc.ofcVerbIdWhenInRoom == 'isActually' || ofc.ofcVerbIdWhenInRoom == 'useHere' || ofc.ofcVerbIdWhenInRoom == 'showMap') {
                            //        el.removeClass("disabled");
                            //}
                            //else {
                            //        el.addClass("disabled");
                            //}
                        }
                        else {
                            // tutto può essere cliccabile come target, tranne in text mode, dove alcuni oggetti sono lì solo per farti sapere che ci sono, perche' non hai la grafica
                            // Le voci dialogiche ("di' che sei...", "chiedi...")
                            // hanno come secondo oggetto soltanto un Character.
                            // Il client grafico applica già questo filtro nel
                            // context menu; deve valere anche nell'interfaccia
                            // testuale, altrimenti si possono comporre frasi
                            // come "di' che sei la nipote alla fontana".
                            const dialogTopicWithNonCharacterTarget =
                                gSelectedObj.ofcIsConversationTopic && !ofc.ofc_is_character;

                            if (!ofc.ofcCanBeUsedAsTargetInTextMode || dialogTopicWithNonCharacterTarget) {
                                el.addClass("disabled");
                                el.removeClass('highl');
                            }
                            else {

                                el.removeClass("disabled");
                                el.addClass('highl');
                            }
                        }
                    }
                    else {
                        console.error("not impl");
                        debugger;
                    }
                }
            });




        }

        updateTextRoomIconsEnabled();

        // Room entries are never direct targets in the main text list. The
        // target selection happens in the dedicated modal; action buttons
        // such as "Usa per" remain independently clickable.
        $(".textModeRoomObj").addClass("disabled").removeClass("highl");
        $(".textModeChildButton").removeClass("disabled");




        $(".textModeInvObject").each((i, el0) => {
            let el = $(el0);
            let loId = el.attr("lo_id");

            let ofcs = g_last_room_desc.grrInvObjects.filter(ofc => ofc.loId == loId);
            if (ofcs.length == 0) {
                debugger;
            }

            let ofc = ofcs[0];
            if (gSelectedVerb == 'pickup') {
                //if (ofc.ofcIsPickableNow) {
                //        el.removeClass("disabled");
                //}
                //else {
                el.addClass("disabled");

                $(".textModeChildButton ").removeClass("disabled");

                //}
            }
            else if (gSelectedVerb == 'remember') {
                //debugger;
                if (ofc.ofc_can_be_remembered) {
                    el.removeClass("disabled");
                }
                else {
                    el.addClass("disabled");
                }

                $(".textModeChildButton ").removeClass("disabled");

                //}
            }
            else if (gSelectedVerb == null) {
                if (gSelectedObj == null) {
                    el.removeClass("disabled");

                    el.removeClass("txtBtnHighlighted");

                    $(".textModeChildButton ").removeClass("disabled");
                }
                else {
                    // tutto può essere cliccabile come target
                    if (gSelectedObj.loId == ofc.loId) {
                        el.removeClass("disabled");
                    }
                    else {
                        el.addClass("disabled");
                    }


                    if (gSelectedObj.loId == ofc.loId) {
                        el.addClass("txtBtnHighlighted");
                    }
                    else {
                        el.removeClass("txtBtnHighlighted");
                    }



                    $(".textModeChildButton ").removeClass("disabled");
                }
            }
            else {
                console.error("not impl");
                debugger;
            }
        });




        if (gSelectedVerb == 'pickup') {
            $(".btnTextPickup").addClass("txtBtnHighlighted");

            $(".textModeInvObject").removeClass("txtBtnHighlighted");

            $(".btnTextRicorda").addClass("disabled");
        }
        else if (gSelectedVerb == 'remember') {
            $(".btnTextRicorda").addClass("txtBtnHighlighted");

            $(".textModeInvObject").removeClass("txtBtnHighlighted");

            $(".btnTextPickup").addClass("disabled");
        }
        else {

            if (gSelectedObj != null) {
                $(".btnTextPickup").addClass("disabled");
                $(".btnTextRicorda").addClass("disabled");
            }
            else {
                $(".btnTextPickup").removeClass("disabled");
                $(".btnTextRicorda").removeClass("disabled");
            }


            $(".btnTextPickup").removeClass("txtBtnHighlighted");
            $(".btnTextRicorda").removeClass("txtBtnHighlighted");
        }



        if (g_last_room_desc.grrTalkNow) {
            if (gSelectedObj != null) {
                $(".btnTextTalk").addClass("disabled");
            }
            else {

                if (gSelectedVerb != null) {
                    $(".btnTextTalk").addClass("disabled");
                }
                else {
                    $(".btnTextTalk").removeClass("disabled");
                }

            }


            $(".btnTextTalk").addClass("textVerbHighlightedYellow");
        }
        else {
            $(".btnTextTalk").addClass("disabled");
            $(".btnTextTalk").removeClass("textVerbHighlightedYellow");
        }



        if (g_last_room_desc.grrObjectives.any(o => !o.obcWasSeen)) {
            $(".btnTextDiario").addClass("textVerbHighlightedYellow");
        }
        else {
            $(".btnTextDiario").removeClass("textVerbHighlightedYellow");
        }


        if (gSelectedObj != null) {
            $(".btnTextWalk").addClass("disabled");
        }
        else {
            if (gSelectedVerb != null) {
                $(".btnTextWalk").addClass("disabled");
            }
            else {
                $(".btnTextWalk").removeClass("disabled");
            }
        }



        if (gSelectedObj != null) {
            $(".btnTextDiario").addClass("disabled");
        }
        else {
            if (gSelectedVerb != null) {
                $(".btnTextDiario").addClass("disabled");
            }
            else {
                $(".btnTextDiario").removeClass("disabled");
            }
        }


        if (gSelectedObj != null) {
            $(".btnTextOpzioni").addClass("disabled");
        }
        else {
            if (gSelectedVerb != null) {
                $(".btnTextOpzioni").addClass("disabled");
            }
            else {
                $(".btnTextOpzioni").removeClass("disabled");
            }
        }


    }

    // anche gli oggetti possono essere highligted
    if (gSelectedObj != null) {
        let el = $(`.invIcon[lo_id='${gSelectedObj.loId}']`);
        el.addClass("highlighted");

        el.find(".selectorIcon").show();
    }


    updateInvObjectsdDisabled();


    // vedi se devi illuminare l'icona obiettivi



    if (roomDesc.grrObjectives.any(o => !o.obcWasSeen)) {
        $(".objectivesIcon .invImg").attr('src', prefissoWebApi + `/${gInvIconsFolder}/notes-yellow.png`);
    }
    else {
        $(".objectivesIcon .invImg").attr('src', prefissoWebApi + `/${gInvIconsFolder}/notes-normal.png`);
    }









}

function mostraPleaseWait() {
    //debugger;
    $("#waitingServer").removeClass('nascosto');
    scheduleClientWaitDiagnostic();
    $(".roomAndInv ").hide();
    $(".invBar").css('visibility', 'hidden');
    $("#pleaseWait").removeClass("opacity-grow").addClass("opacity-grow");
    $(".divLayersContainer").addClass('cursorWait');
    $("#dialogUseFor").addClass('cursorWait');
    $("#dialogInputText").addClass('cursorWait');
    $("#dialogChooseExplanation").addClass('cursorWait');

    $(".radio label, .checkbox label").addClass('cursorWait');

    $("#dialogUseFor .modal-content").addClass('cursorWait');
    $("#dialogInputText .modal-content").addClass('cursorWait');
    $("#dialogChooseExplanation .modal-content").addClass('cursorWait');
    $("input").addClass('cursorWait');
    $("select").addClass('cursorWait');
    $("button").addClass('cursorWait');
    //$("#dialogUseFor .modal-content").css("cursor", "default");
    //$("span.testoRadioExplan").css("cursor", "default");
    //$(".radio.templateRadioExplanation").css("cursor", "default");
    //$("#submitUseFor").css('cursor', "default");
    //debugger;
}

function nascondiPleaseWait() {
    $("#waitingServer").addClass('nascosto');
    if (g_clientWaitDiagnosticTimer) {
        clearTimeout(g_clientWaitDiagnosticTimer);
        g_clientWaitDiagnosticTimer = null;
    }
    //$(".roomAndInv ").show();
    $(".invBar").css('visibility', 'visible');
    $(".divLayersContainer").removeClass('cursorWait');

    $(".radio label, .checkbox label").removeClass('cursorWait');

    $("#dialogUseFor").removeClass('cursorWait');
    $("#dialogInputText").removeClass('cursorWait');
    $("#dialogChooseExplanation").removeClass('cursorWait');

    $("#dialogUseFor .modal-content").removeClass('cursorWait');
    $("#dialogInputText .modal-content").removeClass('cursorWait');
    $("#dialogChooseExplanation .modal-content").removeClass('cursorWait');
    $("input").removeClass('cursorWait');
    $("select").removeClass('cursorWait');
    $("button").removeClass('cursorWait');
}

var gCurTime = null;
function getCurTime() {

    //let curTime;
    //if (g_last_room_desc !== null)
    //        curTime = getCurTime();
    //else
    //{
    //        curTime = null;
    //}

    //return curTime;
    return gCurTime;
}


function onMouseMoveComposer(e) {

    e.preventDefault();
    e.stopPropagation();
    if (gDraggingElement != null) {
        //debugger;
        //console.log('mousemove composer in', gDragOffsetX);
        let screenX = e.clientX;
        let screenY = e.clientY;

        //console.log('screenx = ', screenX);

        screenX -= gDragOffsetX;
        screenY -= gDragOffsetY;


        let re = $(".bodyNewComposer")[0].getBoundingClientRect();
        //console.log('rect' , re);
        let parentX = re.left;
        let parentY = re.top;

        screenX -= parentX;
        screenY -= parentY;


        gDraggingElement.css('left', screenX);
        gDraggingElement.css('top', screenY);
    }
}



function rebuildComposer() {



    //$(".btnComposerAvanti ").addClass('disabled');

    $(".fraseConBuchi").remove();

    if (!gComposerInSayMode) {
        let fraseuse1 = "deduceSomethingAbout".tr().replace('{1}', gLoOfComposer.ofc_name).firstLetterToUpper() + '...';
        $(".composerUsaPerTitolo").text(fraseuse1);
    }
    else {
        let fraseuse1 = "saySomethingTo".tr().replace('{1}', gLoOfComposer.ofc_name).firstLetterToUpper() + '...';
        $(".composerUsaPerTitolo").text(fraseuse1);
    }

    //debugger;
    gTemplatesToUse = g_last_room_desc.grrTemplates

        //gComposerInSayMode ? g_last_room_desc.grrTemplates.filter(te => te.IsForSayVerb) : g_last_room_desc.grrTemplates.filter(te => !te.IsForSayVerb)
        ;


    //debugger;
    if (typeof g_last_room_desc.grrTemplatesToExcludeOfObj[gLoOfComposer.loId] != 'undefined') {
        gTemplatesToUse = gTemplatesToUse.filter(te => !g_last_room_desc.grrTemplatesToExcludeOfObj[gLoOfComposer.loId].includes(te.teId));
    }



    if (gLoOfComposer.ofc_is_character) {
        gTemplatesToUse = gTemplatesToUse.filter(te => te.isForChars);
    }
    else {
        gTemplatesToUse = gTemplatesToUse.filter(te => !te.isForChars);
    }


    gFillersToUse = g_last_room_desc.grrFillers
        //gComposerInSayMode ? g_last_room_desc.grrFillers.filter(te => te.IsForSayVerb) : g_last_room_desc.grrFillers.filter(te => !te.IsForSayVerb)
        ;

    let fraseCorrente = gTemplatesToUse[gICurTemplateInComposer];
    //debugger;
    let fraseDaSplittare = fraseCorrente.heShe;

    function parsaComposer(fraseDaSplittare) {
        let fraseDaParsareCur = fraseDaSplittare;

        let fraseParsataCur = [];

        while (fraseDaParsareCur !== "") {
            let prossimoDaCercare;
            if (fraseDaParsareCur.includes("{1}")) {
                prossimoDaCercare = "{1}";
            }
            else {
                prossimoDaCercare = "{2}";
            }

            let splittata = fraseDaParsareCur.split(prossimoDaCercare);

            if (splittata.length == 2) // se conteneva il numero
            {
                if (splittata[0] == "") {
                    fraseParsataCur.push({ oggetto: '', isStringa: false });
                    fraseParsataCur.push({ stringa: splittata[1], isStringa: true });
                }
                else {
                    fraseParsataCur.push({ stringa: splittata[0], isStringa: true });
                    fraseParsataCur.push({ oggetto: '', isStringa: false });

                }


                fraseDaParsareCur = splittata[1];
            }
            else // se non lo contenteva
            {
                fraseParsataCur.push({ stringa: splittata[0], isStringa: true });
                break;

            }
        }
        return fraseParsataCur;
    }

    let fraseParsata = parsaComposer(fraseDaSplittare);

    let newFraseElement = $("<div class='fraseConBuchi'>").appendTo(".parteConFrasiIncomplete");
    newFraseElement.attr("template_id", fraseCorrente.teId);

    for (let x of fraseParsata) {
        if (x.isStringa) {
            var newPezzo = $("<div class='pezzoComp pezzoNonCliccab'>");


            let stringa = x.stringa.firstLetterToUpper();

            if (stringa.includes("{S}")) {
                stringa = stringa.replace("{S}", `<span class='nomeoggcomposer'>${gLoOfComposer.ofc_name}</span>`);
            }

            // istanzia il pronome
            if (stringa.includes("{P}")) {
                if (gLoOfComposer.ofcGender == 'he') {
                    stringa = stringa.replace("{P}", 'egli'.tr());
                }
                else if (gLoOfComposer.ofcGender == 'she') {
                    stringa = stringa.replace("{P}", 'ella'.tr());
                }
                else if (gLoOfComposer.ofcGender == 'it') {
                    stringa = stringa.replace("{P}", 'essoSogg'.tr());
                }
                else if (gLoOfComposer.ofcGender == 'they') {
                    stringa = stringa.replace("{P}", 'essiSogg'.tr());
                }
                else {
                    console.error("jfdkjkgf");
                }
            }

            if (stringa.includes("{PO}")) {
                if (gLoOfComposer.ofcGender == 'he') {
                    stringa = stringa.replace("{PO}", 'lui'.tr());
                }
                else if (gLoOfComposer.ofcGender == 'she') {
                    stringa = stringa.replace("{PO}", 'lei'.tr());
                }
                else if (gLoOfComposer.ofcGender == 'it') {
                    stringa = stringa.replace("{PO}", 'essoOgg'.tr());
                }
                else if (gLoOfComposer.ofcGender == 'they') {
                    stringa = stringa.replace("{PO}", 'essiOgg'.tr());
                }
                else {
                    console.error("jfdkjkgf 2");
                }
            }

            newPezzo.html(stringa);
            newPezzo.appendTo(newFraseElement);
        }
        else {
            var newPezzo = $("<div class='pezzoComp pezzoCliccab'>");
            var questionCircle = $('<i class="fas fa-question-circle questionCircle"></i>').appendTo(newPezzo);
            newPezzo.appendTo(newFraseElement);
        }
    }


    // ora gli elementi draggabili
    $(".templateObjectComposerNew").remove();
    let curRowWt = 0;

    //debugger;
    let quantiSonoIFiller = gFillersToUse.length;
    let quantiPerRiga = quantiSonoIFiller;
    let tempEl = templateObjectComposerNew.clone().appendTo(".composerUpper2");
    let wtFiller = tempEl.outerWidth(true);
    tempEl.remove();
    let wtDispo = $(".composerUpper2").width();




    //debugger;
    let quantiMettoInUnaRiga;
    if (quantiSonoIFiller * wtFiller < wtDispo) {
        quantiMettoInUnaRiga = quantiSonoIFiller;

    }
    else {
        let quantiEntranoInRigaCur = Math.floor(wtDispo / wtFiller);

        while (true) {
            let quantiRestanoUltimaRiga = quantiSonoIFiller % quantiEntranoInRigaCur;

            let quanteSonoLeRighe = Math.ceil(quantiSonoIFiller / quantiEntranoInRigaCur);
            if (quanteSonoLeRighe > 3) {
                // si sta alzando troppo . non mi importa più se la quarta riga ha filler appesi.
                quantiMettoInUnaRiga = quantiSonoIFiller;
                break;
            }

            if (quantiRestanoUltimaRiga == 0) {
                // risultato perfetto
                quantiMettoInUnaRiga = quantiEntranoInRigaCur;
                break;
            }
            else if (quantiEntranoInRigaCur - quantiRestanoUltimaRiga > 1) {
                if (quantiEntranoInRigaCur > 1) {
                    quantiEntranoInRigaCur--;
                }
                else {
                    quantiMettoInUnaRiga = quantiSonoIFiller;
                    break;
                }
            }
            else {
                quantiMettoInUnaRiga = quantiEntranoInRigaCur;
                break;
            }
        }
    }


    //let divisore = 1;
    //debugger;
    //while (true)
    //{

    //        if (quantiPerRiga * wtFiller < wtDispo)
    //        {
    //                break;

    //        }
    //        else
    //        {
    //                let orfani = quantiSonoIFiller % 
    //                divisore++;
    //                quantiPerRiga = Math.floor(quantiPerRiga / divisore);
    //        }
    //}

    //console.log("quanti per riga = ", quantiMettoInUnaRiga);

    let cur = 0;
    for (let x of gFillersToUse) {
        let newElFiller = templateObjectComposerNew.clone();

        //let utwt = newElFiller.outerWidth(true);

        //if (utwt + curRowWt > $(".composerUpper").width())
        //{
        //        //console.log('wt = ', utwt);
        //        $("<div class='acapo'>").appendTo(".composerUpper");
        //        curRowWt = 0;
        //}

        //curRowWt += utwt;


        newElFiller.find(".imgfacefiller").attr('src', x.Icon);
        newElFiller.appendTo(".composerUpper2");

        newElFiller.attr("filler_id", x.FilId);

        newElFiller.find('.nomeoggpersonacomp').text(x.Name);

        newElFiller.mousedown(function (e) {

            if ($(e.currentTarget).hasClass('dropped')) {
                return;
            }
            //console.log('mousedown', e);
            $(".beingDragged").remove();

            gDraggingElement = $(e.currentTarget).clone().addClass('beingDragged');



            gDraggingElement.css('position', 'absolute');
            gDraggingElement.css('margin', '0'); // altrimenti il drag e' sfasato del margine
            gDragOffsetX = e.offsetX;
            gDragOffsetY = e.offsetY;



            $(".bodyNewComposer").append(gDraggingElement);

            onMouseMoveComposer(e);


        });


        cur++;
        if (cur == quantiMettoInUnaRiga) {

            $("<div class='acapo'>").appendTo(".composerUpper2");
            cur = 0;
        }

    }



    updatePlayArrowComposer();
}

//function isElementInView (element, fullyInView = true)
//{
//        var pageTop = $(window).scrollTop();
//        var pageBottom = pageTop + window.innerHeight;
//        var elementTop = $(element).offset().top;
//        var elementBottom = elementTop + $(element).height();

//        if (fullyInView === true)
//        {
//                return ((pageTop < elementTop) && (pageBottom > elementBottom));
//        } else
//        {
//                return ((elementTop <= pageBottom) && (elementBottom >= pageTop));
//        }
//}
function visibleY(el, container) {
    let elInner = el.getBoundingClientRect();
    let elOuter = container.getBoundingClientRect();

    let bottomInner = elInner.top + elInner.height;
    let bottomOuter = elOuter.top + elOuter.height;
    let dentroParteSotto = bottomInner <= bottomOuter;

    // se la parte sotto non è visibile per 0.qualcosa pixel, dico che è visibile, perché on riesco a scrollare... né di 0 virgola, né di 1.
    let debugDiQuantoNonEVisibileSotto;
    if (!dentroParteSotto) {
        debugDiQuantoNonEVisibileSotto = bottomOuter - bottomInner;// mi aspetto sia positivo

        if (Math.abs(debugDiQuantoNonEVisibileSotto) < 1) {
            dentroParteSotto = true;
        }
    }

    let vis = elInner.top >= elOuter.top
        && dentroParteSotto;

    return vis;
}

function visibleX(el, container) {
    let elInner = el.getBoundingClientRect();
    let elOuter = container.getBoundingClientRect();

    let bottomInner = elInner.left + elInner.width;
    let bottomOuter = elOuter.left + elOuter.width;
    let dentroParteSotto = bottomInner <= bottomOuter;

    // se la parte sotto non è visibile per 0.qualcosa pixel, dico che è visibile, perché on riesco a scrollare... né di 0 virgola, né di 1.
    let debugDiQuantoNonEVisibileSotto;
    if (!dentroParteSotto) {
        debugDiQuantoNonEVisibileSotto = bottomOuter - bottomInner;// mi aspetto sia positivo

        if (Math.abs(debugDiQuantoNonEVisibileSotto) < 1) {
            dentroParteSotto = true;
        }
    }

    let vis = elInner.left >= elOuter.left
        && dentroParteSotto;

    return vis;
}


//var visibleY = function (el)
//{
//        let  rect = el.getBoundingClientRect(), top = rect.top, height = rect.height;

//        el = el.parentNode;

//        // Check if bottom of the element is off the page
//        if (rect.bottom < 0) return false;

//        // Check its within the document viewport
//        if (top > document.documentElement.clientHeight) return false;

//        do
//        {
//                rect = el.getBoundingClientRect();
//                if (top <= rect.bottom === false) return false;
//                // Check if the element is out of view due to a container scrolling
//                if (top + height <= rect.top) return false;
//                el = el.parentNode;
//        } while (el !== document.body);

//        return true;
//};

function testUpdate() {
    $(".bloccoDaScegliereSentence").off("resize").resize(async function () {
        //console.log("resizeddkjd");
        //updateResponsiveClasses();

        //await rebuildGraphics();

        $(".scrollanteConPulsanti").each((i, el0) => {
            aggiornaPulsantiScroll(el0);
        });
        //$("#imgContainer").height($(window).height() * 0.15);
    });


}

// devi passare el0 = il contenitore degli oggetti, non dei pulsanti. i pulsanti se li va a cercare nel parent di el0
function aggiornaPulsantiScroll(el0) {

    //debugger;
    //$(".scrollanteConPulsanti").each((i, el0) =>
    {
        let cosaScrollare = $(el0);

        let isHoriz = cosaScrollare.hasClass('horiz');

        let curpos;

        if (isHoriz) {
            curpos = cosaScrollare.scrollLeft();
        }
        else {
            curpos = cosaScrollare.scrollTop();
        }

        //console.log("aggiornaPulsantiScroll: scroll attuale", curpos);
        let parent = cosaScrollare.parent();

        //let maxHtViewport;

        //if (isHoriz)
        //{
        //        maxHtViewport = cosaScrollare.outerWidth(true);
        //}
        //else
        //{
        //        maxHtViewport = cosaScrollare.outerHeight(true);
        //}

        //let maxHtLista; 
        //if (isHoriz)
        //{
        //        maxHtLista = parent.find(".scrollanteConPulsanti").children().first().outerWidth(true);
        //}
        //else
        //{
        //        maxHtLista = parent.find(".scrollanteConPulsanti").children().first().outerHeight(true);
        //}


        //let maxScrollY = Math.floor(maxHtLista - maxHtViewport);

        //console.log("max scroll ", maxScrollY);

        let btnDown = parent.find(".scrollButtonMio.scrollDown");

        let btnUp = parent.find(".scrollButtonMio.scrollUp");



        let containerCheScrollaNonSiAllunga = cosaScrollare;
        let elementiDaScrollare = cosaScrollare.find(".scrollingItem");

        let nonServeIlPulsanteGiu;
        let iUltimoElementoDaScrollareFullyInView = null;
        {
            //debugger;

            for (let i = 0; i < elementiDaScrollare.length; i++) {
                let el = elementiDaScrollare[i];

                let visible;
                if (isHoriz) {
                    visible = visibleX(el, containerCheScrollaNonSiAllunga[0]);
                }
                else {
                    visible = visibleY(el, containerCheScrollaNonSiAllunga[0]);
                }

                if (visible) {
                    iUltimoElementoDaScrollareFullyInView = i;
                }
            }

            nonServeIlPulsanteGiu = (iUltimoElementoDaScrollareFullyInView === elementiDaScrollare.length - 1);
        }

        let debugUltimoElementoFullyInView;
        if (iUltimoElementoDaScrollareFullyInView != null) {
            debugUltimoElementoFullyInView = elementiDaScrollare[iUltimoElementoDaScrollareFullyInView];
        }

        if (!nonServeIlPulsanteGiu) //(Math.ceil( curpos) < maxScrollY)
        {
            btnDown.removeClass("disabled");
        }
        else {
            btnDown.addClass("disabled");
        }





        let nonServeIlPulsanteSu;
        {
            //debugger;
            let iUltimoElementoDaScrollareFullyInView = null;
            for (let i = elementiDaScrollare.length - 1; i >= 0; i--) {
                let el = elementiDaScrollare[i];

                let visible;
                if (isHoriz) {
                    visible = visibleX(el, containerCheScrollaNonSiAllunga[0]);
                }
                else {
                    visible = visibleY(el, containerCheScrollaNonSiAllunga[0]);

                }
                if (visible) {
                    iUltimoElementoDaScrollareFullyInView = i;
                }
            }


            let debugUltimoFullyVisible = elementiDaScrollare[iUltimoElementoDaScrollareFullyInView];

            nonServeIlPulsanteSu = (iUltimoElementoDaScrollareFullyInView === 0);
        }

        if (!nonServeIlPulsanteSu/* curpos > 0*/) {
            //btnUp.show();
            btnUp.removeClass("disabled");
        }
        else {
            //btnUp.hide();
            btnUp.addClass("disabled");
        }

    }
    //);
}


//function aggiornaPulsantiScrollTutti()
//{
//        aggiornaPulsantiScroll(".scrollanteConPulsanti");
//}
function shuffle(array) {
    var currentIndex = array.length, temporaryValue, randomIndex;

    // While there remain elements to shuffle...
    while (0 !== currentIndex) {

        // Pick a remaining element...
        randomIndex = Math.floor(Math.random() * currentIndex);
        currentIndex -= 1;

        // And swap it with the current element.
        temporaryValue = array[currentIndex];
        array[currentIndex] = array[randomIndex];
        array[randomIndex] = temporaryValue;
    }

    return array;
}




//debugger;
if (localStorage[versionId] === undefined) {
    localStorage.removeItem(credentialsId);

}
else {
    let ver = localStorage[versionId];
    if (ver !== thisVersion) {
        localStorage.removeItem(credentialsId);

    }
}
localStorage[versionId] = thisVersion;



var templateOggettoPicker;
var templateOggettoPickerNonClic;
var templateInvObject;
var templateObjectComposerNew;

var gFraseCompostaFinora = [];
var gQualePersonaForNext = "heShe";

var gContinuationKindAfterObject = null;
var gContinuationsAfterObject = [];






async function mostraPickerOggettiDiUnaRoom(roomId) {

    //debugger;
    let imgDellaLocazDelPicker = g_last_room_desc.grrRooms[roomId].rfc_img;
    await changeImgSrcAndWait($(".imgObjectivesPicker"), imgDellaLocazDelPicker);
    $(".imgObjectivesPicker").show();


    $("#divObjectives").modal("show");



    let titoloDialog = calcolaFraseParzialeConUnOggettoEUnVerboSelez();


    $("#divObjectives #myModalLabel").html(titoloDialog);

    $("#divObjectives .btnOpt").remove();
    $("#divObjectives .divOggettoNonCliccabile").remove();
    $("#divObjectives .btnVerbObject").remove();



    let ofcNellaRoomScelta = g_last_room_desc.grrRooms[roomId].rfc_objects;


    //marcaGliOfcConPrimoFiglioEUltimoFiglio(ofcNellaRoomScelta);

    ofcNellaRoomScelta.forEach(function (ofc, i) {

        let btn = creaPulsanteInvMind(ofc, "#useForOuter", g_last_room_desc, false, true /* secondo ogg*/);

    });

    //debugger;
    marcaButtonsConPrimoFiglio("#useForOuter", ".btnObject");
}


async function mostraPickerOggettiDiInv(roomId) {

    //debugger;
    //let imgDellaLocazDelPicker = g_last_room_desc.grrRooms[roomId].rfc_img;
    //await changeImgSrcAndWait(".imgObjectivesPicker", imgDellaLocazDelPicker);
    $(".imgObjectivesPicker").hide();


    $("#divObjectives").modal("show");



    let titoloDialog = calcolaFraseParzialeConUnOggettoEUnVerboSelez();


    $("#divObjectives #myModalLabel").html(titoloDialog);

    $("#divObjectives .btnOpt").remove();
    $("#divObjectives .divOggettoNonCliccabile").remove();
    $("#divObjectives .btnVerbObject").remove();



    let ofcNellInv = g_last_room_desc.grrInvObjects; //.concat(g_last_room_desc.grrInvConcepts);


    //marcaGliOfcConPrimoFiglioEUltimoFiglio(ofcNellaRoomScelta);
    //debugger;
    ofcNellInv.forEach(function (ofc, i) {

        let btn = creaPulsanteInvMind(ofc, "#useForOuter", g_last_room_desc, false, true /* secondo ogg*/, true /* in container tra parentesi*/);

    });

    //debugger;
    marcaButtonsConPrimoFiglio("#useForOuter", ".btnObject");
}

function isCredentialBootstrapUrl(url) {
    const path = String(url).split("?")[0];
    return path.endsWith("/api/loadGame")
        || path.endsWith("/api/createUserAndStartGame");
}

async function doPost(url, inp, options = {}) {
    if (inp && typeof inp === "object" && !Array.isArray(inp) && inp.tutorialMode === undefined) {
        inp = Object.assign({}, inp, { tutorialMode: g_tutorialMode });
    }
    console.log(`facendo post a ${url}`, inp);
    //try { // non funziona... deve fare try il chiamante
    const useSession = options.useSession !== false && !isCredentialBootstrapUrl(url);
    const sessionToken = useSession ? sessionStorage.getItem(sessionTokenId) : null;
    var ret = $.ajax({
        type: "POST", url: url, data: JSON.stringify(inp), contentType: "application/json",
        headers: sessionToken ? { Authorization: `Bearer ${sessionToken}` } : {}
    }).then(function (data, textStatus, xhr) {
        const newToken = xhr.getResponseHeader("X-Segusum-Session-Token");
        if (newToken) sessionStorage.setItem(sessionTokenId, newToken);
        return data;
    });
    //debugger;
    return ret;
    //}
    //catch (e) {
    //    debugger;
    //}
}


async function doPostTry(url, i, options = {}) {
    let data;
    //let err1;
    try {
        data = await doPost(url, i, options);
        if (data && data.errore === "noauth" && !isCredentialBootstrapUrl(url)) {
            const stored = localStorage[credentialsId] ? JSON.parse(localStorage[credentialsId]) : null;
            if (stored && stored.uname && stored.pwd) {
                sessionStorage.removeItem(sessionTokenId);
                const loginData = await doPost(`${prefissoWebApi}/api/loadGame`, {
                    uname: stored.uname, pwd: stored.pwd, lang: getLang(), curTime: getCurTime(), cred_gameId: gGameId
                }, { useSession: false });
                if (loginData && !loginData.errore)
                    data = await doPost(url, i);
            }
        }
        //err1 = null;
    }
    catch (err) {
        const xhr = err && err.status !== undefined ? err : null;
        data = {
            errore: "conn-error",
            diagnostica: {
                url: url,
                method: "POST",
                status: xhr ? xhr.status : null,
                statusText: xhr ? xhr.statusText : null,
                responseText: xhr ? (xhr.responseText || "").slice(0, 2000) : null,
                exception: err ? String(err) : null,
                page: window.location.href,
                userAgent: navigator.userAgent,
                time: new Date().toISOString()
            }
        };
        showClientDiagnostic("Errore di comunicazione con il server", formatClientDiagnostic(data.diagnostica));
        //debugger;
    }

    return data;
}


function handleErrorsPost(data) {
    if (data.errore === "conn-error") {

        //$("#waitingServer").addClass('nascosto'); // altrimenti non riesco a cliccare i lpuls reload.
        nascondiPleaseWait();
        showClientDiagnostic(
            "Il browser non riesce a completare la richiesta",
            data.diagnostica ? formatClientDiagnostic(data.diagnostica) : "Nessun dettaglio HTTP disponibile."
        );

        BootstrapDialog.show({
            title: "error".tr(),
            message: "cantContactServer".tr()
            , buttons: [{
                label: "retry".tr(), action: function (e) {
                    //console.log("azione refresh");
                    location.reload(true /* svuota cache*/);
                }
            }]
        });
        return false;
    }
    else if (data.errore === "noauth") {

        nascondiPleaseWait();
        //localStorage.removeItem("credentials");

        //$("#waitingServer").addClass('nascosto'); // altrimenti non riesco a cliccare i lpuls reload.

        BootstrapDialog.show({
            title: "error".tr(),
            message: "userNotFound".tr()
            , closable: false
            , buttons: [{
                label: "retry".tr(),
                action: function (dlg) {
                    dlg.close();
                    $("#options").modal("hide");
                    $("#divLogin").modal({ backdrop: 'static' });
                    $("#divLogin").modal("show");
                    $("#btnBackLogin").hide();

                }
            }]
        });




        return false;
    }
    else if (data.errore) {
        console.log("errore", data.errore);
        showClientDiagnostic("Errore restituito dal server", formatClientDiagnostic({
            error: data.errore,
            response: data
        }));
        return false;
    }

    return true;

}

let g_clientWaitDiagnosticTimer = null;

function formatClientDiagnostic(value) {
    try {
        return typeof value === "string" ? value : JSON.stringify(value, null, 2);
    }
    catch (err) {
        return String(err);
    }
}

function showClientDiagnostic(title, details) {
    const diagnosticText = "Client JavaScript: main43.js?ver=" + gClientScriptVersion +
        "\n\n" + title + "\n\n" + (details || "") +
        "\n\nURL pagina: " + window.location.href +
        "\nUser agent: " + navigator.userAgent;
    $("#clientDiagnosticsText").text(diagnosticText);
    $("#clientDiagnostics").show();
}

function scheduleClientWaitDiagnostic() {
    if (g_clientWaitDiagnosticTimer) {
        clearTimeout(g_clientWaitDiagnosticTimer);
    }
    g_clientWaitDiagnosticTimer = setTimeout(() => {
        if (!$("#waitingServer").hasClass("nascosto")) {
            showClientDiagnostic(
                "Il server non ha risposto entro 15 secondi",
                "Controlla che il server sia avviato e che il browser Android stia usando la stessa porta.\n" +
                "Se il server gira su un altro dispositivo, localhost non è corretto: usa l'indirizzo IP di quel dispositivo."
            );
        }
    }, 15000);
}

window.addEventListener("error", event => {
    showClientDiagnostic("Errore JavaScript nella pagina", formatClientDiagnostic({
        message: event.message,
        source: event.filename,
        line: event.lineno,
        column: event.colno
    }));
});

window.addEventListener("unhandledrejection", event => {
    showClientDiagnostic("Errore JavaScript asincrono", formatClientDiagnostic({
        reason: String(event.reason),
        stack: event.reason && event.reason.stack ? event.reason.stack : null
    }));
});
function vediSeMouseOverRect(e) {
    let posx;
    if (e != null) {
        posx = e.clientX - e.currentTarget.offsetLeft;
    }
    else {
        // TODO sbagliato
        //debugger;
        posx = gMouseX;
    }
    let posy;
    if (e != null) {
        posy = e.clientY - e.currentTarget.offsetTop
    }
    else {
        posy = gMouseY;
    }

    var x = 100.0 * (posx) / $(".divLayersContainer").width();
    var y = 100.0 * (posy) / $(".divLayersContainer").height();

    //console.log('mouse coords', { x: x, y: y });
    //debugger;
    let oggettiInCurRoom = getCurRoom().rfc_objects;

    let loHoverManualRect = null;
    let oggettiInCurRoomOrd = _.sortBy(oggettiInCurRoom, e => - e.ofcHotspotPriority);
    for (let lo of oggettiInCurRoomOrd) {
        if (lo.ofcManualCoords !== null) {
            if (lo.ofcManualCoords.x0 <= x && x <= lo.ofcManualCoords.x1

                &&
                lo.ofcManualCoords.y0 <= y && y <= lo.ofcManualCoords.y1) {
                loHoverManualRect = lo;
                break;
            }

            //debugger;
        }
    }
    return loHoverManualRect;
}
function calcolaScalEtc() {
    if (noImgMode()) {
        debugger; // non doveva ess chiamato
    }
    let scal;   // teorwt * scal = actualwt
    let posOfBg;


    if (g_last_room_desc.grrLayersOfCurRoom == null) {
        debugger;
    }


    let acwt = $(".imgGrafica").width();
    //let teorWt = la.lfc_wt;
    scal = acwt / g_last_room_desc.grrRooms[g_last_room_desc.grrCurRoomId].rfc_bg_wt;

    posOfBg = $(".imgGrafica").position();


    //for (la of g_last_room_desc.grrLayersOfCurRoom.values())
    //{
    //        //let la = g_last_room_desc.grrLayersOfCurRoom[ila];

    //        if (typeof la === 'undefined')
    //        {
    //                debugger;
    //        }
    //        if (typeof la.lfc_imgPath === 'undefined')
    //        {
    //                debugger;
    //        }
    //        if (la.lfc_imgPath.endsWith("bg.jpg") || la.lfc_imgPath.endsWith("bg-res1900.jpg") || la.lfc_imgPath.endsWith("bg.png"))
    //        {

    //                let acwt = $(".imgGrafica").width();
    //                let teorWt = la.lfc_wt;
    //                scal = acwt / teorWt;

    //                posOfBg = $(".imgGrafica").position();

    //                break;
    //        }
    //}

    return { scal: scal, posOfBg: posOfBg };
}


function objectsUnderMouse(mouseX, mouseY, earlyStop = null, skipImg0 = null) {
    let dat = calcolaScalEtc();
    let ret = []; // non si riesce a usare yield dentro each().

    // Picking follows the same shared depth order used for rendering.
    // Higher lfc_zIndex means visually/internally closer to the user.
    const layersUnderMouseOrder = $(".imlayer").toArray().sort((a, b) => {
        const aZ = Number($(a).attr("lfc_zIndex") ?? $(a).css("z-index") ?? 0);
        const bZ = Number($(b).attr("lfc_zIndex") ?? $(b).css("z-index") ?? 0);
        return bZ - aZ;
    });

    $(layersUnderMouseOrder).each((i, img0) => {

        if (img0 === skipImg0) {
            return true; // continua sotto l'hit-test corrente
        }

        if (earlyStop != null) {
            if (earlyStop(ret)) {
                return false; // break
            }
        }
        let img = $(img0);

        //console.log("e = ", e);


        //IMPORTANTE: e.offsetX è la posizioen del mouse nel canvas, ma solo se gli altri layer hanno pointer-events none. altrimenti diventa la posizione
        // del mouse nel layer, e questo mi crea problemi.

        //console.log(`mouse = ${mouseX}, ${mouseY}`);
        //console.log(`img src = ${img.attr("src")}, layer xy  = ${img.css('left')},${img.css('top')} , wt = ${img.width()}, ht = ${img.height()}`);

        //console.log(`page xy  = ${e.pageX}, ${e.pageY}, client xy = ${e.clientX}, ${e.clientY}, offset xy = ${e.offsetX}, ${e.offsetY}`);



        // salto il calcolo (pesante, con creazione del canvas) per il bg
        if (img.hasClass("noCollisions")) {

            return true; // continue
        }



        //let scale = img0.getBoundingClientRect().width / img0.offsetWidth;
        let imgLeft = parseInt(img.css('left').replace('px', ''));
        let imgTop = parseInt(img.css('top').replace('px', ''));
        let imgRight = imgLeft + img.width() * dat.scal;
        let imgBottom = imgTop + img.height() * dat.scal;

        //console.log(`oofset x = ${e.offsetX}.`);

        if (imgLeft <= mouseX && mouseX < imgRight && imgTop <= mouseY && mouseY < imgBottom) {
            //console.log(`oofset x = ${e.offsetX}. siamo dentro a src = ${img.attr("src")} , layer xy  = ${imgLeft},${img.css('top')} , right= ${imgRight}, ht = ${img.height() * dat.scal}`);


            // vediamo se è in una zona trasparente


            let canvas = document.createElement('canvas');
            //debugger;
            canvas.width = img.width() * dat.scal;
            canvas.height = img.height() * dat.scal;
            canvas.getContext('2d').drawImage(img[0], 0, 0, img.width() * dat.scal, img.height() * dat.scal);
            let pixelData = canvas.getContext('2d').getImageData(mouseX - imgLeft, mouseY - imgTop, 1, 1).data;

            //console.log("pixel data =", pixelData);

            if (pixelData[3] == 0) {
                //trasparente
                return true; // continue
            }


            //trovatoUnLayer = true;

            // il mouse si trova su questo layer.

            let loId = img.attr("lo_id");

            // trovo il lo dal loid
            let ofcInCurRoom2 = g_last_room_desc.grrRooms[g_last_room_desc.grrCurRoomId].rfc_objects
            let losMatching = ofcInCurRoom2.filter(ofc => ofc.loId === loId);

            let lo;
            if (losMatching.length == 0) {
                // il layer non ha oggetto. significa che sta lì ma non è selezionabile, ma si deve vedere. come un divano, che non poteva essere parte del fondale
                // perché sta davanti a qualcosa, ma non si deve selezionare. è normale. continua al prossimo.
                return true; // comtinue
                //debugger;
                //lo = null; // un layer che nonha oggetto
            }
            else {
                lo = losMatching[0];
            }


            // trovo il la dal loid
            let la;
            for (let lfc of g_last_room_desc.grrLayersOfCurRoom.values()) {
                if (lfc.lfc_loId == loId) {
                    la = lfc;
                }
            }

            ret.push({ lo: lo, la: la });

            //gLoHover = lo;
            //gDatHover = dat;
            //gLayerHover = la;
            //gMouseX = e.clientX;
            //gMouseY = e.clientY;


            ////console.log(`page xy  = ${e.pageX}, ${e.pageY}, client xy = ${e.clientX}, ${e.clientY}, offset xy = ${e.offsetX}, ${e.offsetY}`);
            ////console.log(`mousepos `, e);


            //// deov sottrarre la posizione del canvas
            //let re = $(".divLayersContainer")[0].getBoundingClientRect();

            //var xFinale = e.clientX - re.left;
            //var yFinale = e.clientY - re.top;

            //posizionaDidascaliaOggettoMouse(dat, gLoHover, xFinale, yFinale, true /* is in room*/, null /*premade text*/); // questo e' un layer della room



            //maybeDisableCursorForLookWhenMouseOverRoomObj(lo);


            //return false; // significa break
        }

        //if (!trovatoUnLayer)
        //{
        //        gLoHover = null;
        //        gDatHover = null;
        //        gLayerHover = null;
        //}

    });

    return ret;

}

function onMouseMoveLayersContainer_seeIfMouseOnRect(e) {

    if (noImgMode() == true) {
        return; // non ci sono i box hotspot in text mode
    }

    const targetIsHitTestOnly = e != null && $(e.target).closest(".hitTestOnly").length > 0;

    // Per gli outline non esiste un handler diretto di mouseleave: quando il
    // cursore passa dall'immagine -ou al container, puliamo esplicitamente
    // l'hover precedente. Senza questo, gLoHover poteva restare valorizzato
    // e la didascalia continuava a mostrare l'oggetto ormai non puntato.
    if (gLayerHover != null && gLayerHover.lfcIsOutline && !targetIsHitTestOnly) {
        gLoHover = null;
        gDatHover = null;
        gLayerHover = null;
        $(".btnOggettoInRoom").remove();
        $(".divLayersContainer").css("cursor", "default");
        return;
    }

    // Un hit-test -ou è sopra alla grafica e intercetta il mouse anche quando
    // il pixel sotto il cursore è trasparente. In quel caso offsetX/offsetY
    // sono relativi all'immagine -ou, non al container: ricaviamo quindi le
    // coordinate dal rettangolo del container e cerchiamo esplicitamente
    // l'oggetto più alto sottostante.
    if (e != null && targetIsHitTestOnly) {
        const containerRect = $(".divLayersContainer")[0].getBoundingClientRect();
        const mouseX = e.clientX - containerRect.left;
        const mouseY = e.clientY - containerRect.top;
        const objectsBelow = objectsUnderMouse(mouseX, mouseY)
            .filter(x => !x.lo.ofcIsInCurParty);

        gMouseX = e.clientX;
        gMouseY = e.clientY;
        gLastPositionSentenceX = mouseX;
        gLastPositionSentenceY = mouseY;

        if (objectsBelow.length > 0) {
            const hovered = objectsBelow[0];
            gLoHover = hovered.lo;
            gDatHover = calcolaScalEtc();
            gLayerHover = hovered.la;
            gLoHoverManualRect = null;

            if (!gFrozenMouse) {
                posizionaDidascaliaOggettoMouse(gDatHover, gLoHover, mouseX, mouseY, true, null);
                maybeDisableCursorForLookWhenMouseOverRoomObj(gLoHover);
            }
            return;
        }

        gLoHover = null;
        gDatHover = null;
        gLayerHover = null;
    }

    if (gLoHover == null) // se lohover non null, prevale l'altro sistema
    {
        if (e != null) {
            gLoHoverManualRect = vediSeMouseOverRect(e);
        }
        else {
            // lascio lo stesso glohovermanual
        }

        let dat = calcolaScalEtc();



        let posx;
        let posy;
        if (e != null) {


            posx = e.clientX - e.currentTarget.offsetLeft;

            posy = e.clientY - e.currentTarget.offsetTop;
        }
        else {
            posx = gLastPositionSentenceX;
            posy = gLastPositionSentenceY;
        }
        if (gLoHoverManualRect != null) {
            
            posizionaDidascaliaOggettoMouse(dat, gLoHoverManualRect, posx, posy, true);

            maybeDisableCursorForLookWhenMouseOverRoomObj(gLoHoverManualRect);
        }
        else {
            //console.log("il mouse non è su niente");

            // non siamo su un layer, ma adesso dobbiamo comunque scrivere "usa oggetto con ...". quindi passo null come logicobj attuale

            posizionaDidascaliaOggettoMouse(dat, null, posx, posy, true);
            //}
            //else
            ////if (gLoHover == null)
            //{
            //    // sembra funzionare anche così, anche se sto togliendo la frase ignorando se c'e' un gLoHover dei layer
            //    gLoHover = null;
            //    gDatHover = null;
            //    gLayerHover = null;

            //    $(".btnOggettoInRoom").remove();

            //    $(".divLayersContainer").css("cursor", "default");
            //}
        }
    }
}

var gMouseX;
var gMouseY;

var gLastPositionSentenceX;
var gLastPositionSentenceY;

function cliccatoNelVuotoDeselezionaTutto() {


    gSelectedVerb = null;
    //debugger;
    gSelectedObj = null;
    updateToolbar();
    $(".contextMenu").hide();

    $(".divLayersContainer").css("cursor", "default");

    //debugger;
    // devo aggiornare la didascalia sul mouse come se fosse appena entrato.        
    if (gLayerHover != null) {


        var dat = calcolaScalEtc();

        //// deov sottrarre la posizione del canvas
        //let re = $(".divLayersContainer")[0].getBoundingClientRect();

        //var xFinale = /*e.clientX */gMouseX - re.left;
        //var yFinale = /*e.clientY */ gMouseY - re.top;

        posizionaDidascaliaOggettoMouse(dat, gLoHover, gLastPositionSentenceX, gLastPositionSentenceY, true /* is in room*/); // questo e' un layer della room


    }
    else //if (gLoHover != null)
    {
        //debugger;
        // non fuinziona
        onMouseMoveLayersContainer_seeIfMouseOnRect(null);
    }

}


function deselectAll() // obsoleto
{
    //debugger;
    gSelectedObj = null;
    if (gBtnPushedObj) {
        gBtnPushedObj.removeClass("active");
        gBtnPushedObj = null;
    }

    gVerbChosen = null;
    //$(".btnVerb").removeClass("disabled");
    if (gBtnPushedVerb) {
        gBtnPushedVerb.removeClass("active");
        gBtnPushedVerb = null;

    }


    gObjectiveChosen = null;

    if (gBtnPushedObjective) {
        gBtnPushedObjective.removeClass("active");
        gBtnPushedObjective = null;

    }

    //debugger;
    updateActionBarAndSelectabilityOfObjects();
}



let msPerFarVederePressione = 300;
//function createImage(src){
//    return new Promise((resolve, reject) => {
//        let img = new Image()
//        img.onload = () => resolve(img)
//        img.onerror = reject
//        img.src = src
//    })
//}

//function marcaGliOfcConPrimoFiglioEUltimoFiglio(elencoDiOfc) {

//    let segmentoAperto = false;
//    elencoDiOfc.forEach(function (ofc, i) {



//        if (ofc.ofc_can_be_selected && !segmentoAperto) {
//            ofc.dynIsPrimoFiglio = true;
//            segmentoAperto = true;
//        }

//        let pross = elencoDiOfc[i + 1];
//        if (segmentoAperto && pross && !pross.ofc_can_be_selected
//            ||
//            segmentoAperto && !pross
//        ) {
//            ofc.dynIsUltimoFiglio = true;
//            segmentoAperto = false;
//        }
//    });

//}

function marcaButtonsConPrimoFiglio(container, classPulsanti) {

    //debugger;

    if ($(container).length === 0) {
        throw `Not found : ${container}`;
    }

    let segmentoAperto = false;
    let pulsanti = $(`${container} *`);
    pulsanti.removeClass("ultimoFiglio").removeClass("primoFiglio");

    pulsanti.each(function (i, btn) {



        if (!$(btn).hasClass("disabled") && $(btn).hasClass(classPulsanti) && !segmentoAperto) {
            $(btn).addClass("primoFiglio");
            segmentoAperto = true;
        }

        let pross = pulsanti[i + 1];

        if (segmentoAperto && pross && $(pross).hasClass("disabled")
            ||
            segmentoAperto && !pross
        ) {
            $(btn).addClass("ultimoFiglio");
            segmentoAperto = false;
        }
    });

}

async function mostraMappa() {

    // The engine resolves the exported JPG beside the map JSON and sends the
    // web-root-relative URL plus the editor's canvas geometry.
    let mapImage = g_last_room_desc.grrMapImage;
    let mapX = Number(g_last_room_desc.grrMapImageX || 0);
    let mapY = Number(g_last_room_desc.grrMapImageY || 0);
    let mapW = Number(g_last_room_desc.grrMapImageWidth || 0);
    let mapH = Number(g_last_room_desc.grrMapImageHeight || 0);
    let mapCanvasW = Math.max(4200, mapX + mapW);
    let mapCanvasH = Math.max(4000, mapY + mapH);
    if (mapImage) {
        let mapUrl = cacheBustGraphicsUrl(prefissoWebApi + "/" + mapImage);
        if ($("#imgMap").attr("src") !== mapUrl) {
            $("#imgMap").attr("src", mapUrl);
        }
        $("#imgMap").css({ left: mapX + "px", top: mapY + "px", width: mapW + "px", height: mapH + "px" });
    }

    if (noImgMode()) {
        // map-new.json è già stato trasformato dal server in grrRoomCoords:
        // manteniamo coordinate, accessibilità e panning anche senza il JPG.
        rebuildMapButtons();
        $("#imgMap").hide();
        $("#mapOuterOuter").show();
        $("#mapOuter").css({ overflow: "auto", opacity: 1 });
        $("#mapInner0").css({ width: mapCanvasW + "px", height: mapCanvasH + "px", background: "#ffffff" });
        $("#mapInner").css({
            transform: "none",
            position: "relative",
            width: mapCanvasW + "px",
            height: mapCanvasH + "px",
            background: "#ffffff"
        });
    }
    else {
        rebuildMapButtons();
        $("#imgMap").show();
        $("#mapInner0").css({ width: mapCanvasW + "px", height: mapCanvasH + "px" });
        $("#mapInner").css({ width: mapCanvasW + "px", height: mapCanvasH + "px" });
    }

    $("#mapOuter").css("opacity", 0);
    $("#mapOuterOuter").show(); // prima! se no i calcoli dopo non funzionano



    $(".mapSpiegazInner").text(`${g_last_room_desc.grr_walk_to_translated}...`);

    // scrollo al pulsante il cui roomId è pari al currRoomId
    let btnMap = $(".btnLocation").filter((i, b) => {
        if ($(b).attr("roomId") === g_last_room_desc.grrCurRoomId) {
            return true;
        }
        else {
            return false;
        }
    });

    //debugger;


    //console.log("viewport wt = ", viewportWt);






    // ora scrollo la mappa per centrare la locazione attuale.
    // molto complicato, e non funziona se scalo il container con transform-scale in css, perché poi il css left del pulsante che ottengo qui non è scalato
    // qundi lo scale lo setto da codice.
    // The old map used CSS points for coordinates. Since 1pt is 1.333px,
    // its effective zoom was 0.6 * 1.333. Coordinates are now canvas pixels,
    // so preserve the same visual zoom with the equivalent factor.
    let scal = 0.8;


    // aggiungo un margine in alto perche' ha iniziato stranamente a tagliare il top. anche scrollando non vedevo tutta la mappa in alto.
    let marginTopAggiuntoManualm = 316;
    $("#mapInner0").css("margin-top", scal * marginTopAggiuntoManualm);


    let marginLeftAggiuntoManualm = 62;
    $("#mapInner0").css("margin-left", scal * marginLeftAggiuntoManualm);




    //debugger;
    //let scalingContainerX = $("#mapInner").css("transform");
    let viewportWt = $(window).width();
    let offsLeft = parseInt(btnMap.css("left"));
    let wt = btnMap.width();

    let compensazXPerMargine = marginLeftAggiuntoManualm * scal;
    let left = offsLeft * scal - (viewportWt - wt) * 0.5 + compensazXPerMargine;


    $("#mapOuter").scrollLeft(left);


    let viewportHt = $(window).height(); // già scalato col transform
    let offsTop = parseInt(btnMap.css("top"));
    let ht = btnMap.height();

    let translY = scal * marginTopAggiuntoManualm;
    let top = offsTop * scal - (viewportHt - ht) * 0.5
        + translY // devo compensare per il margine aggiunto in alto
        ;
    $("#mapOuter").scrollTop(top);



    $("#mapInner").css("transform", `scale(${scal},${scal})  `);




    $("#mapOuter").animate({
        opacity: 1

    }, 170);



    // faccio flashare

    let msFade = 150;
    let msPausa = 350;
    $(".mapSpiegazInner").fadeOut(1).fadeIn(msFade);
    await sleepAsync(msPausa);
    $(".mapSpiegazInner").fadeOut(msFade).fadeIn(msFade);


}


function disabilitaOggettiCheLoNecessitano() {

    let pairsObjsDisabledWithThisVerb = g_last_room_desc.grrDisabledVerbs.filter(p => p.ovcVerb === gVerbChosen.vfcSerId);

    $(".btnObject").each((i, el) => { // ciclo sugli oggetti

        let loId = $(el).attr("lo_id");

        let disabilitato = false;


        // vedi se è disabilitato in quanto l'utente ha deciso esplicitamente che deve essere disabilitato su questo verbo
        pairsObjsDisabledWithThisVerb.forEach((pair, ip) => {

            if (loId === pair.ovcObj) {
                $(el).addClass("disabled");
                disabilitato = true;
            }
        });


        // vedi se è disabilitato in quanto il verbo è remember e non hai niente da ricordare su questo oggetto
        if (gVerbChosen.vfc_is_remember && $(el).attr("can_be_remembered") !== "true") {
            $(el).addClass("disabled");
            disabilitato = true;
        }


        // vedi se è disabilitato in quanto il verbo funziona solo su oggetti della room e questo è dell'inv
        if (gVerbChosen.vfcCanOnlyBeUsedWithObjsInRoomNotInv
            && !$(el).hasClass("btnRoomObject")
        ) {
            disabilitato = true;
        }


        // vedi se è disabilitato in quanto ho già selezionato un ogg dell inv e non puoi combinare 2 ogg dell inv
        if (gSelectedObj !== null && gSelectedObj.ofc_is_in_inv && $(el).hasClass("btnInvObject") && loId !== gSelectedObj.loId) {
            disabilitato = true;
        }

        //// vedi se è disabilitato in quanto non ha senso raccoglierlo . tolto, perché l'utenten non capisce perché non lo puoi raccogliere.
        //if (gVerbChosen.vfcIsPickup
        //    && $(el).hasClass("cantPossiblyBePickedUp")
        //) {
        //    disabilitato = true;
        //}

        // vedi se è disabilitato in quanto il verbo è usa e questo oggetto non si può selezionare per primo
        if ($(el).attr("can_be_selected_as_first_obj") !== "true" /*se non si può selez come primo*/
            && gVerbChosen.vfcCanBeUnaryOrBinaryDependingOnObject  /* e il verbo scelto è usa */) {

            disabilitato = true;
        }


        if (!disabilitato) {
            $(el).removeClass("disabled");
        }
        else {
            $(el).addClass("disabled");
        }

    });

}

// questo posiziona la scritta  centrata nel layer
function posizionaDidascaliaOggetto(dat, lo, lfc, forcedName = null) {



    //let text;
    //if (forcedName == null)
    //{
    //        let ofcNameWithArticleMaybe;
    //        if (lo.ofcNameWithArticle != null && lo.ofcNameWithArticle != "")
    //        {
    //                ofcNameWithArticleMaybe = lo.ofcNameWithArticle;
    //        }
    //        else
    //        {
    //                ofcNameWithArticleMaybe = lo.ofc_name;
    //        }


    //        text = lo.ofcHoverStringWhenInRoom.replace("{1}", ofcNameWithArticleMaybe);
    //}
    //else
    //{
    //        text = forcedName;
    //}
    //debugger;

    //let newEl = $("<div class='btnOggettoInRoomHotspot'>").text(text);
    //let newEl = gTemplateImgTarget.clone();

    if (!dat || !dat.posOfBg) {
        throw new Error("dat or posOfBg is null or undefined");
    }


    let offsx = dat.posOfBg.left;
    // rest of the function code...

    let offsy = dat.posOfBg.top;
    let topApprossimato = lfc.lfc_y * dat.scal + offsy + lfc.lfc_ht * dat.scal * 0.5;

    let newEl;
    if (lo.ofcIsExit) {
        if (topApprossimato > $(".divLayersContainer").height() * 0.9) {
            newEl = gTemplateImgTargetExitDown.clone();
        }
        else {
            newEl = gTemplateImgTargetExit.clone();
        }

    }
    else {
        newEl = gTemplateImgTarget.clone();
    }


    newEl.appendTo(".divLayersContainer"); // subito, se no non misura la larghezza
    // Il bersaglio è un indicatore UI sopra i layer grafici della stanza:
    // non deve finire sotto uno sprite o un oggetto con z-index narrativo.
    newEl.css("z-index", 2147483647);


    // Per i layer normali il rettangolo descritto dai dati coincide con
    // l'oggetto visibile. I personaggi possono invece avere una scala CSS,
    // trasparenze o dimensioni effettive diverse dai dati originali.
    // Usiamo quindi il rettangolo realmente renderizzato dal browser.
    const renderedLayer = $(".imlayer").filter(function () {
        return $(this).attr("lo_id") === lfc.lfc_loId;
    }).first()[0];

    let left;
    let top;

    if (renderedLayer) {
        const containerRect = $(".divLayersContainer")[0].getBoundingClientRect();
        const layerRect = renderedLayer.getBoundingClientRect();

        left = layerRect.left - containerRect.left + layerRect.width * 0.5 - newEl.width() * 0.5;
        top = layerRect.top - containerRect.top + layerRect.height * 0.5 - newEl.height() * 0.5;
    }
    else {
        // Fallback per gli oggetti senza un layer grafico.
        left = lfc.lfc_x * dat.scal + offsx + lfc.lfc_wt * dat.scal * 0.5 - newEl.width() * 0.5;
        top = lfc.lfc_y * dat.scal + offsy + lfc.lfc_ht * dat.scal * 0.5 - newEl.height() * 0.5;
    }

    if (left < 0) {
        left = 0;
    }

    newEl.css("left", left);

    if (top > $(".divLayersContainer").height() - newEl.height()) {
        top = $(".divLayersContainer").height() - newEl.height();
    }

    newEl.css("top", top);

    newEl.css('opacity', 0);


    newEl.animate({ 'opacity': 1.0 }, 400);
}


function updateInvObjectsdDisabled() {

    if (gSelectedVerb == 'deduce') {
        $(".invObjTemplate ").addClass("disabled");
    }
    else if (gSelectedVerb == 'pickup') {
        $(".invObjTemplate ").addClass("disabled");
    }
    else if (gSelectedVerb == 'use') {
        $(".invObjTemplate ").addClass("disabled");
    }
    else if (gSelectedVerb == 'talk') {
        $(".invObjTemplate ").addClass("disabled");
    }
    else if (gSelectedObj != null && gLo1ChosenWasInInv) {
        $(`.invObjTemplate[lo_id!='${gSelectedObj.loId}'] `).addClass("disabled");

        $(`.invObjTemplate[lo_id='${gSelectedObj.loId}'] `).removeClass("disabled");
    }
    else {
        $(".invObjTemplate ").removeClass("disabled");
    }
}

function calcolaTextPerFraseMouse(lo /* l'oggetto su cui sei col mouse. puo' essere null se non sei su nessun oggetto*/,
    isInRoom /* l'oggetto su cui sei , cioe' lo, e' nella stanza o nell'inv*/, premadeText = null) {

    //if (lo == null && premadeText == null) {
    //    debugger;
    //}

    let ofcNameWithArticleMaybe;
    if (lo == null) {
        ofcNameWithArticleMaybe = premadeText;
    }
    else if (gSelectedObj == null && gSelectedVerb == null) {
        ofcNameWithArticleMaybe = lo.ofc_name;
    }
    else if (lo.ofcNameWithArticle != null && lo.ofcNameWithArticle != "") {
        ofcNameWithArticleMaybe = lo.ofcNameWithArticle;
    }
    else {
        ofcNameWithArticleMaybe = lo.ofc_name;
    }



    let text;
    if (premadeText !== null) {
        text = premadeText;
    }
    else if (gSelectedObj == null) {
        if (gSelectedVerb == null) {


            if (isInRoom) {
                //nuova logica: mostro menu popup, quindi su hover mostro solo il nome
                if (lo == null) {
                    text = "";
                }
                else
                text = lo.ofc_name;
                //text = lo.ofcHoverStringWhenInRoom.replace("{1}", ofcNameWithArticleMaybe);
            }
            else {

                //text = ""; // adesso il testo è tutto nell'inv, quindi niente hover sentence

                if (lo.ofcVerbWhenUseWithAsFirstObjectOnHoverNotSelected != null) {
                        text = lo.ofcVerbWhenUseWithAsFirstObjectOnHoverNotSelected;
                }
                else {
                        text = lo.ofcHoverStringWhenInInv.replace("{1}", ofcNameWithArticleMaybe);
                }
            }
        }
        else if (gSelectedVerb == 'pickup') {
            if (isInRoom) {
                text = 'pickup1'.tr().replace("{1}", ofcNameWithArticleMaybe);
            }
            else {
                text = "";
            }
        }
        else if (gSelectedVerb == 'deduce') {

            if (isInRoom) {
                text = 'deduceSomethingAbout'.tr().replace("{1}", ofcNameWithArticleMaybe);
            }
            else {

                text = "";

                // vecchia logica:
                //// sull'inv non puoi fare deduci, quindi non faccio vedere niente. tranne per gli oggetti speciali come pizza aglio che hanno deduci nell inv,
                //// perche' l'enigma e' capire COME devi usarlo, non nel mdo tipico


                //if (lo.ofcIsDeduceWhenInInv) {
                //        // succede solo per la pizza aglio
                //        text = 'deduceSomethingAbout'.tr().replace("{1}", lo.ofc_name);
                //}
                //else {
                //        text = "";
                //}

            }
        }
        else if (gSelectedVerb == 'use') {

            if (isInRoom) {
                if (lo.ofcContextMenuUseForOrHereOrDeduce == "useFor") {

                    text = 'use1'.tr().replace("{1}", ofcNameWithArticleMaybe);
                }
                else if (lo.ofcContextMenuUseForOrHereOrDeduce == "useHere") {
                    text = 'use1'.tr().replace("{1}", ofcNameWithArticleMaybe);
                }
                else {
                    // mostrerà l'acesso vietato, ma io intano scrivo usa
                    text = 'use1'.tr().replace("{1}", ofcNameWithArticleMaybe);
                    //debugger;
                }

            }
            else {

                text = "";

            }
        }
        else if (gSelectedVerb == 'talk') {
            text = 'talkTo'.tr().replace("{1}", ofcNameWithArticleMaybe);
        }
        else if (gSelectedVerb == 'look') {
            if (lo.ofcCustomSentenceLook !== null) {
                text = lo.ofcCustomSentenceLook;
            }
            else {
                text = 'look1'.tr().replace("{1}", ofcNameWithArticleMaybe);
            }

        }
        else {
            console.error('fdvijkvfdk');
            debugger;
        }
    }
    else {
        // ho selezionato gSelectedObj e ho il mouseover su un altro oggetto lo, o su niente

        if (lo == null) {
            // non sono su niente. deve apparire "usa noccioline con ..."
            if (gSelectedObj.ofcVerbWhenUseWithAsFirstObjectOnHoverNotSelected != null) {
                text = gSelectedObj.ofcVerbWhenUseWithAsFirstObjectOnHoverNotSelected;
            }
            else {


                
                text = gSelectedObj.ofcHoverStringWhenInInv.replace("{1}", gSelectedObj.ofcNameWithArticle);
            }

        }
        else if (lo.loId == gSelectedObj.loId) {

            // stai combinando qualcosa con se stesso

            // nuova logica: non mostro niente.il testo è nell'inv.
            //text = "bla";

            // non sono su niente. deve apparire "usa noccioline con ..."
            if (gSelectedObj.ofcVerbWhenUseWithAsFirstObjectOnHoverNotSelected != null) {
                text = gSelectedObj.ofcVerbWhenUseWithAsFirstObjectOnHoverNotSelected;
            }
            else {



                text = gSelectedObj.ofcHoverStringWhenInInv.replace("{1}", gSelectedObj.ofcNameWithArticle);
            }


            //if (gSelectedObj.ofcVerbWhenUseWithAsFirstObjectSelectedWithPlaceHolder != null)
            //{

            //        text = gSelectedObj.ofcVerbWhenUseWithAsFirstObjectSelectedWithPlaceHolder;
            //}
            //else if (gSelectedObj.ofcIsConcept) {
            //        text = "correlateTo".tr().replace("{1}", ofcNameWithArticleMaybe).replace("{2}", "");
            //}
            //else {
            //        text = "usewith12".tr().replace("{1}", ofcNameWithArticleMaybe).replace("{2}", "");
            //}
        }
        else {
            // adesso su hover non vedi più l'eventuale frase completa
            //let qq = lo.ofcObjectsYouCanUseWithIt.filter(x => x.ocsLoId == gSelectedObj.loId);
            //if (qq.length > 0) {
            //        text = qq[0].ocsCompleteSentence;
            //}
            //else


            let sel_ofcNameWithArticleMaybe;
            if (gSelectedObj.ofcNameWithArticle != null && gSelectedObj.ofcNameWithArticle != "") {
                sel_ofcNameWithArticleMaybe = gSelectedObj.ofcNameWithArticle;
            }
            else {
                sel_ofcNameWithArticleMaybe = gSelectedObj.ofc_name;
            }




            if (gSelectedObj.ofcVerbWhenUseWithAsFirstObjectSelectedWithPlaceHolderOnHoverSecond != null) {
                text = replaceTargetPossessive(gSelectedObj.ofcVerbWhenUseWithAsFirstObjectSelectedWithPlaceHolderOnHoverSecond, lo, gSelectedObj).replace("{1}", ofcNameWithArticleMaybe);
            }

            else if (gSelectedObj.ofcIsConcept) {
                text = "correlateTo".tr().replace("{1}", sel_ofcNameWithArticleMaybe).replace("{2}", ofcNameWithArticleMaybe);
            }
            else {
                text = "usewith12".tr().replace("{1}", sel_ofcNameWithArticleMaybe).replace("{2}", ofcNameWithArticleMaybe);
            }
        }


    }

    return text.fondiParole();
}


function posizionaContextMenuMouse(xInOggetto, yInOggetto) {
    let re = $(".divLayersContainer")[0].getBoundingClientRect();

    let x = xInOggetto - re.left;
    let y = yInOggetto - re.top;

    let halfLarghezzaTesto = $(".contextMenu").width() * 0.5;
    //let offsx = dat.posOfBg.left - halfLarghezzaTesto;

    let left = x - halfLarghezzaTesto;
    if (left < 0) {
        left = 0;
    }

    if (left > $(".divLayersContainer").width() - $(".contextMenu").outerWidth(true)) {
        left = $(".divLayersContainer").width() - $(".contextMenu").outerWidth(true);
    }


    $(".contextMenu").css("left", left);

    let top = y;
    if (top > $(".divLayersContainer").height() - $(".contextMenu").outerHeight(true)) {
        top = $(".divLayersContainer").height() - $(".contextMenu").outerHeight(true);
    }

    $(".contextMenu").css("top", top);
}

function posizionaDidascaliaOggettoMouse(dat, lo /* può essere null se c'è testo premade*/, x, y, isInRoom, premadeText = null /* serve quando tu non sei su un oggetto ma su un verbo*/) {
    //console.log('chiamato posizionaDidascaliaOggettoMouse');

    gLastPositionSentenceX = x;
    gLastPositionSentenceY = y;

    let text;

    text = calcolaTextPerFraseMouse(lo, isInRoom, premadeText);
    //debugger;
    //console.log(`text calcolato = ${text}`);
    $(".btnOggettoInRoom ").remove();


    // la frase che segue il mouse non si deve mai vedere se il context menu è visibile
    if ($(".contextMenu").is(':visible') || $(".dialogChoiceOuterFullScreen").is(':visible')) {
        return;
    }


    // La didascalia che segue il mouse è un elemento UI, non un layer della
    // scena: deve restare leggibile anche quando il cursore è sopra uno sprite
    // con uno z-index elevato.
    let newEl = $("<div class='btnOggettoInRoom '>")
        .appendTo(".divLayersContainer")
        .text(text)
        .css("z-index", 2147483647);


    let halfLarghezzaTesto = newEl.width() * 0.5;
    //let offsx = dat.posOfBg.left - halfLarghezzaTesto;

    let left = x - halfLarghezzaTesto;
    if (left < 0) {
        left = 0;
    }
    newEl.css("left", left);

    let top = y - 50; //  era 110  con resoluzion 1900 Shifted 30px up from mouse pointer

    if (top > $(".divLayersContainer").height() - newEl.height()) {
        top = $(".divLayersContainer").height() - newEl.height();
    }

    newEl.css("top", top);






    return newEl;



}

function updateActionBarAndSelectabilityOfObjects(secondoOggetto = null) {

    $(".btnOggettoInRoom").remove();
    $(".imlayer").removeClass("dropshadow");

    //debugger;
    if (gVerbChosen === null) {


        $(".btnObject").addClass("disabled");

        if (gSelectedObj === null) {


            $(".fraseParzialeInner").html("");




            //$(".btnVerbObject").removeClass("disabled");
            $(".btnVerb").removeClass("disabled");

            $(".btnVerb").each((i, b) => {
                if ($(b).hasClass("is_highlighted")) {
                    $(b).removeClass("btn-default").addClass("btn-success");
                }
                else {
                    $(b).removeClass("btn-success").addClass("btn-default");
                }
            });


            //else {

            //    let str = "Per " + gObjectiveChosen.readable_name + ", usa ...";
            //    $(".fraseParzialeInner").html(str); 
            //}
        }
        else { // oggetto scelto, verbo no

            //debugger;

            // mostro outline dell oggetto cliccato;
            $(".imlayer").removeClass("dropshadow");
            $(".imlayer").each((i, el0) => {
                let el = $(el0);
                if (el.attr("lo_id") === gSelectedObj.loId) {
                    el.addClass("dropshadow");

                    //debugger;
                    let dat = calcolaScalEtc();
                    let lfc = g_last_room_desc.grrLayersOfCurRoom.filter(lfc => lfc.lfc_loId === gSelectedObj.loId)[0];
                    posizionaDidascaliaOggetto(dat, gSelectedObj, lfc);

                }
            });



            $(".fraseParzialeInner").html("USA " + gSelectedObj.ofc_name.firstLetterToUpper() + " CON "); // non ci metto i puntini davanti, brutto






            // illumino i verbi non zero compatibili con l'oggetto e disabilito i verbi non zero non compatibili
            let pairsVerbDisabledWithThisObj = g_last_room_desc.grrDisabledVerbs.filter(p => p.ovcObj === gSelectedObj.loId);
            $(".btnNonZeroVerb").removeClass("btn-default").removeClass("btn-success");

            $(".btnNonZeroVerb").each((i, el) => {
                let disabilitato = false;

                pairsVerbDisabledWithThisObj.forEach((pair, ip) => {


                    if ($(el).attr("verb_id") === pair.ovcVerb) {
                        $(el).addClass("disabled").addClass("btn-default");
                        disabilitato = true;
                    }
                });

                if (!disabilitato) {
                    //$(el).removeClass("disabled").addClass('btn-success');
                    $(el).removeClass("disabled").addClass("btn-default");
                }

            });




            // i verbi zero semplicemente li metto in modo normale
            $(".btnZeroVerb").removeClass("btn-success").addClass("btn-default");

        }
    }
    else { // il verbo è stato scelto



        $(".btnVerb").removeClass("btn-success").addClass("btn-default");

        if (gSelectedObj === null) {

            //let secPart = "";
            //if (gVerbChosen.vfcSecondPart !== null) {
            //    secPart = " " + gVerbChosen.vfcSecondPart + " ...";
            //}

            // se il verbo è remember, devo mostare i nomi degli oggetti nelle posizioni giuste
            if (gVerbChosen.vfc_is_remember) {

                $(".imlayer").addClass("dropshadow");
                let dat = calcolaScalEtc();

                $(".btnOggettoInRoom").remove();

                for (let lfc of g_last_room_desc.grrLayersOfCurRoom.values()) {
                    if (lfc.lfc_loId !== null && lfc.lfc_loId !== 'bg' && lfc.lfc_nameMustAppearInGraphics) {
                        let ofcInCurRoom = g_last_room_desc.grrRooms[g_last_room_desc.grrCurRoomId].rfc_objects;
                        let los = ofcInCurRoom.filter(ofc => ofc.loId === lfc.lfc_loId);


                        let lo = los[0];
                        //if (typeof lo === 'undefined') {
                        //    //debugger;
                        //}



                        posizionaDidascaliaOggetto(dat, lo, lfc);

                    }
                }

                $(".fraseParzialeInner").html(g_last_room_desc.grrStrClickAnObjectToRemember.firstLetterToUpper());
            }
            else if (gObjectiveChosen === null) {
                if (gVerbChosen.vfcIsZeroVerb) {
                    $(".fraseParzialeInner").html(gVerbChosen.vfcName.firstLetterToUpper());


                }
                else {
                    $(".fraseParzialeInner").html(gVerbChosen.vfcName.firstLetterToUpper() + " ..."); //+ secPart);




                    // devo disabilitare tutti gli oggetti che non sono compatibili con quel verbo scelto, e abilitare quelli che sono compatibili
                    disabilitaOggettiCheLoNecessitano();
                }

            }
            else {

                $(".fraseParzialeInner").html(g_last_room_desc.grr_in_order_to_translated.firstLetterToUpper() + " " + gObjectiveChosen.readable_name + ", " + gVerbChosen.vfcName.toUpperCase() + " ..."); //+ secPart);


                // devo disabilitare tutti gli oggetti che non sono compatibili con quel verbo scelto, e abilitare quelli che sono compatibili
                disabilitaOggettiCheLoNecessitano();
            }




        }
        else {


            // verbo e oggetto sono stati scelti

            disabilitaOggettiCheLoNecessitano();

            $(".btnVerb").removeClass("btn-success").addClass("btn-default");

            //// in ogni caso nella frase parziale metto solo verbo e oggetto, senza "con" e senza puntini
            //$(".fraseParzialeInner").text(gVerbChosen.vfcName.firstLetterToUpper() + " " + gLo1Chosen.ofc_name);

            if (secondoOggetto !== null) { // se ho selezionato un verbo e due oggetti, devo scrivere USA x CON y

                if (gObjectiveChosen === null) {
                    $(".fraseParzialeInner").html(gVerbChosen.vfcName.firstLetterToUpper() + " " + gSelectedObj.ofc_name + " " + gVerbChosen.vfcSecondPart + " " + secondoOggetto.ofc_name);
                }
                else {
                    $(".fraseParzialeInner").html(g_last_room_desc.grr_in_order_to_translated.firstLetterToUpper() + " " + gObjectiveChosen.readable_name + ", " + gVerbChosen.vfcName.toUpperCase() + " " + gSelectedObj.ofc_name + " " + gVerbChosen.vfcSecondPart.toUpperCase() + " " + secondoOggetto.ofc_name);

                }
            }
            else {
                //if (!gLo1Chosen.ofc_is_in_inv && !gVerbChosen.vfcIsUnary && gLo1Chosen.ofcCouldPotentiallyBePickedUp) // caso nuovo: il verbo scelto è usa ma si comporta come raccogli, perché l'oggetto non è nell inv
                //{
                //    let tempPickupName = g_last_room_desc.grrPickupReadableNameTransl;
                //    $(".fraseParzialeInner").text(tempPickupName + " " + gLo1Chosen.ofc_name);
                //}
                //else
                if (gVerbChosen.vfcIsUnary || gSelectedObj.ofcUseMode === 0 /*use-for */) {
                    // mostra la frase completa, ad esempio "usa albero". senza puntini

                    if (gObjectiveChosen === null) {
                        $(".fraseParzialeInner").text(gVerbChosen.vfcName.firstLetterToUpper() + " " + gSelectedObj.ofc_name);
                    }
                    else {
                        $(".fraseParzialeInner").text(g_last_room_desc.grr_in_order_to_translated.firstLetterToUpper() + " " + gObjectiveChosen.readable_name + ", " + gVerbChosen.vfcName + " " + gSelectedObj.ofc_name.toUpperCase());
                    }
                }

                else {
                    if (gSelectedObj.ofc_is_character && gVerbChosen.vfcCharIsAlwaysLast) {
                        // forzo il char per ultimo
                        if (gObjectiveChosen === null) {
                            $(".fraseParzialeInner").html(gVerbChosen.vfcName.firstLetterToUpper() + " " + "..." + " " + gVerbChosen.vfcSecondPart + " " + gSelectedObj.ofc_name);
                        }
                        else {
                            $(".fraseParzialeInner").html(g_last_room_desc.grr_in_order_to_translated.firstLetterToUpper() + " " + gObjectiveChosen.readable_name + ", " + gVerbChosen.vfcName + " " + "..." + " " + gVerbChosen.vfcSecondPart + " " + gSelectedObj.ofc_name);
                        }
                    }
                    else if (!gSelectedObj.ofc_is_character && gVerbChosen.vfcCharIsAlwaysFirst) {
                        // forzo il char per primo
                        if (gObjectiveChosen === null) {
                            $(".fraseParzialeInner").html(gVerbChosen.vfcName.firstLetterToUpper() + " " + "..." + " " + gVerbChosen.vfcSecondPart + " " + gSelectedObj.ofc_name);
                        }
                        else {
                            $(".fraseParzialeInner").html(g_last_room_desc.grr_in_order_to_translated.firstLetterToUpper() + " " + gObjectiveChosen.readable_name + ", " + gVerbChosen.vfcName + " " + "..." + " " + gVerbChosen.vfcSecondPart + " " + gSelectedObj.ofc_name);
                        }
                    }
                    else {
                        if (gObjectiveChosen === null) {
                            $(".fraseParzialeInner").html(gVerbChosen.vfcName.firstLetterToUpper() + " " + gSelectedObj.ofc_name + " " + gVerbChosen.vfcSecondPart.toUpperCase() + " ...");
                        }
                        else {
                            $(".fraseParzialeInner").html(g_last_room_desc.grr_in_order_to_translated.firstLetterToUpper() + " " + gObjectiveChosen.readable_name + ", " + gVerbChosen.vfcName.toUpperCase() + " " + gSelectedObj.ofc_name + " " + gVerbChosen.vfcSecondPart.toUpperCase() + " ...");
                        }

                    }
                }
            }
        }


        //marcaButtonsConPrimoFiglio(".contPersonaggiEOggetti", ".btnObject");

        //marcaButtonsConPrimoFiglio("#contOggettiInv", ".btnObject"); // dato che se premo "ricorda" potrei aver disabilitato alcuni
        //marcaButtonsConPrimoFiglio("#contMindInv", ".btnObject"); // dato che se premo "ricorda" potrei aver disabilitato alcuni
    }


    // inoltre aggiorno l'highlight sul verbo walk.
    //if (gLo1Chosen === null) {
    //    $(".exitVerb").removeClass("btn-primary").removeClass("btn-default").addClass("btn-default");
    //}


    //if (gLo1Chosen !== null) {
    //    $(".btnZeroVerb").addClass("disabled");
    //}
    //else {
    //    $(".btnZeroVerb").removeClass("disabled");
    //}

}

// API client usata da callback asincrone e dal test CLI.
globalThis.updateActionBarAndSelectabilityOfObjects = updateActionBarAndSelectabilityOfObjects;
globalThis.__clientBootstrapReachedUpdateExport = true;

function sleepAsync(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

function sleepUntil(f) {
    return new Promise(resolve => {
        function checkFlag() {
            let flag = f();
            if (flag) {
                resolve();
            }
            else {
                window.setTimeout(checkFlag, 50);
            }
        }
        checkFlag();
    });
}




//let gCurCutScene = null;
//let gCurCutSceneIndex = -1;
//let gRoomDescAfterCutScene = null;
//let gScrollUpAfterCutScene = false;
//let gCloseDialogChoicesAfterCutScene = null;

function serverDoesNotRespond() {

    BootstrapDialog.show({
        title: "error".tr(),
        message: "cantContactServer".tr()
    });
}


async function callStartNewGame(tutorialMode = false, casualMode = null) {
    setTutorialMode(tutorialMode);
    let cred = JSON.parse(localStorage[credentialsId]);
    let inp = {
        lang: getLang()
        , curTime: getCurTime()
        , uname: cred.uname,
        pwd: cred.pwd,
        cred_gameId: gGameId,
        tutorialMode: tutorialMode
        //token: cred.token
    };

    mostraPleaseWait();

    let data = await doPostTry(`${prefissoWebApi}/api/startNewGame`, inp);

    let canContinue = handleErrorsPost(data);
    if (canContinue) {

        nascondiPleaseWait();


        //console.log("ok: ", data.ret);

        //debugger;



        let ar = data.ret.res;
        if (casualMode === null) {
            await chooseGameModeThenRun(ar);
        }
        else {
            const modeData = await setGameModeOnServer(casualMode);
            // setGameMode restituisce una room aggiornata, ma startNewGame
            // aveva già restituito il primo token della cutscene iniziale.
            // Nel tutorial dobbiamo conservare proprio quella risposta:
            // altrimenti la room sovrascrive il preambolo di character.
            if (handleErrorsPost(modeData)) await handleAr(ar);
        }




    }
}

function setGameModeOnServer(casualMode) {
    const cred = JSON.parse(localStorage[credentialsId]);
    const input = {
        uname: cred.uname, pwd: cred.pwd, lang: getLang(),
        curTime: getCurTime(), cred_gameId: gGameId, casualMode: casualMode,
        tutorialMode: g_tutorialMode
    };
    return doPostTry(`${prefissoWebApi}/api/setGameMode`, input);
}

async function chooseGameModeThenRun(ar) {
    return new Promise(resolve => {
        const room = ar?.room || {};
        const proTitle = room.grrProInterfaceTitle || "Interfaccia Pro";
        const proSubtitle = room.grrProInterfaceSubtitle || "Il gioco ti chiede di spiegare cosa pensi succederà, così non rischi di risolvere puzzle per caso mentre sperimenti. Adatta ai puristi dei puzzle.";
        const casualTitle = room.grrCasualInterfaceTitle || "Interfaccia casual";
        const casualSubtitle = room.grrCasualInterfaceSubtitle || "Simile alle interfacce tradizionali. Scegli questa se ti interessa soprattutto la storia e non ti importa se risolverai dei puzzle per caso mentre sperimenti.";

        let completed = false;
        let dialog;
        const choose = async casualMode => {
            if (completed) return;
            completed = true;
            dialog.close();
            const data = await setGameModeOnServer(casualMode);
            if (handleErrorsPost(data)) await handleAr(ar);
            resolve();
        };

        const modeDescriptions = $("<div class='gameModeDescriptions'>");
        const addModeCard = (title, subtitle, casualMode) => {
            const card = $("<div class='gameModeDescription'>")
                .css({
                    border: "1px solid #777",
                    borderRadius: "6px",
                    padding: "12px",
                    marginBottom: "12px"
                });
            $("<div class='gameModeDescriptionTitle'>")
                .css({ fontSize: "1.2em", fontWeight: "bold", marginBottom: "7px" })
                .text(title)
                .appendTo(card);
            $("<div class='gameModeDescriptionSubtitle'>")
                .css({ lineHeight: "1.35", marginBottom: "11px" })
                .text(subtitle)
                .appendTo(card);
            $("<button type='button' class='btn btn-primary gameModeChooseButton'>")
                .text("Scegli questa")
                .css({ width: "100%" })
                .on("click", () => choose(casualMode))
                .appendTo(card);
            card.appendTo(modeDescriptions);
        };
        addModeCard(proTitle, proSubtitle, false);
        addModeCard(casualTitle, casualSubtitle, true);

        const tutorialButtons = $("<div>").css({ marginTop: "12px" });
        const startTutorial = casualMode => {
                if (completed) return;
                completed = true;
                dialog.close();
                callStartNewGame(true, casualMode);
        };
        $("<button type='button' class='btn btn-default'>")
            .text("Gioca tutorial in modalità Normale")
            .css({ width: "100%", marginBottom: "7px" })
            .on("click", () => startTutorial(false))
            .appendTo(tutorialButtons);
        $("<button type='button' class='btn btn-default'>")
            .text("Gioca tutorial in modalità Casual")
            .css({ width: "100%" })
            .on("click", () => startTutorial(true))
            .appendTo(tutorialButtons);

        dialog = BootstrapDialog.show({
            title: "Scegli l'interfaccia",
            message: $("<div>")
                .append($("<p>").text("Scegli come vuoi giocare:"))
                .append(modeDescriptions)
                .append(tutorialButtons),
            closable: false
        });
    });
}


async function callGetHint(objSerId) {
    let cred = JSON.parse(localStorage[credentialsId]);
    let inp = {
        lang: getLang()
        , curTime: getCurTime()
        , uname: cred.uname,
        pwd: cred.pwd,
        gnh_objId: objSerId
        , cred_gameId: gGameId

    };

    mostraPleaseWait();

    let data = await doPostTry(`${prefissoWebApi}/api/getNextHint`, inp);

    let canContinue = handleErrorsPost(data);
    if (canContinue) {


        nascondiPleaseWait();


        //console.log("ok: ", data.ret);

        //debugger;



        let ar = data.ret;

        //debugger;
        await handleAr(ar);





    }
}


async function callGetCurrentHints(objSerId) {
    let cred = JSON.parse(localStorage[credentialsId]);
    let inp = {
        lang: getLang()
        , curTime: getCurTime()
        , uname: cred.uname,
        pwd: cred.pwd,
        gnh_objId: objSerId
        , cred_gameId: gGameId
    };

    mostraPleaseWait();

    let data = await doPostTry(`${prefissoWebApi}/api/getCurrentHints`, inp);

    let canContinue = handleErrorsPost(data);
    if (canContinue) {


        nascondiPleaseWait();


        //console.log("ok: ", data.ret);

        //debugger;

        return data.ret;




    }
    else {
        return null;
    }
}


async function callRemember(loId, isPickup, isUseHere, isLook) {
    let cred = JSON.parse(localStorage[credentialsId]);
    let i = {
        uname: cred.uname,
        pwd: cred.pwd,
        lo_id: loId,
        lang: getLang()
        , curTime: getCurTime()
        , cred_gameId: gGameId
    };

    //console.log("remember: ", i);


    mostraPleaseWait();

    let data;

    if (isUseHere) {
        data = await doPostTry(`${prefissoWebApi}/api/useHere`, i);
    }
    else if (isLook) {
        data = await doPostTry(`${prefissoWebApi}/api/look`, i);
    }
    else if (isPickup) {
        data = await doPostTry(`${prefissoWebApi}/api/pickup`, i);
    }
    else {
        data = await doPostTry(`${prefissoWebApi}/api/remember`, i);
    }
    let canContinue = handleErrorsPost(data);
    if (canContinue) {
        nascondiPleaseWait();

        //console.log("ok: ", data.ret);


        let ar = data.ret;

        await handleAr(ar);


    }

}


async function callUpdateObjectives(osiObjectivesSeen) {
    let cred = JSON.parse(localStorage[credentialsId]);
    let i = {
        uname: cred.uname,
        pwd: cred.pwd,

        lang: getLang()
        , curTime: getCurTime()
        , osiObjectivesSeen: osiObjectivesSeen
        , cred_gameId: gGameId
    };

    //console.log("markObjectivesSeen: ", i);


    //mostraPleaseWait();

    let data;


    data = await doPostTry(`${prefissoWebApi}/api/markObjectivesSeen`, i);

    let canContinue = handleErrorsPost(data);
    if (canContinue) {
        //nascondiPleaseWait();

        //console.log("markObjectivesSeen returned: ", data.ret);

        // non devo fare niente

        //let ar = data.ret;

        //await handleAr(ar);


    }

}

async function callTalkHere() {


    gSelectedVerb = null;
    gSelectedObj = null;

    let cred = JSON.parse(localStorage[credentialsId]);
    let i = {
        uname: cred.uname,
        pwd: cred.pwd,

        lang: getLang()
        , curTime: getCurTime()
        , cred_gameId: gGameId
    };

    //console.log("talk here: ", i);


    mostraPleaseWait();

    let data;


    data = await doPostTry(`${prefissoWebApi}/api/talkHere`, i);


    let canContinue = handleErrorsPost(data);
    if (canContinue) {
        nascondiPleaseWait();

        //console.log("ok: ", data.ret);


        let ar = data.ret;

        await handleAr(ar);


    }

}


async function runTutorialPrompt(kind, firstObjectId, secondObjectId, continuation) {
    if (!g_tutorialMode) {
        return await continuation();
    }

    let cred = JSON.parse(localStorage[credentialsId]);
    let input = {
        uname: cred.uname,
        pwd: cred.pwd,
        tpiKind: kind,
        tpiFirstObjectId: firstObjectId,
        tpiSecondObjectId: secondObjectId,
        lang: getLang(),
        curTime: getCurTime(),
        cred_gameId: gGameId,
        tutorialMode: true
    };

    // La scena introduttiva viene gestita da handleAr, che al suo termine
    // ricostruisce la stanza e deseleziona gli oggetti. Conserviamo il primo
    // oggetto selezionato e lo ripristiniamo prima di riprendere l'azione:
    // senza questo passaggio onLoClickedRoom non vede più una coppia da
    // completare e la modal delle explanation non può aprirsi.
    const selectedObjectBeforePrompt = gSelectedObj;
    const continuationAfterPrompt = async () => {
        if (selectedObjectBeforePrompt != null) {
            gSelectedObj = selectedObjectBeforePrompt;
            gSelectedVerb = null;
            updateToolbar();
        }
        await continuation();
    };

    let data = await doPostTry(`${prefissoWebApi}/api/tutorialPrompt`, input);
    if (!handleErrorsPost(data)) {
        return;
    }

    let ar = data.ret;
    if (ar != null && ar.nextCutSceneToken != null) {
        g_afterTutorialPrompt = continuationAfterPrompt;
        await handleAr(ar);
    }
    else {
        await continuationAfterPrompt();
    }
}


async function callUseWith(loId1, loId2, expId, youAlreadyKnowItWillFail) {

    // Casual non usa mai la preview locale dell'azione. Pulisci subito anche
    // eventuali residui lasciati da un percorso precedente del compositore.
    if (g_last_room_desc?.grrCasualMode === true) {
        $(".textModeFraseComposta").html("&nbsp;").removeClass("fallisce");
    }

    let cred = JSON.parse(localStorage[credentialsId]);
    let i = {
        uname: cred.uname,
        pwd: cred.pwd,
        //token: cred.token,
        uwaLoId1: loId1,
        uwaLoId2: loId2,
        uwaExplanationId: expId,
        uwaAlreadyKnowItFails: youAlreadyKnowItWillFail,
        lang: getLang()
        , curTime: getCurTime()
        , cred_gameId: gGameId
    };

    //console.log("use with: ", i);

    if (!youAlreadyKnowItWillFail) {
        mostraPleaseWait();
    }

    let data;


    data = await doPostTry(`${prefissoWebApi}/api/useWith`, i);

    let canContinue = handleErrorsPost(data);
    if (canContinue) {
        if (!youAlreadyKnowItWillFail) {
            nascondiPleaseWait();
        }

        //console.log("ok: ", data.ret);


        let ar = data.ret;

        await handleAr(ar, false, youAlreadyKnowItWillFail);


    }


}


async function callUseFor(loId, objId, expId) {

    let cred = JSON.parse(localStorage[credentialsId]);
    let i = {
        uname: cred.uname,
        pwd: cred.pwd,
        //token: cred.token,
        ufiLoId: loId,
        ufiObjId: objId,
        ufiExpId: expId,

        lang: getLang()
        , curTime: getCurTime()
        , cred_gameId: gGameId
    };

    //console.log("use with: ", i);


    mostraPleaseWait();


    let data;

    //await delay(14000);
    data = await doPostTry(`${prefissoWebApi}/api/useFor`, i);

    let canContinue = handleErrorsPost(data);
    if (canContinue) {

        nascondiPleaseWait();


        //console.log("ok: ", data.ret);


        let ar = data.ret;

        await handleAr(ar);


    }


}



async function callIsActually(loId, ex1Id, ex2Id) {

    let cred = JSON.parse(localStorage[credentialsId]);
    let i = {
        uname: cred.uname,
        pwd: cred.pwd,
        iaLoId: loId,
        iaExp1Id: ex1Id,
        iaExp2Id: ex2Id,

        lang: getLang()
        , curTime: getCurTime()
        , cred_gameId: gGameId
    };

    //console.log("is actually: ", i);


    mostraPleaseWait();


    let data;

    //await delay(14000);
    data = await doPostTry(`${prefissoWebApi}/api/isActually`, i);

    let canContinue = handleErrorsPost(data);
    if (canContinue) {

        nascondiPleaseWait();


        //console.log("ok: ", data.ret);


        let ar = data.ret;

        await handleAr(ar);


    }


}


async function callUseInComposer(loId, pezzi, teId, fiId1, fiId2) {
    let cred = JSON.parse(localStorage[credentialsId]);
    let i = {
        uname: cred.uname,
        pwd: cred.pwd,
        //token: cred.token,
        uwcLoId: loId,
        uwcTemplateId: teId,
        uwcFillerId1: fiId1,
        uwcFillerId2: fiId2,
        uwcPezzi: pezzi,
        lang: getLang()
        , curTime: getCurTime()
        , cred_gameId: gGameId
    };

    //console.log("use in composer: ", i);


    mostraPleaseWait();

    let data;


    data = await doPostTry(`${prefissoWebApi}/api/useInComposer`, i);

    let canContinue = handleErrorsPost(data);
    if (canContinue) {
        nascondiPleaseWait();

        //console.log("ok: ", data.ret);


        let ar = data.ret;

        await handleAr(ar);


    }

}


async function sceltoUnVerboEUnOggetto(vfc, loChosen, roomDesc) {


    // ho cliccato un oggetto dopo aver cliccato un verbo.
    // se il verbo è unario e non richiede il puzzle, devo chiamare web api con binAction
    // se il verbo è unario e richiede il puzzle, devo mostrare PER...
    // se il verbo è binario, devo chiedere il secondo oggetto, quindi solo aggiornare la actionbar.

    //debugger;

    if (vfc.vfcIsUnary && vfc.vfc_is_remember) {
        await callRemember(loChosen.loId, false, false, false);
        //debugger;

    }
    else if (vfc.vfcIsUnary && !vfc.vfcRequiresPuzzle) { // pick up


        updateActionBarAndSelectabilityOfObjects(); // si vede in trasparenza quindi aggiorniamola

        //await sleepAsync(msPerFarVederePressione); //lo tolgo perché non ho più la action bar


        let cred = JSON.parse(localStorage[credentialsId]);
        let i = {
            uname: cred.uname,
            pwd: cred.pwd,
            //token: cred.token,

            unoLoId: loChosen.loId,
            unoUnVerbId: vfc.vfcSerId,

            lang: getLang()
            , curTime: getCurTime()
            , cred_gameId: gGameId
        };

        //console.log("remember: ", i);


        mostraPleaseWait();

        let data = await doPostTry(`${prefissoWebApi}/api/unNoObAction`, i);
        let canContinue = handleErrorsPost(data);
        if (canContinue) {
            nascondiPleaseWait();

            //console.log("ok: ", data.ret);


            let ar = data.ret;

            await handleAr(ar);


        }
    }
    else if (vfc.vfcIsUnary && !vfc.vfc_is_remember && vfc.vfcRequiresPuzzle) {
        // devo eseguire azione PER 



        updateActionBarAndSelectabilityOfObjects(); // si vede in trasparenza quindi aggiorniamola
        //await sleepAsync(msPerFarVederePressione); lo tolgo perché non ho più la action bar




        let cred = JSON.parse(localStorage[credentialsId]);
        let i = {
            uname: cred.uname,
            pwd: cred.pwd,
            //token: cred.token,
            taiuUnVerbId: vfc.vfcSerId,
            taiuLoId: loChosen.loId,
            taiuPuzId: gObjectiveChosen.ser_id,
            lang: getLang()
            , curTime: getCurTime()
            , cred_gameId: gGameId
        };

        //console.log("ternario. facendo post: ", i);

        $("#divObjectives").modal("hide");

        mostraPleaseWait();

        let data = await doPostTry(`${prefissoWebApi}/api/terActionUn`, i);
        let canContinue = handleErrorsPost(data);
        if (canContinue) {
            nascondiPleaseWait();

            //console.log("ok: ", data.ret);


            let ar = data.ret;

            await handleAr(ar);


        }





        $("#divObjectives").modal("show");

    }



    //else if (vfc.vfcIsBinary
    //    && loChosen.ofcUseInLocation  // l'oggetto si usa in un posto preciso
    //    //&& loChosen.ofc_is_in_inv // altrimenti deve cercare di prenderlo
    //) {
    //    // devo chiamare l'azione nel posto preciso
    //    //debugger;
    //    let cred = JSON.parse(localStorage[credentialsId]);
    //    let i = {
    //        uname: cred.uname,
    //        pwd: cred.pwd,
    //        token: cred.token,
    //        uilBinVerbId: vfc.vfcSerId,
    //        uilLoId: loChosen.loId,
    //        uilRoomId: roomDesc.grrCurRoomId,
    //        uilPuzId: gObjectiveChosen.ser_id,
    //        lang: getLang()
    //    };

    //    console.log("use in location. facendo post: ", i);

    //    $("#divObjectives").modal("hide");

    //    mostraPleaseWait();

    //    let data = await doPost(`${prefissoWebApi}/api/useInLocationAction`, i);


    //    if (data.errore) {
    //        console.log("errore", data.errore);
    //    }
    //    else {
    //        $("#waitingServer").hide();

    //        console.log("ok: ", data.ret);


    //        let ar = data.ret;

    //        await handleAr(ar);


    //    }

    //}
    else if (
        vfc.vfcIsBinary /* è usa */
        && loChosen.ofcUseMode === 1 // UseWith   --- e l'oggetto scelto si usa con un altro

        //&& (loChosen.ofc_is_in_inv

        //    //|| !loChosen.ofcCouldPotentiallyBePickedUp// altrimenti devo cmq lanciare pickup (novità)
        //    )

    ) {
        // comportamento normale, non lancio pickup
        updateActionBarAndSelectabilityOfObjects(); // si vede in trasparenza quindi aggiorniamola

    }

    //else if ( 
    //    vfc.vfcIsBinary /* è usa */

    //    && loChosen.ofcCouldPotentiallyBePickedUp

    //    // non importa piu
    //    //&& loChosen.ofcUseMode === 1 // UseBinaryAsTool    e l'oggetto scelto si usa con un altro


    //    && !loChosen.ofc_is_in_inv // devo cmq lanciare pickup (novità)
    //) {

    //    updateActionBarAndSelectabilityOfObjects(); // si vede in trasparenza quindi aggiorniamola
    //    await sleepAsync(msPerFarVederePressione);


    //    let cred = JSON.parse(localStorage[credentialsId]);
    //    let i = {
    //        uname: cred.uname,
    //        pwd: cred.pwd,
    //        token: cred.token,

    //        unoLoId: loChosen.loId,
    //        unoUnVerbId: g_last_room_desc.grrPickupVerbId,

    //        lang: getLang()

    //    };

    //    console.log("remember: ", i);


    //    mostraPleaseWait();

    //    let data = await doPost(`${prefissoWebApi}/api/unNoObAction`, i);


    //    if (data.errore) {
    //        console.log("errore", data.errore);
    //    }
    //    else {
    //        $("#waitingServer").hide();

    //        console.log("ok: ", data.ret);


    //        let ar = data.ret;

    //        await handleAr(ar);


    //    }

    //}
    else if (vfc.vfcIsBinary && vfc.vfcCanBeUnaryOrBinaryDependingOnObject // ho scelto il verbo usa
        && loChosen.ofcUseMode === 0 /* UseFor       ---  e l'oggetto scelto è unario */
    ) {



        // devo eseguire azione usefor 


        updateActionBarAndSelectabilityOfObjects(); // si vede in trasparenza quindi aggiorniamola
        //await sleepAsync(msPerFarVederePressione);//lo tolgo perché non ho più la action bar



        let cred = JSON.parse(localStorage[credentialsId]);
        let i = {
            uname: cred.uname,
            pwd: cred.pwd,
            //token: cred.token,
            taiBinVerbId: vfc.vfcSerId,
            taiLoId: loChosen.loId,
            taiPuzId: gObjectiveChosen.ser_id,
            lang: getLang()
            , curTime: getCurTime()
            , cred_gameId: gGameId
        };

        //console.log("ternario. facendo post: ", i);


        mostraPleaseWait();

        let data = await doPostTry(`${prefissoWebApi}/api/terAction`, i);
        let canContinue = handleErrorsPost(data);
        if (canContinue) {
            nascondiPleaseWait();

            //console.log("ok: ", data.ret);


            let ar = data.ret;

            await handleAr(ar);


        }


    }

}


function selezionaUscita(ofc) {
    if (ofc.is_obvious_exit) {
        // se è un'uscita ovvia, devo illuminare il verbo exit-through.
        $(".exitVerb").removeClass("btn-default").addClass("btn-primary");

        $(".invNew").scrollTop(0); // l'inv deve scrollare in alto perché il pulsante walk-through potreb non essere visibile
    }
    else {
        $(".exitVerb").removeClass("btn-primary").addClass("btn-default");
    }

}













function changeImgSrcAndDoNotWait(img, src) {

    //$(img).hide();

    let outsideResolve;
    let pr = new Promise((resolve, reject) => {
        outsideResolve = resolve;
    });

    img.off('error').on('error', function () {
        outsideResolve();
    });

    img.off("load").on("load", function () {
        outsideResolve();

    });

    img.attr("src", src);

    return pr;

    //if (!noImgMode) {
    //$(img).show(); // setta display inline
    //}

}



function calcolaFraseParzialeConUnOggettoEUnVerboSelez() {


    let titoloDialog;


    titoloDialog = "{1} {2} {3} {4}"; // tradurre


    //titoloDialog = titoloDialog.replace("{1}", gVerbChosen.vfcName.firstLetterToUpper())
    //    .replace("{2}", gLo1Chosen.ofc_name).replace("{3}", gVerbChosen.vfcSecondPart).replace("{4}", "...");


    // non mi piace più questa logica ad hoc per i personaggi
    if (gSelectedObj.ofc_is_character && gVerbChosen.vfcCharIsAlwaysLast) {
        // forzo char per ultimo
        titoloDialog = titoloDialog.replace("{1}", gVerbChosen.vfcName.toUpperCase())
            .replace("{2}", "...").replace("{3}", gVerbChosen.vfcSecondPart.toUpperCase()).replace("{4}", gSelectedObj.ofc_name);
    }
    else if (gSelectedObj.ofc_is_character && gVerbChosen.vfcCharIsAlwaysFirst) {
        // forzo cha per primo
        titoloDialog = titoloDialog.replace("{1}", gVerbChosen.vfcName.toUpperCase())
            .replace("{2}", gSelectedObj.ofc_name).replace("{3}", gVerbChosen.vfcSecondPart.toUpperCase()).replace("{4}", "...");
    }
    else { // lascio l'ordine scelto dall'utente
        titoloDialog = titoloDialog.replace("{1}", gVerbChosen.vfcName.toUpperCase())
            .replace("{2}", gSelectedObj.ofc_name).replace("{3}", gVerbChosen.vfcSecondPart.toUpperCase()).replace("{4}", "...");
    }

    return titoloDialog;
}



function drawline(ax, ay, bx, by) {
    //console.log('ax: ' + ax);
    //console.log('ay: ' + ay);
    //console.log('bx: ' + bx);
    //console.log('by: ' + by);


    if (ax > bx) {
        bx = ax + bx;
        ax = bx - ax;
        bx = bx - ax;
        by = ay + by;
        ay = by - ay;
        by = by - ay;
    }


    //console.log('ax: ' + ax);
    //console.log('ay: ' + ay);
    //console.log('bx: ' + bx);
    //console.log('by: ' + by);

    var angle = Math.atan((ay - by) / (bx - ax));
    //console.log('angle: ' + angle);

    angle = (angle * 180 / Math.PI);
    //console.log('angle: ' + angle);
    angle = -angle;
    //console.log('angle: ' + angle);

    var length = Math.sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
    //console.log('length: ' + length);

    var style = "";
    style += "left:" + (ax) + "px;";
    style += "top:" + (ay) + "px;";
    style += "width:" + length + "px;";
    style += "height:1px;";
    style += "background-color:black;";
    style += "position:absolute;";
    //style += "transform:scale(0.6,0.6);";
    style += "transform:rotate(" + angle + "deg);";
    style += "-ms-transform:rotate(" + angle + "deg);";
    style += "transform-origin:0% 0%;";
    style += "-moz-transform:rotate(" + angle + "deg);";
    style += "-moz-transform-origin:0% 0%;";
    style += "-webkit-transform:rotate(" + angle + "deg);";
    style += "-webkit-transform-origin:0% 0%;";
    style += "-o-transform:rotate(" + angle + "deg);";
    style += "-o-transform-origin:0% 0%;";
    //style += "-webkit-box-shadow: 0px 0px 1px 1px rgba(0, 0, 0, .1);";
    //style += "box-shadow: 0px 0px 1px 1px rgba(0, 0, 0, .1);";
    style += "z-index:99;";
    $("<div class='dyn_line' style='" + style + "'></div>").appendTo("#mapInner");
}

function rebuildMapButtons() {


    // ricostruisco i pulsanti della mappa perché potrebbero aver cambiato visibilità 
    $("#mapInner .btnLocation").remove();

    //console.log("roomDesc.grrRoomCoords.len = ", g_last_room_desc.grrRoomCoords.length);

    //debugger;

    // disegno le linee dinamiche
    //$(".dyn_line").remove();
    //g_last_room_desc.grrDynLines.forEach(line => {
    //    if (line.dlcIsVisibleNow) {
    //        //debugger;
    //        let mul = 1.335; // trovato a tentativi
    //        //console.log(`linea da ${line.startPoint.x}, ${line.startPoint.y}, ${line.endPoint.x}, ${line.endPoint.y} `);
    //        drawline(mul * line.dlcStartPoint.x, mul * line.dlcStartPoint.y, mul * line.dlcEndPoint.x, mul * line.dlcEndPoint.y);
    //    }
    //});



    //disegno i pulsanti room
    for (let rc of g_last_room_desc.grrRoomCoords) {
        let btnRoomMap = $("<button>").addClass("btn btnLocation");

        btnRoomMap.attr("roomId", rc.rcRoomId);



        if (rc.rcX != null && rc.rcY != null) {
            // MapEditor coordinates and background geometry are canvas pixels.
            // Do not use CSS points here: 1pt is 1.333px in the browser and
            // would make the buttons drift relative to the exported image.
            btnRoomMap.attr("style", `left:${Number(rc.rcX)}px; top:${Number(rc.rcY)}px`);


            //let mul =  1.335;
            //drawline(rc.rcX * mul, rc.rcY * mul, (rc.rcX + 100) * mul, (rc.rcY + 50) * mul);

            if (rc.rcIsCurRoom) {
                btnRoomMap.addClass("btn-primary");

            }
            else if (!rc.rcAlreadyVisitedOnce && rc.rcIsAccessibleFromHere && !rc.rcIsCurRoom) {
                btnRoomMap.addClass("btnNeverVisitedPlace btn-default");
            }
            else {
                btnRoomMap.addClass("btn-default");
                btnRoomMap.addClass("btnCurRoom");
            }

            let devoFarVedereComeAccessibileQuestoPulsante;

            devoFarVedereComeAccessibileQuestoPulsante = rc.rcIsAccessibleFromHere;


            if (!devoFarVedereComeAccessibileQuestoPulsante) {
                btnRoomMap.addClass("unvisited");
                btnRoomMap.text("?");

                btnRoomMap.click(async function (e) {
                    e.preventDefault();



                    {
                        BootstrapDialog.show({
                            title: "tooFar".tr(),
                            message: "youCanOnlyMove".tr() // 'Puoi spostarti solo in luoghi già visitati, o vicini a luoghi già visitati.'
                        });
                    }

                });
            }
            else {
                btnRoomMap.text(rc.rcRoomName.firstLetterToUpper());

                btnRoomMap.click(async function (e) {



                    e.preventDefault();

                    // se ero in modalità picker, devo mostrare gli oggetti della location. altrimenti devo andare in quella locazione


                    {



                        // devo andare in quella locazione
                        if (rc.rcIsCurRoom) {
                            // non serve chiamare web api, chiudi la mappa e basta
                            $("#mapOuterOuter").hide();
                        }
                        else {

                            let cred = JSON.parse(localStorage[credentialsId]);
                            let i = {
                                uname: cred.uname,
                                pwd: cred.pwd,
                                //token: cred.token,
                                qmiRoomId: rc.rcRoomId,
                                lang: getLang()
                                , curTime: getCurTime()
                                , cred_gameId: gGameId
                            };

                            mostraPleaseWait();
                            let data = await doPostTry(`${prefissoWebApi}/api/quickMove`, i);


                            //data.errore = "conn-error"; // temp

                            let canContinue = handleErrorsPost(data);
                            if (canContinue) {

                                //console.log("output quickmove: ", data.ret);
                                nascondiPleaseWait();

                                $("#mapOuterOuter").hide();



                                // forzo il rebuild room se lo spostamento non ha prodotto cutscene
                                let forceRoom;
                                let ar = data.ret;
                                forceRoom = (ar.room && ar.nextCutSceneToken === null);



                                await handleAr(data.ret, forceRoom);


                            }

                        }
                    }

                });


            }


            btnRoomMap.appendTo("#mapInner");












            // lo scroll di deve fare quando faccio show()
            //if (rc.rcIsCurRoom) {

            //    //console.log("scrollando");
            //    $("#mapOuter").scrollTop(400);
            //    //$("#mapInner").scrollLeft(300);
            //}

        }
    }






}



function pscMatcha(pscSolutionCur  /*il resto della soluzione da matchare*/, selectedCur /*il resto della frase inserita*/, objsInRoomMatchanoSempre, lastMatchedEnu = null) {
    if (selectedCur.length === 0) {
        return { matcha: true, nextInSentence: head(pscSolutionCur), lastMatchedEnu: lastMatchedEnu };
    }

    if (head(pscSolutionCur) === null) {
        return { matcha: false, nextInSentence: null, lastMatchedEnu: lastMatchedEnu };
    }


    if (!head(selectedCur).isEnu) {
        if (objsInRoomMatchanoSempre) {
            return pscMatcha(tail(pscSolutionCur), tail(selectedCur), objsInRoomMatchanoSempre, lastMatchedEnu);
        }
        else {
            if (head(pscSolutionCur).oir_loIdCorrect === head(selectedCur).oir_loIdCorrect) {
                // se è un object in room, fingo sempre che matchi, perché devo cmq presentare la continuazione in caso di errore come se avesse matchato. questo aveva senso quando io avevo "dì a X che Y, e per qualunque X dovevo sempre presentare la stessa continuazione, anche se X non matchava.
                // ma adesso mi serve continuazione specifica per ogni oggetto, altrimenti se clicchi "usa spruzzatore" ed è sbagliato, non presenta "in".
                return pscMatcha(tail(pscSolutionCur), tail(selectedCur), objsInRoomMatchanoSempre, lastMatchedEnu);
            }
            else {
                return { matcha: false, nextInSentence: null, lastMatchedEnu: lastMatchedEnu };
            }
            // obsoleto - se è un object in room, fingo sempre che matchi, perché devo cmq presentare la continuazione in caso di errore come se avesse matchato

        }
    }
    else if (head(selectedCur).isEnu && head(pscSolutionCur).etc_qtokCorrect === head(selectedCur).qt_serId) {
        return pscMatcha(tail(pscSolutionCur), tail(selectedCur), objsInRoomMatchanoSempre, head(pscSolutionCur));
    }
    else {
        return { matcha: false, nextInSentence: null, lastMatchedEnu: lastMatchedEnu };
    }
}


function svuotaScelteDaFare() {
    $(".bloccoDaScegliereSentence .btnSceltaSentence").remove();
    $(".bloccoDaScegliereSentence .titoloSeparatoreCompositoreSentence").remove();
}
function aggiungiSceltaFatta(testo, maiuscolo) {
    var newScelto = $('<div class="parSentence nonObiettivo">').text(testo).appendTo(".bloccoObiettivo");
    if (maiuscolo) {
        newScelto.addClass("maiusc");
    }
    $("#indietroDiUno").removeClass("disabled");
}

var gDynamicQtokenToAdd = [];
var gDynamicQtokenToAddFromObjectives = [];

function aggiungiClickHandlerRoomInv(ofc, newBtn, ob) {
    if (!newBtn.hasClass("disabled")) {
        newBtn.click(async e => {

            //devo settare la persona, perché adesso , con il verbo "deduci che", il room obj può avere subito una chiusura dopo di lui
            let mainQtokSerId = gQualePersonaForNext = ofc.ofcAssociatedQtokens[0];
            let mainQtok = g_last_room_desc.dicQtokOfSerId[mainQtokSerId];
            gQualePersonaForNext = mainQtok.qt_qualePersona;



            gFraseCompostaFinora.push({ isEnu: false, oir_loIdCorrect: ofc.loId, psi_readableName: newBtn.text() /*ofc.ofc_name*/, qt_loId: ofc.loId });
            svuotaScelteDaFare();
            aggiungiSceltaFatta(newBtn.text()/*ofc.ofc_name*/, false);



            // devo creare anche il qtoken corrispondente, enumerated token . così può comporre la frase tipo: USE tiro a segno SO THAT tiro a segno...
            // se non ha un enumerated token associato, devo crearlo fittizio
            {
                gDynamicQtokenToAdd = [];
                ofc.ofcAssociatedQtokens.forEach(qt => {
                    gDynamicQtokenToAdd.push(g_last_room_desc.dicQtokOfSerId[qt]);

                });

                // non faccio più il fittizio perché non avrebbe il ser_id e quindi uscirebbe duplicato, nel caso anche l'obiettivo avesse quell oggetto logico
                if (ofc.ofcAssociatedQtokens.length === 0) {
                    throw "Missing ofcAssociatedQtokens";
                    //    // llo creo fittizio perche possa andare avanti, fallendo ovviamente
                    //    gDynamicQtokenToAdd.push({ isEnu: true, qt_serId: "ser-id-fittizio", qt_qualePersona : "heShe", qt_readableNameHeShe: ofc.ofc_name, qt_readableNameThey: ofc.ofc_name, qt_readableNameYou: ofc.ofc_name, qt_loId: null, qt_isStillUnknown: false, qt_IsBecause: false, qt_IsSoThat: false });
                }


            }

            //{ // anche gli obiettivi creano automaticamente un token
            //    gDynamicQtokenToAddFromObjectives = [];
            //    ob.ocAssociatedQtokens.forEach(qt => {
            //        gDynamicQtokenToAddFromObjectives.push(g_last_room_desc.dicQtokOfSerId[qt]);

            //    });


            //}



            await prossimoToken(ob);


        });
    }

}



//function vaEsclusoDinamicamente(qt, ob) {

//    let vaEscluso = g_last_room_desc.grrDynamicExclusions.some(ex => {
//        return ex.dec_objSerId === ob.ser_id

//            &&
//            ex.dec_qtokToExclude === qt.qt_serId
//            &&
//            (ex.dec_qtoksChosen.length === 0
//                ||
//                gFraseCompostaFinora.some(qtInserito => ex.dec_qtoksChosen.some(qte => qte === qtInserito.qt_serId))
//            )
//            ;

//    });
//    if (vaEscluso) {
//        console.log("escludo");
//    }
//    return vaEscluso;
//}


////////

function updatePlayArrowComposer() {
    // devo vedere se devo abilitare il pulsante invia
    let disabilitato = false;
    let corretto;


    let filler_id_droppati = $(".templateObjectComposerNew.dropped ").map((i, el0) => {
        //debugger;
        let el = $(el0);
        return el.attr("filler_id");
    });
    //debugger;

    let esisteUnFillerNonSpecificato = false;
    $(".pezzoComp.pezzoCliccab").each((i, el0) => {


        let el = $(el0);

        let elementoDropped = el.find(".templateObjectComposerNew.dropped ");
        if (elementoDropped.length == 0) {
            esisteUnFillerNonSpecificato = true;
            return false;
        }
    });

    if (esisteUnFillerNonSpecificato) {
        disabilitato = true;
        corretto = null;
    }
    else {
        // secondo controllo:  questo oggetto ha un handler con quel template e quei filler

        let ilTemplateCombacia;

        ilTemplateCombacia = gLoOfComposer.ofcCompatibleTemplates.any(x => x.teId == gTemplatesToUse[gICurTemplateInComposer].teId

            && x.fiIds.every((fiId, indexFiId) => {
                let fillerCheDeveCorrispondere = filler_id_droppati[indexFiId];
                return fillerCheDeveCorrispondere == fiId;
            }));

        disabilitato = false; //!ilTemplateCombacia;
        corretto = ilTemplateCombacia;
    }

    if (!disabilitato) {
        $(".btnComposerAvanti ").removeClass('disabled');
        if (corretto) {
            $(".playIconImgDisabled").hide();
            $(".playIconImg").show();
        }
        else {
            $(".playIconImgDisabled").show();
            $(".playIconImg").hide();
        }

    }
    else {
        $(".btnComposerAvanti ").addClass('disabled');
        $(".playIconImgDisabled").hide();
        $(".playIconImg").show();
    }


}





async function showDialogIsActually(ofc, skipTutorialPrompt = false) {

    if (g_tutorialMode && !skipTutorialPrompt) {
        await runTutorialPrompt("IsActually", ofc.loId, null,
            () => showDialogIsActually(ofc, true));
        return;
    }



    function updateEnabledSubmit() {

        let exp1 = $("#dialogIsActually .radioExplan:checked");

        let exp2val = $(".dropdIsActually").val();
        if (exp1.length > 0 && exp2val != "") {

            $("#submitIsActually").removeClass("disabled");
        }
        else {
            $("#submitIsActually").addClass("disabled");
        }
    }


    let ofcNameWithArticleMaybe;
    if (ofc.ofcNameWithArticle != null && ofc.ofcNameWithArticle != "") {
        ofcNameWithArticleMaybe = ofc.ofcNameWithArticle;
    }
    else {
        ofcNameWithArticleMaybe = ofc.ofc_name;
    }

    let istanziato;
    istanziato = "deduciChe1Puntini".tr().replace('{1}', ofcNameWithArticleMaybe).firstLetterToUpper();

    $(".introTextInput.primo").text(istanziato);



    $(".dropdIsActually").off('change').change(e => {
        updateEnabledSubmit();
    });

    $(".optIsActually").remove();
    $(".dropdIsActually").hide();

    $(".templateRadioExplanation").remove();

    for (let ec of g_last_room_desc.grrExplanationsWithCont) { //

        var newDiv = gTemplateRadioExplanation.clone();
        newDiv.appendTo("#dialogIsActually .containerExplanations.level1 ");
        newDiv.find('.testoRadioExplan').html(ec.exName);
        newDiv.find('.radioExplan').attr("exp_id", ec.expId);


        newDiv.find('.radioExplan').val(ec.expId);

        newDiv.find('.radioExplan').change(e => {
            // devo ricostruire la dropdown di secondo livello

            let expGiustaEl = $("#dialogIsActually .radioExplan:checked");
            if (expGiustaEl.length > 0) {
                let expGiustaId = expGiustaEl.attr("exp_id");
                let expGiusta = g_last_room_desc.grrExplanationsWithCont.filter(ex => ex.expId == expGiustaId)[0];

                $(".optIsActually").remove();

                $("<option class='optIsActually'>").appendTo(".dropdIsActually").text("").attr("value", "");

                for (let ex of expGiusta.eclContinuations) {
                    let newOption = $("<option class='optIsActually'>").appendTo(".dropdIsActually").text(ex.exName).attr("value", ex.expId);
                }

                $(".dropdIsActually").show();
            }
            else {
                $(".dropdIsActually").hide();
            }


            updateEnabledSubmit();
        });

    }






    $("#submitIsActually").off('click').on('click', async e => {

        e.preventDefault();

        if ($("#submitIsActually").hasClass("disabled")) {
            return;
        }

        $("#submitIsActually").addClass('disabled');


        let checkedGroup = $("#dialogIsActually .radioExplan:checked");

        let explId = checkedGroup.first().val();

        let exp2Id = $(".dropdIsActually").val();

        await callIsActually(ofc.loId, explId, exp2Id);

    });


    updateEnabledSubmit();

    $("#dialogIsActually").modal("show");




}










async function showDialogUseFor(ofc, skipTutorialPrompt = false) {

    if (g_tutorialMode && !skipTutorialPrompt) {
        await runTutorialPrompt("UseFor", ofc.loId, null,
            () => showDialogUseFor(ofc, true));
        return;
    }


    function useForNeedsExplanation(ob) {
        const exact = ofc.ofcUseForExactExplanationByObjective?.[ob.ser_id];
        // An exact handler always wins, including an exact handler without
        // explanation.  Otherwise use the objective-level fallback.
            return !g_last_room_desc.grrCasualMode && (exact === undefined ? !ob.obcDoNotShowExplanations : exact);
    }


    function updateEnabledSubmitUseFor() {

        //debugger;
        let objId = $(".selectObiettivo").val();
        console.log("[UseForJS] updateEnabledSubmit objId=", objId);

        if (objId == '') {
            $("#submitUseFor").addClass('disabled');
            $("#submitUseFor").removeClass('blink_me_2');
        }
        else {
            let ob = g_last_room_desc.grrObjectives.filter(x => x.ser_id == objId)[0];
            console.log("[UseForJS] selected ob=", ob?.ser_id, "label=", ob?.readable_name, "doNotShow=", ob?.obcDoNotShowExplanations);
            //let obiettivoNonHaSpiegazioni = .obcDoNotShowExplanations;

            if (!useForNeedsExplanation(ob)) {

                let mustFlash = $("#submitUseFor").hasClass('disabled');

                $("#submitUseFor").removeClass('disabled');
                if (mustFlash) {
                    $("#submitUseFor").addClass('blink_me_2');
                }
            }
            else {
                let checked = $("#dialogUseFor .radioExplan:checked");
                if (checked.length == 0) {
                    $("#submitUseFor").addClass('disabled');
                    $("#submitUseFor").removeClass('blink_me_2');
                }
                else {
                    let mustFlash = $("#submitUseFor").hasClass('disabled');
                    $("#submitUseFor").removeClass('disabled');

                    if (mustFlash) {
                        $("#submitUseFor").addClass('blink_me_2');
                    }
                }
            }
        }

    }

    let ofcNameWithArticleMaybe;
    if (ofc.ofcNameWithArticle != null && ofc.ofcNameWithArticle != "") {
        ofcNameWithArticleMaybe = ofc.ofcNameWithArticle;
    }
    else {
        ofcNameWithArticleMaybe = ofc.ofc_name;
    }

    // costruisci la frase PER fare X, usa Y

    let istanziato;

    if (ofc.ofcVerbWhenUseForInDialogIntro != null) {
        istanziato = ofc.ofcVerbWhenUseForInDialogIntro.replace('{1}', ofcNameWithArticleMaybe).firstLetterToUpper();
    }
    else {
        istanziato = "use1For".tr().replace('{1}', ofcNameWithArticleMaybe).firstLetterToUpper();
    }

    $(".introTextInput.primo").text(istanziato);


    $(".itemUseForObj").remove();

    $("<option class='itemUseForObj' value=''>").appendTo(".selectObiettivo");

    for (let ob of g_last_room_desc.grrObjectives) {
        let newOption = $("<option class='itemUseForObj'>").appendTo(".selectObiettivo").text(ob.readable_name).attr("value", ob.ser_id);
    }
    console.log("[UseForJS] dropdown objectives=", g_last_room_desc.grrObjectives.map(o => ({ id: o.ser_id, label: o.readable_name, doNotShow: o.obcDoNotShowExplanations })));





    $("#dialogUseFor .introExplanations").hide();
    $(".templateRadioExplanation").remove();

    $(".selectObiettivo").off('change').change(e => {

        //console.log('obiettivo changed');

        if ($(".selectObiettivo").val() == '') {
            $("#dialogUseFor .introExplanations").hide();
            $(".templateRadioExplanation").remove();
        }
        else {


            let objId = $(".selectObiettivo").val();
            let ob = g_last_room_desc.grrObjectives.filter(x => x.ser_id == objId)[0];
            console.log("[UseForJS] change objId=", objId, "label=", ob?.readable_name, "doNotShow=", ob?.obcDoNotShowExplanations, "customExps=", ob?.obcCustomExplanations?.length ?? null);
            //let obiettivoNonHaSpiegazioni = .obcDoNotShowExplanations;

            if (!useForNeedsExplanation(ob)) {
                $("#dialogUseFor .introExplanations").hide();
                $(".templateRadioExplanation").remove();
                console.log("[UseForJS] no explanations for objId=", objId);
            }
            else {

                let spiegazioniDaMostrare;
                const useForContext = ofc.ofcUseForExplanationsByObjective != null
                    ? ofc.ofcUseForExplanationsByObjective[objId]
                    : null;
                if (useForContext?.ufeExplanations != null) {
                    spiegazioniDaMostrare = useForContext.ufeExplanations;
                }
                else if (ob.obcCustomExplanations == null) {
                    spiegazioniDaMostrare = g_last_room_desc.grrExplanationsGlobal;
                }
                else {
                    spiegazioniDaMostrare = ob.obcCustomExplanations;
                }

                $(".templateRadioExplanation").remove();

                $("#dialogUseFor .introExplanations").show();

                if (useForContext?.ufeCustomExplanationIntro != null) {
                    $(".introExplanations").text(useForContext.ufeCustomExplanationIntro);
                }
                else if (ob.obcCustomExplanationIntro != null) {
                    $(".introExplanations").text(ob.obcCustomExplanationIntro);
                }
                else {
                    $(".introExplanations").text("inmodoche".tr());
                }


                let appendedCount = 0;
                for (let ex of spiegazioniDaMostrare) {
                    //debugger;
                    // se questa expl non è da nascondere con quell obietivo
                    if (typeof g_last_room_desc.grrExplanationsToExcludeOfObjective[ob.ser_id] == 'undefined'
                        || g_last_room_desc.grrExplanationsToExcludeOfObjective[ob.ser_id].every(x => x != ex.expId)) {




                        var newDiv = gTemplateRadioExplanation.clone();
                        newDiv.appendTo("#dialogUseFor .containerExplanations ");

                        if (ob.obcContainedSubject == null) {
                            newDiv.find('.testoRadioExplan').html(ex.exName.replace("{1}", ofcNameWithArticleMaybe));
                        }
                        else {
                            newDiv.find('.testoRadioExplan').html(ex.exName.replace("{1}", ob.obcContainedSubject));
                        }

                        newDiv.find('.radioExplan').val(ex.expId);

                        newDiv.find('.radioExplan').change(e => {
                            updateEnabledSubmitUseFor();
                        });
                        appendedCount++;
                    }
                }
                console.log("[UseForJS] visible explanations for", objId, spiegazioniDaMostrare.map(ex => ex.expId), "appended=", appendedCount, "containerChildren=", $("#dialogUseFor .containerExplanations").children().length);
            }
        }


        updateEnabledSubmitUseFor();
    });


    $("#submitUseFor").off('click').on('click', async e => {

        e.preventDefault();

        if ($("#submitUseFor").hasClass("disabled")) {
            return;
        }

        $("#submitUseFor").addClass('disabled');

        //$("#submitUseFor").addClass("disabled");
        //$("#dialogUseFor .modal-content").css("cursor", "wait");
        //$("span.testoRadioExplan").css("cursor", "wait");
        //$(".radio.templateRadioExplanation").css("cursor", "wait");
        //$("#submitUseFor").css('cursor', "wait");

        //await delay(14000);

        //debugger;
        let checked = $("#dialogUseFor .radioExplan:checked");

        let explId = checked.first().val();

        let objId = $(".selectObiettivo").val();
        console.log("[UseForJS] submit objId=", objId, "explId=", explId);

        await callUseFor(ofc.loId, objId, explId);
    });

    $("#dialogUseFor").modal("show");
    $("#obiettivoExplanationInner").scrollTop(0);

    updateEnabledSubmitUseFor();
}

function getCurRoom() {
    return g_last_room_desc.grrRooms[g_last_room_desc.grrCurRoomId];
}

var gDraggingElement = null;
var gDragOffsetX = null;
var gDragOffsetY = null;
var gICurTemplateInComposer = 0;
var gComposerInSayMode = null;
var gTemplatesToUse = [];
var gFillersToUse = [];

var gLoOfComposer = null;



async function showNewActionComposer(lo) {
    //console.log("USE ");

    gSelectedVerb = null;
    gSelectedObj = null;


    $("#sentenceComposerNew").modal('show'); // prima di rebuild, perche deve calcolare la larghezza dispo

    gLoOfComposer = lo;


    gICurTemplateInComposer = 0;
    rebuildComposer();



    let htFrase = 168;
    let htFrecciaSuGiu = 48;

    //$(".fraseConBuchi").css('height', htFrase);
    //$(".parteConFrasiIncomplete").scrollTop(htFrase * 0.5);
    $(".parteConFrasiIncomplete").css("height", htFrase);
    $(".ombraComposer.up").css('top', htFrecciaSuGiu);
    $(".ombraComposer.down").css('bottom', htFrecciaSuGiu);





    $(".btnComposerAvanti").off('click').click(async e => {

        if ($(e.currentTarget).hasClass('disabled')) {
            return;
        }

        if ($(".playIconImgDisabled").is(':visible')) {
            return;
        }
        //debugger;


        let pezzi = [];
        $(".pezzoComp").each((i, el0) => {
            let el = $(el0);
            if (el.hasClass("pezzoNonCliccab")) {
                pezzi.push({ cinCliccabile: false, cinText: el.text() });

            }
            else {
                pezzi.push({ cinCliccabile: true, cinFiId: el.find('.dropped').attr("filler_id") });
            }
        });


        let fiId1 = $(".fraseConBuchi").find(".pezzoCliccab").first().find(".templateObjectComposerNew ").attr("filler_id");

        let fiId2;
        if ($(".fraseConBuchi").find(".pezzoCliccab").eq(1) /* il secondo elemento*/.length > 0) {
            fiId2 = $(".fraseConBuchi").find(".pezzoCliccab").eq(1).find(".templateObjectComposerNew ").attr("filler_id")
        }
        else {
            fiId2 = null;
        }


        let teId = $(".fraseConBuchi").attr("template_id");
        let loId;



        loId = gLoOfComposer.loId;


        await callUseInComposer(loId, pezzi, teId, fiId1, fiId2);

    });

    $(".frecciaComposerNew.su").off('click').click(e => {
        gICurTemplateInComposer--;

        if (gICurTemplateInComposer < 0) {
            gICurTemplateInComposer = gTemplatesToUse.length - 1;
        }


        rebuildComposer();
    });

    $(".frecciaComposerNew.giu").off('click').click(e => {
        gICurTemplateInComposer++;
        if (gICurTemplateInComposer >= gTemplatesToUse.length) {
            gICurTemplateInComposer = 0;
        }

        rebuildComposer();
    });


    $("#sentenceComposerNew").off('mouseup').mouseup(function (e) {
        //console.log('mouseup', e);

        if (gDraggingElement == null) {
            return;
        }


        $(".pezzoCliccab.pezzoComp").each((i, el0) => {
            let el = $(el0);





            // ora mettici dentro quello
            let box1 = el0.getBoundingClientRect();
            let box2 = gDraggingElement[0].getBoundingClientRect();

            //console.log("box1", box1);
            //console.log("box2", box2);

            if (intersectRect(box1, box2)) {
                //console.log('intersect');


                // se non e' vuoto elimina prmia il contenuto
                let contenuto = el.find('.templateObjectComposerNew');
                contenuto.remove();



                gDraggingElement.removeClass("beingDragged").addClass("dropped");
                gDraggingElement.css('position', 'relative');
                gDraggingElement.css('margin', 'unset');
                //gDraggingElement.css('border-radius', '6px');
                gDraggingElement.css('left', '0px');
                gDraggingElement.css('top', '0px');
                //gDraggingElement.css('border', 'none');
                el.find(".questionCircle").remove();
                el.append(gDraggingElement);

                el.addClass("containsSomething");
                //el.css('border', 0);




                return false;
            }

        });

        $(".beingDragged").remove();
        gDraggingElement = null;


        updatePlayArrowComposer();

    });




    $(".bodyNewComposer").off('mousemove').mousemove(e => {
        if (gFrozenMouse) {
            e.preventDefault();
            e.stopPropagation();
            return;
        }


        onMouseMoveComposer(e);
    });

}



// Gestisce il click su un oggetto della stanza.
//
// lo:
//     Logic object selezionato nella stanza, normalmente un elemento di
//     g_last_room_desc.grrRooms[...].rfc_objects.
//
// mouseX, mouseY:
//     Coordinate client del mouse, cioè e.clientX/e.clientY. Sono usate per
//     posizionare il context menu e per le azioni che dipendono dal punto del
//     click; non sono coordinate relative al singolo <img>.
//
// fromHitTestOnly:
//     true se il click è arrivato attraverso un layer -ou. Va passato true
//     anche quando il pixel -ou è trasparente e l'oggetto risolto sotto è,
//     per esempio, Van Helsing: il context menu deve comunque stare sopra
//     l'hit-test e fermare la propagazione verso il container della stanza.
//     false per un click normale su un layer visibile.
async function onLoClickedRoom(lo, mouseX, mouseY, fromHitTestOnly = false, skipTutorialPrompt = false) {
    //console.log("onLoClickedRoom ", lo.loId);

    $(".contextMenu").toggleClass("fromHitTestOnly", fromHitTestOnly);


    if (gSelectedVerb == 'look') {
        console.error('non dovrebbe più succedere vriji43');
        if (lo.ofcIsLookableNow) {
            gSelectedVerb = null;
            updateToolbar();

            await callRemember(lo.loId, false, false, true);
        }
        else {
            // niente, ignora il click
        }
    }

    else if (gSelectedVerb == 'pickup') {

        if (lo.ofcIsPickableNow) {
            gSelectedVerb = null;
            updateToolbar();

            await callRemember(lo.loId, true, false, false);
        }
        else {
            // ignora click perché tanto vedi già accesso vietato
        }
    }
    else if (gSelectedVerb == 'use') {
        //debugger;
        if (lo.ofcContextMenuUseForOrHereOrDeduce == "useFor") {
            gSelectedVerb = null;
            updateToolbar();

            await showDialogUseFor(lo);
        }
        else if (lo.ofcContextMenuUseForOrHereOrDeduce == "useHere") {
            gSelectedVerb = null;
            updateToolbar();

            await callRemember(lo.loId, false, true, false);
        }
        else {
            // ignora click perché tanto vedi già accesso vietato
        }
    }
    else if (gSelectedVerb == 'remember') {
        console.error('non dovrebbe più succedere vdijfrj');
        //debugger;
        if (lo.ofc_can_be_remembered) {
            gSelectedVerb = null;
            updateToolbar();

            await callRemember(lo.loId, false, false, false);
        }
        else {
            // ignora click
        }
    }
    else if (gSelectedVerb == 'talk') {
        if (lo.ofcCanTalkToCharacterNow) {
            if (lo.ofc_is_character) {
                gSelectedVerb = null;
                updateToolbar();
                gComposerInSayMode = true;
                await showNewActionComposer(lo);
            }
            else {
                // ignora
            }
        }
        else {
            // ignora
        }
    }
    else if (gSelectedVerb == 'deduce') {
        //debugger;
        let loIsDeducable = lo.ofcContextMenuUseForOrHereOrDeduce == 'deduce';
        if (loIsDeducable) {
            gSelectedVerb = null;
            updateToolbar();

            await showDialogIsActually(lo);
        }
        else {
            // ignora click perché tanto vedi già accesso vietato
        }



    }
    else if (gSelectedVerb == null) { // cliccato un oggetto room quando non è selezionato un verbo
        if (gSelectedObj == null) { // e non è selezionato un altro oggetto

            if (lo.ofcIsExit) {
                // per l'uscita, esegui use-here. non mostrare il menu con solo use here
                gSelectedVerb = null;
                updateToolbar();

                await callRemember(lo.loId, false, true, false);
            }
            //if (lo.ofcVerbIdWhenInRoom == 'showMap')
            //{
            //        gSelectedVerb = null;
            //        updateToolbar();

            //        await mostraMappa();
            //}
            //else if (lo.ofcVerbIdWhenInRoom == 'showContextMenu')
            //{
            //        debugger;
            //        if ($(".contextMenu").is(':visible'))
            //        {
            //                $(".contextMenu").hide();
            //        }
            //        else if (lo.ofcIsExit)
            //        {
            //                // per l'uscita, esegui use-here. non mostrare il menu con solo use here
            //                gSelectedVerb = null;
            //                updateToolbar();

            //                await callRemember(lo.loId, false, true, false);
            //        }
            //        else
            //        {





            //        }
            //}


            ////else if (lo.ofcVerbIdWhenInRoom == 'useHere') {
            ////        gSelectedVerb = null;
            ////        updateToolbar();

            ////        await callRemember(lo.loId, false, true, false);
            ////}


            ////else if (lo.ofcVerbIdWhenInRoom == 'useFor') {
            ////        await showDialogUseFor(lo);
            ////}
            ////else if (lo.ofcVerbIdWhenInRoom == 'isActually') {
            ////        await showDialogIsActually(lo);
            ////}

            //else if (lo.ofcVerbIdWhenInRoom == null)

            {
                // mostro il context menu:  bm_context_menu

                if (lo.ofc_is_character) {


                    $(".contextMenuItem.ciTalkTo").show();

                    $(".contextMenuItem.ciDressUpAs").show();
                    $(".contextMenuItem.ciHideInside").hide();
                    $(".contextMenuItem.ciClimb").hide();

                    $(".contextMenuItem.ciTalkTo").off('mousedown').on('mousedown', async e => {

                        e.stopPropagation(); // altrimenti sente il clic sulla zona sotto e parte (ad esempio) entra nel castello, se clicco "parla con guardia"
                        //debugger;
                        $(".contextMenu").hide();


                        // qui ho fuso la vecchia icona talk. finché la vecchia icona era gialla, fai le veci di quella. altrimenti permetto di dire le cose specifiche.
                        if (g_last_room_desc.grrTalkNow) {
                            await callTalkHere();
                        }
                        else {
                            rebuildDialogChoicesMenu(lo);
                        }

                    });


                    $(".contextMenuItem.ciPickUp").hide();


                    $(".contextMenuItem.ciDressUpAs").off('mousedown').on('mousedown', async e => {
                        //debugger;
                        e.stopPropagation()
                        e.preventDefault()

                        $(".contextMenu").hide();
                        gSelectedVerb = null;
                        updateToolbar();

                        //debugger;
                        // faccio finta che questo cliccato sia il secondo oggetto e che il primo sia climb. cosi' fara' fallback al meccanismo che mostra la 
                        // dialog che chiede explanation
                        gSelectedObj = g_last_room_desc.grrInvObjects.filter(x => x.loId == g_last_room_desc.grrTravestitiLoId)[0]; // per questo e' necessario che sia nell'inv il verbo speciale

                        //debugger;
                        await onLoClickedRoom(lo, mouseX, mouseY);

                        //debugger;

                    });


                    // ora i custom verbs. ma non ci sono più

                    $(".contextMenuItem.custom").remove();

                    //for (let io of g_last_room_desc.grrInvObjects) {
                    //        if (io.ofcIsCustomVerbForRoomCharacters) {
                    //                let nuov = $("<div class='contextMenuItem custom'>").appendTo(".contextMenu");
                    //                nuov.text(io.ofcVerbWhenUseWithAsFirstObjectOnHoverNotSelected.firstLetterToUpper().replace("...", ""));
                    //                nuov.mousedown(async e => {
                    //                        e.stopPropagation(); // altrimenti un altro mousedown handler chiama cliccatonelvuoto e cancella gSelectedObj
                    //                        $(".contextMenu").hide();
                    //                        //debugger;
                    //                        // devo fare finta che sia stato selezionato prima questo logicobject e poi lo.
                    //                        gSelectedObj = io;
                    //                        await onLoClickedRoom(lo, mouseX, mouseY);
                    //                });
                    //        }
                    //}



                }
                else { // is an object

                    //debugger; // non lo devo aprire!

                    $(".contextMenuItem.ciTalkTo").hide();
                    $(".contextMenuItem.ciDressUpAs").hide();
                    $(".contextMenuItem.ciHideInside").show();
                    $(".contextMenuItem.ciClimb").show();


                    if (lo.ofcIsPickableNow) {
                        $(".contextMenuItem.ciPickUp").show();

                        $(".contextMenuItem.ciPickUp").off('mousedown').on('mousedown', async e => {
                            //debugger;
                            e.stopPropagation()
                            e.preventDefault()

                            $(".contextMenu").hide();
                            gSelectedVerb = null;
                            updateToolbar();

                            await callRemember(lo.loId, true, false, false);

                        });


                    }
                    else {
                        $(".contextMenuItem.ciPickUp").hide();
                    }

                    $(".contextMenuItem.ciClimb").off('mousedown').on('mousedown', async e => {
                        //debugger;
                        e.stopPropagation()
                        e.preventDefault()

                        $(".contextMenu").hide();
                        gSelectedVerb = null;
                        updateToolbar();

                        //debugger;
                        // faccio finta che questo cliccato sia il secondo oggetto e che il primo sia climb. cosi' fara' fallback al meccanismo che mostra la 
                        // dialog che chiede explanation
                        gSelectedObj = g_last_room_desc.grrInvObjects.filter(x => x.loId == g_last_room_desc.grrClimbLoId)[0]; // per questo e' necessario che sia nell'inv il verbo speciale

                        await onLoClickedRoom(lo, mouseX, mouseY);


                    });
                    $(".contextMenuItem.ciHideInside").off('mousedown').on('mousedown', async e => {
                        //debugger;
                        e.stopPropagation()
                        e.preventDefault()

                        $(".contextMenu").hide();
                        gSelectedVerb = null;
                        updateToolbar();

                        //debugger;
                        // faccio finta che questo cliccato sia il secondo oggetto e che il primo sia climb. cosi' fara' fallback al meccanismo che mostra la 
                        // dialog che chiede explanation
                        gSelectedObj = g_last_room_desc.grrInvObjects.filter(x => x.loId == g_last_room_desc.grrHideInsideLoId)[0]; // per questo e' necessario che sia nell'inv il verbo speciale

                        //debugger;
                        await onLoClickedRoom(lo, mouseX, mouseY);

                        //debugger;

                    });



                    // ora i custom verbs . non ci sono più
                    $(".contextMenuItem.custom").remove();

                    //for (let io of g_last_room_desc.grrInvObjects) {
                    //        if (io.ofcIsCustomVerbForRoomObjects) {
                    //                let nuov = $("<div class='contextMenuItem custom'>").appendTo(".contextMenu");
                    //                nuov.text(io.ofcVerbWhenUseWithAsFirstObjectOnHoverNotSelected.firstLetterToUpper().replace("...", ""));
                    //                nuov.mousedown(async e => {
                    //                        // devo fare finta che sia stato selezionato prima questo logicobject e poi lo.

                    //                        e.stopPropagation(); // altrimenti un altro mousedown handler chiama cliccatonelvuoto e cancella gSelectedObj
                    //                        $(".contextMenu").hide();
                    //                        gSelectedObj = io;
                    //                        await onLoClickedRoom(lo, mouseX, mouseY);
                    //                });
                    //        }
                    //}



                }

                if (lo.ofcContextMenuUseForOrHereOrDeduce == "deduce") {
                    $(".contextMenuItem.ciDeduce").show();
                    $(".contextMenuItem.ciUseFor").hide();
                    $(".contextMenuItem.ciUseHere").hide();

                    $(".contextMenuItem.ciDeduce").off('mousedown').on('mousedown', async () => {
                        $(".contextMenu").hide();
                        await showDialogIsActually(lo);
                    });
                }
                else if (lo.ofcContextMenuUseForOrHereOrDeduce == "useHere") {
                    $(".contextMenuItem.ciDeduce").hide();
                    $(".contextMenuItem.ciUseFor").hide();
                    $(".contextMenuItem.ciUseHere").show();

                    $(".contextMenuItem.ciUseHere").off('mousedown').on('mousedown', async () => {
                        gSelectedVerb = null;
                        updateToolbar();
                        $(".contextMenu").hide();

                        let fullText = lo.ofcCustomSentenceUseHere;

                        if (fullText) {
                            if (isImgMode()) {
                                let dat = calcolaScalEtc();
                                let newEl = posizionaDidascaliaOggettoMouse(
                                    dat,
                                    lo,
                                    gLastPositionSentenceX,
                                    gLastPositionSentenceY,
                                    true,
                                    fullText);

                                if (newEl) {
                                    newEl.addClass("highlighted");
                                }
                            }

                            if (noImgMode()) {
                                $(".textModeFraseComposta")
                                    .html(fullText.fondiParole().firstLetterToUpper())
                                    .removeClass("fallisce");
                            }

                            gFrozenMouse = true;
                            disabilitaTuttoTemporaneamenteMentreVediFrase();

                            try {
                                await delay(calcolaTempoFrase(fullText));
                                await callRemember(lo.loId, false, true, false);
                            }
                            finally {
                                $(".btnOggettoInRoom").remove();
                                $(".textModeFraseComposta").html('&nbsp;');
                                riabilitaTuttoTemporaneamenteDisabilitatoFrase();
                                gFrozenMouse = false;
                            }
                        }
                        else {
                            await callRemember(lo.loId, false, true, false);
                        }
                    });
                }
                else if (lo.ofcContextMenuUseForOrHereOrDeduce == "useFor") {
                    $(".contextMenuItem.ciDeduce").hide();
                    $(".contextMenuItem.ciUseFor").show();
                    $(".contextMenuItem.ciUseHere").hide();

                    $(".contextMenuItem.ciUseFor").off('mousedown').on('mousedown', async () => {
                        console.log('cliccato use for');
                        $(".contextMenu").hide();
                        await showDialogUseFor(lo);
                    });
                }
                else if (lo.ofcContextMenuUseForOrHereOrDeduce == "nothing") // si usa per la statua, per evitare che tu possa fare usa-statua-per
                {
                    // si arriva qui per il paint bucket
                    //debugger; // qui adesso non vedrei niente, quindi devo mostrare l'accesso vietato e non aprire il menu
                    $(".contextMenuItem.ciDeduce").hide();
                    $(".contextMenuItem.ciUseFor").hide();
                    $(".contextMenuItem.ciUseHere").hide();


                }
                else {
                    console.error("non gestito fdjkjfdk");
                }

                // metti il menu alla stessa posizione del clic del mouse

                //if (!lo.ofcIsPickableNow && lo.ofcContextMenuUseForOrHereOrDeduce == "nothing") { // TODO aggiungi condizione "e non si puo' ricordare"

                //    // mostra accesso vietato

                //    $(".divLayersContainer").css("cursor", "not-allowed");


                //    // ora cambia il colore della frase sul mouse: rossa o gialla
                //    //let fullText = "Niente";


                //    //var dat = calcolaScalEtc();

                //    //let newEl = posizionaDidascaliaOggettoMouse(dat, lo, gLastPositionSentenceX, gLastPositionSentenceY, true /* is in room*/, fullText); // questo e' un layer della room



                //    //newEl.addClass("unhandled");






                //    gFrozenMouse = true;


                //    disabilitaTuttoTemporaneamenteMentreVediFrase();


                //    await delay(600); // 700 con il server lento è sufficiente







                //    riabilitaTuttoTemporaneamenteDisabilitatoFrase();

                //    gFrozenMouse = false;



                //    //debugger;
                //    cliccatoNelVuotoDeselezionaTutto();

                //    //$(".divLayersContainer").css("cursor", "");
                //}
                //else

                {

                    posizionaContextMenuMouse(mouseX, mouseY);



                    $(".contextMenu").show();

                    // nascondo la frase che segue il mouse
                    $(".btnOggettoInRoom ").remove();


                }


            } // mostro il context menu





            //else // se è  un oggetto normale, non un'uscita
            //{

            //    // succede se clicco su un oggetto room che non ha né deduce, ne' usefor ne' usehere. ad esempio la legna o il camino

            //    //console.log("ho cliccato oggetto room che non è un'uscita");

            //    //mostra accesso vietato


            //    $(".divLayersContainer").css("cursor", "not-allowed");


            //    // ora cambia il colore della frase sul mouse: rossa o gialla
            //    //let fullText = "Niente";


            //    //var dat = calcolaScalEtc();

            //    //let newEl = posizionaDidascaliaOggettoMouse(dat, lo, gLastPositionSentenceX, gLastPositionSentenceY, true /* is in room*/, fullText); // questo e' un layer della room



            //    //newEl.addClass("unhandled");






            //    gFrozenMouse = true;


            //    disabilitaTuttoTemporaneamenteMentreVediFrase();


            //    await delay(600); // 700 con il server lento è sufficiente







            //    riabilitaTuttoTemporaneamenteDisabilitatoFrase();

            //    gFrozenMouse = false;



            //    //debugger;
            //    cliccatoNelVuotoDeselezionaTutto();

            //    //$(".divLayersContainer").css("cursor", "");
            //}
            ////else
            ////{
            ////        console.error("ofcVerbIdWhenInRoom not handled: " + lo.ofcVerbIdWhenInRoom);
            ////}
        }
        else   // era il secondo oggetto cliccato. 
        {
            function updateEnabledSubmitChooseExplan() {

                //debugger;

                let checked = $("#dialogChooseExplanation .radioExplan:checked");
                if (checked.length == 0) {
                    $("#submitExplanation").addClass('disabled');
                    $("#submitExplanation").removeClass('blink_me_2');
                }
                else {
                    let mustFlash = $("#submitExplanation").hasClass('disabled');
                    $("#submitExplanation").removeClass('disabled');

                    if (mustFlash) {
                        $("#submitExplanation").addClass('blink_me_2');
                    }
                }

            }



            let sel_ofcNameWithArticleMaybe;
            if (gSelectedObj.ofcNameWithArticle != null && gSelectedObj.ofcNameWithArticle != "") {
                sel_ofcNameWithArticleMaybe = gSelectedObj.ofcNameWithArticle;
            }
            else {
                sel_ofcNameWithArticleMaybe = gSelectedObj.ofc_name;
            }


            let lo_ofcNameWithArticleMaybe;
            if (lo.ofcNameWithArticle != null && lo.ofcNameWithArticle != "") {
                lo_ofcNameWithArticleMaybe = lo.ofcNameWithArticle;
            }
            else {
                lo_ofcNameWithArticleMaybe = lo.ofc_name;
            }




            // La coppia è orientata. Prima prevale l'handler esatto O1 -> O2;
            // in sua assenza, vale la regola generale di O1: basta un handler
            // con explanation. Se O1 non ha handler, si usa il default.
            let pairExplanationData = gSelectedObj.ofcCombineExplanationsByTarget != null
                ? gSelectedObj.ofcCombineExplanationsByTarget[lo.loId]
                : null;
            let hasExactPairHandler = pairExplanationData?.cedIsExactHandler === true;
            const isCasualSpecialVerb = gSelectedObj.loId === g_last_room_desc.grrHideInsideLoId
                || gSelectedObj.loId === g_last_room_desc.grrTravestitiLoId
                || gSelectedObj.ofcKeepExplanationInCasual === true;
            let selectedPairRequiresExplanation = hasExactPairHandler
                ? pairExplanationData.cedRequiresExplanation
                : gSelectedObj.ofcDefaultCombineRequiresExplanation;
            if (g_last_room_desc.grrCasualMode) {
                selectedPairRequiresExplanation = hasExactPairHandler
                    ? pairExplanationData.cedRequiresExplanation === true
                        && pairExplanationData.cedKeepExplanationInCasual === true
                    : isCasualSpecialVerb;
            }

            console.log("[UseWithExplanation]", {
                first: gSelectedObj.loId,
                target: lo.loId,
                exactPairHandler: hasExactPairHandler,
                requires: selectedPairRequiresExplanation
            });

            if (selectedPairRequiresExplanation && g_tutorialMode && !skipTutorialPrompt) {
                let promptKind = "UseWith";
                if (gSelectedObj.loId === g_last_room_desc.grrHideInsideLoId) {
                    promptKind = "HideInside";
                }
                else if (gSelectedObj.loId === g_last_room_desc.grrTravestitiLoId) {
                    promptKind = "DisguiseAs";
                }

                await runTutorialPrompt(promptKind, gSelectedObj.loId, lo.loId,
                    () => onLoClickedRoom(lo, mouseX, mouseY, fromHitTestOnly, true));
                return;
            }

            if (selectedPairRequiresExplanation) {
                // devo chiedere la spiegazione







                // costruisci la frase PER fare X, usa Y
                let istanziato;
                let templatePer = gSelectedObj.ofcVerbWhenUseWithAsFirstObjectSelectedWithPlaceHolderOnHoverSecond;
                if (templatePer == null) {
                    istanziato = 'usewith12'.tr().replace('{1}', sel_ofcNameWithArticleMaybe).replace('{2}', lo_ofcNameWithArticleMaybe).firstLetterToUpper();
                }
                else {
                    istanziato = replaceTargetPossessive(templatePer, lo, gSelectedObj).replace('{1}', lo_ofcNameWithArticleMaybe).firstLetterToUpper();
                }

                $(".fraseCheHaiScelto").text(istanziato.fondiParole());






                $(".templateRadioExplanation").remove();



                if (pairExplanationData != null && pairExplanationData.cedCustomExplanationIntro != null) {
                    $(".introExplanations").html(pairExplanationData.cedCustomExplanationIntro.replace("{1}", lo_ofcNameWithArticleMaybe));
                }
                else if (gSelectedObj.ofcCustomExplanationsIntro != null) {
                    $(".introExplanations").html(gSelectedObj.ofcCustomExplanationsIntro.replace("{1}", lo_ofcNameWithArticleMaybe));
                }
                else {
                    $(".introExplanations").html("cosasucced".tr());
                }


                let spiegazioniDaMostrare;
                if (pairExplanationData != null && pairExplanationData.cedExplanations != null) {
                    spiegazioniDaMostrare = pairExplanationData.cedExplanations;
                }
                else if (gSelectedObj.ofcDefaultCombineExplanations != null) {
                    spiegazioniDaMostrare = gSelectedObj.ofcDefaultCombineExplanations;
                }
                else if (gSelectedObj.ofcCustomExplanations != null) {
                    spiegazioniDaMostrare = gSelectedObj.ofcCustomExplanations;


                }
                else {
                    spiegazioniDaMostrare = g_last_room_desc.grrExplanationsGlobal;


                }


                //debugger;
                // sottraggo quelle da escludere
                if (typeof g_last_room_desc.grrExplanationsToExcludeOfLo[gSelectedObj.loId] != 'undefined') {
                    let daNascondere = g_last_room_desc.grrExplanationsToExcludeOfLo[gSelectedObj.loId];
                    spiegazioniDaMostrare = spiegazioniDaMostrare.filter(ex => !daNascondere.any(ex2Id => ex2Id == ex.expId));
                }

                for (let ex of spiegazioniDaMostrare) {
                    var newDiv = gTemplateRadioExplanation.clone();
                    newDiv.appendTo("#dialogChooseExplanation .containerExplanations ");
                    //debugger;
                    newDiv.find('.testoRadioExplan').html(ex.exName.replace("{1}", lo_ofcNameWithArticleMaybe));


                    newDiv.find('.radioExplan').val(ex.expId);

                    newDiv.find('.radioExplan').change(e => {
                        updateEnabledSubmitChooseExplan();
                    });
                }


                $("#submitExplanation").off('click').on('click', async e => {

                    e.preventDefault();

                    if ($("#submitExplanation").hasClass("disabled")) {
                        return;
                    }
                    //debugger;
                    let checked = $("#dialogChooseExplanation .radioExplan:checked");

                    let explId = checked.first().val();

                    await callUseWith(gSelectedObj.loId, lo.loId, explId, false);
                });

                $(".introExplanations").show();
                $("#dialogChooseExplanation").modal("show");
                $("#chooseExplaInner").scrollTop(0);


                updateEnabledSubmitChooseExplan();
            }
            else {
                    // Casual: non eseguire mai la preview locale dell'azione.
                    // In particolare non leggere ofcObjectsYouCanUseWithIt e
                    // non calcolare "already know it fails": il server deve
                    // essere l'unico a decidere successo o fallimento.
                    if (g_last_room_desc.grrCasualMode === true) {
                        const casualFirstLoId = gSelectedObj.loId;
                        await callUseWith(casualFirstLoId, lo.loId, null, false);
                        gSelectedObj = null;
                        gSelectedVerb = null;
                        updateToolbar();
                        return;
                    }

                    // non devo chiedere spiegazione, devo solo inviare azione

                    // se sta fallendo, adesso mostra il cursore wrong
                    //debugger;

                    const isCasualMode = g_last_room_desc.grrCasualMode === true;
                    // In Casual il client non deve conoscere né calcolare in
                    // anticipo se l'azione fallirà. La validazione completa,
                    // inclusa la visibilità narrativa dell'explanation, è
                    // esclusivamente responsabilità del server.
                    let soGiaChefallisce = false;
                    if (!isCasualMode) {
                        soGiaChefallisce = !lo.ofcObjectsYouCanUseWithIt.any(x => x.ocsLoId == gSelectedObj.loId);
                    }
                    // In Casual non mostrare mai il feedback transitorio:
                    // il server restituirà direttamente il successo oppure
                    // il ciclo di errore esplicito.
                    const suppressTransientFeedback = isCasualMode;

                    if (isCasualMode) {
                        // In Casual non lasciare a schermo eventuali residui
                        // del compositore: nessun feedback transitorio viene
                        // mostrato prima della risposta del server.
                        $(".textModeFraseComposta").html("&nbsp;").removeClass("fallisce");
                    }

                    if (suppressTransientFeedback) {
                        // Il server produrrà direttamente il messaggio generico.
                    }
                    else if (soGiaChefallisce) {
                        $(".divLayersContainer").css("cursor", "not-allowed");
                    }
                    else {




                        //console.log(`mousepos x  = ${e.pageX}, ${e.pageY}`);
                        //console.log(`mousepos `, e);




                        $(".divLayersContainer").css("cursor", "");
                    }


                    if (!suppressTransientFeedback) {
                    // ora cambia il colore della frase sul mouse: rossa o gialla
                    let fullText = null;
                    if (soGiaChefallisce) {

                        fullText = calcolaTextPerFraseMouse(lo, true, null);
                        //fullText = $(".btnOggettoInRoom").text(); //resta uguale
                    }
                    else {
                        fullText = lo.ofcObjectsYouCanUseWithIt.filter(x => x.ocsLoId == gSelectedObj.loId)[0].ocsCompleteSentence;
                    }

                    if (isImgMode()) {
                        var dat = calcolaScalEtc();

                        let newEl = posizionaDidascaliaOggettoMouse(dat, lo, gLastPositionSentenceX, gLastPositionSentenceY, true /* is in room*/, fullText); // questo e' un layer della room


                        if (newEl) {
                            if (soGiaChefallisce) {
                                newEl.addClass("unhandled");
                            }
                            else {
                                newEl.addClass("highlighted");
                            }
                        }
                    }

                    // devo fare use with


                    if (noImgMode()) {
                        //debugger;
                        $(".textModeFraseComposta").html(fullText.fondiParole().firstLetterToUpper());

                        if (soGiaChefallisce) {
                            $(".textModeFraseComposta").addClass("fallisce");
                        }
                        else {
                            $(".textModeFraseComposta").removeClass("fallisce");
                        }
                    }

                    gFrozenMouse = true;


                    disabilitaTuttoTemporaneamenteMentreVediFrase();

                    if (soGiaChefallisce) {
                        await delay(700); // 700 con il server lento è sufficiente
                    }
                    else {


                        let tempo = calcolaTempoFrase(fullText);
                        //console.log('aspetto per', tempo);
                        await delay(tempo); // 1000 a volte è poco per frasi lunghe
                    }
                    }




                    if (gSelectedObj == null) {
                        debugger;
                    }
                    // Per una explanation nascosta il server deve comunque
                    // costruire il fallimento generico, quindi non usare il flag
                    // "già so che fallisce" che sopprime il dialogo server-side.
                    await callUseWith(gSelectedObj.loId, lo.loId, null,
                        isCasualMode ? false : soGiaChefallisce);

                    riabilitaTuttoTemporaneamenteDisabilitatoFrase();

                    //if (!soGiaChefallisce)
                    //{
                    //        debugger;
                    //}
                    gFrozenMouse = false;



                    if (soGiaChefallisce) {
                        cliccatoNelVuotoDeselezionaTutto();
                    }
                    else {
                        //debugger;
                        gSelectedObj = null;
                        gSelectedVerb = null;
                    }


                }


            }
        }
    }

/**
 * Controlla se il pixel cliccato è trasparente
 * @param {jQuery} imageElement - Elemento immagine jQuery
 * @param {Event} event - Evento mouse (con offsetX/Y)
 * @returns {boolean} True se il pixel è trasparente
 */
function isTransparentPixel(imageElement, x, y) {
    const img = imageElement[0];
    const canvas = document.createElement('canvas');
    const ctx = canvas.getContext('2d');

    // Ottiene le dimensioni dell'immagine
    const width = imageElement.width();
    const height = imageElement.height();

    // Gestione caricamento immagine
    if (!img.complete || width === 0 || height === 0) {
        console.warn('Immagine non completamente caricata');
        return true;
    }

    // Configura canvas
    canvas.width = width;
    canvas.height = height;
    ctx.drawImage(img, 0, 0, width, height);

    // Controlla il pixel
    const pixelData = ctx.getImageData(x,y, 1, 1).data;
    return pixelData[3] === 0; // Alpha = 0 → trasparente
}

function maybeDisableCursorForLookWhenMouseOverRoomObj(lo) {
    // il cursore potrebbe doversi trasformare in accesso vietato
    if (!lo.ofcIsLookableNow && gSelectedVerb == 'look') {
        $(".divLayersContainer").css("cursor", "not-allowed");
    }
    else if (gSelectedVerb == 'pickup' && !lo.ofcIsPickableNow) {
        $(".divLayersContainer").css("cursor", "not-allowed");
    }

    else if (gSelectedVerb == 'deduce' && lo.ofcContextMenuUseForOrHereOrDeduce != 'deduce') {
        $(".divLayersContainer").css("cursor", "not-allowed");
    }
    else if (gSelectedVerb == 'use' && lo.ofcContextMenuUseForOrHereOrDeduce != 'useFor' && lo.ofcContextMenuUseForOrHereOrDeduce != 'useHere') {
        $(".divLayersContainer").css("cursor", "not-allowed");
    }
    else if (gSelectedVerb == 'talk' && !lo.ofcCanTalkToCharacterNow) {
        $(".divLayersContainer").css("cursor", "not-allowed");
    }
    else if (!lo.ofc_is_character && gSelectedVerb == 'talk') {
        $(".divLayersContainer").css("cursor", "not-allowed");
    }

    else if (gSelectedObj != null && !lo.ofcObjectsYouCanUseWithIt.any(x => x.ocsLoId == gSelectedObj.loId)) {
        // adesso prima del click non dico se funzionera
        $(".divLayersContainer").css("cursor", "default");
        //$(".divLayersContainer").css("cursor", "not-allowed");
    }
    else {
        $(".divLayersContainer").css("cursor", "default");
    }
}

let g_graphicsCacheVersion = 0;
const narRoomCaptionFadeMs = 150;

let g_handleArGeneration = 0;
// Dopo una cutscene la stanza va riletta dal server: anche con lo stesso room
// ID possono essere cambiati o rimossi dei layer.
let g_roomNeedsServerRefresh = false;

function waitForBrowserLayout() {
    return new Promise(resolve => {
        requestAnimationFrame(() => requestAnimationFrame(resolve));
    });
}

function waitForRoomImages(selector) {
    const images = Array.from(document.querySelectorAll(selector));
    return Promise.all(images.map(image => {
        if (image.complete) return Promise.resolve();
        return new Promise(resolve => {
            image.addEventListener("load", resolve, { once: true });
            image.addEventListener("error", resolve, { once: true });
        });
    }));
}

function roomLayoutRect(selector) {
    const element = document.querySelector(selector);
    if (!element) return null;
    const rect = element.getBoundingClientRect();
    return {
        x: Math.round(rect.x * 100) / 100,
        y: Math.round(rect.y * 100) / 100,
        width: Math.round(rect.width * 100) / 100,
        height: Math.round(rect.height * 100) / 100
    };
}

function logRoomLayout(stage, extra = {}) {
    const image = document.querySelector(".imgGrafica");
    console.log(`[Segusum][room-layout] ${stage}`, JSON.stringify({
        time: new Date().toISOString(),
        roomData: typeof g_last_room_desc === "undefined" || g_last_room_desc === null
            ? null
            : (g_last_room_desc.grrCurRoomId ?? null),
        window: {
            innerWidth: window.innerWidth,
            innerHeight: window.innerHeight,
            clientWidth: document.documentElement.clientWidth,
            clientHeight: document.documentElement.clientHeight,
            visualWidth: window.visualViewport?.width ?? null,
            visualHeight: window.visualViewport?.height ?? null,
            devicePixelRatio: window.devicePixelRatio
        },
        image: image ? {
            naturalWidth: image.naturalWidth,
            naturalHeight: image.naturalHeight,
            complete: image.complete,
            src: image.currentSrc || image.src,
            rect: roomLayoutRect(".imgGrafica")
        } : null,
        rects: {
            roomOuter: roomLayoutRect("#roomOuter"),
            graphics: roomLayoutRect(".graphicsAndSidebar"),
            layers: roomLayoutRect(".divLayersContainer"),
            roomAndInv: roomLayoutRect(".roomAndInv"),
            inventory: roomLayoutRect(".invBar")
        },
        ...extra
    }));
}

function cacheBustGraphicsUrl(url) {
    if (!url) {
        return url;
    }

    const separator = url.includes("?") ? "&" : "?";
    return `${url}${separator}v=${g_graphicsCacheVersion}`;
}

// Il cambio stanza e il resize del browser possono richiedere entrambi un
// rebuild. Serializzandoli evitiamo che una seconda chiamata misuri l'immagine
// mentre la prima l'ha temporaneamente nascosta durante il caricamento.
let g_rebuildGraphicsQueue = Promise.resolve();

function rebuildGraphics(reloadAssets = true) {
    const currentRebuild = g_rebuildGraphicsQueue.then(() => rebuildGraphicsCore(reloadAssets));
    g_rebuildGraphicsQueue = currentRebuild.catch(error => {
        console.error("[Segusum] rebuildGraphics", error);
    });
    return currentRebuild;
}

async function rebuildGraphicsCore(reloadAssets) {
    console.log("rebuildgraph");
    logRoomLayout(`graphics-start reload=${reloadAssets}`);

    if (typeof g_last_room_desc === "undefined" || g_last_room_desc == null) {
        return; //ho appena caricato i lsito
    }
    if (!localStorage[credentialsId]) {
        return; // stai facendo login
    }

    // Solo l'ingresso in una nuova stanza deve ricaricare gli asset. Un resize
    // deve ricalcolare il layout senza nascondere/ricaricare il PNG.
    if (reloadAssets) {
        g_graphicsCacheVersion = Date.now();
    }

    // tolgo i layer della grafica se non si vedono mentre carico l'img grande!
    if (reloadAssets) {
        $("#imgContainerNarBig .imlayer").remove(); // anche quelli del nar img!
    }
    $(".divLayersContainer .imlayer").remove();

    if (noImgMode()) {
        if (!g_last_room_desc) {
            debugger;
        }
        //g_last_room_desc.roomImg = "img/";
    }

    if (isImgMode()) {

        if (!g_last_room_desc.roomImg) {
            throw new Error("dfdk");
        }
        if (reloadAssets) {
            await changeImgSrcAndWait(
                $(".imgGrafica"),
                cacheBustGraphicsUrl(prefissoWebApi + "/" + g_last_room_desc.roomImg));

            // Dopo il cambio dell'immagine e dell'inventario il browser deve
            // completare almeno un ciclo di layout prima di restituire width/height.
            await waitForBrowserLayout();
        }
        else {
            $(".imgGrafica").show();
        }

        console.log("[Segusum] room render", {
            roomImg: g_last_room_desc.roomImg,
            roomId: g_last_room_desc.grrCurRoomId,
            layers: g_last_room_desc.grrLayersOfCurRoom?.length ?? 0,
            layerUrls: (g_last_room_desc.grrLayersOfCurRoom ?? []).slice(0, 5).map(x => x.lfc_imgPath)
        });
        logRoomLayout("after-image-load");

        //await sleepAsync(3000); // temp simula img grande

        $(".divLayersContainer .imlayer").remove();

        $(".divLayersContainer .textModeRoomObj").remove();



        // trovo larghezza NATURALE di sfondo e di viewport
        //$("#roomOuter").show();
        let imgGrafica = $(".imgGrafica");
        let graphicsAndSidebar  = $(".graphicsAndSidebar");
        let imgElement = imgGrafica[0];
        let imgWt = imgElement?.naturalWidth || imgGrafica.width();
        if (imgWt == 0 && noImgMode() != true) {
            console.error("ejkkjgfgf"); // probabilmente è nascosto
            return;
        }
        let imgHt = imgElement?.naturalHeight || imgGrafica.height();
        if (imgHt == 0 && noImgMode() != true) {
            console.error("gg5g5"); // probabilmente è nascosto
            return;
        }


        let imgRatio = imgHt / imgWt;
        //debugger;

        //$(".roomAndInv").css("height", ""); // sblocca l'altezza

        //// BEGIN settta la larghezza o altezza iniziale dello sfondo, in modo da vedersi tutto nello schermo
        //let viewportwt = $("#grafica").width();
        //let viewportht = $("#grafica").height();



        // ora devo settare altezza e larghezza dell'immagine



        //                if (!gIsNarrowScreen)
        {
            // logica normale

            //trova lo spazio massimo disponibile per la grafica, x e y

            let viewportwt = $(window).width()

                //- $(".roomAndInv").outerWidth(true)   // ora non c'e' piu la sidebar

                ;

            let htBarraInv = $(".invBar").height();
            
            let viewportht = window.innerHeight - htBarraInv; // non usare jquery perché non sottrae la toolbar di chrome

            logRoomLayout("before-size", {
                naturalImage: { width: imgWt, height: imgHt },
                imageRatio: imgRatio,
                inventoryHeight: htBarraInv,
                calculatedViewport: { width: viewportwt, height: viewportht }
            });



            // prima di tutto, la grandezza del testo deve essere proporzionale alla altezza della finestra. così se ingrandisci si ingrandisce il testo, e quindi non puoi ingrandire più di tanto,
            // quindi non vedrai mai i pixel troppo grandi

            //$(".invObjectTesto").css("font-size", `${viewportht / 30}px`);

            //console.log("view port wt = ", viewportwt);
            //console.log("view port ht = ", viewportht);
            let viewpRatio = viewportht / viewportwt;

            logRoomLayout("before-size-branch", {
                naturalImage: { width: imgWt, height: imgHt },
                imageRatio: imgRatio,
                inventoryHeight: htBarraInv,
                calculatedViewport: { width: viewportwt, height: viewportht },
                viewportRatio: viewpRatio,
                branch: viewpRatio > imgRatio ? "width" : "height"
            });



            $(".divLayersContainer").width("");

            
            // quindi non vedrai mai i pixel troppo grandi. il modo più semplice di farlo è renderla proporzionale alla altezza dell'inventario.

            
            if (viewpRatio > imgRatio) {
                console.log("caso 1");
                
                imgGrafica.height(""); //imgGrafica.width(viewportwt);

                let wtGrafica = window.innerWidth; // 1.65
                let htGrafica = wtGrafica * imgRatio;
                graphicsAndSidebar.height(htGrafica);
                imgGrafica.width(wtGrafica); // l'altezza della grafica deve essere dipendente da quella dei caratteri. così se ingrandisci si ingrandisce il testo, e quindi non puoi ingrandire più di tanto,

                $(".divLayersContainer").width(wtGrafica);
                $(".divLayersContainer").height(htGrafica);
                //$(".divLayersContainer").css("flex-basis", wtGrafica);

            }
            else {
                let altezzaGrafica = window.innerHeight - htBarraInv; // 1.65
                console.log("caso 2");
                imgGrafica.width(""); //imgGrafica.width(viewportwt);

                
                imgGrafica.height(altezzaGrafica); // l'altezza della grafica deve essere dipendente da quella dei caratteri. così se ingrandisci si ingrandisce il testo, e quindi non puoi ingrandire più di tanto,
                
                $(".divLayersContainer").height(altezzaGrafica);
                $(".divLayersContainer").css("flex-basis", altezzaGrafica);
                graphicsAndSidebar.height(altezzaGrafica);

 
                
            }
            //{
            //        //debugger;
            //        //altezza maggiore di larghezza

            //        //la larg immagine deve eguagliare quella del viewport
            //        // vwt = mul * imwt
            //        let mul = viewportwt / imgWt;



            //        imgGrafica.width(""); //imgGrafica.width(viewportwt);

            //        let altezzaGrafica = htBarraInv ;
            //        imgGrafica.height(altezzaGrafica);

            //        //// fisso l'altezza della grafica pari allo spazio libero al netto dell'inventario. la larghezza andrà bene e non sfonderà la largheza dispo? devo capirlo

            //        //let larghezzaCheRisulterebbe = viewportht / imgRatio;

            //        //if (larghezzaCheRisulterebbe <= viewportwt)
            //        //{


            //        //}
            //        //else
            //        //{
            //        //        // non posso settare l'altezza per riempire tutto, perché la larghezza sarebbe maggiore della finestra. quindi setto la larghezza
            //        //        imgGrafica.width(viewportwt);
            //        //        imgGrafica.height(""); // libera, si adatterà di conseguenza
            //        //        //debugger;
            //        //}

            //        $(".divLayersContainer").width(viewportwt);
            //        $(".divLayersContainer").height("");
            //        //debugger;


            //        //console.log("path1");
            //        //if ($("#imgGrafica").width() !== viewportwt) {
            //        //    throw "errore";
            //        //}

            //}
            //else
            //{
            //        //let mul = viewportht / imgHt;
            //        //imgGrafica.height(viewportht);
            //        //imgGrafica.width("");
            //        //$(".divLayersContainer").height(viewportht);
            //        //$(".divLayersContainer").width("");


            //}


            //alla fine, la barra di destra setta la sua altezza inmodo da coincidere con l'altezza finale dell'img di sfondo.
            let finalHtImage = $(".imgGrafica").height();
            $(".roomAndInv").css("height", finalHtImage);

            $("#grafica").height(""); // libera. serve nell altra modalita
            //debugger;


            $(".roomAndInv").hide();
            let finalWtImage = $(".imgGrafica").width();

            let wtInvBar;
            //let minWtInv = 940;
            //if (finalWtImage > minWtInv)
            {
                wtInvBar = finalWtImage;
            }
            //else {
            //    wtInvBar = minWtInv;
            //}
            $(".invBar").width(wtInvBar);

            logRoomLayout("after-size", {
                setImage: {
                    width: imgGrafica.width(),
                    height: imgGrafica.height()
                },
                setGraphics: {
                    width: graphicsAndSidebar.width(),
                    height: graphicsAndSidebar.height()
                }
            });

            //$(".invBar").css("flex-basis", finalWtImage);

            




        }

        // END










        // scalo e posiziono i layer, considerando di quanto ho scalato lo sfondo.







    }


    let lCurRoom = g_last_room_desc.grrRooms[g_last_room_desc.grrCurRoomId];
    let ofcInCurRoom2 = g_last_room_desc.grrRooms[g_last_room_desc.grrCurRoomId].rfc_objects;


    if (noImgMode() === false) {


        let dat = calcolaScalEtc();

        for (let la of g_last_room_desc.grrLayersOfCurRoom.values()) {
            //let la = g_last_room_desc.grrLayersOfCurRoom[ila];

            if (typeof la.lfc_imgPath === 'undefined') {
                debugger;
            }
            if (la.lfc_imgPath.endsWith("bg.jpg") || la.lfc_imgPath.endsWith("bg-res1900.jpg") || la.lfc_imgPath.endsWith("bg.png")) {
                let niente;
            }
            else {







                let newImg;
                if (noImgMode() == false) {
                    newImg = $("<img class='imlayer'>").appendTo(".divLayersContainer")
                        .attr('src', cacheBustGraphicsUrl(la.lfc_imgPath))
                        .attr("lo_id", la.lfc_loId);
                }

                if (la.lfcIsOutline) {
                    newImg
                        .addClass("hitTestOnly")
                        .css("opacity", 0); // così sente il mouseover ma non si vede
                }

                newImg
                    .attr("lfc_zIndex", la.lfc_zIndex ?? 0)
                    .css("z-index", la.lfc_zIndex ?? 0);

                if (la.lfc_isHires) {
                    newImg.css("image-rendering", "auto");
                }

                let losMatching = ofcInCurRoom2.filter(ofc => ofc.loId === la.lfc_loId);

                let lo;
                if (losMatching.length == 0) {
                    lo = null; // un layer che nonha oggetto
                }
                else {
                    lo = losMatching[0];
                }


                if (lo == null) {
                    newImg.addClass("noCollisions");
                }

                if (lo != null && noImgMode() == true) {
                    newImg.text(lo.ofc_name);
                }

                if (lo !== null && lo.ofcMustBeShownInTextRoomRecap && !la.lfcIsOutline) {

                    function handlerIn(e) // mouse entra o si muove su layer  - mousemove su layer
                    {

                        //if (gFrozenMouse)
                        //{
                        //        //console.log('mousemoveon layer - frozen mouse');
                        //        e.preventDefault();
                        //        e.stopPropagation();
                        //        return;
                        //}

                        //console.log("mouse entered or moved", lo);
                        gLoHover = lo;
                        gDatHover = dat;
                        gLayerHover = la;
                        gMouseX = e.clientX;
                        gMouseY = e.clientY;


                        // devo capire se è in una zona trasparente
                        //debugger;
                        let img = $(`.imlayer[lo_id='${lo.loId}']`);
                        if (isTransparentPixel(img, e.offsetX, e.offsetY)) {
                            // devo fare come se fosse uscito dal layer
                            handlerOut(e);
                            return;
                        }

                        //let imgLeft = parseInt(img.css('left').replace('px', ''));
                        //let imgTop = parseInt(img.css('top').replace('px', ''));
                        //let imgRight = imgLeft + img.width() * dat.scal;
                        //let imgBottom = imgTop + img.height() * dat.scal;

                        //console.log(`clientx = ${e.clientX}, offsetx ${e.offsetX}`);
                        // vediamo se è in una zona trasparente




                        //console.log(`mousepos x  = ${e.pageX}, ${e.pageY}`);
                        //console.log(`mousepos `, e);


                        // devo sottrarre la posizione del canvas
                        const re = $(".divLayersContainer")[0].getBoundingClientRect();

                        var xFinale = e.clientX - re.left;
                        var yFinale = e.clientY - re.top;

                        if (!gFrozenMouse) {
                            posizionaDidascaliaOggettoMouse(dat, gLoHover, xFinale, yFinale, true /* is in room*/, null /*premade text*/); // questo e' un layer della room

                            maybeDisableCursorForLookWhenMouseOverRoomObj(lo);
                        }


                    }
                    function handlerOut(e) {
                        //if (gFrozenMouse)
                        //{
                        //        e.preventDefault();
                        //        e.stopPropagation();
                        //        return;
                        //}

                        //console.log("mouse exited", la.lfc_loId);
                        gLoHover = null;
                        gDatHover = null;
                        gLayerHover = null;
                        gMouseX = e.clientX;
                        gMouseY = e.clientY;

                        if (!gFrozenMouse) {
                            $(".btnOggettoInRoom").remove();

                            $(".divLayersContainer").css("cursor", "default");
                        }


                    }
                    newImg.hover(handlerIn, handlerOut);   // hover layer
                    newImg.mousemove(handlerIn);

                    newImg.mousedown(async function (e) // cliccato un layer
                    {
                        console.log("entro mousedown");
                        if (gFrozenMouse || $(e.currentTarget).hasClass("disabled") || $(e.currentTarget).hasClass("tempDisabled")) {
                            e.preventDefault();
                            e.stopPropagation();
                            return;
                        }


                        if (e.which == 3) {
                            // pulsante destro
                            cliccatoNelVuotoDeselezionaTutto();

                        }
                        else {
                            let mouseX = e.clientX;
                            let mouseY = e.clientY;
                            let img = $(`.imlayer[lo_id='${lo.loId}']`);
                            if (isTransparentPixel(img, e.offsetX, e.offsetY))
                            {
                                //console.log("cliccato parte trasparente");
                                cliccatoNelVuotoDeselezionaTutto();
                            }
                            else {
                                await onLoClickedRoom(lo, mouseX, mouseY);
                            }
                            //debugger;
                        }
                        //console.log("esco mousedown");
                    })
                }

                if (noImgMode() == false) {

                    // se è un personaggio hires, devo dividere per tre le dimensioni.
                    // lo capisco dalla presenza di -hi nel nome del layer
                    //let spl = la.lfc_imgPath.split(/[-.]/)   // uso sia - che . come separatori
                    //if (spl.includes("hi")) {

                    //    newImg.css("transform", `scale(${dat.scal / 3},${dat.scal / 3})`);
                    //}
                    //else {
                    newImg.css("transform", `scale(${dat.scal},${dat.scal})`);
                    //}
                    newImg.css("transform-origin", "0% 0%");
                }

                if (lo !== null && !lo.ofcMustBeShownInTextRoomRecap) {
                    newImg.css("pointer-events", "none"); // altrimenti non si vede ad es il layer van helsing perche' coperto dal layer vestito
                }

                if (typeof dat.posOfBg === 'undefined') {
                    //
                    debugger; // errore, forse manca bg.jpg nel file coords. prova ad aggiungere un layer vuoto in cima al gruppo bg.jpg
                }

                let offsx = dat.posOfBg.left;

                let left = la.lfc_x * dat.scal + offsx;
                newImg.css("left", left);

                newImg.attr("left", left);

                let top = la.lfc_y * dat.scal;
                newImg.css("top", top);

                newImg.attr("top", top);

                if (lo !== null) { // succede solo per character
                    newImg.mousedown(async e => {
                        if (gFrozenMouse) {
                            e.preventDefault();
                            e.stopPropagation();
                            return;
                        }


                        e.stopPropagation(); // se no scatta il click sul fondale

                        if (la.lfcIsOutline) {
                            const hitTestImg = $(e.currentTarget);
                            const isTransparent = isTransparentPixel(hitTestImg, e.offsetX, e.offsetY);

                            if (isTransparent) {
                                // L'evento è arrivato sull'immagine dell'hit-test,
                                // quindi non può propagarsi al layer sotto. Cerca
                                // esplicitamente il primo oggetto sottostante.
                                const containerRect = $(".divLayersContainer")[0].getBoundingClientRect();
                                const mouseX = e.clientX - containerRect.left;
                                const mouseY = e.clientY - containerRect.top;
                                const objectsBelow = objectsUnderMouse(mouseX, mouseY, null, e.currentTarget)
                                    .filter(x => !x.lo.ofcIsInCurParty);

                                if (objectsBelow.length > 0) {
                                    await onLoClickedRoom(
                                        objectsBelow[0].lo,
                                        e.clientX,
                                        e.clientY,
                                        true // il menu nasce comunque sopra un layer -ou
                                    );
                                }
                                else {
                                    cliccatoNelVuotoDeselezionaTutto();
                                }
                            }
                            else {
                                await onLoClickedRoom(lo, e.clientX, e.clientY, true);
                            }
                            return;
                        }


                        //debugger;
                        if (gVerbChosen === null) {

                            // impedisco di cliccare l'oggetto prima del verbo, confonde.
                            //deselectAll();

                            //gLo1Chosen = lo;


                            //updateActionBarAndSelectabilityOfObjects();
                        }
                        else {
                            // se il verbo è ricorda, devo eseguire il remember

                            gSelectedObj = lo;
                            gLo1ChosenWasInInv = false;
                            sceltoUnVerboEUnOggetto(gVerbChosen, gSelectedObj, g_last_room_desc);
                        }

                    });
                }
                else {
                    newImg.addClass("pointerEventsNone");
                }





            }
        }

    }
    else { // text mode

        //debugger;

        $(".dovesei").remove();
        $("<div class='dovesei'>").appendTo(".textModeLeftBar").text(lCurRoom.rfcNameTextMode);


        $(".quivedi").remove();
        let quiVedi = $("<div class='quivedi'>").appendTo(".textModeLeftBar").text("quiVedi".tr());


        $(".textModeRoomObj").remove();

        $(".nienteDiSpecRoom").remove();
        function initOfcTextMode(ofc) {
            // Un contenitore non interattivo evita button annidati: Chrome
            // può ignorare il click del pulsante figlio in quel caso.
            let textModeObjectRoom = $("<div class='textModeRoomObj' role='button' tabindex='0'>").appendTo(".textModeLeftBar").attr("lo_id", ofc.loId);
            textModeObjectRoom.text(ofc.ofc_name.firstLetterToUpper());


            textModeObjectRoom.addClass("disabled");

            // In text mode the old ofcVerbIdWhenInRoom field was removed from
            // the server DTO.  Use the current context-menu capability field
            // so room objects (e.g. the suitcase) expose their action too.
            const roomAction = ofc.ofcContextMenuUseForOrHereOrDeduce;
            if (roomAction == 'useFor' || roomAction == 'deduce' || roomAction == 'useHere') {
                let testoChildButton;
                if (roomAction == 'deduce') {
                    testoChildButton = "deduce".tr().firstLetterToUpper(); // usare ofcHoverStringWhenInRoom sarebbe troppo lungo, perche' è "deduci qualcosa su"
                }
                else if (roomAction == 'useFor') {
                    testoChildButton = "useForUpper".tr().firstLetterToUpper();
                }
                else {
                    testoChildButton = "useUpper".tr().firstLetterToUpper();
                }
                let childButton = $("<button class='textModeChildButton btn btn-default'>").text(testoChildButton).appendTo(textModeObjectRoom);

                if (gIsNarrowScreen) // viauslizzazione mobile: pulsanti child attaccati al testo
                {
                    let leftPos = textModeObjectRoom.width() + 30;
                    childButton.css('left', leftPos);
                }

                childButton.click(async e => {
                    e.preventDefault();
                    e.stopPropagation();
                    //console.log("cliccato child");

                    if (childButton.hasClass("disabled")) {
                        return;
                    }


                    if (e.which == 3) {
                        // pulsante destro
                        cliccatoNelVuotoDeselezionaTutto();

                    }
                    else {
                        // Il pulsante figlio è già l'azione scelta. In text
                        // mode non c'è un context menu intermedio: chiamare
                        // onLoClickedRoom con gSelectedVerb nullo non avrebbe
                        // quindi alcun effetto.
                        try {
                            if (roomAction == 'useFor') {
                                await showDialogUseFor(ofc);
                            }
                            else if (roomAction == 'deduce') {
                                await showDialogIsActually(ofc);
                            }
                            else if (roomAction == 'useHere') {
                                await callRemember(ofc.loId, false, true, false);
                            }
                        }
                        catch (error) {
                            showClientDiagnostic(
                                "Errore apertura azione oggetto stanza",
                                formatClientDiagnostic({
                                    roomAction: roomAction,
                                    objectId: ofc.loId,
                                    objectName: ofc.ofc_name,
                                    error: error?.stack || String(error)
                                }));
                        }
                    }
                });
            }


            textModeObjectRoom.click(async e => {
                //console.log("textModeObjectRoom click");
                //debugger;
                e.preventDefault();
                e.stopPropagation(); // purtroppo essendo async non funziona.

                if (gFrozenMouse || $(e.currentTarget).hasClass("disabled") || $(e.currentTarget).hasClass("tempDisabled")) {
                    e.preventDefault();
                    e.stopPropagation();
                    return;
                }

                if (textModeObjectRoom.hasClass("disabled")) {
                    return;
                }


                if (e.which == 3) {
                    // pulsante destro
                    cliccatoNelVuotoDeselezionaTutto();

                }
                else {
                    //debugger;
                    let mouseX = e.clientX;
                    let mouseY = e.clientY;
                    await onLoClickedRoom(ofc, mouseX, mouseY);
                }
            });
        }

        for (let ofc of ofcInCurRoom2) {
            if (ofc.ofcMustBeShownInTextRoomRecap) {
                if (!ofc.ofcIsExit) {

                    initOfcTextMode(ofc);
                }
            }
        }

        if (!gIsNarrowScreen) // versione non mobile: pulsanti tutti stessa larghezza (in quanto non attaccati al testo)
        // setto in js la larghezza e la posizione dei pulsanti child (usa, deduci)
        //debugger;
        {
            $(".textModeChildButton ").css('width', '');

            let textModeCh = $(".textModeChildButton");
            //let wt0 = textModeCh.outerWidth();
            let larghezze = textModeCh.map((i, x) => $(x).outerWidth()).get();
            if (larghezze.length > 0) {
                let maxWt = Math.max.apply(null, larghezze);
                //debugger;
                maxWt = Math.max(maxWt, 63);
                $(".textModeChildButton").css('width', `${maxWt}px`);
            }
        }

        let oggettiRoomMessi = $(".textModeRoomObj");
        if (oggettiRoomMessi.length == 0) {

            $("<div class='nienteDiSpecRoom'>").text("Nientedispeciale".tr()).appendTo(".textModeLeftBar");
        }

        //$("<div class='quivedi'>").appendTo(".textModeLeftBar").text("Uscite ovvie:");

        //for (let ofc of ofcInCurRoom2)
        //{
        //        if (ofc.ofcMustBeShownInTextRoomRecap)
        //        {
        //                if (ofc.ofcIsExit)
        //                {

        //                        initOfcTextMode(ofc);
        //                }
        //        }
        //}







        // colonna di destra: verbi

        function textModeRoomTargetIsSelectable(ofc) {
            if (!ofc.ofcCanBeUsedAsTargetInTextMode) {
                return false;
            }

            if (gSelectedVerb === 'pickup') {
                return ofc.ofcIsPickableNow;
            }

            const selectedLoId = gSelectedObj?.loId;
            const hideInside = selectedLoId === g_last_room_desc.grrHideInsideLoId;
            const climb = selectedLoId === g_last_room_desc.grrClimbLoId;
            const disguise = selectedLoId === g_last_room_desc.grrTravestitiLoId;

            // Keep the text-mode target filter aligned with the original
            // context menu: hiding and climbing accept room objects, while
            // disguising and dialogue topics accept Characters.
            if (hideInside || climb) {
                return !ofc.ofc_is_character;
            }
            if (disguise || gSelectedObj?.ofcIsConversationTopic) {
                return ofc.ofc_is_character;
            }

            return true;
        }

        globalThis.showTextModeTargetsModal = async function (mode = "combine") {
            const room = g_last_room_desc.grrRooms[g_last_room_desc.grrCurRoomId];
            const roomObjects = (room?.rfc_objects ?? [])
                .filter(ofc => ofc.ofcMustBeShownInTextRoomRecap && !ofc.ofcIsExit);
            const inventoryObjects = g_last_room_desc.grrInvObjects ?? [];
            const isRememberMode = mode === "remember";
            const isPickupMode = mode === "pickup";
            const list = $("#textModeRoomTargetsModal .textModeRoomTargetsList");
            const inventoryList = $("#textModeRoomTargetsModal .textModeRoomTargetsInventoryList");
            const empty = $("#textModeRoomTargetsModal .textModeRoomTargetsEmpty");
            list.empty();
            inventoryList.empty();
            $("#textModeRoomTargetsModal .textModeRoomTargetsRoomTitle").toggle(isRememberMode || isPickupMode);
            $("#textModeRoomTargetsModal .textModeRoomTargetsInventoryTitle").toggle(isRememberMode);

            const selectedObject = gSelectedObj;
            const actionTemplate = selectedObject?.ofcVerbWhenUseWithAsFirstObjectOnHoverNotSelected
                || selectedObject?.ofcHoverStringWhenInInv
                || selectedObject?.ofc_name
                || "Azione";
            const actionText = actionTemplate.replace("{1}", selectedObject?.ofc_name || "");
            $("#textModeRoomTargetsTitle").text(isRememberMode
                ? "remember".tr().firstLetterToUpper()
                : isPickupMode
                    ? "raccogliPuntini".tr().firstLetterToUpper()
                    : actionText.firstLetterToUpper());

            let selectableCount = 0;
            const appendEntry = (container, ofc, selectable, onClick) => {
                const entry = $(selectable
                    ? "<button type='button' class='textModeRoomTargetEntry btn btn-default'>"
                    : "<div class='textModeRoomTargetEntry textModeRoomTargetEntryDisabled'>")
                    .text(ofc.ofc_name.firstLetterToUpper())
                    .attr("lo_id", ofc.loId);
                container.append(entry);

                if (selectable) {
                    selectableCount++;
                    entry.on("click", async e => {
                        e.preventDefault();
                        e.stopPropagation();
                        gTextModeRoomTargetChosen = true;
                        $("#textModeRoomTargetsModal").modal("hide");
                        await onClick(ofc, e);
                    });
                }
            };

            for (const ofc of roomObjects) {
                appendEntry(list, ofc,
                    isRememberMode
                        ? ofc.ofc_can_be_remembered
                        : isPickupMode
                            ? ofc.ofcIsPickableNow
                            : textModeRoomTargetIsSelectable(ofc),
                    async (selected, e) => isRememberMode
                        ? await callRemember(selected.loId, false, false, false)
                        : isPickupMode
                            ? await callRemember(selected.loId, true, false, false)
                        : await onLoClickedRoom(selected, e.clientX, e.clientY));
            }

            if (isRememberMode) {
                for (const ofc of inventoryObjects) {
                    appendEntry(inventoryList, ofc, ofc.ofc_can_be_remembered,
                        async selected => await callRemember(selected.loId, false, false, false));
                }
            }

            empty.text(isRememberMode
                ? "Non ci sono oggetti ricordabili in questa stanza o nell'inventario."
                : isPickupMode
                    ? "nienteDaRacQui".tr()
                    : "Non ci sono oggetti combinabili nella stanza.");
            empty.toggle(selectableCount === 0);
            $("#textModeRoomTargetsModal").modal("show");
        };

        async function onTextObjectOrVerbClicked(e, ofc) {

            //debugger;
            if (gFrozenMouse) {
                e.preventDefault();
                e.stopPropagation();
                return;
            }

            if (e.which == 1) {
                e.preventDefault();
                e.stopPropagation();

                if (gSelectedObj != null && gLo1ChosenWasInInv) // cliccato su oggetto dell'inv - ignora
                {
                    return;
                }
                else if (gSelectedObj != null && gSelectedObj.loId == ofc.loId) // cliccato su se stesso - ignora il click
                {
                    return;
                }


                if (gSelectedVerb == 'pickup') {
                    // non posso raccogliere ciò che ho già - ignora il click

                    //await callRemember(ofc.loId, true);

                    //gSelectedVerb = null;
                }
                else if (gSelectedVerb == 'remember') {
                    if (ofc.ofc_can_be_remembered) {
                        await callRemember(ofc.loId, false, false, false);
                    }
                    else {
                        // ignora il click
                    }
                }
                else if (gSelectedVerb == null) {
                    if (gSelectedObj == null) {
                        //debugger;
                        if (ofc.ofcIsUseInLocationWhenInInv) {

                            await callRemember(ofc.loId, false, true, false);
                        }
                        else if (ofc.ofcIsUseWithWhenInInv) {
                            gSelectedObj = ofc;
                            gLo1ChosenWasInInv = true;
                            gSelectedVerb = null;
                        }
                        else if (ofc.ofcIsUseForWhenInInv) {



                            await showDialogUseFor(ofc);
                        }
                        else // è use in composer
                        {
                            console.error("non gestito fdfd8dj48");
                            debugger;
                            //gComposerInSayMode = false;
                            //await showNewActionComposer(ofc);

                        }
                    }
                    else {
                        // devo eseguire use with tra due oggetti dell'inv. in realtà non si può...
                        console.error("impossibile 48jfdfd4fh4u combinare 2 oggetti inv");
                        debugger;
                        //await callUseWith(gSelectedObj.loId, ofc.loId);

                        //gSelectedObj = null;
                        //gSelectedVerb = null;
                    }
                }
                else {
                    console.error("non gfesstito fdjkfdfdfdcddk");
                }

                updateToolbar();

                // A normal inventory object selected as the first operand
                // needs a room target. Show the room-target modal instead of
                // scrolling the user away from the selected object/verb.
                if (gSelectedObj === ofc && gLo1ChosenWasInInv) {
                    await globalThis.showTextModeTargetsModal("combine");
                }

                //let dat = calcolaScalEtc();
                //let xf = calcolaXYDelMouseRispettoAlCanvas(e);
                //posizionaDidascaliaOggettoMouse(dat, ofc, xf.x, xf.y, false /* is in inv*/);
            }
        }


        $(".textModeInvObject").remove();
        for (let ofc of g_last_room_desc.grrInvObjects) {
            if (ofc.ofcIsConcept) {
                let newBtn = $("<button class='btn btn-default btnTextModeRight textModeInvObject '>").insertBefore(".textModeSepOpz").attr("lo_id", ofc.loId);
                if (ofc.ofcVerbWhenUseWithAsFirstObjectOnHoverNotSelected == null) {   // succeder per l'oggett "solve puzzle of richiamo ucceli"
                    let text = ofc.ofcHoverStringWhenInInv;
                    newBtn.text(text.firstLetterToUpper());
                }
                else {
                    newBtn.text(ofc.ofcVerbWhenUseWithAsFirstObjectOnHoverNotSelected.firstLetterToUpper());
                }


                newBtn.click(async e => {
                    //debugger;
                    e.preventDefault();
                    //console.log("textModeInvObject.click");
                    await onTextObjectOrVerbClicked(e, ofc);
                });

                newBtn.contextmenu(e => {
                    cliccatoNelVuotoDeselezionaTutto();
                });

            }
        }

        // colonna destra : oggetti inv
        //$(".textModeIntroAzioniOgggetti.oggetti").remove();
        //$("<div class='textModeIntroAzioniOgggetti oggetti'>").appendTo(".textModeRightBar").text("Oggetti che puoi usare:");

        //$("<div class='quivedi portiConTe'>").appendTo(".textModeLeftBar").text("Oggetti che hai con te:");

        //$(".textModeInvObject").remove();
        for (let ofc of g_last_room_desc.grrInvObjects) {
            if (!ofc.ofcIsConcept) {
                let newImg = $("<button class='btn btn-default btnTextModeRight  textModeInvObject btn btn-default'>").insertBefore(".textModeSepOpz").attr("lo_id", ofc.loId);
                //if (ofc.ofcVerbWhenUseWithAsFirstObjectOnHoverNotSelected == null) {
                //        debugger;
                //}
                let txt = ofc.ofcHoverStringWhenInInv.replace("{1}", ofc.ofc_name);
                newImg.text(txt.firstLetterToUpper());

                newImg.click(async e => {
                    //console.log("textModeInvObject.click");

                    await onTextObjectOrVerbClicked(e, ofc);
                });

                newImg.contextmenu(e => {
                    cliccatoNelVuotoDeselezionaTutto();
                });

            }
        }





    }





    // Non mostrare la stanza finché anche i layer non hanno terminato il
    // caricamento: altrimenti si vede per un istante solo il fondale.
    if (isImgMode()) {
        await waitForRoomImages(".divLayersContainer .imlayer");
        await waitForBrowserLayout();
    }

}

function calcolaXYDelMouseRispettoAlCanvas(e) {


    //console.log(`mousepos x  = ${e.pageX}, ${e.pageY}`);
    //console.log(`mousepos `, e);


    // deov sottrarre la posizione del canvas
    let re = $(".divLayersContainer")[0].getBoundingClientRect();

    var xFinale = e.clientX - re.left;
    var yFinale = e.clientY - re.top;

    return { x: xFinale, y: yFinale };
}




async function rebuildInv() {
    $(".invObjTemplate").remove();

    let allobjs = g_last_room_desc.grrInvObjects; //.concat(g_last_room_desc.grrInvConcepts);


    if (g_last_room_desc.grrTalkNow) {
        $(".talkIcon img").css('opacity', 1);
        $(".talkIcon img").attr('src', prefissoWebApi + `/${gInvIconsFolder}/talkHalo.png`);
        $(".talkIcon").css('cursor', "default");
    }
    else {
        $(".talkIcon img").css('opacity', 0.26);
        $(".talkIcon img").attr('src', prefissoWebApi + `/${gInvIconsFolder}/talk.png`);

        $(".talkIcon").css('cursor', "not-allowed");
    }

    function azzeraSelezioneComeSeNonFosseSuNiente() {
        gLoHover = null;
        gDatHover = null;
        gLayerHover = null;

        $(".btnOggettoInRoom").remove();
    }

    for (let ofc of allobjs) // sono oggetti dell'inv
    {

        // skippo i verbi "nasconditi", "climb", "hide inside", che devono essere nell'inv perche' il context menu lo richiede, ma non si devono vedere nell'inv
        // (commenta questo se devi testare inventario piu lungo)
        if (ofc.loId == g_last_room_desc.grrClimbLoId || ofc.loId == g_last_room_desc.grrTravestitiLoId || ofc.loId == g_last_room_desc.grrHideInsideLoId

            || ofc.ofcIsConversationTopic) {
            continue;
        }

        // skippo alcune cose che non devono apparire nell'inv: i verbi del context menu e i topic di conversazione. ma non ci sono più
        //if (ofc.ofcIsCustomVerbForRoomCharacters || ofc.ofcIsCustomVerbForRoomObjects) {
        //        continue;
        //}

        let newBtn = templateInvObject.clone();

        if (ofc.ofcIsConcept) {
            newBtn.addClass("isConcept");
        }
        else {
            newBtn.addClass("isPhysObj");
        }


        //newBtn.find(".nameinv").text(ofc.ofc_name)
        if (ofc.ofcCustomInvIcon != null) {
            newBtn.find(".imginv").attr('src', ofc.ofcCustomInvIcon);
        }
        else {
            newBtn.find(".imginv").attr('src', `${prefissoWebApi}/${gInvIconsFolder}/${ofc.loId}.png`)
        }

        if (ofc.ofcVerbWhenUseWithAsFirstObjectOnHoverNotSelected != null) {
            newBtn.find(".invObjectTesto").text(ofc.ofcVerbWhenUseWithAsFirstObjectOnHoverNotSelected.firstLetterToUpper())
        }
        else {
            let testo = ofc.ofcHoverStringWhenInInv.replace("{1}", ofc.ofc_name).firstLetterToUpper();
            newBtn.find(".invObjectTesto").text(testo);

        }


        newBtn.attr("lo_id", ofc.loId);


        function handlerInOrMoveOverInvObj(e) // bm_mousemoved su ogetto dell'inv (ofc)
        {
            if (gFrozenMouse) {
                e.preventDefault();
                e.stopPropagation();
                return;
            }

            if (gSelectedObj == null) {
                if (gSelectedVerb == 'pickup' || gSelectedVerb == 'deduce') {
                    newBtn.css("cursor", "not-allowed");
                }
                else if (gSelectedVerb == 'look' && !ofc.ofcIsLookableNow) {
                    newBtn.css("cursor", "not-allowed");
                }
                else {
                    newBtn.css("cursor", "default");
                }
            }

            if (gSelectedObj != null && gLo1ChosenWasInInv && gSelectedObj != ofc) {
                //console.log("caso a");
                azzeraSelezioneComeSeNonFosseSuNiente();

                newBtn.css("cursor", "not-allowed");
            }
            else if (gSelectedObj !== null && gSelectedObj.loId == ofc.loId) {
                //azzeraSelezioneComeSeNonFosseSuNiente();
                //newBtn.css("cursor", "default");
                let dat = calcolaScalEtc();
                gMouseX = e.clientX;
                gMouseY = e.clientY;



                let xf = calcolaXYDelMouseRispettoAlCanvas(e)
                posizionaDidascaliaOggettoMouse(dat, ofc, xf.x, xf.y, false /* is in inv*/); // questo e' un oggetto dell'inv
            }
            else {
                //console.log("mouse entered or moved on inv object while no obj selected", ofc);
                let dat = calcolaScalEtc();
                gLoHover = ofc;
                gDatHover = dat;
                gLayerHover = null;
                gMouseX = e.clientX;
                gMouseY = e.clientY;



                let xf = calcolaXYDelMouseRispettoAlCanvas(e)

                posizionaDidascaliaOggettoMouse(dat, gLoHover, xf.x, xf.y, false /* is in inv*/); // questo e' un oggetto dell'inv
            }
        }
        function handlerOut(e) {
            if (gFrozenMouse) {
                e.preventDefault();
                e.stopPropagation();
                return;
            }

            //console.log("mouse exited", la.lfc_loId);
            gMouseX = e.clientX;
            gMouseY = e.clientY;

            azzeraSelezioneComeSeNonFosseSuNiente();
        }
        newBtn.hover(handlerInOrMoveOverInvObj, handlerOut);
        newBtn.mousemove(handlerInOrMoveOverInvObj); // mousemove su ogetto dell'inv




        newBtn.mousedown(async e =>    // click su oggetto dell'inv (che è ofc)
        {
            if (gFrozenMouse) {
                e.preventDefault();
                e.stopPropagation();
                return;
            }

            if (e.which == 1) {
                e.stopPropagation();
                if (gSelectedObj != null && gLo1ChosenWasInInv) // cliccato su oggetto dell'inv - ignora
                {
                    return;
                }
                else if (gSelectedObj != null && gSelectedObj.loId == ofc.loId) // cliccato su se stesso - ignora il click
                {
                    return;
                }


                if (gSelectedVerb == 'look') {
                    if (ofc.ofcIsLookableNow) {
                        await callRemember(ofc.loId, false, false, true);

                        gSelectedVerb = null;
                    }
                    else {
                        // ignora click
                    }
                }
                else if (gSelectedVerb == 'pickup') {
                    // non posso raccogliere ciò che ho già - ignora il click

                    //await callRemember(ofc.loId, true);

                    //gSelectedVerb = null;
                }
                else if (gSelectedVerb == 'talk') {
                    // ignoro il click
                }
                else if (gSelectedVerb == 'deduce') {

                    // ignoro il click. TODO se ofc ha deduce when in inv (pizza all'aglio, gestisci)
                }
                else if (gSelectedVerb == null) {
                    if (gSelectedObj == null) {
                        //debugger;
                        if (ofc.ofcIsUseInLocationWhenInInv) {

                            await callRemember(ofc.loId, false, true, false);
                        }
                        else if (ofc.ofcIsUseWithWhenInInv) {
                            gSelectedObj = ofc;
                            gLo1ChosenWasInInv = true;
                            gSelectedVerb = null;
                        }
                        else if (ofc.ofcIsUseForWhenInInv) {



                            await showDialogUseFor(ofc);
                        }
                        else // è use in composer
                        {
                            console.error("non gestito 8dj48");
                            debugger;
                            //gComposerInSayMode = false;
                            //await showNewActionComposer(ofc);

                        }
                    }
                    else {
                        // devo eseguire use with tra due oggetti dell'inv. in realtà non si può...
                        console.error("impossibile 48j4fh4u combinare 2 oggetti inv");
                        debugger;
                        //await callUseWith(gSelectedObj.loId, ofc.loId);

                        //gSelectedObj = null;
                        //gSelectedVerb = null;
                    }
                }
                else {
                    console.error("non gfesstito fdjkfdk");
                }

                updateToolbar();

                let dat = calcolaScalEtc();
                let xf = calcolaXYDelMouseRispettoAlCanvas(e);
                posizionaDidascaliaOggettoMouse(dat, ofc, xf.x, xf.y, false /* is in inv*/);
            }
        });

        newBtn.appendTo(".invContenitoreOggettiInner");

    }

}

async function rebuildRoom(roomDesc, doGraphics, doInv) {
    console.log("rebuildRoom", roomDesc);
    logRoomLayout("rebuild-room-start", {
        incomingRoom: roomDesc?.grrCurRoomId ?? null,
        doGraphics,
        doInv
    });

    if (doGraphics) {
        $("#roomOuter").addClass("room-loading");
    }

    gSelectedVerb = null;
    gLoHover = null; // fixa bug raro dopo azione, bm_dj48d48ij
    gDatHover = null;
    gLayerHover = null;


    $(".dialogChoiceOuterFullScreen").hide();



    g_last_room_desc = roomDesc;
    $("#btnGameMode").text(roomDesc.grrCasualMode === true ? "Modalità: casual" : "Modalità: normale");



    if (g_last_room_desc.grrIsTextMode) {


        $(".roomTextMode").show();

        $("body").addClass("noimg");

        $(".textModeFraseComposta").html("&nbsp;");
    }
    else {
        $(".roomTextMode").hide();
        $("body").removeClass("noimg");
    }

    //shuffle(g_last_room_desc.grrTemplates);

    shuffle(g_last_room_desc.grrExplanationsGlobal);

    $(".btnCammina").text(roomDesc.grr_walk_translated.firstLetterToUpper());
    $(".qui_vedi").text(roomDesc.grr_here_you_see.firstLetterToUpper() + ":");



    $("#roomTitle").hide();


    let newInventoryObjects = [];

    //if (doInv) 
    {
        await rebuildInv();


        // forse scrolla l'inv sul nuovo oggetto
        if (gLastInvNonIgnorato != null) {
            newInventoryObjects = g_last_room_desc.grrInvObjects.filter(ofcNew => !gLastInvNonIgnorato.any(ofcOld => ofcOld.loId == ofcNew.loId));
            if (newInventoryObjects.length > 0) {
                // scrolla il primo
                let loid = newInventoryObjects[0].loId;
                let elementWithThisObjs = $(`.invObjTemplate[lo_id='${loid}']`);

                if (elementWithThisObjs.length > 0
                    && typeof elementWithThisObjs[0].scrollIntoView === "function") {
                    elementWithThisObjs[0].scrollIntoView({ block: "nearest" });
                }

                // e fai lampeggiare tutti
                for (let ob of newInventoryObjects) {
                    let loid = ob.loId;

                    let elementWithThisObjs = $(`.invObjTemplate[lo_id='${loid}']`);
                    elementWithThisObjs.removeClass("blink_me");
                    elementWithThisObjs.addClass("blink_me");
                }


                //for (var el of elementWithThisObjs)
                //{

                //}
            }
        }


        //debugger;
        aggiornaPulsantiScroll(".invContainerInnerJustObjects ");



        gLastInvNonIgnorato = g_last_room_desc.grrInvObjects;


        //console.log("img grafica wt 2 = ", imgGrafica.width());
    }

    // L'altezza dell'inventario entra nel calcolo della viewport della stanza.
    // Va quindi ricostruito prima della grafica, altrimenti la stanza può
    // risultare tagliata fino al successivo resize della finestra.
    const rebuildTextInventory = noImgMode() && newInventoryObjects.length > 0;
    if (doGraphics || rebuildTextInventory) {
        await rebuildGraphics(true);

        // In text mode rebuildGraphicsCore crea i pulsanti dell'inventario
        // dopo rebuildInv. Solo ora il primo oggetto nuovo è realmente
        // scrollabile e può essere evidenziato.
        if (noImgMode() && newInventoryObjects.length > 0) {
            await waitForBrowserLayout();
            const firstNewObject = $(`.textModeInvObject[lo_id='${newInventoryObjects[0].loId}']`);
            if (firstNewObject.length > 0
                && typeof firstNewObject[0].scrollIntoView === "function") {
                firstNewObject[0].scrollIntoView({ block: "nearest", behavior: "smooth" });
            }
            for (const newObject of newInventoryObjects) {
                const newButton = $(`.textModeInvObject[lo_id='${newObject.loId}']`);
                newButton.removeClass("blink_me");
                // Forza il riavvio dell'animazione anche se lo stesso oggetto
                // era stato evidenziato da una precedente azione.
                void newButton[0]?.offsetWidth;
                newButton.addClass("blink_me");
            }
        }
    }

    logRoomLayout("rebuild-room-end", {
        incomingRoom: roomDesc?.grrCurRoomId ?? null
    });

    if (doGraphics) {
        $("#roomOuter").removeClass("room-loading");
    }

    $(".par").remove(); // svuota la descrizione della stanza precedente

    updateActionBarAndSelectabilityOfObjects();


    //if (scrollUp) {
    //window.scrollTo(0, $("#room").offset().top);
    //console.log("room", $("#room"));
    //$("#room").scrollTop(0); // la room deve scrollare in alto perché potrei aver cambiato stanza.




    //$(".invNewOuter").scrollTop(0); // l'inv deve scrollare in alto perché devo sempre vedere i verbi pinned.
    $(".scrollbar-outer").scrollTop(0); // l'inv deve scrollare in alto perché devo sempre vedere i verbi pinned.

    //}






    // la room


    // la zona "tu"

    //$(".spanYou").html(roomDesc.activeChar.ofc_name.firstLetterToUpper());


    // uscite ovvie
    //$(".contUscite .btnVerbObject").remove();
    //roomDesc.grrExits.forEach(function (kel) {

    //    creaPulsanteInvMind(kel, ".contUscite", roomDesc);

    //});



    // adesso l'inv
    $("#contOggettiInv .btnVerbObject").remove();

    $(".contVerbiInv .btnVerbObject").filter((i, el) => !($(el).hasClass("btnMap"))).remove();

    $("#contMindInv .btnVerbObject").remove();

    $(".titoloVerbi").html("quickActions".tr().firstLetterToUpper() + ":");
    $(".titoloRoomObjs").html(roomDesc.grr_here_you_see.firstLetterToUpper() + ":");
    $(".titoloOggetti").html(roomDesc.invTitle.firstLetterToUpper() + ":");
    $(".titoloPensieri").html(roomDesc.mindTitle.firstLetterToUpper() + ":");
    $(".titoloOpzioni").html(roomDesc.optionsTitle.firstLetterToUpper());
    $(".your_objectives").html("thingsToDo".tr().firstLetterToUpper() + ":");
    $(".bisognoAiuto").html("areYouStuck".tr().firstLetterToUpper());
    $(".more").html(roomDesc.grr_other.firstLetterToUpper() + ":");
    $(".btnOptMio").html(roomDesc.grr_options.firstLetterToUpper());
    $("#inv-h4").html(roomDesc.grrRememberAnObject.firstLetterToUpper());
    $(".btnApriInv").html(roomDesc.grrRememberAnObject.firstLetterToUpper());
    $(".spanExitMap").html(roomDesc.grr_back.firstLetterToUpper());
    //$("#btnChiudiObiettivi").html(roomDesc.grr_cancel.firstLetterToUpper());
    $(".btn_named_cut_scenes").html("rereadClues".tr());
    $("#mindEmpty").html(roomDesc.grr_nothing_special.firstLetterToUpper());
    $("#roomEmpty").html(roomDesc.grr_nothing_special.firstLetterToUpper());

    $(".livIntelligenza").html(roomDesc.grr_IQLevel);



    //for (vfc of roomDesc.grrVerbs)
    //{

    //        if (!vfc.vfcIsAskForHints // questo va in un'altra sezione
    //                && !vfc.vfc_is_remember // non lo metto più così

    //        )
    //        {
    //                creaPulsanteVerb(vfc, ".contVerbiInv", roomDesc);
    //        }
    //}


    //roomDesc.grrInvObjects.forEach(keywordClient =>
    //{
    //        creaPulsanteInvMind(keywordClient, "#contOggettiInv", roomDesc, true, false /* primo ogg*/, false);

    //});


    //roomDesc.grrInvConcepts.forEach(keywordClient =>
    //{

    //        creaPulsanteInvMind(keywordClient, "#contMindInv", roomDesc, true, false /* primo ogg*/, true);

    //});

    //if (roomDesc.grrInvConcepts.length > 0)
    //{
    //        $("#mindEmpty").hide();
    //}
    //else
    //{
    //        $("#mindEmpty").show();
    //}


    // obiettivi

    $(".contObiettiviRecap .btnObiettivo").remove();
    $(".contObiettiviRecap .obiettivoDisabled").remove();
    roomDesc.grrObjectives.forEach(function (ob, i) {


        let objectiveIsDisabled = g_last_room_desc.grrDisabledObjectives.filter(x => x.vocObjective === ob.ser_id);
        let newButton;
        if (objectiveIsDisabled.length > 0) {
            //newButton.attr("disabled", "disabled");
            let newButton = $('<div class="btnOpt  obiettivoDisabled scrollingItem" >').html(ob.readable_name.firstLetterToUpper());
            newButton.appendTo(".contObiettiviRecap");
        }
        else {
            let newButtonObiettivo = $('<div class="form-control btn btn-default btnOpt btnObiettivo scrollingItem  btnGroupedRounded" >').html(ob.readable_name.firstLetterToUpper())
                .attr("obiett", ob.ser_id)
                ;


            newButtonObiettivo.appendTo(".contObiettiviRecap");

            if (i === 0) {
                newButtonObiettivo.addClass("primoFiglio");
            }


            //if (ob.oc_is_temp_disabled_for_random_trials) {
            //    newButton.attr("disabled", "disabled");
            //}


            newButtonObiettivo.off('click').on('click', async function (e) {
                e.preventDefault();
                e.stopPropagation();



                //e.stopImmediatePropagation();
                //debugger;
                deselectAll();




                updateActionBarAndSelectabilityOfObjects();


                if (g_last_room_desc.grrStoryMode) {
                    let cred = JSON.parse(localStorage[credentialsId]);
                    let i = {
                        uname: cred.uname,
                        pwd: cred.pwd,
                        //token: cred.token,
                        psi_objective: ob,


                        lang: getLang()
                        , curTime: getCurTime()
                        , cred_gameId: gGameId
                    };

                    //console.log("autoSolvePuzzle: ", i);


                    mostraPleaseWait();

                    let data = await doPostTry(`${prefissoWebApi}/api/autoSolvePuzzle`, i);
                    let canContinue = handleErrorsPost(data);
                    if (canContinue) {
                        nascondiPleaseWait();

                        //console.log("ok: ", data.ret);

                        let ar = data.ret;

                        await handleAr(ar);


                    }

                }
                else {


                    gObjectiveChosen = ob;
                    gBtnPushedObjective = newButtonObiettivo;

                    $("#sentenceComposer").modal('show');

                    $("#indietroDiUno").addClass("disabled");

                    $("#sentenceComposer .titoloPer").html(g_last_room_desc.grr_in_order_to_translated.toUpperCase() + "  &nbsp;" + ob.readable_name + ",");


                    svuotaScelteDaFare();

                    proponiVerbi(ob, roomDesc);

                    //newButton.removeClass("active").addClass("active");

                    //gVerbChosen = roomDesc.grrUseVerb; // automatico under the hood.

                }



            });

            //let div = $("<div class='obiettivo'>");

            //div.html(kel.readable_name.firstLetterToUpper());
            //div.appendTo(".contObiettiviRecap");
        }


    });


    marcaButtonsConPrimoFiglio(".contObiettiviRecap", "btnObiettivo");
    //let vfcChiediAiuto = roomDesc.grrVerbs.filter(v => v.vfcIsAskForHints)[0];
    //let testoChiediAiuto = vfcChiediAiuto.vfcName.firstLetterToUpper();
    //$("#spanAiuto").text(testoChiediAiuto);

    //$("#btnChiediAiuto").off('click');
    //$("#btnChiediAiuto").click(async function (e) {
    //    e.stopPropagation();
    //    //debugger;

    //    gBtnPushedVerb = $("#btnChiediAiuto");
    //    await onVerbClicked(vfcChiediAiuto, roomDesc, gBtnPushedVerb);
    //});



    updateActionBarAndSelectabilityOfObjects(); // per rendere disabled gli oggetti

    //debugger;
    aggiornaPulsantiScroll(".invNewOuter");

    updateToolbar(roomDesc);

}




async function appendCutSceneItemDialog(ct, title) {

    //$("#narDialogInner2").show(); // era stato nascosto per l'input testuale

    if (title) {
        let divActionTitle = $('<div class="parDialog actionTitle">');
        divActionTitle.html(title);
        divActionTitle.appendTo(".divperspingereilrestoGiu"); // cosi' posso fare position absolute e allineare il mio bottom al top di quello sotto
    }



    let divDialog = $('<div class="parDialog">');



    if (ct.dtCharName) { // è un dialogToken: charName e par
        //è un dialogo
        let charName = $('<span class="charNameDialog">').html(ct.dtCharName.firstLetterToUpper()).appendTo(divDialog);
        let par = $('<span class="narPar">').html(ct.dtPar).appendTo(divDialog);

        if (ct.img && !noImgMode()) {

            let src;
            //if (noImgMode === true) {
            //        src = "img/";
            //}
            //else // char-only o false
            {
                src = ct.img;
            }
            await changeImgSrcAndWait($("#imgMain"), src);

            if (ct.ntSize == 1) // medium
            {
                $("#imgContainer").addClass("ntSizeMedium");
            }
            else {
                $("#imgContainer").removeClass("ntSizeMedium");
            }


            $("#imgContainer").removeClass("separator");
            $("#imgContainer").show();
        }
        else {
            $("#imgContainer").removeClass("ntSizeMedium");
            if (noImgMode()) $("#imgContainer").hide();
            else $("#imgContainer").show(); // se no oscilla quando un personaggio non ha icona
            //$("#imgContainer").hide(); 
        }
    }
    else if (ct.pars) {
        // è una sequenza di narrazioni raggruppate
        ct.pars.forEach(function (par) {
            $('<span class="narPar">').html(par + "</br>").appendTo(divDialog);
        });

        $("#imgContainer").hide();
    }
    else { // è un narToken: solo par.
        //debugger;
        let par = $('<span class="narPar">').html(ct.ntPar).appendTo(divDialog);



        if (ct.img && !noImgMode()) {
            // se questo nar ha un'img
            //console.log("il ct ha un img");
            $("#imgContainer").hide();



            await changeImgSrcAndWait($("#imgMain"), ct.img);


            $("#imgMain").removeClass("separator");

            if (ct.ntSize == 1) // medium
            {
                $("#imgContainer").addClass("ntSizeMedium");
            }
            else {
                $("#imgContainer").removeClass("ntSizeMedium");
            }

            $("#imgContainer").removeClass("separator");
            $("#imgContainer").show();
        }
        else if (!title || noImgMode()) {
            // se il nartoken non ha img e non ha titolo, metto il separatore grafico

            $("#imgContainer").hide();


            await changeImgSrcAndWait($("#imgMain"), "");
            $("#imgMain").addClass("separator");
            $("#imgContainer").addClass("separator");

            $("#imgContainer").removeClass("ntSizeMedium");
            $("#imgContainer").show();
        }
        else {
            $("#imgContainer").hide();
        }



    }

    divDialog.appendTo("#narDialogInner");

    let pressToCont;
    pressToCont = "pressToCont".tr();
    //if (g_last_room_desc !== null)
    //{
    //        pressToCont = g_last_room_desc.grr_press_to_continue;
    //}
    //else
    //{

    //}
    let divClick = $('<div class="clickToContinue parDialog">').html(/*${pressToCont.firstLetterToUpper()} */`<i class="fas fa-caret-right cssRightArrowContinue"></i>`).appendTo("#narDialogInner");


}

async function getNextDialogOrNar(thisIsNar) {

    if ($(".clickToContinue").visible() || thisIsNar) {

        mostraPleaseWait();
        //console.log("calling getNext");
        let cred = JSON.parse(localStorage[credentialsId]);

        let i = {
            uname: cred.uname,
            pwd: cred.pwd,
            //token: cred.token,
            lang: getLang()
            , curTime: getCurTime()
            , cred_gameId: gGameId
        };

        let data = await doPostTry(`${prefissoWebApi}/api/getNextAr`, i);
        let canContinue = handleErrorsPost(data);
        if (canContinue) {
            nascondiPleaseWait();

            //console.log("ok: ", data.ret);

            let ar = data.ret;

            await handleAr(ar);

        }

    }
    else {
        window.scrollBy({ left: 0, top: $(window).height() / 2, behavior: "smooth" });
    }

}

function showSingleHintWindow(ob, hintsSeenOfObjSerId) {


    $("#modalHints").modal('hide');



    $(".hintPiece, .hintSeparator, .obiettivoHint").remove();
    $("#modalHintsSingleObjective").modal('show');

    $("#titoloObiettivoScelto").text(ob.readable_name.firstLetterToUpper());


    if (ob.ser_id in hintsSeenOfObjSerId) { // containsKey
        let hintsSeenOfThisObj = hintsSeenOfObjSerId[ob.ser_id];
        for (let hi of hintsSeenOfThisObj.ohcHintsSeen) {

            for (let piece of hi.hvcPieces) {
                let newPiece = $("<div class='hintPiece '>").html(piece).appendTo(".contenitoreHintVisti");
            }
            let newSep = $("<hr class='hintSeparator '>").appendTo(".contenitoreHintVisti");
        }





        // ricordo il numero
        gQuantiHintEranoVisibiliUltimaVoltaPerEnigma[ob.ser_id] = hintsSeenOfThisObj.ohcHintsSeen.length;

    }
    else {
        gQuantiHintEranoVisibiliUltimaVoltaPerEnigma[ob.ser_id] = 0;
    }

    let getNextHintButton = $("<div class='btn btn-default form-control obiettivoHint'>").text("nextHint".tr()).appendTo(".contenitoreHintVisti");
    getNextHintButton.mousedown(async e => {
        $("#modalHintsSingleObjective").modal('hide');

        gIsReadingHint = ob;
        await callGetHint(ob.ser_id);
    });

    getNextHintButton[0].scrollIntoView();

}


async function showHintList() {

    let hintsSeenOfObjSerId = await callGetCurrentHints();

    $(".obiettivoHint").remove();
    for (let ob of g_last_room_desc.grrObjectives) {



        let newButton = $('<div class="obiettivoHint scrollingItem btn btn-default form-control" >').html(ob.readable_name.firstLetterToUpper());

        newButton.mousedown(async e => {



            showSingleHintWindow(ob, hintsSeenOfObjSerId);

        });

        newButton.appendTo(".contenitoreHintTitles");



    }


    $("#options").modal("hide");

    $("#modalHints").modal('show');
}



async function getPreviousCutSceneElement() {



    mostraPleaseWait();
    //console.log("calling getNext");
    let cred = JSON.parse(localStorage[credentialsId]);

    let i = {
        uname: cred.uname,
        pwd: cred.pwd,
        //token: cred.token,
        lang: getLang()
        , curTime: getCurTime()
        , cred_gameId: gGameId
    };

    let data = await doPostTry(`${prefissoWebApi}/api/getPreviousCutSceneElement`, i);
    let canContinue = handleErrorsPost(data);
    if (canContinue) {
        nascondiPleaseWait();

        //console.log("ok: ", data.ret);

        let ar = data.ret;

        // todo gestisci errorCannotGoBackInCutsceneAlreadyBeginning 

        await handleAr(ar);

    }

}




async function skipToEndOfCutScene() {



    mostraPleaseWait();
    //console.log("calling getNext");
    let cred = JSON.parse(localStorage[credentialsId]);

    let i = {
        uname: cred.uname,
        pwd: cred.pwd,
        //token: cred.token,
        lang: getLang()
        , curTime: getCurTime()
        , cred_gameId: gGameId
    };

    let data = await doPostTry(`${prefissoWebApi}/api/skipToEndOfCutScene`, i);
    let canContinue = handleErrorsPost(data);
    if (canContinue) {
        nascondiPleaseWait();

        //console.log("ok: ", data.ret);

        let ar = data.ret;



        await handleAr(ar);

    }

}

async function handleAr(ar, forceRoom = false, soGiaCheFallisce = false) {

    // Le risposte asincrone possono arrivare mentre una precedente handleAr
    // sta ancora caricando un fondale. Solo l'ultima risposta deve poter
    // modificare la scena, altrimenti la didascalia può essere nascosta e
    // mostrata nuovamente durante il caricamento.
    const handleArGeneration = ++g_handleArGeneration;
    const isCurrentHandleAr = () => handleArGeneration === g_handleArGeneration;

    if (ar.nextCutSceneToken) {
        g_roomNeedsServerRefresh = true;
    }

    //console.log('force room ', forceRoom);

    gStopEndTitles = true; // in ogni caso termina i titoli

    $("#narDialogInner2").show(); // era stato nascosto nel mostrare l'input testuale. solo ora che è arrivato il msg dal server posso rimostrarlo. se no si vedeva il vecchio dialogo prima di inviare il submit text al server.



    if (ar.savegame_invalid) {
        BootstrapDialog.show({
            title: "savegameInvalid".tr(),
            message: "yourSaveg".tr()
            //                        , buttons: [{
            //                                label: "restart".tr(), action: function (e)
            //                                {
            //                                        console.log("azione refresh");
            //                                        // non posso fare reload, perche' rchiamerebbe loagame che di nuovo direbbe savegame invalid

            ////                                        location.reload(true /* svuota cache*/);
            //                                }
            //                        }]
        });

        callStartNewGame();
    }
    else if (ar.ar_oldSessionMustTakeOver) {

        BootstrapDialog.show({
            title: "pleaseRefresh".tr(), //This window contains an old game state. 
            message: "you_have_advanced_on_other_dev".tr()
            , buttons: [{
                label: "refresh".tr(), action: function (e) {
                    //console.log("azione refresh");
                    location.reload(true /* svuota cache*/);
                }
            }]
        });
    }

    $(".contextMenu").hide();

    $("#verbMenu").hide();
    $("#sentenceComposerNew").modal('hide');

    $("#dialogChooseExplanation").modal('hide');
    $("#dialogUseFor").modal('hide');
    $("#dialogIsActually").modal('hide');

    if (!soGiaCheFallisce) {
        $(".simplebar-content-wrapper").scrollTop(0);
    }

    gCurTime = ar.ar_curTime;

    async function maybeReopenHint() {
        if (gIsReadingHint != null) {

            //let ob = g_last_room_desc.grrObjectives.filter(x => x.ser_id == reopenHintObjSerId)[0];
            let hintsSeenOfObjSerId = await callGetCurrentHints();

            let quantiHintAdessoPerQuestoEnigma;
            if (typeof hintsSeenOfObjSerId[gIsReadingHint.ser_id] == 'undefined') {
                quantiHintAdessoPerQuestoEnigma = 0;
            }
            else {
                quantiHintAdessoPerQuestoEnigma = hintsSeenOfObjSerId[gIsReadingHint.ser_id].ohcHintsSeen.length;
            }
            if (quantiHintAdessoPerQuestoEnigma > 0) {
                //debugger;

                // devo mostrare la dialog se i suggerimenti sono aumentati. 
                let devoMostrareFinestra;

                //if (gQuantiHintEranoVisibiliUltimaVoltaPerEnigma == null)
                //{
                //        devoMostrareFinestra = true;
                //}
                //else
                //{
                devoMostrareFinestra = gQuantiHintEranoVisibiliUltimaVoltaPerEnigma[gIsReadingHint.ser_id] < quantiHintAdessoPerQuestoEnigma;
                //}

                if (devoMostrareFinestra) {
                    showSingleHintWindow(gIsReadingHint, hintsSeenOfObjSerId);
                }
            }
            else {
                // se ha chiesto hint e sono ancora zero quelli visti, significa che non ci sono suggerimenti o è troppo presto. non riaprire la dialog!

            }
        }
    }

    if (soGiaCheFallisce) {
        // non devo far vedere nietne, dovevo solo aggiornare il curtime. 

        if (ar.room) {
            g_last_room_desc = ar.room; // se no crasha dopo noImgMode()
        }



        if (noImgMode()) {
            $(".textModeFraseComposta").html("&nbsp;");
        }

        await maybeReopenHint();

    }
    else if (ar.room && !ar.nextCutSceneToken && ar.arEndGame == null) {

        deselectAll();





        $("#narDialog").hide();
        $("#narBig").hide();
        $("#dialogChoices").hide();
        //$("#roomOuter").show();



        // L'inventario deve essere visibile prima del calcolo della grafica:
        // rebuildGraphics usa la sua altezza per dimensionare la stanza.
        $(".invBar").show();

        // Attendo il completamento del rebuild: altrimenti la cutscene può
        // nascondere/mostrare elementi mentre la stanza viene ancora misurata.
        const forceServerRoom = g_roomNeedsServerRefresh;
        if (forceServerRoom) {
            // ar.room proviene già dalla risposta server a getNextAr().
            // Non usare loadGame qui: ricaricherebbe lo stato corrente invece
            // di consumare la transizione della cutscene.
            console.log("[Segusum] ridisegno la stanza restituita da getNextAr dopo la cutscene", ar.room.grrCurRoomId);
            g_roomNeedsServerRefresh = false;
        }

        await rebuildRoom(ar.room, forceRoom || forceServerRoom, true/*inv - sì, deve flashare*/);

        if (g_afterTutorialPrompt != null) {
            const continuation = g_afterTutorialPrompt;
            g_afterTutorialPrompt = null;
            await continuation();
        }



        // se stavo vedendo hint, riaprilo automaticamente

        await maybeReopenHint();

        //$(".divLayersContainer").css("cursor", "default");
    }
    else if (ar.nextCutSceneToken && ar.arEndGame == null) { // è un dial or nar

        $(".invBar").hide();

        $('.btnBackCutScene').show(); // erano nascosti solo ad end game
        $(".btnStopRemembering ").show(); // erano nascosti solo ad end game
        $(".btnSaveDuringCutscene").show();
        $(".btnOptionsEndTitles").hide();

        //let pressToCont;
        //if (g_last_room_desc !== null) {
        //    pressToCont = g_last_room_desc.grr_press_to_continue;
        //}
        //else {
        //    pressToCont = "Press to continue";
        //}
        //let divClick = $('<div class="clickToContinueNar parDialog">').html(`--- ${pressToCont.firstLetterToUpper()} ---`).appendTo("#narBig");


        if (ar.room) {
            g_last_room_desc = ar.room; // se no crasha dopo noImgMode()
        }



        let title = ar.nextCutSceneToken.actionReadable;
        let ct = ar.nextCutSceneToken.cutSceneToken;
        const adminNarrativeMessageId = ct.adminNarrativeMessageId;
        //$("#roomOuter").hide();
        $("#dialogChoices").hide();
        let isDialog = typeof ct.dtCharName !== 'undefined';


        if (

            ct.img !== null
            &&
            !isDialog

            && !noImgMode()
            &&
            (ct.ntLayers.length > 0 || ct.ntSize == 3 /* fullscreen*/)





            //&& (noImgMode() === false || noImgMode() === "char-only")//in ogni caso se sono senza img va il nar vecchio


        ) {

            //debugger;
            // è un nar con grafica. nuova gestione con immagine grande con layer

            // tolgo i layer della grafica se non si vedono mentre carico l'img grande!
            $(".divLayersContainer .imlayer").remove();
            $("#imgContainerNarBig .imlayer").remove();



            //if (ct.par != "")
            {

                // Il fondale viene mostrato appena è disponibile; la didascalia
                // entra successivamente con una dissolvenza.
                $("#imgMainNarBig")
                    .stop(true, true)
                    .hide()
                    .css("opacity", 0);
                $("#narBig").show(); // devo farlo subito per poi misurare
                $("#narDialog").hide();

                // La didascalia resta trasparente fino alla comparsa del fondale.
                const narCaption = $(".didascNarBig");
                narCaption
                    .stop(true, true)
                    .html(ct.ntPar)
                    .css({ display: ct.ntPar === "" ? "none" : "block", opacity: 0 });
            }
            //else
            //{
            //        $(".didascNarBig").hide(); // se no quando faccio narbig show si vede stretto
            //        $("#narBig").show(); // devo farlo subito per poi misurare
            //        $("#narDialog").hide();
            //}


            $("#narBig").off("click").click(async function (e) {
                await getNextDialogOrNar(true);
            });

            //debugger;

            // Nessun ritardo intenzionale: il fondale deve apparire appena
            // terminato il caricamento dell'immagine.
            await changeImgSrcAndWait($("#imgMainNarBig"), prefissoWebApi + "/" + ct.img);  //aspetto perché devo calcolare le dimensioni reali

            if (!isCurrentHandleAr()) {
                return;
            }


            // BEGIN settta la larghezza o altezza iniziale dello sfondo, in modo da vedersi tutto nello schermo
            // La barra dei controlli ha sempre la stessa altezza. Misuro solo
            // lo spazio realmente disponibile per la scena, non l'intero
            // contenitore che comprende anche la barra.
            const narControls = $("#narBig .containerPulsantiBackNar");
            narControls.css({
                height: "60px",
                minHeight: "60px",
                flex: "0 0 60px"
            });

            let viewportwt = $("#narBig").width();
            let viewportht = $("#narBig .narOuter2").innerHeight();
            //console.log("view port wt = ", viewportwt);
            //console.log("view port ht = ", viewportht);
            let viewpRatio = viewportht / viewportwt;

            let imgGrafica = $("#imgMainNarBig");
            function validateImageDimensions(width, height) {
                if (!width || !height) {
                    throw new Error("Width and height must be defined and non-zero.");
                }
            }


            let imgWt = imgGrafica.width();
            let imgHt = imgGrafica.height();

            validateImageDimensions(imgWt, imgHt);

            // Le coordinate dei layer del nar sono relative al fondale del
            // narRoom appena caricato, non alla room giocabile contenuta in
            // g_last_room_desc. Conserviamo quindi le dimensioni naturali
            // dell'immagine prima di ridimensionarla al viewport.
            const narBgNaturalWt = imgGrafica[0].naturalWidth || imgWt;
            const narBgNaturalHt = imgGrafica[0].naturalHeight || imgHt;

            validateImageDimensions(narBgNaturalWt, narBgNaturalHt);

            let imgRatio = narBgNaturalHt / narBgNaturalWt;


            //console.log("img grafica wt 1 = ", imgWt);




            let scal;   // teorwt * scal = actualwt
            if (viewpRatio > imgRatio) {
                //la larg immagine deve eguagliare quella del viewport
                // vwt = mul * imwt
                let finalWt = viewportwt;
                imgGrafica.width(finalWt);

                imgGrafica.height("");
                //$(".imgContainerNarBig").width(viewportwt);
                //$(".imgContainerNarBig").height("");
                //debugger;

                let acwt = imgGrafica.width();

                scal = acwt / narBgNaturalWt;


            }
            else {
                // raro: immagine piu' allungata verticalmente rispetto alla finestra comment__1
                imgGrafica.height(viewportht);
                imgGrafica.width("");


                scal = imgGrafica.height() / narBgNaturalHt;

                //$(".imgContainerNarBig").height(viewportht);
                //$(".imgContainerNarBig").width("");
            }

            //debugger;

















            // calcolo lo scaling dei layer a partire dallo scaling del bg


            let posOfBg;



            imgGrafica.attr("orig_ht", narBgNaturalHt);
            imgGrafica.attr("orig_wt", narBgNaturalWt);

            //posOfBg = $(".imgGrafica").position();
            posOfBg = imgGrafica.position();

            console.log(`Position of background: ${JSON.stringify(posOfBg)}`);

            //for (let la of ct.ntLayers) {
            //    if (la.lfc_imgPath.endsWith("bg.jpg") || la.lfc_imgPath.endsWith("bg-res1900.jpg") || la.lfc_imgPath.endsWith("bg.png")) {

            //        let acwt = imgGrafica.width();
            //        let teorWt = la.lfc_wt;
            //        scal = acwt / teorWt;

            //        posOfBg = $(".imgGrafica").position();

            //    }
            //}

            let dat = { scal: scal, posOfBg: posOfBg };





            // stampo i layer



            $("#imgContainerNarBig .imlayer").remove();

            for (let la of ct.ntLayers) {
                if (la.lfc_imgPath.endsWith("bg.jpg") || la.lfc_imgPath.endsWith("bg-res1900.jpg") || la.lfc_imgPath.endsWith("bg.png") || la.lfcIsOutline) {
                    let niente;
                }
                else {

                    {


                        let newImg = $("<img class='imlayer'>").appendTo("#imgContainerNarBig")
                            .attr('src', cacheBustGraphicsUrl(la.lfc_imgPath))
                            .attr("lo_id", la.lfc_loId)
                            .css("opacity", 0);



                        // Always use the base scale, regardless of "-hi" in filename
                        newImg.css("transform", `scale(${dat.scal},${dat.scal})`);


                        if (la.lfc_isHires) {
                            newImg.css("image-rendering", "auto");
                        }


                        newImg.css("transform-origin", "0% 0%");

                        if (typeof dat.posOfBg === 'undefined') {
                            //
                            debugger; // errore, forse manca bg.jpg nel file coords. prova ad aggiungere un layer vuoto in cima al gruppo bg.jpg
                        }

                        let offsx = dat.posOfBg.left;
                        let offsy = dat.posOfBg.top;

                        let left = la.lfc_x * dat.scal + offsx;
                        newImg.css("left", left);

                        newImg.attr("left", left);
                        newImg.attr("orig_x", la.lfc_x);

                        let top = la.lfc_y * dat.scal + offsy;
                        newImg.css("top", top);

                        newImg.attr("orig_y", la.lfc_y);

                        newImg.attr("top", top);

                    }



                }
            }

            // Sfondo e personaggi sono immediatamente visibili.
            $("#imgMainNarBig, #imgContainerNarBig .imlayer")
                .stop(true, true)
                .css("opacity", 1);

            // Solo la didascalia entra con dissolvenza, a partire da quando
            // il fondale è diventato visibile.
            if (ct.ntPar !== "") {
                $(".didascNarBig")
                    .stop(true, true)
                    .fadeTo(narRoomCaptionFadeMs, 1);
            }








        }
        else { // è un dial o nar piccolo o nar testuale


            $('.btnBackCutScene').show(); // erano nascosti solo ad end game
            $(".btnStopRemembering ").show(); // erano nascosti solo ad end game
            $(".btnSaveDuringCutscene").show();
            $(".btnOptionsEndTitles").hide(); // serve solo nei titoli

            $("#narDialog").show();
            $("#narBig").hide();

            $(".parDialog").remove();


            window.scrollTo(0, $("#roomOuter").offset().top);



            await appendCutSceneItemDialog(ct, title);

            $("#narDialog").off("click").click(async function (e) {
                await getNextDialogOrNar(false);
            });

        }

        // ACK only after the token has actually been put in the visible DOM.
        if (adminNarrativeMessageId != null) {
            await ackAdminNarrativeMessage(adminNarrativeMessageId);
        }




        // vedi se mostrare back e skip
        if (ct.cstCanBeSkipped) {
            $(".btnStopRemembering").removeClass("disabled");
        }
        else {
            $(".btnStopRemembering").addClass("disabled");
        }


        if (ct.cstCanGoBackToPrevious) {
            $(".btnBackCutScene").removeClass("disabled");
        }
        else {
            $(".btnBackCutScene").addClass("disabled");
        }


        // il primo token della cutscene mi dà anche il necessario per disegnare la room in bg
        //if (ar.room != null) {
        //        rebuildRoom(ar.room , true /* solo room, l'inv dopo*/, false /* inv non ora*/);

        //}

    }

    else if (ar.questions !== null && ar.arEndGame == null) {
        // è un dialogo. mostra le scelte di dialogo



        $("#narDialog").hide();
        $("#narBig").hide();
        $("#dialogChoices").show();
        $(".dialogChoice").remove();
        //$("#roomOuter").hide();


        //$(window).off('click');
        let topicsYouCanAskThisChar = ar.questions;
        topicsYouCanAskThisChar.forEach(function (oti) {

            let divTopic = $('<div class="dialogChoice">').html(oti.questionText);

            divTopic.click(async function (e) {



                let cred = JSON.parse(localStorage[credentialsId]);
                let i = {
                    uname: cred.uname,
                    pwd: cred.pwd,
                    //token: cred.token,

                    questionId: oti.questionId, // di cosa parli
                    lang: getLang()
                    , curTime: getCurTime()
                    , cred_gameId: gGameId
                };
                //console.log("chiamando talk");

                $("#verbMenu").hide();
                mostraPleaseWait();

                let data = await doPostTry(`${prefissoWebApi}/api/talkAction`, i);
                let canContinue = handleErrorsPost(data);
                if (canContinue) {
                    nascondiPleaseWait();

                    //console.log("ok: ", data.ret);



                    let ar = data.ret;

                    await handleAr(ar);



                }



            });
            divTopic.appendTo("#dialogChoices");



        });





    }
    else if (ar.textInputRes !== null && ar.arEndGame == null) { // è un text input


        $(".invBar").hide();

        let ti = ar.textInputRes;

        function updateEnabledSubmit() {
            if (ti.tiCorrectExplanation == null) {
                if ($("#myTextInput").val().replace(/ /g, '') == '') {
                    $("#submitTextInput").addClass('disabled');
                    $("#submitTextInput").removeClass('blink_me_2');
                }
                else {

                    let mustFlash = $("#submitTextInput").hasClass('disabled');
                    $("#submitTextInput").removeClass('disabled');

                    if (mustFlash) {

                        $("#submitTextInput").addClass('blink_me_2');
                    }


                }
            }
            else {
                let checked = $("#dialogInputText .radioExplan:checked");
                if ($("#myTextInput").val().replace(/ /g, '') == '' || checked.length == 0) {
                    $("#submitTextInput").addClass('disabled');
                    $("#submitTextInput").removeClass('blink_me_2');
                }
                else {
                    let mustFlash = $("#submitTextInput").hasClass('disabled');
                    $("#submitTextInput").removeClass('disabled');

                    if (mustFlash) {

                        $("#submitTextInput").addClass('blink_me_2');
                    }
                }
            }

        }

        $("#myTextInput").val('');
        $("#myTextInput2").val('');
        $("#submitTextInput").addClass('disabled');

        $("#myTextInput").off('input').on('input', e => {
            //debugger;
            updateEnabledSubmit();
        });

        $("#dialogInputText .introTextInput.primo").html(ti.tiIntroBeforeTextbox);

        if (ti.tiIntroBeforeSecondTextbox != null) {
            $("#dialogInputText .introTextInput.secondo").html(ti.tiIntroBeforeSecondTextbox);

            $("#dialogInputText .introTextInput.secondo").show();
            $("#myTextInput2").show();
        }
        else {
            $("#dialogInputText .introTextInput.secondo").hide();
            $("#myTextInput2").hide();
        }


        $("#dialogInputText .modal-title").html(ti.tiShortTitle.firstLetterToUpper());

        $("#dialogInputText .containerIntroPars .riepilogoSottotit").remove();


        $(".templateRadioExplanation").remove();

        if (ti.tiCorrectExplanation != null) {


            $(".introExplanations").html(ti.tiPreamboloExplanation);

            for (let ex of ti.tiVisibleExplanations) {
                var newDiv = gTemplateRadioExplanation.clone();
                newDiv.appendTo("#dialogInputText .containerExplanations ");
                newDiv.find('.testoRadioExplan').html(ex.exName);


                newDiv.find('.radioExplan').val(ex.expId);

                newDiv.find('.radioExplan').change(e => {
                    updateEnabledSubmit();
                });
            }


            $(".introExplanations").show();
        }
        else {
            $(".introExplanations").hide();
        }


        // prima di mostrare la dialog di inserimento test, nascondo il dialogo sotto, se no poi riappare pe run attimo
        $("#narDialogInner2").hide();

        $("#dialogInputText").modal("show");
        $("#textInputInner").scrollTop(0);
        itIsTheUserWhoClosedDialogTextInput = null;



        // questo non metterlo! se no lo chiama 2 volte! c'e' già on hidden bs modal
        //$("#cancelTextInput").off("click").on("click", /*async tolto async se no chiama 2 volte */function (e)
        //{
        //        e.preventDefault();

        //        $("#dialogInputText").modal("hide");
        //        itIsTheUserWhoClosedDialogTextInput = true;
        //        /*await tolto se no chiama 2 volte */onCanceledTextInput(ar);
        //});



        $("#dialogInputText").off("hidden.bs.modal").on("hidden.bs.modal", async function (e) { // bm_closing

            e.preventDefault();
            if (itIsTheUserWhoClosedDialogTextInput === null) {
                await onCanceledTextInput(ar);
            }

        });


        $("#submitTextInput").off("click").on("click", async function (e) {
            e.preventDefault();
            e.stopPropagation();

            if ($("#submitTextInput").hasClass("disabled")) {
                return;
            }

            $("#submitTextInput").addClass('disabled');

            itIsTheUserWhoClosedDialogTextInput = true;
            $("#dialogInputText").modal("hide");

            let testoImmesso = $("#dialogInputText #myTextInput").val();

            let testoImmesso2 = $("#dialogInputText #myTextInput2").val();


            let stiExplId;
            let checked = $(" #dialogInputText   .radioExplan:checked");
            if (checked.length > 0) {
                stiExplId = checked.first().val();
            }
            else {
                stiExplId = null;
            }

            let cred = JSON.parse(localStorage[credentialsId]);
            let i = {
                uname: cred.uname,
                pwd: cred.pwd,

                stiSerId: ar.textInputRes.serId,
                stiText: testoImmesso,
                stiText2: testoImmesso2,
                stiExplId: stiExplId
                , lang: getLang()
                , curTime: getCurTime()
                , cred_gameId: gGameId
            };
            //console.log("chiamando submit text input");

            $("#verbMenu").hide();

            mostraPleaseWait();

            let data = await doPostTry(`${prefissoWebApi}/api/submitTextInputAction`, i);
            let canContinue = handleErrorsPost(data);
            if (canContinue) {
                nascondiPleaseWait();

                //console.log("ok: ", data.ret);

                let ar = data.ret;

                await handleAr(ar);

            }


        });


    }
    else if (ar.arEndGame != null) {
        // gioco finito

        g_last_room_desc = ar.room; // se no crasha quando carichi salvataggio a giocofinito

        $(".divLayersContainer .imlayer").remove();

        $("#imgContainerNarBig .imlayer").remove();


        $(".btnOptionsEndTitles").show(); // serve solo nei titoli
        $('.btnBackCutScene').hide();
        $(".btnStopRemembering ").hide();
        $(".btnSaveDuringCutscene").hide();


        $(".endTitesItem").remove();

        for (let str of ar.arEndGame.egsCredits) {
            let newItem = $('<div class="endTitesItem">').appendTo(".endTitlesContainer").html(str);
        }

        {

            $(".didascNarBig").hide(); // se no quando faccio narbig show si vede stretto
            $("#narBig").show(); // devo farlo subito per poi misurare
            $("#narDialog").hide();

            //$(".didascNarBig").html(ct.ntPar);
        }


        $("#narBig").off("click");

        //debugger;

        // metto l'immagine di sfondo
        await changeImgSrcAndWait($("#imgMainNarBig"), prefissoWebApi + "/" + ar.arEndGame.egsImg);  //aspetto perché devo calcolare le dimensioni reali


        // BEGIN settta la larghezza o altezza iniziale dello sfondo, in modo da vedersi tutto nello schermo
        let viewportwt = $("#narBig").width();
        let viewportht = $("#narBig").height();
        //console.log("view port wt = ", viewportwt);
        //console.log("view port ht = ", viewportht);
        let viewpRatio = viewportht / viewportwt;

        let imgGrafica = $("#imgMainNarBig");
        let imgWt = imgGrafica.width();

        imgGrafica.css('opacity', 0);


        //console.log("img grafica wt 1 = ", imgWt);

        let imgHt = imgGrafica.height();
        let imgRatio = imgHt / imgWt;


        if (viewpRatio > imgRatio) {
            //la larg immagine deve eguagliare quella del viewport
            // vwt = mul * imwt
            let mul = viewportwt / imgWt;
            imgGrafica.width(viewportwt);

            imgGrafica.height("");
            //$(".imgContainerNarBig").width(viewportwt);
            //$(".imgContainerNarBig").height("");
            //debugger;

            $(".containerPulsantiBackNar").height("43px");

        }
        else {
            let mul = viewportht / imgHt;

            //debugger;
            let htPulsantiBack = $("#narBig .containerPulsantiBackNar").outerHeight();

            //$("#narOuter").height(viewportht - htPulsantiBack);

            imgGrafica.height(viewportht - htPulsantiBack);
            imgGrafica.width("");




            //$(".imgContainerNarBig").height(viewportht);
            //$(".imgContainerNarBig").width("");
        }

        //if (ct.ntPar != "")
        //{
        //        $(".didascNarBig").show(); // solo ora, se no si vedeva stretto
        //}
        //debugger;



















        // stampo i layer



        $("#imgContainerNarBig .imlayer").remove();



        imgGrafica.animate({ 'opacity': 1.0 }, 2000);




        let htImg = $("#imgMainNarBig").height();
        $(".endTitlesContainer").css("padding-top", htImg * 0.5);
        $(".endTitlesContainer").attr('my_top', 0);


        $(".endTitlesContainer").css('opacity', 0);
        $(".btnOptionsEndTitles").css('opacity', 0);

        $(".endTitesItem").css('font-size', htImg * 0.023);
        $(".endTitesItem").css('min-height', htImg * 0.05);

        await delay(4000);

        $(".endTitlesContainer").animate({ 'opacity': 1.0 }, 1000);

        $(".btnOptionsEndTitles").css('visibility', 'hidden');
        await delay(4000);

        let attesaFatta = 0;
        gStopEndTitles = null;
        while (true) {
            //debugger;
            let attesaStep = 10000 / htImg;
            await delay(attesaStep);

            attesaFatta += attesaStep;



            let curTop = $(".endTitlesContainer").attr('my_top');

            let iCurTop = parseInt(curTop);
            $(".endTitlesContainer").css('top', iCurTop - 1);
            $(".endTitlesContainer").attr('my_top', iCurTop - 1);


            if (attesaFatta > 6000 && $(".btnOptionsEndTitles").css('visibility') == 'hidden') {
                $(".btnOptionsEndTitles").css('visibility', 'visible');

                $(".btnOptionsEndTitles").animate({ 'opacity': 1.0 }, 2000);
                //break;
            }



            if (/*attesaFatta > 3 * 60 * 1000 || */gStopEndTitles == true) // dopo 3 minuti
            {
                $(".endTitesItem").remove(); // cancello le scritte
                gStopEndTitles = null;
                break; // termino il loop infinito
            }
        }

        //console.log("fuori dal loop fine gioco");


    }
    else {
        console.log("caso non gestito");
    }





}

async function ackAdminNarrativeMessage(messageId) {
    try {
        const cred = JSON.parse(localStorage[credentialsId]);
        await doPostTry(`${prefissoWebApi}/api/markAdminNarrativeSeen`, {
            uname: cred.uname, pwd: cred.pwd, lang: getLang(), cred_gameId: gGameId,
            messageIds: [messageId]
        });
    } catch (e) {
        // A failed ACK must not interrupt the narrative; the server will retry delivery.
        console.warn("admin narrative ACK failed", e);
    }
}
async function onCanceledTextInput(ar) {
    let cred = JSON.parse(localStorage[credentialsId]);
    let i = {
        uname: cred.uname,
        pwd: cred.pwd,
        //token: cred.token,

        ctiSerId: ar.textInputRes.serId,
        lang: getLang()
        , curTime: getCurTime()
        , cred_gameId: gGameId
    };
    //console.log("chiamando cancel text input");

    $("#verbMenu").hide();
    mostraPleaseWait();

    let data = await doPostTry(`${prefissoWebApi}/api/cancelTextInputAction`, i);
    let canContinue = handleErrorsPost(data);
    if (canContinue) {
        nascondiPleaseWait();

        //console.log("ok: ", data.ret);



        let ar = data.ret;

        await handleAr(ar);



    }


}

//function mostraPleaseWait()
//{
//        $("#verbMenu").hide();
//        $("#waitingServer").show();
//        $("#pleaseWait").removeClass("opacity-grow").addClass("opacity-grow");

//}

$(function () {

    $("#clientDiagnosticsReload").click(() => window.location.reload());
    $("#clientDiagnosticsCopy").click(async () => {
        const text = $("#clientDiagnosticsText").text();
        try {
            if (navigator.clipboard && navigator.clipboard.writeText) {
                await navigator.clipboard.writeText(text);
            }
            else {
                const temporary = document.createElement("textarea");
                temporary.value = text;
                temporary.style.position = "fixed";
                temporary.style.opacity = "0";
                document.body.appendChild(temporary);
                temporary.focus();
                temporary.select();
                if (!document.execCommand("copy")) {
                    throw new Error("Il browser non supporta la copia automatica.");
                }
                temporary.remove();
            }
            $("#clientDiagnosticsCopy").text("Copiata");
        }
        catch (err) {
            showClientDiagnostic("Diagnostica", text + "\n\nCopia automatica non disponibile: " + String(err));
        }
    });

    const $inventoryContainer = $('.invContenitoreOggettiInner');
    const $scrollLeftButton = $('.scrollLeft');
    const $scrollRightButton = $('.scrollRight');
    const itemSelector = '.invObjTemplate.scrollingItem';
    const scrollItemsCount = 2;
    const animationDuration = 200; // Definiamo la durata dell'animazione per consistenza

    let isInventoryAnimating = false; // <<<< NUOVA VARIABILE DI STATO (FLAG)

    function getItemWidth() {
        const $firstItem = $inventoryContainer.find(itemSelector).first();
        return $firstItem.length ? $firstItem.outerWidth(true) : 0;
    }

    function updateArrowStates() {
        // ... (nessuna modifica qui, la funzione rimane la stessa)
        if (!$inventoryContainer.length || !$scrollLeftButton.length || !$scrollRightButton.length) {
            return;
        }
        const containerWidth = $inventoryContainer.width();
        let totalItemsWidth = 0;
        $inventoryContainer.find(itemSelector).each(function () {
            totalItemsWidth += $(this).outerWidth(true);
        });
        const currentScrollLeft = Math.round($inventoryContainer.scrollLeft());
        const maxScrollLeft = Math.max(0, totalItemsWidth - containerWidth);

        if (totalItemsWidth <= containerWidth || currentScrollLeft <= 0) {
            $scrollLeftButton.css('opacity', 0.5).addClass('scroll-disabled');
        } else {
            $scrollLeftButton.css('opacity', 1).removeClass('scroll-disabled');
        }

        if (totalItemsWidth <= containerWidth || currentScrollLeft >= (maxScrollLeft - 1)) {
            $scrollRightButton.css('opacity', 0.5).addClass('scroll-disabled');
        } else {
            $scrollRightButton.css('opacity', 1).removeClass('scroll-disabled');
        }
    }

    // Helper function to perform the scroll and manage the animation flag
    function doInventoryScroll(targetScroll) {
        if (isInventoryAnimating && $inventoryContainer.scrollLeft() === targetScroll) {
            // If already animating towards the same target, or if target is current, do nothing.
            // This handles cases where scrollInventory might be called multiple times for the same target.
            return;
        }

        // Se lo scroll attuale è già quello desiderato, non animare
        if ($inventoryContainer.scrollLeft() === targetScroll) {
            isInventoryAnimating = false; // Assicurati che il flag sia resettato
            updateArrowStates();
            return;
        }

        isInventoryAnimating = true; // <<<< IMPOSTA IL FLAG ALL'INIZIO DELL'OPERAZIONE DI SCROLL
        $inventoryContainer.stop(true, true).animate({ scrollLeft: targetScroll }, animationDuration, function () {
            isInventoryAnimating = false; // <<<< RESETTA IL FLAG AL COMPLETAMENTO DELL'ANIMAZIONE
            updateArrowStates();
        });
    }


    $scrollLeftButton.on('click', function () {
        if ($(this).hasClass('scroll-disabled')) { // Anche se non dovrebbe essere cliccabile, meglio controllare
            return;
        }
        // Non controlliamo isInventoryAnimating qui per i click sui pulsanti,
        // vogliamo che il click interrompa e inizi un nuovo scroll.
        // stop(true,true) nell'animate si occuperà di questo.

        const itemWidth = getItemWidth();
        if (itemWidth > 0) {
            const currentScroll = $inventoryContainer.scrollLeft();
            const newScroll = Math.max(0, currentScroll - (itemWidth * scrollItemsCount));
            doInventoryScroll(newScroll);
        }
    });

    $scrollRightButton.on('click', function () {
        if ($(this).hasClass('scroll-disabled')) {
            return;
        }
        const itemWidth = getItemWidth();
        if (itemWidth > 0) {
            const containerWidth = $inventoryContainer.width();
            let totalItemsWidth = 0;
            $inventoryContainer.find(itemSelector).each(function () {
                totalItemsWidth += $(this).outerWidth(true);
            });
            const maxScroll = Math.max(0, totalItemsWidth - containerWidth);
            const currentScroll = $inventoryContainer.scrollLeft();
            const newScroll = Math.min(maxScroll, currentScroll + (itemWidth * scrollItemsCount));
            doInventoryScroll(newScroll);
        }
    });

    if ($inventoryContainer.length) {
        $inventoryContainer.on('wheel', function (event) {
            if (isInventoryAnimating) { // <<<< CONTROLLA IL FLAG QUI
                event.preventDefault(); // Preveniamo lo scroll della pagina anche se ignoriamo l'azione sull'inventario
                return; // Ignora se un'animazione è già in corso
            }

            const e = event.originalEvent;
            const itemWidth = getItemWidth();
            if (itemWidth <= 0) return; // Non c'è niente da scrollare o item non validi

            // Non è necessario ricontrollare canScrollLeft/Right qui se le frecce
            // sono già aggiornate correttamente, perché lo scroll non avverrà
            // se si tenta di andare oltre i limiti (Math.max/min)

            if (e.deltaY < 0) { // Scroll della rotellina verso l'alto (sinistra)
                if (!$scrollLeftButton.hasClass('scroll-disabled')) { // Verifica se è possibile scrollare a sinistra
                    event.preventDefault();
                    const currentScroll = $inventoryContainer.scrollLeft();
                    const newScroll = Math.max(0, currentScroll - (itemWidth * scrollItemsCount));
                    doInventoryScroll(newScroll);
                }
            } else if (e.deltaY > 0) { // Scroll della rotellina verso il basso (destra)
                if (!$scrollRightButton.hasClass('scroll-disabled')) { // Verifica se è possibile scrollare a destra
                    event.preventDefault();
                    const containerWidth = $inventoryContainer.width();
                    let totalItemsWidth = 0;
                    $inventoryContainer.find(itemSelector).each(function () {
                        totalItemsWidth += $(this).outerWidth(true);
                    });
                    const maxScroll = Math.max(0, totalItemsWidth - containerWidth);
                    const currentScroll = $inventoryContainer.scrollLeft();
                    const newScroll = Math.min(maxScroll, currentScroll + (itemWidth * scrollItemsCount));
                    doInventoryScroll(newScroll);
                }
            }
        });
    }

    setTimeout(updateArrowStates, 200);
    window.refreshInventoryScrollButtons = updateArrowStates;

    let resizeTimer;
    $(window).on('resize', function () {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(function () {
            // Se sta animando, potremmo voler aspettare o interrompere.
            // Per semplicità, per ora aggiorniamo sempre.
            // Se il resize causa problemi con un'animazione in corso,
            // potremmo fermare l'animazione: $inventoryContainer.stop(true, true);
            updateArrowStates();
        }, 250);
    });

    if (window.MutationObserver && $inventoryContainer.length) {
        const observer = new MutationObserver(function (mutationsList, observer) {
            // Anche qui, considerazioni simili se l'observer scatta durante un'animazione.
            updateArrowStates();
        });
        observer.observe($inventoryContainer.get(0), { childList: true, subtree: true });
    }

    //fisso i template
    templateOggettoPicker = $(".templateOggettoPicker").first().clone();
    templateOggettoPickerNonClic = $(".templateOggettoPickerNonClic").first().clone();
    templateInvObject = $(".invObjTemplate").first().clone();
    gTemplateDialogChoice = $(".dialogChoiceTemplate").first().clone();
    templateObjectComposerNew = $(".templateObjectComposerNew").first().clone();

    $(".templateOggettoPicker").remove();
    $(".templateOggettoPickerNonClic").remove();
    $(".dialogChoiceTemplate").remove();


    gTemplateImgTarget = $(".imgTarget").first().clone();
    $(".imgTarget").remove();

    gTemplateImgTargetExit = $(".imgTargetExit").first().clone();
    $(".imgTargetExit").remove();

    gTemplateImgTargetExitDown = $(".imgTargetExitDown").first().clone();
    $(".imgTargetExitDown").remove();


    gTemplateRadioExplanation = $(".templateRadioExplanation").first().clone();
    $(".templateRadioExplanation").remove();

    //-----------








    $("#narDialog").hide();
    $("#narBig").hide();
    $("#dialogChoices").hide();
    $(".clickSecondObj").hide();

    $("#waitingServer").addClass('nascosto');

    $("#divRegister").hide();
    $("#divLogin").hide();


    let viewer = new TouchScroll();
    viewer.init({
        id: "mapOuter",
        draggable: true,
        wait: false
    });


    $("#btnChiudiObiettivi").click(function () {
        //console.log("jidfkfg");

        //gLo1Chosen = null;
        //if (btnPushed) {

        //    btnPushed.removeClass("active");
        //    btnPushed = null;
        //}

        //updateActionBar();
        $("#divObjectives").modal("hide");


    });


    $("#divObjectives").on("hidden.bs.modal", function () {

        deselectAll();


    });

    $("#textModeRoomTargetsModal").on("hidden.bs.modal", function () {
        // Closing with X, Chiudi or the backdrop cancels the pending
        // selection. A target click sets the flag because the selected first
        // object must survive until onLoClickedRoom executes.
        if (!gTextModeRoomTargetChosen) {
            globalThis.deselectAll();
        }
        gTextModeRoomTargetChosen = false;
    });

    $(".textModeLeftBar").off("click.textModeDeselect").on("click.textModeDeselect", function (e) {
        // Keep the old click-in-empty-room-area cancellation behavior.
        if ($(e.target).closest(".textModeChildButton").length > 0) {
            return;
        }
        globalThis.deselectAll();
    });

    function updateResponsiveClasses() {
        if (window.innerHeight > window.innerWidth * 1.2) {
            $("body").addClass("viewNarrow").removeClass("viewWide");
            gIsNarrowScreen = true;
        }
        else {
            $("body").removeClass("viewNarrow").addClass("viewWide");
            gIsNarrowScreen = false;
        }

        if (window.innerHeight > window.innerWidth * 1.6) // serve solo per le dialog...
        {
            $("body").addClass("viewVeryNarrow");
        }
        else {
            $("body").removeClass("viewVeryNarrow");
        }

        if (window.innerHeight < 514) { // per il cellulare quando c'è il text input che prende mezzo schermo
            $("body").addClass("narrowVert");
        }
        else {
            $("body").removeClass("narrowVert");
        }
    }




    //$("#imgContainer").height($(window).height() * 0.15);
    let graphicsResizeTimer;
    $(window).resize(function () {
        updateResponsiveClasses();
        logRoomLayout("resize-event");
        clearTimeout(graphicsResizeTimer);
        graphicsResizeTimer = setTimeout(async function () {
            logRoomLayout("resize-rebuild-start");
            await rebuildGraphics(false);

            $(".scrollanteConPulsanti").each((i, el0) => {
                aggiornaPulsantiScroll(el0);
            });
        }, 100);
    });




    //$("#registerMoreInfo").click(function ()
    //{
    //        BootstrapDialog.show({
    //                title: "Informazione",
    //                message: "Il tuo segnalibro viene già salvato automaticamente da questa app. Però, se tu dovessi svuotare la cache del tuo browser, il segnalibro andrebbe perso e dovresti ricominciare la lettura da capo. Per evitare ciò, basta registrarsi con un nome utente e una password. A quel punto, non perderai mai la posizione di lettura. Un altro vantaggio è che potrai continuare la lettura su un dispositivo diverso."
    //        });
    //});




    $("#btnWait").click(async function (e) {

        e.stopPropagation(); // altrimenti sente window.click e chiama next()

        let cred = JSON.parse(localStorage[credentialsId]);


        mostraPleaseWait();

        let data = await doPostTry(`${prefissoWebApi}/api/waitOneTurn`, cred);
        let canContinue = handleErrorsPost(data);
        if (canContinue) {
            nascondiPleaseWait();

            //console.log("ok: ", data.ret);

            let ar = data.ret;

            await handleAr(ar);






        }


    });



    async function loadSavedGame() {

        let cred = JSON.parse(localStorage[credentialsId]);
        let i = {
            uname: cred.uname,
            pwd: cred.pwd,
            //token: cred.token,

            lang: getLang()
            , curTime: getCurTime()
            , cred_gameId: gGameId

        };

        //console.log("chiamando load");

        mostraPleaseWait();

        let data = await doPostTry(`${prefissoWebApi}/api/loadGame`, i, { useSession: false });
        ////let err1;
        //try
        //{
        //        data = await doPost(`${prefissoWebApi}/api/loadGame`, i);
        //        //err1 = null;
        //}
        //catch (err)
        //{
        //        data = { errore : "conn-error" };
        //        //debugger;
        //}
        let canContinue = handleErrorsPost(data);
        if (canContinue) {
            nascondiPleaseWait();

            //console.log("ok: ", data.ret);

            let ar = data.ret;

            await handleAr(ar, true /* forzo rebuild room*/);


        }


    }

    //$("#btnLoadGame").click(function (e) {

    //    e.stopPropagation(); // altrimenti sente window.click e chiama next()
    //    loadSavedGame();

    //});


    $("#btnSaveBookmarkBack").click(function () {
        $("#divRegister").hide();
    });


    $("#btnRegister").click(async function () {

        let email = $("#saveBookmarkEmail").val();
        let pwd1 = $("#saveBookmarkPwd").val();
        let pwd2 = $("#saveBookmarkPwd2").val();

        let inp = {
            lang: getLang()
            , curTime: getCurTime()
            , uname: email
            , pwd1: pwd1
            , pwd2: pwd2
            , gameId: gGameId
            //pwd1: cred.pwd,
            //token: cred.token
        };

        let data = await doPostTry(`${prefissoWebApi}/api/createUserAndStartGame`, inp, { useSession: false });
        if (data.errore === "username-already-taken") {
            BootstrapDialog.show({
                title: "error".tr(),
                message: "questoNomeUt".tr()
            });
            nascondiPleaseWait();
        }
        else if (data.errore === "invalid-credentials-null") {
            BootstrapDialog.show({
                title: "error".tr(),
                message: "youHaventSpec".tr()
            });
            nascondiPleaseWait();
        }
        else if (data.errore === "passwords-not-equal") {
            BootstrapDialog.show({
                title: "error".tr(),
                message: "passwordsNotEq".tr()
            });
            nascondiPleaseWait();
        }

        else {

            let canContinue = handleErrorsPost(data);
            if (canContinue) {

                {
                    nascondiPleaseWait();


                    //console.log("ok: ", data.ret);

                    // per ora togli se no copre il titolo
                    //BootstrapDialog.show({
                    //        title: "registered".tr(),
                    //        message: "seiReg".tr()
                    //});



                    $("#divRegister").modal("hide");

                    let newCred = {
                        uname: email,
                        pwd: pwd1

                    };

                    localStorage[credentialsId] = JSON.stringify(newCred);

                    $("#divRegister").hide();
                    $("#btnRegisterOuter").hide();


                    //console.log("ok: ", data.ret);

                    //debugger;






                    let ar = data.ret.res;

                    await chooseGameModeThenRun(ar);




                }

            }

        }
    });



    $("#btnLogin0").click(function (e) {

        e.stopPropagation(); // altrimenti sente window.click e chiama next()

        $("#options").modal("hide");

        $("#divLogin").modal("show");










    });


    $("#btnLogin").click(async function (e) {

        let uname = $("#loginUsername").val();
        let pwd = $("#loginPwd").val();
        let cred = {
            uname: uname,
            pwd: pwd,
            lang: getLang()
            , curTime: getCurTime()
            , cred_gameId: gGameId
        };

        mostraPleaseWait();

        let data = await doPostTry(`${prefissoWebApi}/api/loadGame`, cred, { useSession: false });
        let canContinue = handleErrorsPost(data);
        if (canContinue) {


            {
                nascondiPleaseWait();

                //console.log("ok: ", data.ret);


                BootstrapDialog.show({
                    title: "loggedIn".tr(),
                    message: "yourGameProgress".tr()
                });


                $("#divLogin").modal("hide");

                let newCred = {
                    uname: uname,
                    pwd: pwd,
                    token: null
                };

                localStorage[credentialsId] = JSON.stringify(newCred);


                let ar = data.ret;

                await handleAr(ar);


            }


        }


    });



    //$("#btnRegister0").click(function (e)
    //{

    //        e.stopPropagation(); // altrimenti sente window.click e chiama next()

    //        $("#options").modal("hide");
    //        $("#divRegister").modal("show");







    //});


    async function createUserAndStartGame() {

        //if (g_last_room_desc == null)
        //{
        //        debugger;
        //}
        //console.log("chiamando create user start");



        if (localStorage[credentialsId]) // se ho gia credenziali nei cookie, start new game senza creare utente
        {
            callStartNewGame();

        }
        else {

            // chiedi con dialog di registrarsi (se vuole può anche scegliere login col pulsante sotto)
            //debugger;
            $(".invBar").hide();
            $(".roomTextMode").hide(); // si vede sotto al login
            $("#options").modal("hide");
            $("#divRegister").modal("show");
            $("#btnBackRegister").hide();
            //$("#btnBackLogin").hide(); // quando era login era così


        }




    }

    $("#btnRegisterInstead").click(async function (e) {

        e.stopPropagation(); // altrimenti sente window.click e chiama next()
        $("#options").modal("hide");

        $("#divLogin").modal("hide");
        $("#divRegister").modal("show");

        $("#btnBackRegister").hide();
    });


    $("#btnLoginInstead").click(async function (e) {

        e.stopPropagation(); // altrimenti sente window.click e chiama next()
        $("#options").modal("hide");

        $("#divRegister").modal("hide");
        $("#divLogin").modal("show");


        $("#btnBackLogin").hide();
    });

    $("#btnNewGame").click(async function (e) {

        e.stopPropagation(); // altrimenti sente window.click e chiama next()
        $("#options").modal("hide");

        await createUserAndStartGame();

    });


    $("#btnLogOut0").click(async function (e) {
        //debugger;
        e.stopPropagation(); // altrimenti sente window.click e chiama next()

        try {
            if (sessionStorage.getItem(sessionTokenId)) await doPost(`${prefissoWebApi}/api/logout`, {});
        } catch (err) {
            // Logout locale deve funzionare anche se il server è già irraggiungibile.
        }
        localStorage.removeItem(credentialsId);
        sessionStorage.removeItem(sessionTokenId);
        location.reload(true);
    });


    function updateSaveGameOverwriteNotice() {
        const enteredName = String($("#txtSaveGameName").val() || "").trim();
        const existingNames = g_last_room_desc?.grrSaveNames || [];
        const exists = enteredName !== "" && existingNames.some(name => name === enteredName);
        const notice = $("#saveGameOverwriteNotice");

        if (exists) {
            notice.text("savegameAlreadyExists".tr()).show();
        }
        else {
            notice.text("").hide();
        }
    }

    function renderExistingSaveGames() {
        const list = $("#saveGamesExistingList");
        list.empty();

        const existingNames = g_last_room_desc?.grrSaveNames || [];
        for (const saveName of existingNames) {
            $("<button type='button' class='btn btn-default form-control saveGameExistingButton'>")
                .text(saveName)
                .appendTo(list)
                .on("click", function (e) {
                    e.preventDefault();
                    $("#txtSaveGameName").val(saveName).trigger("input").focus();
                });
        }

        if (existingNames.length === 0) {
            $("<div class='saveGamesExistingEmpty'>")
                .text("nonHaiSalv".tr())
                .appendTo(list);
        }
    }

    $("#txtSaveGameName").on("input", updateSaveGameOverwriteNotice);

    $(".btnSalvaPartita0, #btnSaveGame0").click(function (e) {
        $("#options").modal('hide');
        $("#txtSaveGameName").val("");
        $("#saveGameOverwriteNotice").hide().text("");
        renderExistingSaveGames();
        $("#divSave").modal("show");
        e.stopPropagation();
    });


    $(".btn_named_cut_scenes").click(function (e) {
        e.stopPropagation();
        $("#div_scene_passate_dialog").modal("show");

        $(".btn_indizio").remove();
        for (let ncs of g_last_room_desc.grr_named_cut_scenes) {



            let btn = $("<div class='btn btn-default btn_indizio form-control btnOpt scrollingItem'>").appendTo("#scene_passate_inner");
            btn.text(ncs.ncsc_title_translated);
            btn.click(async function (e) {
                e.preventDefault();

                $("#div_scene_passate_dialog").modal("hide");
                mostraPleaseWait();
                let cred = JSON.parse(localStorage[credentialsId]);
                let i = {
                    uname: cred.uname,
                    pwd: cred.pwd,
                    token: cred.token,
                    cut_scene_title: ncs.ncsc_ser_id,
                    lang: getLang()
                    , curTime: getCurTime()
                    , cred_gameId: gGameId
                };

                let data = await doPostTry(`${prefissoWebApi}/api/replay_cut_scene`, i);


                let canContinue = handleErrorsPost(data);
                if (canContinue) {
                    nascondiPleaseWait();

                    //console.log("ok: ", data.ret);
                    let ar = data.ret;

                    await handleAr(ar);
                }

            });
        }


        let cosaScrollare = $("#div_scene_passate_dialog  .scrollanteConPulsanti");
        let cosaScroll0 = cosaScrollare[0];
        aggiornaPulsantiScroll(cosaScrollare);

    });

    $("#btnRiepilogo").click(function (e) {

        $("#riepilogoInner .divRiepilogoRoom").remove();
        $("#riepilogoInner .divRiepilogoObj").remove();

        g_last_room_desc.grrRoomCoords.forEach(rc => {
            let divRiepilogoRoom = $("<div>").addClass("divRiepilogoRoom");

            divRiepilogoRoom.attr("roomId", rc.rcRoomId);






            let devoFarVedereComeAccessibileQuestoPulsante = rc.rcAlreadyVisitedOnce;


            if (!devoFarVedereComeAccessibileQuestoPulsante) {
                //divRiepilogoRoom.addClass("unvisited");
                //divRiepilogoRoom.text("?");


            }
            else {
                divRiepilogoRoom.text(rc.rcRoomName.firstLetterToUpper());

                divRiepilogoRoom.appendTo("#riepilogoInner");



                let ofcInCurRoom = g_last_room_desc.grrRooms[rc.rcRoomId].rfc_objects;

                if (ofcInCurRoom.length === 0) {
                    let divRiepilogoObj = $("<div>").addClass("divRiepilogoObj").addClass("nonSelezionabile");
                    divRiepilogoObj.text(g_last_room_desc.grr_nothing_special);
                    divRiepilogoObj.appendTo("#riepilogoInner");
                }
                else {

                    ofcInCurRoom.forEach(function (kel) {

                        let divRiepilogoObj = $("<div>").addClass("divRiepilogoObj");
                        divRiepilogoObj.text(kel.ofc_name.firstLetterToUpper());

                        //if (!(kel.ofcUseMode === 0 || kel.ofcUseMode === 1) /* se non è selezionabile come primo oggetto */) {
                        //    divRiepilogoObj.addClass("nonSelezionabile");
                        //}
                        divRiepilogoObj.appendTo("#riepilogoInner");


                    });
                }




            }







        });

        $("#divRiepilogo").modal("show");
        e.stopPropagation();



    });



    $(".btnCaricaPartita0, #btnLoadGame0").click(function (e) {
        $("#options").modal('hide');

        $(".btnLoadGame").remove();
        $(".btnLoadGameFake").remove();
        for (let savename of g_last_room_desc.grrSaveNames) {


            let newBut = $("<button class='btn btn-default form-control btnLoadGame scrollingItem'>").appendTo("#modalBodyLoad");
            let encoded = _.escape(savename);
            newBut.attr("save_name", encoded);
            newBut.text(savename);

            newBut.click(async e => {
                e.stopPropagation();
                let conf = confirm("restoreSave".tr());
                if (conf) {


                    let loadname = $("#txtLoadGameName").val();

                    // chiamo web api save

                    let cred = JSON.parse(localStorage[credentialsId]);
                    let i = {
                        uname: cred.uname,
                        pwd: cred.pwd,
                        token: cred.token,
                        savegameName: savename,
                        lang: getLang()
                        , curTime: getCurTime()
                        , cred_gameId: gGameId
                    };

                    //console.log("facendo post: ", i);



                    mostraPleaseWait();
                    let data = await doPostTry(`${prefissoWebApi}/api/loadGameWithName`, i);

                    let canContinue = handleErrorsPost(data);
                    if (canContinue) {
                        if (data.errore === "save_game_not_found") {

                            BootstrapDialog.show({
                                title: "error".tr(),
                                message: "savegamNotFou".tr()
                            });
                        }
                        else if (data.errore) {
                            console.log("errore", data.errore);
                        }
                        else {
                            nascondiPleaseWait();

                            //console.log("ok: ", data.ret);


                            // chiudo dialog
                            $("#divLoadGame").modal("hide");

                            let ar = data.ret;

                            // Il salvataggio può avere una grafica diversa pur
                            // trovandosi nella stessa room: forza quindi il
                            // ridisegno del fondale e dei layer.
                            await handleAr(ar, true /* forzo rebuild room dopo loadGameWithName */);


                        }
                    }
                }
            });


        }


        if (g_last_room_desc.grrSaveNames.length === 0) {
            $("<div class='btnLoadGameFake scrollingItem'>").text("nonHaiSalv".tr()).appendTo("#modalBodyLoad");
        }


        $("#divLoadGame").modal("show");

        aggiornaPulsantiScroll("#modalBodyLoad.invContentOuter");
        e.stopPropagation();
    });

    $("#btnSalvaConNome").click(async function (e) {

        e.stopPropagation();

        let savename = String($("#txtSaveGameName").val() || "").trim();

        const existingNames = g_last_room_desc?.grrSaveNames || [];
        if (existingNames.some(name => name === savename)) {
            const overwriteConfirmed = confirm("overwriteSave".tr().replace("{1}", savename));
            if (!overwriteConfirmed) {
                return;
            }
        }

        // chiamo web api save

        let cred = JSON.parse(localStorage[credentialsId]);
        let i = {
            uname: cred.uname,
            pwd: cred.pwd,
            token: cred.token,
            savegameName: savename,
            lang: getLang()
            , curTime: getCurTime()
            , cred_gameId: gGameId
        };



        mostraPleaseWait();

        let data = await doPostTry(`${prefissoWebApi}/api/saveGameWithName`, i);

        let canContinue = handleErrorsPost(data);
        if (canContinue) {
            nascondiPleaseWait();

            //console.log("ok: ", data.ret);

            //debugger;
            g_last_room_desc.grrSaveNames = data.ret.newsavenames;
            renderExistingSaveGames();
            updateSaveGameOverwriteNotice();

            // non è avanzato lo stato, quindi non devo fare niente.

            // chiudo dialog
            $("#divSave").modal("hide");

            //let ar = data.ret;

            //handleAr(ar);


        }

    });



    //$("#btnCarica").click(async function (e) {

    //    e.stopPropagation();

    //    let loadname = $("#txtLoadGameName").val();

    //    // chiamo web api save

    //    let cred = JSON.parse(localStorage[credentialsId]);
    //    let i = {
    //        uname: cred.uname,
    //        pwd: cred.pwd,
    //        token: cred.token,
    //        savegameName: loadname,
    //        lang: getLang()

    //    };

    //    console.log("facendo post: ", i);



    //    mostraPleaseWait();
    //    let data = await doPost(`${prefissoWebApi}/api/loadGameWithName`, i);


    //    if (data.errore === "save_game_not_found") {

    //        BootstrapDialog.show({
    //            title: "Errore",
    //            message: "Il salvataggio non è stato trovato."
    //        });
    //    }
    //    else if (data.errore) {
    //        console.log("errore", data.errore);
    //    }
    //    else {
    //        $("#waitingServer").hide();

    //        console.log("ok: ", data.ret);


    //        // chiudo dialog
    //        $("#divLoadGame").modal("hide");

    //        let ar = data.ret;

    //        await handleAr(ar);


    //    }

    //});


    $(".btnBackCutScene").click(async function (e) {

        e.stopPropagation();
        //console.log("cliccato back");

        if (!$(".btnBackCutScene").hasClass("disabled")) {
            await getPreviousCutSceneElement();
        }
    });

    $(".btnStopRemembering ").click(async function (e) {

        e.stopPropagation();
        //console.log("cliccato skip");

        if (!$(".btnStopRemembering").hasClass("disabled")) {
            await skipToEndOfCutScene();
        }
    });

    $(".btnSaveDuringCutscene").click(function (e) {
        e.preventDefault();
        e.stopPropagation();
        $("#txtSaveGameName").val("");
        $("#saveGameOverwriteNotice").hide().text("");
        renderExistingSaveGames();
        $("#divSave").modal("show");
    });

    function mostraDialogOpzioni(e) {
        $("#btnPlayTutorial").text(g_tutorialMode ? "Esci dal tutorial" : "Gioca tutorial");
        $("#options").modal("show");

        if (localStorage[credentialsId]) {
            let cred = JSON.parse(localStorage[credentialsId]);
            if (cred.uname !== "") {
                $("#btnRegisterOuter").hide();
                $("#btnLoginOuter").hide();
                $("#btnLogoutOuter").show();
                //$("#btnNewGame").html("logOut".tr());
                //$("#newGameSubtitle").html("toLoginAsAno".tr());
            }
            else {
                $("#btnRegisterOuter").show();
                $("#btnLoginOuter").show();
                $("#btnLogoutOuter").hide();
                //$("#btnNewGame").html("restart".tr());
                //$("#newGameSubtitle").html("toStartOver".tr());
            }

        }
        else {
            $("#btnRegisterOuter").hide();
            $("#btnLoginOuter").hide();

            $("#btnLogoutOuter").show();

            //$("#btnNewGame").html("logOut".tr());
            //$("#newGameSubtitle").html("toLoginAsAno".tr());
        }



        e.stopPropagation(); // altrimenti sente window.click e chiama next()
    }

    $(".btnOptMio , .optionsIcon, .btnTextOpzioni").click(function (e) {
        mostraDialogOpzioni(e);

    });

    $("#btnGameMode").click(async function (e) {
        e.preventDefault();
        e.stopPropagation();
        const currentlyCasual = g_last_room_desc?.grrCasualMode === true;
        BootstrapDialog.show({
            title: "Modalità di gioco",
            message: currentlyCasual ? "Stai giocando in modalità Casual." : "Stai giocando in modalità Normale.",
            buttons: [
                { label: "Normale", action: async dlg => {
                    dlg.close();
                    const data = await setGameModeOnServer(false);
                    if (handleErrorsPost(data)) await handleAr(data.ret);
                }},
                { label: "Casual", action: async dlg => {
                    dlg.close();
                    const data = await setGameModeOnServer(true);
                    if (handleErrorsPost(data)) await handleAr(data.ret);
                }}
            ]
        });
    });

    $("#btnPlayTutorial").click(async function (e) {
        e.preventDefault();
        e.stopPropagation();
        $("#options").modal("hide");
        if (g_tutorialMode) {
            setTutorialMode(false);
            await loadSavedGame();
        }
        else {
            const casual = g_last_room_desc?.grrCasualMode === true;
            await callStartNewGame(true, casual);
        }
    });



    //$("#room").mousedown(function (e)
    //{
    //        if (gFrozenMouse)
    //        {
    //                e.preventDefault();
    //                e.stopPropagation();
    //                return;
    //        }

    //        e.preventDefault();
    //        deselectAll();


    //});



    // non posso, altrimenti scatta prima questo quando clicco un obiettivo
    //$(".invNew").click(function (e) {
    //    e.preventDefault();
    //    deselectAll();
    //});



    $(".btnMap ").mousedown(async function (e) {
        if (gFrozenMouse || $(e.currentTarget).hasClass("disabled") || $(e.currentTarget).hasClass("tempDisabled")) {
            e.preventDefault();
            e.stopPropagation();
            return;
        }

        e.preventDefault();



        await mostraMappa();

    });


    $(".btnTextWalk").click(async function (e) {
        if (gFrozenMouse || $(e.currentTarget).hasClass("disabled") || $(e.currentTarget).hasClass("tempDisabled")) {
            e.preventDefault();
            e.stopPropagation();
            return;
        }

        e.preventDefault();



        await mostraMappa();

    });


    //$("#btnExitMap").click(function (e) {
    //    e.preventDefault();

    //    $("#mapOuter").hide();


    //    // deseleziono tutto perche potrei essere inmodalità picker
    //    deselectAll();

    //});

    $("#indietroDiUno").click( /* importante non async, se no si chiude la dialog perche non scatta preventdefault*/ e => {
        e.preventDefault();
        e.stopPropagation();
        if (gFraseCompostaFinora.length !== 0) {

            gFraseCompostaFinora.pop();

            //debugger;
            svuotaScelteDaFare();



            // elimina ultima scelta fatta
            var todel = $(".bloccoObiettivo .parSentence.nonObiettivo:last");
            todel.remove();

            if (gFraseCompostaFinora.length === 0) {
                $("#indietroDiUno").addClass("disabled");
            }

            prossimoToken(gObjectiveChosen);


        }
    });

    $("#btnExitMapPicker").click(function (e) {
        e.preventDefault();

        $("#mapOuterOuter").hide();


        // deseleziono tutto perche potrei essere inmodalità picker

        deselectAll();

    });


    //$(".btnApriInv").click(function (e)
    //{
    //        e.preventDefault();
    //        $(".bloccoDaScegliereInv *").remove();




    //        function aggiungiClickHandlerInv(ofc, newBtn)
    //        {

    //                let vfcRicorda = g_last_room_desc.grrVerbs.filter(v => v.vfc_is_remember)[0];

    //                newBtn.click(e =>
    //                {
    //                        $("#invModal").modal('hide');
    //                        sceltoUnVerboEUnOggetto(vfcRicorda, ofc, g_last_room_desc);
    //                });
    //        }


    //        function creaPulsantePerWizardInv(ofc, usaInContainerTraParentesi = false)
    //        {

    //                //let btn;
    //                //if (ofc.loId == "mom") {
    //                //    debugger;
    //                //}

    //                if (!ofc.ofcMustBeShownInTextRoomRecap)
    //                {
    //                        return;
    //                }


    //                if (ofc.ofc_can_be_remembered)
    //                {
    //                        let btn = templateOggettoPicker.clone();


    //                        if (usaInContainerTraParentesi)
    //                        {
    //                                btn.find(".testoPickerInner").text(ofc.ofc_name_with_in.firstLetterToUpper());
    //                        }
    //                        else
    //                        {
    //                                btn.find(".testoPickerInner").text(ofc.ofc_name.firstLetterToUpper());
    //                        }


    //                        // non si usa piu
    //                        //btn.find(".imgOggettoPicker").attr("src", ofc.ofcimagePortrait);

    //                        btn.appendTo(".bloccoDaScegliereInv");

    //                        let vfcRicorda = g_last_room_desc.grrVerbs.filter(v => v.vfc_is_remember)[0];

    //                        btn.click(e =>
    //                        {
    //                                $("#invModal").modal('hide');
    //                                sceltoUnVerboEUnOggetto(vfcRicorda, ofc, g_last_room_desc);
    //                        });

    //                }
    //                else
    //                {
    //                        let btn = templateOggettoPickerNonClic.clone();

    //                        if (usaInContainerTraParentesi)
    //                        {
    //                                btn.find(".testoPickerInner").text(ofc.ofc_name_with_in.firstLetterToUpper());
    //                        }
    //                        else
    //                        {
    //                                btn.find(".testoPickerInner").text(ofc.ofc_name.firstLetterToUpper());
    //                        }

    //                        //non si usa piu
    //                        //btn.find(".imgOggettoPicker").attr("src", ofc.ofcimagePortrait);

    //                        btn.appendTo(".bloccoDaScegliereInv");

    //                }
    //                //return btn;
    //        }


    //        $("<div class='titoloInv scrollingItem'>").text("objectsInThisPlace".tr()).appendTo($(".bloccoDaScegliereInv"));

    //        for (ofc of g_last_room_desc.grrRooms[g_last_room_desc.grrCurRoomId].rfc_objects)
    //        {

    //                creaPulsantePerWizardInv(ofc);



    //        }

    //        $("<div class='titoloInv scrollingItem'>").text("objectsYouAreCarrying".tr()).appendTo($(".bloccoDaScegliereInv"));

    //        g_last_room_desc.grrInvObjects.forEach(function (ofc)
    //        {

    //                creaPulsantePerWizardInv(ofc);



    //        });
    //        g_last_room_desc.grrInvConcepts.forEach(function (ofc)
    //        {

    //                creaPulsantePerWizardInv(ofc);



    //        });

    //        //debugger;




    //        $("#invModal").modal('show');
    //});


    $("#grafica").mousedown(e => {
        if (gFrozenMouse) {
            e.preventDefault();
            e.stopPropagation();
            return;
        }

        console.log("grafica mousedown - deselect all");
        //debugger;
        deselectAll();
        updateActionBarAndSelectabilityOfObjects();
    });







    if (localStorage[credentialsId]) {

        // ho le credenziali salvate nel browser. allora carico direttamente il punto in cui era arrivato.
        loadSavedGame(); // await non va perché sono in jquery document ready


    }
    else {
        createUserAndStartGame();// await non va perché sono in jquery document ready


    }

    updateResponsiveClasses();


    // metto una pezza alle modal , al fatto che ho dovuto aggiungere un div. ma se clicchi su quel div, ma non sui figli, si deve chiudere la modal
    $(".modalDialogOuter").click(e => {

        let curt = $(e.currentTarget).attr('class');
        let targ = $(e.target).attr('class');

        //debugger;
        // ignoro il click se è su un figlio... se no si chiude a sproposito.
        if (e.target !== e.currentTarget)  //https://stackoverflow.com/a/53815609/195188
        {
            // faccio anche questo se no si chiude a volte quando scrollo la nuova scrollbar js, non so perche
            // ah, no, è quando tu clicchi sulla scrollbar ma poi mentre trascini sposti il mouse, così quando rilasci il 
            // mouse si trova fuori... non so come evitarlo, ma questo non lo evita.
            //e.preventDefault();
            //e.stopPropagation(); 
            ////

            return;
        }
        let thiss = $(e.target);
        // se ha cliccato proprio lì, chiudo la modal.simulo un click sullo sfondoù

        let parent = thiss.parent();
        parent.trigger('click');

    });


    //let stepScroll = 150;
    let deltaTimeScroll = 100;
    $(".scrollDown").off('click').click(e => {
        //console.log('scroll down');
        e.preventDefault();




        if ($(e.currentTarget).hasClass("disabled")) {
            return;
        }
        //debugger;
        let parent = $(e.currentTarget).parent();
        let cosaScrollare = parent.find(".scrollanteConPulsanti");

        let isHoriz = cosaScrollare.hasClass('horiz');

        let elementiDaScrollare = cosaScrollare.find(".scrollingItem");


        let containerCheScrollaNonSiAllunga = cosaScrollare;

        //debugger;
        let iUltimoElementoDaScrollareFullyInView = null;
        for (let i = 0; i < elementiDaScrollare.length; i++) {
            let el = elementiDaScrollare[i];

            let visible;
            if (isHoriz) {
                visible = visibleX(el, containerCheScrollaNonSiAllunga[0]);
            }
            else {
                visible = visibleY(el, containerCheScrollaNonSiAllunga[0]);
            }
            if (visible) {
                iUltimoElementoDaScrollareFullyInView = i;
            }
        }

        //if (ultimoElementoDaScrollareFullyInView == null)
        //{
        //        debugger; // era ultimo sibling
        //}

        let debug_ultimoElementoDaScrollareFullyInView = elementiDaScrollare[iUltimoElementoDaScrollareFullyInView];

        let maxHtViewport;

        if (isHoriz) {
            maxHtViewport = cosaScrollare.outerWidth(true);
        }
        else {
            maxHtViewport = cosaScrollare.outerHeight(true);
        }


        let maxHtLista;
        if (isHoriz) {
            maxHtLista = parent.find(".scrollanteConPulsanti").children().first().outerWidth(true);
        }
        else {
            maxHtLista = parent.find(".scrollanteConPulsanti").children().first().outerHeight(true);
        }


        let maxScrollY = Math.ceil(maxHtLista - maxHtViewport); // ceil è importante, se no rischia di avere un maxscrollY leggermente troppo grande, e poi animate sotto non fa niente, e la freccia non 

        let elementoCheVoglioFarVedere = elementiDaScrollare[iUltimoElementoDaScrollareFullyInView + 1]; // il prossimo sibling. non posso usare .nxt() jquery perché sono annidati, non un elenco di sibling

        if (typeof elementoCheVoglioFarVedere === 'undefined') {
            if (isHoriz) {
                cosaScrollare.scrollLeft(maxScrollY); // per debug.---animando partono troppo presto le proc che nascondono i pulsanti scroll? 
            }
            else {
                cosaScrollare.scrollTop(maxScrollY); // per debug.---animando partono troppo presto le proc che nascondono i pulsanti scroll? 
            }
            aggiornaPulsantiScroll(cosaScrollare[0]);
        }
        else {
            //if (elementoCheVoglioFarVedere.length === 0)
            //{
            //        // era ultimo sibling
            //}


            let boxElem = elementoCheVoglioFarVedere.getBoundingClientRect();
            let boxContainer = containerCheScrollaNonSiAllunga[0].getBoundingClientRect();

            let diQuantoNonEVisibile;

            if (isHoriz) {
                diQuantoNonEVisibile = boxElem.left + boxElem.width - (boxContainer.left + boxContainer.width); // può essere 0.2
            }
            else {
                diQuantoNonEVisibile = boxElem.top + boxElem.height - (boxContainer.top + boxContainer.height); // può essere 0.2

            }
            let altezzaDaScrollare = Math.ceil(diQuantoNonEVisibile + 1); // +1 se no si blocca...

            //let altezzaDaScrollare = $(elementoCheVoglioFarVedere).outerHeight(true);

            //sparisce mai

            //console.log("maxHtLista", maxHtLista);
            //console.log("maxHtV", maxHtViewport);

            let curpos;

            if (isHoriz) {
                curpos = cosaScrollare.scrollLeft();
            }
            else {
                curpos = cosaScrollare.scrollTop();
            }

            let targetY = curpos + altezzaDaScrollare;
            //let forseEventoScrollNonScatta;
            ////if (targetY > maxScrollY) // commentato perché in alcuni casi (solo per rileggi-gli-indizi) maxScrollY era troppo piccola... non sono riuscito a capire perché . ma cmq funziona.
            ////{
            ////        targetY = maxScrollY; // + 1;
            ////        forseEventoScrollNonScatta = true;
            ////}
            ////else
            //{
            //        forseEventoScrollNonScatta = false;
            //}


            //cosaScrollare.animate({ scrollTop: targetY }, deltaTimeScroll); // ora che scrollo un item alla volta non serve
            if (isHoriz) {
                cosaScrollare.scrollLeft(targetY); // per debug.---animando partono troppo presto le proc che nascondono i pulsanti scroll? 
            }
            else {
                cosaScrollare.scrollTop(targetY); // per debug.---animando partono troppo presto le proc che nascondono i pulsanti scroll? 

            }

            //if (forseEventoScrollNonScatta)
            //{
            //        aggiornaPulsantiScroll(cosaScrollare[0], isHoriz); // no, c'è evento scroll
            //}

        }
    });

    $(".scrollUp").off('click').click(e => {

        //console.log('scroll up');
        e.preventDefault();


        if ($(e.currentTarget).hasClass("disabled")) {
            return;
        }
        let parent = $(e.currentTarget).parent();
        let cosaScrollare = parent.find(".scrollanteConPulsanti");
        let isHoriz = cosaScrollare.hasClass('horiz');


        let elementiDaScrollare = cosaScrollare.find(".scrollingItem");


        let containerCheScrollaNonSiAllunga = cosaScrollare;

        //debugger;
        let iUltimoElementoDaScrollareFullyInView = null;
        for (let i = elementiDaScrollare.length - 1; i >= 0; i--) // scorro da sotto a sopra
        {
            let el = elementiDaScrollare[i];

            let visible;

            if (isHoriz) {
                visible = visibleX(el, containerCheScrollaNonSiAllunga[0]);
            }
            else {
                visible = visibleY(el, containerCheScrollaNonSiAllunga[0]);
            }
            if (visible) {
                iUltimoElementoDaScrollareFullyInView = i;
            }
        }


        let ultimoFullyVisible = elementiDaScrollare[iUltimoElementoDaScrollareFullyInView];

        if (iUltimoElementoDaScrollareFullyInView > 0) { // posso scrollare
            let elementoCheVoglioFarVedere = elementiDaScrollare[iUltimoElementoDaScrollareFullyInView - 1]; // il prossimo sibling verso l'alto
            //if (typeof elementoCheVoglioFarVedere === 'undefined')
            //{
            //        debugger;
            //}
            let boxElem = elementoCheVoglioFarVedere.getBoundingClientRect();
            let boxContainer = containerCheScrollaNonSiAllunga[0].getBoundingClientRect();

            let diQuantoNonEVisibile;

            if (isHoriz) {
                diQuantoNonEVisibile = Math.abs(boxElem.left - boxContainer.left); // può essere 0.2
            }
            else {
                diQuantoNonEVisibile = Math.abs(boxElem.top - boxContainer.top); // può essere 0.2
            }


            let altezzaDaScrollare = Math.ceil(diQuantoNonEVisibile + 1); // non meno di 10


            //debugger;

            let curpos;

            if (isHoriz) {
                curpos = cosaScrollare.scrollLeft();
            }
            else {
                curpos = cosaScrollare.scrollTop();
            }

            let targetY = curpos - altezzaDaScrollare;

            if (targetY < 0) {
                targetY = 0;
            }


            if (isHoriz) {

                cosaScrollare.scrollLeft(targetY); // per debug.---animando partono troppo presto le proc che nascondono i pulsanti scroll? 
            }
            else {
                cosaScrollare.scrollTop(targetY); // per debug.---animando partono troppo presto le proc che nascondono i pulsanti scroll? 

            }
            //aggiornaPulsantiScroll(cosaScrollare[0]); // no, c'è l'evento scroll

            //cosaScrollare.animate({ scrollTop: targetY}, deltaTimeScroll);

        }
        else {
            // sono già all inizio
        }
    });

    $(".scrollanteConPulsanti").scroll(e => {
        //let isHoriz = $(e.currentTarget).hasClass('horiz');
        // serve se scrolli con rotella
        aggiornaPulsantiScroll(e.currentTarget);
    });

    //$(".bloccoDaScegliereSentence").resize(e =>
    //{
    //        console.log("resized");
    //});

    //$(".scrollanteConPulsanti").each((i, el0) =>
    //{
    //        aggiornaPulsantiScroll(el0);
    //});

    //$(".scrollbar-outer").scrollbar();



    async function onPickupIconPressed(e) {
        //console.log("pickup pressed");
        e.preventDefault();
        e.stopPropagation();
        if (e.which != 1) // succede solo se era mousedown, ma se è click no
        {
            cliccatoNelVuotoDeselezionaTutto();
            return;
        }

        if (gFrozenMouse || $(e.currentTarget).hasClass("disabled") || $(e.currentTarget).hasClass("tempDisabled")) {
            e.preventDefault();
            e.stopPropagation();
            return;
        }

        gSelectedVerb = 'pickup';
        gSelectedObj = null;
        updateToolbar();
        if (noImgMode() && typeof globalThis.showTextModeTargetsModal === "function") {
            gSelectedVerb = null;
            updateToolbar();
            await globalThis.showTextModeTargetsModal("pickup");
        }
        e.stopPropagation();
    }

    async function onDeduceIconPressed(e) {
        //console.log("pickup pressed");
        e.preventDefault();
        e.stopPropagation();
        if (e.which != 1) // succede solo se era mousedown, ma se è click no
        {
            cliccatoNelVuotoDeselezionaTutto();
            return;
        }

        if (gFrozenMouse || $(e.currentTarget).hasClass("disabled") || $(e.currentTarget).hasClass("tempDisabled")) {
            e.preventDefault();
            e.stopPropagation();
            return;
        }





        {


            gSelectedVerb = 'deduce';
            gSelectedObj = null;
            updateToolbar();
            e.stopPropagation();
        }
    }



    async function onUseIconPressed(e) {
        //console.log("pickup pressed");
        e.preventDefault();
        e.stopPropagation();
        if (e.which != 1) // succede solo se era mousedown, ma se è click no
        {
            cliccatoNelVuotoDeselezionaTutto();
            return;
        }

        if (gFrozenMouse || $(e.currentTarget).hasClass("disabled") || $(e.currentTarget).hasClass("tempDisabled")) {
            e.preventDefault();
            e.stopPropagation();
            return;
        }





        {


            gSelectedVerb = 'use';
            gSelectedObj = null;
            updateToolbar();
            e.stopPropagation();
        }
    }




    $(".pickUpIcon").mousedown(async e => {
        e.preventDefault();
        e.stopPropagation();
        await onPickupIconPressed(e);

    });

    $(".useIcon").mousedown(async e => {
        e.preventDefault();
        e.stopPropagation();
        await onUseIconPressed(e);

    });

    $(".deduceIcon").mousedown(async e => {
        e.preventDefault();
        e.stopPropagation();
        await onDeduceIconPressed(e);

    });
    $(" .btnTextPickup").click(async e => {
        e.preventDefault();
        e.stopPropagation();
        await onPickupIconPressed(e);

    });

    $(" .btnTextPickup").contextmenu(e => {
        e.preventDefault();
        e.stopPropagation();
        cliccatoNelVuotoDeselezionaTutto();

    });



    async function onRememberPressed(e) {
        //console.log("remember pressed");
        e.preventDefault();
        e.stopPropagation();
        if (e.which != 1) // succede solo se era mousedown, ma se è click no
        {
            cliccatoNelVuotoDeselezionaTutto();
            return;
        }

        if (gFrozenMouse || $(e.currentTarget).hasClass("disabled") || $(e.currentTarget).hasClass("tempDisabled")) {
            e.preventDefault();
            e.stopPropagation();
            return;
        }

        let ofcInCurRoom = g_last_room_desc.grrRooms[g_last_room_desc.grrCurRoomId].rfc_objects;
        let invObjects = g_last_room_desc.grrInvObjects;
        let nienteDaRicordareQui = !ofcInCurRoom.any(ofc => ofc.ofc_can_be_remembered) && !invObjects.any(ofc => ofc.ofc_can_be_remembered);
        if (noImgMode() && nienteDaRicordareQui) {



            gFrozenMouse = true;


            disabilitaTuttoTemporaneamenteMentreVediFrase();

            let fullText = "nienteDaRicQui".tr();

            $(".textModeFraseComposta").html(fullText.fondiParole().firstLetterToUpper());

            //if (soGiaChefallisce) {
            $(".textModeFraseComposta").addClass("fallisce");

            let tempo = 1500; //calcolaTempoFrase(fullText);
            //console.log('aspetto per', tempo);
            await delay(tempo); // 1000 a volte è poco per frasi lunghe



            riabilitaTuttoTemporaneamenteDisabilitatoFrase();


            gFrozenMouse = false;

            updateToolbar();

            $(".textModeFraseComposta").html('&nbsp;');

            //if (soGiaChefallisce) {
            $(".textModeFraseComposta").removeClass("fallisce");


        }
        else {


            gSelectedVerb = 'remember';
            gSelectedObj = null;
            updateToolbar();
            if (noImgMode() && typeof globalThis.showTextModeTargetsModal === "function") {
                gSelectedVerb = null;
                updateToolbar();
                await globalThis.showTextModeTargetsModal("remember");
            }
            e.stopPropagation();
        }
    }


    $(" .btnTextRicorda").click(async e => {
        e.preventDefault();
        e.stopPropagation();
        await onRememberPressed(e);

    });

    $(" .btnTextRicorda").contextmenu(e => {
        e.preventDefault();
        e.stopPropagation();
        cliccatoNelVuotoDeselezionaTutto();

    });

    //$(".deduceIcon").mousedown(e => {
    //        if (gFrozenMouse) {
    //                e.preventDefault();
    //                e.stopPropagation();
    //                return;
    //        }

    //        if (e.which != 1) return;
    //        gSelectedVerb = 'deduce';
    //        gSelectedObj = null;
    //        updateToolbar();
    //        e.stopPropagation();
    //});

    $(".eyeIcon").mousedown(e => {
        if (gFrozenMouse || $(e.currentTarget).hasClass("disabled") || $(e.currentTarget).hasClass("tempDisabled")) {
            e.preventDefault();
            e.stopPropagation();
            return;
        }

        if (e.which != 1) return;
        gSelectedVerb = null;
        gSelectedObj = null;
        updateToolbar();
        e.stopPropagation();
    });

    if (is_touch_device1()) {
        $(".scrollanteConPulsanti").css("overflow-y", "scroll");
    }
    else {
        $(".scrollanteConPulsanti").css("overflow-y", "hidden");

        $('.scrollanteConPulsanti').bind('wheel', function (e) {
            if (e.originalEvent.deltaY < 0) {
                let parent = $(this).parent();

                let btnUp = parent.find(".scrollUp");
                btnUp.trigger('click');
            }
            else {
                let parent = $(this).parent();

                let btnUp = parent.find(".scrollDown");
                btnUp.trigger('click');
            }
        });


        // sistema di zoom bacato se ho il mouse su un layer di un personaggio... e cmq anche normalmente un po' bacato
        //let startWt = null;
        //let startHt = null;
        //let scaleStep = 0.2;
        //$(".divLayersContainer").bind('mousewheel', function (e)
        //{
        //        console.log(e);

        //        //debugger;
        //        if (e.originalEvent.wheelDelta / 120 > 0) // up
        //        {
        //                gCurScale += scaleStep;
        //        }
        //        else // down
        //        {
        //                if (gCurScale - scaleStep > 1.0)
        //                {
        //                        gCurScale -= scaleStep;
        //                }
        //                else
        //                {
        //                        gCurScale = 1.0;
        //                }

        //        }
        //        //let orig = e.originalEvent.screenX + "px " + e.originalEvent.screenY + "px ";
        //        let orig = e.offsetX + "px " + e.offsetY + "px ";
        //        //let orig = e.originalEvent.clientX + "px " + e.originalEvent.clientX + "px ";
        //        console.log('orig', orig);
        //        //$(".divLayersContainer").css('transform', `scale(${gCurScale}) translate(-${e.originalEvent.offsetX}px,-${e.originalEvent.offsetY}px)`);

        //        if (startWt == null)
        //        {
        //                startWt = $(".divLayersContainer").width();
        //        }

        //        if (startHt == null)
        //        {
        //                startHt = $(".divLayersContainer").height();
        //        }

        //        //debugger;
        //        let xx = e.offsetX / startWt;
        //        let yy = e.offsetY / startHt;

        //        //console.log('wt', wt);
        //        let targx = - xx* ( gCurScale * startWt  - startWt);
        //        let targy = - yy * (gCurScale * startHt - startHt);

        //        //let targy = $(".divLayersContainer").height() * 0.5 - e.originalEvent.offsetY;

        //        $(".divLayersContainer").css('transform', `translate(${(targx)}px,${(targy)}px)        scale(${gCurScale}) `);
        //        $(".divLayersContainer").css('transform-origin', 'top left');

        //});




    }


    function mostraHotspot() {
        $(".btnOggettoInRoomHotspot").remove();
        $(".imgTarget").remove();
        $(".imgTargetExit").remove();
        $(".imgTargetExitDown").remove();
        if (g_last_room_desc == null) {
            debugger;
        }
        let ofcInCurRoom2 = g_last_room_desc.grrRooms[g_last_room_desc.grrCurRoomId].rfc_objects;

        let dat = calcolaScalEtc();


        // ora quelli che non sono layer, le uscite




        let loHoverManualRect = null;
        let oggettiInCurRoomOrd = _.sortBy(ofcInCurRoom2, e => - e.ofcHotspotPriority);

        //for (let lo of oggettiInCurRoomOrd)
        //{
        //        //if (lo.loId == "exitTavernUp") {
        //        //        debugger;
        //        //}
        //        if (lo.ofcManualCoords !== null)
        //        {
        //                //if (lo.ofcManualCoords.x0 <= x && x <= lo.ofcManualCoords.x1

        //                //        &&
        //                //        lo.ofcManualCoords.y0 <= y && y <= lo.ofcManualCoords.y1)
        //                {


        //                        let text = lo.ofc_name;


        //                        //debugger;
        //                        //let newEl = $("<div class='btnOggettoInRoomHotspot '>").text(text);

        //                        let newEl;
        //                        if (lo.ofcIsExit)
        //                        {
        //                                newEl = gTemplateImgTargetExit.clone();
        //                        }
        //                        else
        //                        {
        //                                newEl = gTemplateImgTarget.clone();
        //                        }


        //                        newEl.appendTo(".divLayersContainer"); // subito, se no non misura
        //                        let offsx = dat.posOfBg.left;
        //                        let offsy = dat.posOfBg.top;

        //                        let x0 = $(".divLayersContainer").width() * lo.ofcManualCoords.x0 / 100.0;
        //                        let x1 = $(".divLayersContainer").width() * lo.ofcManualCoords.x1 / 100.0;

        //                        let y0 = $(".divLayersContainer").height() * lo.ofcManualCoords.y0 / 100.0;
        //                        let y1 = $(".divLayersContainer").height() * lo.ofcManualCoords.y1 / 100.0;




        //                        let wt = x1 - x0;
        //                        let ht = y1 - y0;

        //                        let left = x0 + offsx + wt * 0.5 - newEl.width() * 0.5;

        //                        if (left < 0)
        //                        {
        //                                left = 0;
        //                        }

        //                        newEl.css("left", left);



        //                        //let top = lfc.lfc_y * dat.scal + lfc.lfc_ht * dat.scal - 8;
        //                        let top = y0 + offsy + ht * 0.5 - newEl.height() * 0.5;

        //                        if (top > $(".divLayersContainer").height() - newEl.height())
        //                        {
        //                                top = $(".divLayersContainer").height() - newEl.height();
        //                        }

        //                        newEl.css("top", top);


        //                        newEl.css('opacity', 0);


        //                        newEl.animate({ 'opacity': 0.5 }, 400);


        //                }

        //                //debugger;
        //        }
        //}

        // ora i layer
        for (let lfc of g_last_room_desc.grrLayersOfCurRoom.values()) {

            if (lfc.lfc_loId !== 'bg') {
                let oggettiCorrispondentiAlLayer = ofcInCurRoom2.filter(ofc => ofc.loId == lfc.lfc_loId);
                if (oggettiCorrispondentiAlLayer.length == 0) {
                    //debugger; // todo succede ad esemio con windowvan
                }
                else {

                    let lo = oggettiCorrispondentiAlLayer[0]; // todo fai dizionario
                    //if (lo.loId == "exitTavernUp") {
                    //        debugger;
                    //}
                    if (lo.ofcMustBeShownInTextRoomRecap) {


                        let forcedName = lo.ofc_name;
                        posizionaDidascaliaOggetto(dat, lo, lfc, forcedName);
                    }
                }
            }

        }


    }




    function enterOrMove(e) // mousemove on inv icon
    {
        if (gFrozenMouse) {
            e.preventDefault();
            e.stopPropagation();
            return;
        }


        let screenX = e.screenX;
        let screenY = e.screenY;

        // deov sottrarre la posizione del canvas
        let re = $(".divLayersContainer")[0].getBoundingClientRect();
        let parentX = re.left;
        let parentY = re.top;

        screenX -= parentX;
        screenY -= parentY;

        var xFinale = e.clientX - re.left;
        var yFinale = e.clientY - re.top;


        let thi = $(e.currentTarget);
        let dat = calcolaScalEtc();

        //debugger;
        let text;
        if (thi.hasClass("eyeIcon")) {
            text =  'lookat'.tr();

        }
        else if (thi.hasClass('walkIcon')) {
            //text = ''; // ora c'è il testo nell'inv
            text = 'walk'.tr();
        }
        else if (thi.hasClass('pickUpIcon')) {
            text = ''; // ora c'è il testo nell'inv
            //text = 'pickup'.tr();
        }
        else if (thi.hasClass('talkIcon')) {
            text = 'talk'.tr();
            //text = ''; // ora c'è il testo nell'inv
        }
        else if (thi.hasClass('deduceIcon')) {
            //text = 'deduce'.tr();
            text = ''; // ora c'è il testo nell'inv
        }
        else if (thi.hasClass('optionsIcon')) {
            text = 'optionsLower'.tr();
            //text = ''; // ora c'è il testo nell'inv
        }
        else if (thi.hasClass('objectivesIcon')) {
            text = 'diary'.tr();
            //text = ''; // ora c'è il testo nell'inv
        }

        posizionaDidascaliaOggettoMouse(dat, null, xFinale, yFinale, false, text); // questo è un verbo nella toolbar - occhio ecc.
    }


    function enter(e)  //on inv icon
    {
        enterOrMove(e);


        // in più quando è l'occhio mostra gli hotspot
        let thi = $(e.currentTarget);
        if (thi.hasClass("eyeIcon")) {
            mostraHotspot();
        }
    }
    function leave(e) {


        $(".btnOggettoInRoom ").remove();
        $(".btnOggettoInRoomHotspot ").remove();
        $(".imgTarget").remove();
        $(".imgTargetExit").remove();
        $(".imgTargetExitDown").remove();
    }
    $(".invIconNotObject").hover(enter, leave);
    $(".invIconNotObject").mousemove(e => {
        if (gFrozenMouse) {
            e.preventDefault();
            e.stopPropagation();
            return;
        }

        enterOrMove(e);


    });



    $(".objectivesIcon, .btnTextDiario").mousedown(async e => {
        if (gFrozenMouse || $(e.currentTarget).hasClass("disabled") || $(e.currentTarget).hasClass("tempDisabled")) {
            e.preventDefault();
            e.stopPropagation();
            return;
        }

        if (e.which != 1) return;



        $(".obiettivoNew").remove();
        for (let ob of g_last_room_desc.grrObjectives) {



            let newButton = $('<div class="obiettivoNew scrollingItem" >').html(ob.readable_name.firstLetterToUpper());

            if (!ob.obcWasSeen) {
                newButton.addClass("unseen");
            }
            newButton.appendTo(".obiettiviOuter");

        }



        $(".btn_indizio").remove();
        for (let ncs of g_last_room_desc.grr_named_cut_scenes) {



            let btn = $("<div class='btn btn-default btn_indizio form-control btnOpt scrollingItem'>").appendTo(".diarioOuter");
            btn.text(ncs.ncsc_title_translated);
            btn.click(async function (e) {
                e.preventDefault();

                $("#divObjectives").modal('hide');
                mostraPleaseWait();
                let cred = JSON.parse(localStorage[credentialsId]);
                let i = {
                    uname: cred.uname,
                    pwd: cred.pwd,
                    token: cred.token,
                    cut_scene_title: ncs.ncsc_ser_id,
                    lang: getLang()
                    , curTime: getCurTime()
                    , cred_gameId: gGameId
                };

                let data = await doPostTry(`${prefissoWebApi}/api/replay_cut_scene`, i);


                let canContinue = handleErrorsPost(data);
                if (canContinue) {
                    nascondiPleaseWait();

                    //console.log("ok: ", data.ret);
                    let ar = data.ret;

                    await handleAr(ar);
                }

            });
        }

        aggiornaPulsantiScroll(".obiettiviOuter");

        $('#myTabs a:first').tab('show'); // Select first tab
        $("#divObjectives").modal('show');




        // manda il messaggio al server che gli obiettivi sono stati visualizzati
        let osiObjectivesSeen = g_last_room_desc.grrObjectives.map(o => o.ser_id);

        await callUpdateObjectives(osiObjectivesSeen);


        // senza aspettare il ritorno internamente marco tutto seen
        for (let o of g_last_room_desc.grrObjectives) {
            o.obcWasSeen = true;
        }

        updateToolbar();
    });


    $("#btnHintSystem").mousedown(async e => {


        await showHintList();
    });


    $(".talkIcon").mousedown(async e => {
        if (gFrozenMouse || $(e.currentTarget).hasClass("disabled") || $(e.currentTarget).hasClass("tempDisabled")) {
            e.preventDefault();
            e.stopPropagation();
            return;
        }

        if (e.which != 1) return;
        if (g_last_room_desc.grrTalkNow) {
            await callTalkHere();
        }
        else {

            // redesign - ora niente.
            //gSelectedVerb = 'talk';

            //updateToolbar();


        }
    });


    $(" .btnTextTalk").click(async e => {
        if (gFrozenMouse || $(e.currentTarget).hasClass("disabled") || $(e.currentTarget).hasClass("tempDisabled")) {
            e.preventDefault();
            e.stopPropagation();
            return;
        }

        if (e.which != 1) return;
        if (g_last_room_desc.grrTalkNow) {
            await callTalkHere();
        }
        else {

            // redesign - ora niente.
            //gSelectedVerb = 'talk';

            //updateToolbar();


        }
    });
    $(".modal-dialog").click(e => {


        let curt = $(e.currentTarget).attr('class');
        let targ = $(e.target).attr('class');

        //debugger;
        // ignoro il click se è su un figlio... se no si chiude a sproposito.
        if (e.target !== e.currentTarget)  //https://stackoverflow.com/a/53815609/195188
        {

        }
        else {
            //console.log("clicked modal lateralmente");

            $(e.target).parent().parent().modal('hide');
        }
    });



    $(".divLayersContainer").mousemove(e => {   // bm_mousemovedOnBg
        //console.log("divLayersContainer mouse moved ");

        if (gFrozenMouse) {
            e.preventDefault();
            e.stopPropagation();
            return;
        }


        //var x = (e.clientX - e.currentTarget.offsetLeft) ;
        //var y = (e.clientY - e.currentTarget.offsetTop) ;

        onMouseMoveLayersContainer_seeIfMouseOnRect(e);



    });

    $(".divLayersContainer").mouseleave(e => {
        if (gLayerHover != null && gLayerHover.lfcIsOutline) {
            gLoHover = null;
            gDatHover = null;
            gLayerHover = null;
            $(".btnOggettoInRoom").remove();
            $(".divLayersContainer").css("cursor", "default");
        }
    });




    //$(".imgGrafica").mousedown(e =>
    //{
    //        e.preventDefault();
    //});

    $(".divLayersContainer").mousedown(async e => {
        if (gFrozenMouse) {
            e.preventDefault();
            e.stopPropagation();
            return;
        }

        if (e.which == 1) {

            //console.log("clicked layers container");
            //debugger;

            let oggettiSottoMouseTranneMe = objectsUnderMouse(e.offsetX, e.offsetY, ret => ret.filter(x => !x.lo.ofcIsInCurParty).length > 0)
                .filter(x => !x.lo.ofcIsInCurParty);

            if (oggettiSottoMouseTranneMe.length > 0) {
                let primoOggetto = oggettiSottoMouseTranneMe[0];
                await onLoClickedRoom(primoOggetto.lo);
            }
            else {

                gLoHoverManualRect = vediSeMouseOverRect(e);

                if (gLoHoverManualRect != null) {

                    e.preventDefault();
                    e.stopPropagation(); // altrimenti parte qualcosa che fa sparire la scritta (ma non deseleziona il verbo)


                    let lo = gLoHoverManualRect;

                    let mouseX = e.clientX;
                    let mouseY = e.clientY;
                    await onLoClickedRoom(lo, mouseX, mouseY);

                }
                else {
                    // clicato nel vuoto room

                    cliccatoNelVuotoDeselezionaTutto();
                }
            }
        }
        else {
            cliccatoNelVuotoDeselezionaTutto();
        }

    });


    $(".invContainerInnerJustObjects ").mousedown(e => {
        if (gFrozenMouse) {
            e.preventDefault();
            e.stopPropagation();
            return;
        }

        e.preventDefault();
        cliccatoNelVuotoDeselezionaTutto();
    });


    //$(".invScrollRight").click(e =>
    //{

    //        let el = $(e.currentTarget);

    //        if (el.hasClass("disabled"))
    //        {
    //                return;
    //        }


    //        let wt = $(".invObjTemplate").first().outerWidth(true);
    //        console.log("scroll by", wt);
    //        let curScroll = $(".invContainerInnerJustObjects").scrollLeft();
    //        $(".invContainerInnerJustObjects").scrollLeft(curScroll + wt);
    //});

    //$(".invScrollLeft").click(e =>
    //{

    //        let el = $(e.currentTarget);

    //        if (el.hasClass("disabled"))
    //        {
    //                return;
    //        }


    //        let wt = $(".invObjTemplate").first().outerWidth(true);
    //        console.log("scroll by", wt);
    //        let curScroll = $(".invContainerInnerJustObjects").scrollLeft();
    //        $(".invContainerInnerJustObjects").scrollLeft(curScroll - wt);
    //});




    //aggiornaPulsantiScroll(".invContainerInnerJustObjects ");  // temp, metto scrollbar vert per ora



    $(".nocontextmenu").contextmenu(e => {
        e.preventDefault();
    });

    // Il context menu è dentro il container della stanza. Le sue voci devono
    // ricevere mouseover e mousedown senza far ripartire la logica di click
    // della stanza, che altrimenti riaprirebbe il menu sotto il cursore.
    $(".contextMenu").on("mousedown", e => {
        if ($(e.currentTarget).hasClass("fromHitTestOnly")) {
            e.stopPropagation();
        }
    });

    //$("#submitTextInput").mousedown(e =>
    //{

    //        if ($(this).hasClass('disabled'))
    //        {
    //                e.preventDefault();
    //        }
    //});

    $(".invIcon , .invContainer").mousedown(e => {
        if (gFrozenMouse || $(e.currentTarget).hasClass("disabled") || $(e.currentTarget).hasClass("tempDisabled")) {
            e.preventDefault();
            e.stopPropagation();
            return;
        }

        //debugger;
        if (e.which == 3) {
            cliccatoNelVuotoDeselezionaTutto();
        }
    });


    $(".roomTextMode").click(e => {
        //console.log("cliccato roomTextMode. frozenmouse = ", gFrozenMouse);
        //debugger;
        //if (e.currentTarget == e.target) { // questo seerver perche' con async non funziona stopPropagation. quindi se clicca su un pulsante figlio disabled, poi arriva questo e deseleziona tutto
        e.preventDefault();

        if (!gFrozenMouse) {
            cliccatoNelVuotoDeselezionaTutto();
        }
        //}
    });

    $(".roomTextMode").contextmenu(async e => {

        //console.log("right cliccato roomTextMode. frozenmouse = ", gFrozenMouse);
        e.preventDefault();

        if (!gFrozenMouse) {
            cliccatoNelVuotoDeselezionaTutto();
        }

    });

    $("#dialogChooseExplanation").on('hidden.bs.modal', function (e) {
        cliccatoNelVuotoDeselezionaTutto();
    });


    $(".btnOptionsEndTitles ").click(e => {
        mostraDialogOpzioni(e);
    });


    $(".hrefCredits").click(e => {
        e.preventDefault();

        $("#options").modal('hide');
        $("#divModalCredits").modal("show");

    });


    $('.modal-content').keypress(function (e) {
        //console.log("keypress");
        if (e.which == 13) {
            ////dosomething
            //alert('Enter pressed');

            let defBut = $(this).find("[default_button='true']").first();
            if (defBut.length > 0) {
                e.preventDefault();
                defBut.trigger('click');
            }
            //debugger;
            //let submitId = $(this).attr("submit_button");
            //if (submitId != "")
            //{
            //        debugger;
            //}
        }
    })

    $("#modalHintsSingleObjective").on("hidden.bs.modal", function (e) { // bm_closing


        gIsReadingHint = null;

    });

    $(".btnHintEmail").mousedown(e => {
        e.preventDefault();
        e.stopPropagation();
        window.open('mailto:segusum@example.invalid', '_blank');
    });



    $(document).on("keydown", function (event) {
        // Check if the pressed key is the "Escape" key
        if (event.key === "Escape" || event.keyCode === 27) {
            // Your code to handle the "Escape" key press goes here
            mostraDialogOpzioni(event);
        }
    });

});
