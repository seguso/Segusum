using System;
using System.Collections.Generic;

namespace Seg
{
        /// <summary>
        /// il server restituisce il prossimo token della cut scene, oppure, se è finita, restituisce o una room desc, o , se sei in un dialogo, le nuove voci di dialogo
        /// </summary>
        public class SegActionRes
        {
                

                public ulong? ar_curTime { get; set; }

                public GetRoomRes room;

                public CutSceneTokenWithTitle nextCutSceneToken;

                public List<QuestionClient> questions;


                public TextInputClient textInputRes;

                public EndGameStuffClient arEndGame;

                // il client svuoterà la cache se si accorge di essere vecchio
                public int last_client_version; // bm_58f855j8

                // il tuo salvataggio è obsoleto, devi ricominciare
                public bool savegame_invalid;

                // eri su una finestra vecchia
                public bool ar_oldSessionMustTakeOver;

                public bool errorCannotGoBackInCutsceneAlreadyBeginning = false;

                public SegActionRes(ulong? ar_curTime)
                {
                        this.ar_curTime = ar_curTime;
                }
        }



        public class EndGameStuffClient
        {
                public EndGameStuffClient(string egsImg, string[] egsCredits)
                {
                        this.egsImg = egsImg ?? throw new ArgumentNullException(nameof(egsImg));
                        this.egsCredits = egsCredits ?? throw new ArgumentNullException(nameof(egsCredits));
                }

                public string egsImg { get; set; }

                public string[] egsCredits { get; set; }
        }
}
