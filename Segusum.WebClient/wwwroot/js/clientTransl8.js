(function () {
    const bootstrapElement = document.getElementById("segusum-bootstrap");
    let bootstrap = { language: "en", strings: {} };
    try {
        if (bootstrapElement) bootstrap = JSON.parse(bootstrapElement.textContent || "{}");
    } catch (error) {
        console.error("Segusum client string bootstrap is invalid", error);
    }
    window.segusumBootstrap = bootstrap;
    window.segusumClientStrings = bootstrap.strings || {};
})();

function segusumTranslate(key) {
    const value = window.segusumClientStrings?.[key];
    if (value == null) {
        console.error("Missing Segusum client string: " + key);
        return key;
    }
    return value;
}

String.prototype.tr = function () {
    return segusumTranslate(String(this));
};
$(function () {
    $("[my_transl]").each((i, el0) => {
        let el = $(el0);
        let key = el.attr("my_transl");
        let tra = key.tr();
        el.text(tra);
    });

    // The old "play/exit tutorial" option is replaced by an explicit restart
    // action that is only available while the tutorial is actually running.
    $("#btnPlayTutorial").closest(".form-group").hide();

    if ($("#btnRestartTutorial").length === 0) {
        $("<div class='form-group' id='restartTutorialGroup'>")
            .append($("<button id='btnRestartTutorial' type='button' class='form-control btn btn-default btnOpt'>").text("restartTutorial".tr()))
            .insertAfter($("#btnNewGame").closest(".form-group"));
    }

    function updateRestartTutorialButton() {
        $("#restartTutorialGroup").toggle(g_tutorialMode === true);
    }

    updateRestartTutorialButton();
    $("#options").on("show.bs.modal", updateRestartTutorialButton);

    const originalSetTutorialMode = setTutorialMode;
    setTutorialMode = function (mode) {
        originalSetTutorialMode(mode);
        updateRestartTutorialButton();
    };

    $("#btnRestartTutorial").off("click").on("click", async function (e) {
        e.preventDefault();
        e.stopPropagation();
        $("#options").modal("hide");
        const casualMode = g_last_room_desc?.grrCasualMode === true;
        await callStartNewGame(true, casualMode);
    });

    // New-game flow: first choose the interface, then separately decide
    // whether to play the strongly recommended tutorial/minigame.
    chooseGameModeThenRun = function (ar) {
        return new Promise(resolve => {
            const room = ar?.room || {};
            const isItalian = gLang === "it";
            const proTitle = room.grrProInterfaceTitle || (isItalian ? "Interfaccia Pro" : "Pro interface");
            const proSubtitle = room.grrProInterfaceSubtitle || (isItalian
                ? "Il gioco ti chiede di spiegare cosa pensi succederà, così non rischi di risolvere puzzle per caso mentre sperimenti. Adatta ai puristi dei puzzle."
                : "The game asks you to explain what you think will happen, so you do not accidentally solve puzzles while experimenting. Best for puzzle purists.");
            const casualTitle = room.grrCasualInterfaceTitle || (isItalian ? "Interfaccia Casual" : "Casual interface");
            const casualSubtitle = room.grrCasualInterfaceSubtitle || (isItalian
                ? "Simile alle interfacce tradizionali. Scegli questa se ti interessa soprattutto la storia e non ti importa se risolverai dei puzzle per caso mentre sperimenti."
                : "Similar to traditional interfaces. Choose this if you care mostly about the story and do not mind accidentally solving puzzles while experimenting.");

            let interfaceChosen = false;
            let interfaceDialog;

            const finishWithoutTutorial = async (casualMode, tutorialDialog) => {
                tutorialDialog.close();
                // Nuova partita senza tutorial: setGameModeOnServer legge
                // g_tutorialMode per costruire la request. Se arriviamo dal
                // tutorial, dobbiamo azzerarlo prima della chiamata, altrimenti
                // il server può avviare la partita normale come tutorial.
                setTutorialMode(false);
                const data = await setGameModeOnServer(casualMode);
                if (handleErrorsPost(data)) {
                    await handleAr(ar);
                }
                resolve();
            };

            const startTutorial = async (casualMode, tutorialDialog) => {
                tutorialDialog.close();
                await callStartNewGame(true, casualMode);
                resolve();
            };

            const askTutorial = casualMode => {
                if (interfaceChosen) return;
                interfaceChosen = true;
                interfaceDialog.close();

                const message = $("<div>")
                    .append($("<p>").text("chooseTutorialQuestion".tr()))
                    .append($("<p>").append($("<strong>").text("tutorialStronglyRecommendedLabel".tr() + " "))
                        .append(document.createTextNode("tutorialStronglyRecommendedText".tr())));

                BootstrapDialog.show({
                    title: "tutorialInterfaceTitle".tr(),
                    message: message,
                    closable: false,
                    buttons: [
                        {
                            label: "yesPlayTutorial".tr(),
                            cssClass: "btn-primary",
                            action: dialog => startTutorial(casualMode, dialog)
                        },
                        {
                            label: "noStartGame".tr(),
                            action: dialog => finishWithoutTutorial(casualMode, dialog)
                        }
                    ]
                });
            };

            const modeDescriptions = $("<div class='gameModeDescriptions'>");
            const addModeCard = (title, subtitle, casualMode) => {
                const card = $("<div class='gameModeDescription'>").css({
                    border: "1px solid #777",
                    borderRadius: "6px",
                    padding: "12px",
                    marginBottom: "10px"
                });
                $("<div>").css({ fontWeight: "bold", marginBottom: "5px" }).text(title).appendTo(card);
                $("<div>").css({ marginBottom: "10px" }).text(subtitle).appendTo(card);
                $("<button type='button' class='btn btn-default'>")
                    .css({ width: "100%" })
                    .text("chooseThis".tr())
                    .on("click", () => askTutorial(casualMode))
                    .appendTo(card);
                card.appendTo(modeDescriptions);
            };

            addModeCard(proTitle, proSubtitle, false);
            addModeCard(casualTitle, casualSubtitle, true);

            interfaceDialog = BootstrapDialog.show({
                title: "chooseInterface".tr(),
                message: $("<div>")
                    .append($("<p>").text("chooseHowPlay".tr()))
                    .append(modeDescriptions),
                closable: false
            });
        });
    };
});
