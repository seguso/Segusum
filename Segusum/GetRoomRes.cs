using System.Collections.Generic;

namespace Seg
{
    public class GetRoomRes
    {
        public List<RoomCoords> grrRoomCoords;
        public string grrMapImage;
        public double grrMapImageX;
        public double grrMapImageY;
        public double grrMapImageWidth;
        public double grrMapImageHeight;

        public string grrHideInsideLoId { get; set; }
        public string grrTravestitiLoId { get; set; }
        public string grrClimbLoId { get; set; }

        //public List<DynLineClient> grrDynLines;

        //public List<PuzzleSolutionClient> grrPuzzleSolutions;

        public ExplanationWithContClient[] grrExplanationsWithCont;

        public List<ObjectiveClient> grrObjectives;

        public Dictionary<string, string[]> grrTemplatesToExcludeOfObj;

        public Dictionary<string, string[]> grrExplanationsToExcludeOfObjective;

        public Dictionary<string, string[]> grrExplanationsToExcludeOfLo;

        public List<Template> grrTemplates;

        public ExplanationClient[] grrExplanationsGlobal;

        public bool grrTalkNow;

        public string grrProInterfaceTitle { get; set; }
        public string grrProInterfaceSubtitle { get; set; }
        public string grrCasualInterfaceTitle { get; set; }
        public string grrCasualInterfaceSubtitle { get; set; }

        public bool grrIsTextMode { get; set; }

        //public bool grrCannotTalkNow;


        public List<Filler> grrFillers;

        public bool grrMustShowYouSeeNothingSpecialHere { get; set; }

        public LayerForClient[] grrLayersOfCurRoom;

        public Dictionary<string, RoomForClient> grrRooms;

        public List<ObjAndVerbClient> grrDisabledVerbs;
        public List<VerbAndObjectiveClient> grrDisabledObjectives;

        //public List<VerbForClient> grrVerbs;

        //public List<DynamicExclusionClient> grrDynamicExclusions;

        //public VerbForClient grrUseVerb;


        public List<ObjForClient> grrInvObjects;
        //public List<ObjForClient> grrInvConcepts;


        public ObjForClient activeChar;

        //public string[] allClosingQtoks;
        //public Dictionary<string, QtokClient> dicQtokOfSerId;

        public string[] grrSaveNames;

        public string roomName;
        public string grrCurRoomId;


        /// <summary>
        /// deve essere del tipo {wo.graphicsRootFolderName()}/{assetFolderName}/bg.png, senza prefisso c:\ perche' il client ci mettera' il prefisso http://ecc
        /// </summary>
        public string roomImg;

        public string invTitle;
        public string grrStrOggettiCheVediQui;
        public string grrStrClickAnObjectToRemember;
        //public string grrStrOggettiChePortiConTe;
        public string mindTitle;
        //public string verbsTitle;
        public string optionsTitle;

        public List<NamedCutSceneClient> grr_named_cut_scenes;

        public string grr_walk_translated;
        public string grr_walk_to_translated;
        public string grr_in_order_to_translated;


        //public string grr_useTokenSerId { get; set; }
        public string grr_here_you_see;
        public string grr_your_objects;
        public string grr_objects_seen_somewhere;
        //public string grr_possible_actions;
        //public string grr_your_objectives;
        public string grrRememberAnObject;
        //public string grr_are_you_stuck;
        //public string grrPickupVerbId;
        //public string grrPickupReadableNameTransl;
        public string grr_other;
        public string grr_options;
        public string grr_cancel;
        public string grr_back;
        public string grr_reread_clues;
        //public string grr_press_to_continue;
        public string grr_nothing_special;

        
        //public string grr_IQLevel;
        public bool grrStoryMode { get; set; }
        public bool grrCasualMode { get; set; }

        //public string grr_you_dont_see_how_this_can_help;



    }



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
