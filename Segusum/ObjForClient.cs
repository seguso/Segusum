using System;
using System.Collections.Generic;

using System.Linq;

namespace Seg
{

    public class TemplateAndFillers
    {
        public string teId { get; set; }

        public string[] fiIds { get; set; }
    }

    public class ObjectAndCompleteSentence
    {
        public string ocsLoId { get; set; }

        public string ocsCompleteSentence { get; set; }
        public bool ocsRequiresExplanation { get; set; }
    }

    public class CombineExplanationDataClient
    {
        public bool cedRequiresExplanation { get; set; }
        public bool cedRequiredExplanationIsVisible { get; set; }
        public bool cedKeepExplanationInCasual { get; set; }
        public bool cedIsExactHandler { get; set; }
        public ExplanationClient[] cedExplanations { get; set; }
        public string cedCustomExplanationIntro { get; set; }
    }

    public class UseForExplanationDataClient
    {
        public ExplanationClient[] ufeExplanations { get; set; }
        public string ufeCustomExplanationIntro { get; set; }
    }

    public class ObjForClient
    {

        public class TargetPossessiveFormsClient
        {
            public string he { get; set; }
            public string she { get; set; }
            public string it { get; set; }
            public string they { get; set; }
        }



        public bool ofcCouldPotentiallyBePickedUp;
        //public bool ofcUseInLocation;
        public bool ofc_is_in_inv;
        public bool ofcIsConcept;
        public bool ofcIsConversationTopic;


        public bool ofcCanBeUsedAsTargetInTextMode;
        public bool ofcIsExit;

        public string ofcGender { get; set; }
        public TargetPossessiveFormsClient ofcTargetPossessiveForms { get; set; }


        //public bool ofcCannotBeUsed;

        //public bool ofcNameMustAppearInGraphics;

        public bool ofc_can_be_remembered;

        //public bool ofcCanTalkToCharacterNow { get; set; }
        public int ofcHotspotPriority { get; set; }



        public string ofcVerbWhenUseWithAsFirstObjectOnHoverNotSelected { get; set; }

        /// <summary>
        /// example : "talk about {1}"
        /// </summary>
        public string ofcVerbWhenUseWithAsFirstObjectSelectedWithPlaceHolder { get; set; }

        public string ofcVerbWhenUseWithAsFirstObjectSelectedWithPlaceHolderOnHoverSecond { get; set; }

        public string ofcimagePortrait;

        public bool ofcIsLookableNow { get; set; }

        public string ofcCustomSentenceLook { get; set; }

        public string ofcCustomSentenceUseHere { get; set; }

        public ExplanationClient[] ofcCustomExplanations;
        public bool ofcIsPickableNow { get; set; }

        public string ofc_name_with_in;
        public string loId;
        public string ofc_name;
        public string ofcNameWithArticle;

        public bool ofcMustBeShownInTextRoomRecap { get; set; }

        //public int ofcUseMode;

        //public string[] ofcFailureContinuations;

        public bool is_obvious_exit = false;

        public bool ofc_is_character = false;

        public bool ofcIsInCurParty = false;

        //public string[] ofcAssociatedQtokens { get; set; }

        public string ofcHoverStringWhenInRoom { get; set; }

        public string ofcVerbWhenUseForInDialogIntro { get; set; }

        public string ofcHoverStringWhenInInv { get; set; }

        //public string ofcVerbIdWhenInRoom { get; set; }
        public string ofcContextMenuUseForOrHereOrDeduce { get; set; }

        public bool ofcIsUseForWhenInInv => !ofcIsUseWithWhenInInv && !ofcIsUseInLocationWhenInInv;


        public TemplateAndFillers[] ofcCompatibleTemplates { get; set; }


        public bool ofcIsUseWithWhenInInv { get; set; }
        public bool ofcIsUseInLocationWhenInInv { get; set; }


        public ObjectAndCompleteSentence[] ofcObjectsYouCanUseWithIt { get; set; }

        public Dictionary<string, CombineExplanationDataClient> ofcCombineExplanationsByTarget { get; set; }
        public bool ofcDefaultCombineRequiresExplanation { get; set; }
        public bool ofcKeepExplanationInCasual { get; set; }
        public ExplanationClient[] ofcDefaultCombineExplanations { get; set; }

        // Exact use-for overrides.  The value says whether the exact
        // (object, objective) handler requires an explanation.  If an
        // objective is absent, the client uses the objective-level fallback.
        public Dictionary<string, bool> ofcUseForExactExplanationByObjective { get; set; }
        public Dictionary<string, UseForExplanationDataClient> ofcUseForExplanationsByObjective { get; set; }

        public ManualCoords ofcManualCoords { get; set; }


        public string ofcCustomInvIcon { get; set; }

        public string ofcCustomExplanationsIntro { get; set; }

        public ObjForClient(LogicObj lo, XDocIndexed xdi)
        {

            ofcUseForExactExplanationByObjective = lo.wo.useForHandlers
                    .Where(handler => handler.Lo == lo)
                    .GroupBy(handler => handler.Objective.serId)
                    .ToDictionary(group => group.Key, group => group.Single().Explanation != null);

            ofcUseForExplanationsByObjective = lo.wo.getActiveUseForExplanationContexts(lo)
                .GroupBy(context => context.Objective.serId)
                .ToDictionary(group => group.Key, group =>
                {
                    var context = group.First();
                    var intro = context.CustomExplanationIntro
                        ?? lo.wo.getExplanationGroupIntro(context.Group.FirstOrDefault());
                    return new UseForExplanationDataClient
                    {
                        ufeExplanations = BuildExplanationClients(lo, context.Group, xdi, null),
                        ufeCustomExplanationIntro = intro == null
                            ? null
                            : lo.wo.translateDialogOrNarOrAnnotated(intro, xdi)
                    };
                });

            ofcIsExit = lo.IsExit;

            if (lo.VerbWhenUseForInDialogIntro.is_not_null_or_white())
            {
                ofcVerbWhenUseForInDialogIntro = lo.wo.translateDialogOrNarOrAnnotated(lo.VerbWhenUseForInDialogIntro, xdi);
            }

            var customExplanationIntro = lo.CustomExplanationsIntro
                ?? (lo.CustomExplanations?.FirstOrDefault() is Explanation explanation
                    ? lo.wo.getExplanationGroupIntro(explanation)
                    : null);
            if (customExplanationIntro.is_not_null_or_white())
            {
                ofcCustomExplanationsIntro = lo.wo.translateDialogOrNarOrAnnotated(customExplanationIntro, xdi);
            }
            if (lo.CustomExplanations != null)
            {
                ofcCustomExplanations = lo.CustomExplanations
                        .Where(ex => lo.wo.explanationIsVisible(ex))
                        // filtra
                        .Select(ex => new ExplanationClient(ex.expId, lo.wo.translateDialogOrNarOrAnnotated(ex.exName, xdi))).ToArray();
            }

            ofcIsLookableNow = lo.wo.lookHandlers

                    .Where(ha => ha.IsLookableNow == null || ha.IsLookableNow())
                    .Any(ha => ha.lo1 == lo);

            var ofcCustomSentenceLookUntransl = lo.wo.lookHandlers
                    .Where(ha => ha.lo1 == lo)
                    .Where(ha => ha.IsLookableNow == null || ha.IsLookableNow())
                    .Where(ha => ha.DynamicSentence != null)
                    .Select(ha => ha.DynamicSentence()).FirstOrDefault();

            if (ofcCustomSentenceLookUntransl != null)
            {
                ofcCustomSentenceLook = lo.wo.translateDialogOrNarOrAnnotated(ofcCustomSentenceLookUntransl, xdi);
            }

            var ofcCustomSentenceUseHereUntransl = lo.wo.useHereHandlers
                    .Where(ha => ha.lo1 == lo)
                    .Where(ha => ha.DynamicSentence != null)
                    .Select(ha => ha.DynamicSentence())
                    .FirstOrDefault();

            if (ofcCustomSentenceUseHereUntransl != null)
            {
                ofcCustomSentenceUseHere = lo.wo.translateDialogOrNarOrAnnotated(ofcCustomSentenceUseHereUntransl, xdi);
            }

            //if (lo.loId == "quadroConScenaTropicale")
            //{
            //        var y = 4;
            //}
            ofcIsPickableNow = lo.wo.pickUpHandlers.Any(ha => ha.lo1 == lo);

            ofcCustomInvIcon = lo.CustomInvIcon;

            ofcObjectsYouCanUseWithIt = lo.wo.combineHandlers.Where(ha => ha.lo2 == lo)

                    .Where(ha => ha.IsPossibleNow == null || ha.IsPossibleNow())
                    .Select(ha =>
                            {
                                string sentenceUntransl;
                                if (ha.DynamicSentence != null)
                                {
                                    sentenceUntransl = ha.DynamicSentence();
                                }
                                else
                                {
                                    sentenceUntransl = ha.SentenceUntransl;
                                }

                                return new ObjectAndCompleteSentence
                                {
                                    ocsLoId = ha.lo1.loId,
                                    ocsCompleteSentence = lo.wo.translateDialogOrNarOrAnnotated(sentenceUntransl, xdi),
                                    ocsRequiresExplanation = ha.Explanation != null
                                };
                    }).ToArray();

            var combineHandlersForThisObject = lo.wo.getCombineHandlersForFirst(lo);
            ofcKeepExplanationInCasual = lo.wo.CasualModeKeepsExplanation(lo, null);
            var combineExplanationFamily = lo.wo.getCombineExplanationFamily(lo);
            var fallbackFirstObjects = combineExplanationFamily.Length > 0
                    ? combineExplanationFamily
                    : new[] { lo };
            var fallbackHandlers = fallbackFirstObjects
                    .SelectMany(lo.wo.getCombineHandlersForFirst)
                    .ToList();
            ofcDefaultCombineRequiresExplanation = !fallbackHandlers.Any()
                    || fallbackHandlers.Any(ha => ha.Explanation != null);
            var defaultExplanationGroup = lo.CustomExplanations
                    ?? lo.wo.getGlobalExplanations();
            ofcDefaultCombineExplanations = BuildExplanationClients(lo, defaultExplanationGroup, xdi, null);

            var activeCombineExplanationContexts = lo.wo.getActiveCombineExplanationContexts(lo).ToList();

            ofcCombineExplanationsByTarget = combineHandlersForThisObject
                    .ToDictionary(ha => ha.lo2.loId, ha =>
                    {
                        var matchingContext = ha.Explanation == null
                            ? null
                            : activeCombineExplanationContexts.FirstOrDefault(context =>
                                context.Target == ha.lo2 && context.Group.Contains(ha.Explanation));

                        return new CombineExplanationDataClient
                        {
                            cedRequiresExplanation = ha.Explanation != null,
                            cedRequiredExplanationIsVisible = ha.Explanation == null
                                || lo.wo.explanationIsVisible(ha.Explanation),
                            cedKeepExplanationInCasual = lo.wo.CasualModeKeepsExplanation(lo, ha.lo2),
                            cedIsExactHandler = true,
                            cedExplanations = matchingContext != null
                                ? BuildExplanationClients(lo, matchingContext.Group, xdi, ha.Explanation, ignoreGeneratedObjectExclusions: true)
                                : BuildCombineExplanations(lo, ha, xdi),
                            cedCustomExplanationIntro = (matchingContext?.CustomExplanationIntro
                                    ?? (ha.Explanation == null
                                        ? null
                                        : lo.wo.getExplanationGroupIntro(ha.Explanation))) == null
                                ? null
                                : lo.wo.translateDialogOrNarOrAnnotated(
                                    matchingContext?.CustomExplanationIntro
                                        ?? (ha.Explanation == null
                                            ? null
                                            : lo.wo.getExplanationGroupIntro(ha.Explanation)), xdi)
                        };
                    });

            foreach (var contextRule in activeCombineExplanationContexts)
            {
                if (ofcCombineExplanationsByTarget.ContainsKey(contextRule.Target.loId))
                {
                    continue;
                }

                ofcCombineExplanationsByTarget[contextRule.Target.loId] = new CombineExplanationDataClient
                {
                    cedRequiresExplanation = ofcDefaultCombineRequiresExplanation,
                    cedKeepExplanationInCasual = lo.wo.CasualModeKeepsExplanation(lo, contextRule.Target),
                    cedIsExactHandler = false,
                    cedExplanations = BuildExplanationClients(lo, contextRule.Group, xdi, null, ignoreGeneratedObjectExclusions: true),
                    cedCustomExplanationIntro = (contextRule.CustomExplanationIntro
                            ?? lo.wo.getExplanationGroupIntro(contextRule.Group.FirstOrDefault())) == null
                        ? null
                        : lo.wo.translateDialogOrNarOrAnnotated(
                            contextRule.CustomExplanationIntro
                                ?? lo.wo.getExplanationGroupIntro(contextRule.Group.FirstOrDefault()), xdi)
                };
            }

            ofcManualCoords = null; // lo.ManualCoords;

            ofcCompatibleTemplates = lo.wo.deduceHandlers.Where(ha => ha.lo == lo)
                    .Select(ha => new TemplateAndFillers { teId = ha.template.teId, fiIds = ha.fillers.Select(fi => fi.FilId).ToArray() })
                    .ToArray();

            if (lo.genderNumber == GenderNumber.He)
            {
                ofcGender = "he";
            }
            else if (lo.genderNumber == GenderNumber.It)
            {
                ofcGender = "it";
            }
            else if (lo.genderNumber == GenderNumber.She)
            {
                ofcGender = "she";
            }
            else if (lo.genderNumber == GenderNumber.They)
            {
                ofcGender = "they";
            }
            else
            {
                throw new NotImplementedException();
            }

            ofcTargetPossessiveForms = lo.wo.targetPossessiveForms(lo, xdi);

            //if (lo is Character ch)
            //{
            //        ofcCanTalkToCharacterNow = lo.wo.canTalkToCharacterNow(ch);
            //}
            //else
            //{
            //        ofcCanTalkToCharacterNow = false;
            //}

            //this.ofcAssociatedQtokens = ( from q in lo.associatedQToks select q.serId).ToArray();

            ofcHotspotPriority = lo.HotspotPriority;
            ofc_can_be_remembered = lo.wo.namedCutScenesSeen.Any(nc => nc.oggettiMenzionati.Contains(lo));

            if (lo.isInInvOfPartyMember(out Character cha))
            {
                ofc_name_with_in = $"{lo.dynamicNameTranslated(xdi, withThe: false, isForDialog: false)} ({lo.translatedInTheHandOf(xdi)})".inst(cha.dynamicNameTranslated(xdi, withThe: false, isForDialog: false));
                ofc_is_in_inv = true;
            }
            else
            {
                ofc_name_with_in = lo.dynamicNameTranslated(xdi, withThe: false, isForDialog: false);
                ofc_is_in_inv = false;

            }


            ofcIsConcept = lo.IsConcept;
            ofcIsConversationTopic = lo.IsConversationTopic;

            ofcCanBeUsedAsTargetInTextMode = lo.canBeUsedAsTargetInTextMode;


            if (lo.VerbWhenUseWithAsFirstObjectOnHoverNotSelected.is_not_null_or_white())
            {
                ofcVerbWhenUseWithAsFirstObjectOnHoverNotSelected = lo.wo.translateDialogOrNarOrAnnotated(lo.VerbWhenUseWithAsFirstObjectOnHoverNotSelected, xdi);
            }

            if (lo.VerbWhenUseWithAsFirstObjectSelectedWithPlaceHolder.is_not_null_or_white())
            {
                ofcVerbWhenUseWithAsFirstObjectSelectedWithPlaceHolder = lo.wo.translateDialogOrNarOrAnnotated(lo.VerbWhenUseWithAsFirstObjectSelectedWithPlaceHolder, xdi);
            }

            if (lo.VerbWhenUseWithAsFirstObjectSelectedWithPlaceHolderOnHoverSecond.is_not_null_or_white())
            {
                ofcVerbWhenUseWithAsFirstObjectSelectedWithPlaceHolderOnHoverSecond = lo.wo.translateDialogOrNarOrAnnotated(lo.VerbWhenUseWithAsFirstObjectSelectedWithPlaceHolderOnHoverSecond, xdi);
            }

            loId = lo.loId;
            ofc_name = lo.dynamicNameTranslated(xdi, withThe: false, isForDialog: false);


            ofcNameWithArticle = lo.dynamicNameTranslated(xdi, withThe: true, isForDialog: false);
            if (ofcNameWithArticle == null)
            {
                if (lo.shortNameWithDet.is_not_null_or_white())
                {
                    ofcNameWithArticle = lo.wo.translateDialogOrNarOrAnnotated(lo.shortNameWithDet, xdi);
                }


            }
            ofcMustBeShownInTextRoomRecap = !lo.isInCurParty() && !lo.onlyInGraphics;
            //this.ofcUseMode = (int ) lo.useWith;
            is_obvious_exit = false;
            ofc_is_character = lo is Character;
            ofcimagePortrait = lo.calcImgPortrait();

            ofcIsInCurParty = lo.isInCurParty();

            //if (lo.HoverActionWhenInRoom == HoverActionWhenInRoom.LookAndWorkAsTarget)
            //{
            //        if (lo.wo.lookHandlers.Any(ha=> ha.lo1 == lo))
            //        {
            //                var lookTranslated = lo.wo.translateDialogOrNarOrAnnotated("guarda {1}");
            //                this.ofcHoverStringWhenInRoom = lookTranslated;
            //                this.ofcVerbIdWhenInRoom = "look";
            //        }
            //        else
            //        {
            //                this.ofcHoverStringWhenInRoom = "{1}"; // solo nome oggetto
            //                this.ofcVerbIdWhenInRoom = null;
            //        }
            //}
            //else


            //if (lo.HoverActionWhenInRoom == HoverActionWhenInRoom.UseHere)
            //{
            //        string str;
            //        if (lo.VerbWhenUseHere.is_not_null_or_white())
            //        {
            //                var VerbWhenUseHereInRoomTransl = lo.wo.translateDialogOrNarOrAnnotated(lo.VerbWhenUseHere, xdi);
            //                str = VerbWhenUseHereInRoomTransl;
            //        }
            //        else
            //        {
            //                str = lo.wo.translateDialogOrNarOrAnnotated("usa {1}".translatable(), xdi);
            //        }

            //        ofcHoverStringWhenInRoom = str;
            //        ofcVerbIdWhenInRoom = "useHere";
            //}
            //else if (lo.HoverActionWhenInRoom == HoverActionWhenInRoom.IsActually)
            //{

            //        ofcHoverStringWhenInRoom = lo.wo.translateDialogOrNarOrAnnotated("deduci qualcosa su {1}".translatable(), xdi);
            //        ofcVerbIdWhenInRoom = "isActually";
            //}
            ////else if (lo.HoverActionWhenInRoom == HoverActionWhenInRoom.Deduce)
            ////{
            ////        var lookTranslated = lo.wo.translateDialogOrNarOrAnnotated("deduci qualcosa su {1}");
            ////        this.ofcHoverStringWhenInRoom = lookTranslated;
            ////        this.ofcVerbIdWhenInRoom = "useFor";
            ////}
            //else


            //if (lo.HoverActionWhenInRoom == HoverActionWhenInRoom.ShowMap)
            //{

            //        string str;
            //        if (lo.VerbWhenUseHere.is_not_null_or_white())
            //        {
            //                var VerbWhenUseHereInRoomTransl = lo.wo.translateDialogOrNarOrAnnotated(lo.VerbWhenUseHere,xdi );
            //                str = VerbWhenUseHereInRoomTransl;
            //        }
            //        else
            //        {
            //                str = lo.wo.translateDialogOrNarOrAnnotated("esci".translatable(), xdi);
            //        }


            //        ofcHoverStringWhenInRoom = str;
            //        ofcVerbIdWhenInRoom = "showMap";
            //}
            //else if (lo.HoverActionWhenInRoom == HoverActionWhenInRoom.Nothing)
            {

                {
                    ofcHoverStringWhenInRoom = "{1}"; // solo nome oggetto
                }
                //ofcVerbIdWhenInRoom = "showContextMenu";


                if (lo.UseKindWhenInRoom == UseKindForRoomObjects.UseFor)
                {
                    ofcContextMenuUseForOrHereOrDeduce = "useFor";
                }
                else if (lo.UseKindWhenInRoom == UseKindForRoomObjects.UseHere)
                {
                    ofcContextMenuUseForOrHereOrDeduce = "useHere";
                }
                else if (lo.UseKindWhenInRoom == UseKindForRoomObjects.Deduce)
                {
                    ofcContextMenuUseForOrHereOrDeduce = "deduce";
                }
                else if (lo.UseKindWhenInRoom == UseKindForRoomObjects.Nothing)
                {
                    ofcContextMenuUseForOrHereOrDeduce = "nothing";
                    //ofcVerbIdWhenInRoom = null; // non deve proprio aprire il context menu
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            //else if (lo.HoverActionWhenInRoom == HoverActionWhenInRoom.UseFor)
            //{

            //        if (lo.VerbWhenUseForOnHover.is_not_null_or_white())
            //        {
            //                ofcHoverStringWhenInRoom = lo.wo.translateDialogOrNarOrAnnotated(lo.VerbWhenUseForOnHover, xdi);
            //        }
            //        else { 
            //                ofcHoverStringWhenInRoom = lo.wo.translateDialogOrNarOrAnnotated( "usa {1}".translatable(), xdi); 
            //        }
            //        ofcVerbIdWhenInRoom = "useFor";
            //}
            //else
            //{
            //        throw new NotImplementedException();
            //}

            //todo parla di legna da spaccare con la gente nel percorso della corsa e al mercato e in piazza

            //if (lo.VerbWhenUseWithAsFirstObjectOnHoverNotSelected.is_not_null_or_white())
            //{
            //        ofcHoverStringWhenInInv = lo.wo.translateDialogOrNarOrAnnotated(lo.VerbWhenUseWithAsFirstObjectOnHoverNotSelected).Replace("{1}", lo.dynamicNameTranslated(xdi, withArticle: false));
            //        ofcIsUseWithWhenInInv = true;
            //        ofcIsUseInLocationWhenInInv = false;
            //}
            //else 
            if (lo.HoverActionWhenInInv == HoverActionWhenInInv.UseHere)
            {

                string str;
                if (lo.VerbWhenUseHere.is_not_null_or_white())
                {
                    var VerbWhenUseHereInRoomTransl = lo.wo.translateDialogOrNarOrAnnotated(lo.VerbWhenUseHere, xdi);
                    str = VerbWhenUseHereInRoomTransl;
                }
                else
                {
                    str = lo.wo.translateDialogOrNarOrAnnotated("usa {1}".translatable(), xdi);
                }


                ofcHoverStringWhenInInv = str;
                ofcIsUseWithWhenInInv = false;
                ofcIsUseInLocationWhenInInv = true;
            }
            else if (lo.HoverActionWhenInInv == HoverActionWhenInInv.UseWith)
            {
                //if (lo.IsConcept)
                //{
                //        ofcHoverStringWhenInInv = lo.wo.translateDialogOrNarOrAnnotated("trova un collegamento tra ''{1}'' e ...".translatable());
                //}
                //else
                {
                    ofcHoverStringWhenInInv = lo.wo.translateDialogOrNarOrAnnotated("usa {1} con...".translatable(), xdi);
                }
                ofcIsUseWithWhenInInv = true;
                ofcIsUseInLocationWhenInInv = false;
            }
            else if (lo.HoverActionWhenInInv == HoverActionWhenInInv.UseFor)
            {

                //if (lo.IsConcept)
                //{
                //        ofcHoverStringWhenInInv = lo.wo.translateDialogOrNarOrAnnotated("trova un collegamento tra ''{1}'' e ...".translatable());
                //}
                //else
                {
                    ofcHoverStringWhenInInv = lo.wo.translateDialogOrNarOrAnnotated("usa {1} per...".translatable(), xdi);
                }
                ofcIsUseWithWhenInInv = false;
                ofcIsUseInLocationWhenInInv = false;
            }

            //else if (lo.HoverActionWhenInInv == HoverActionWhenInInv.Deduce)
            //{
            //        ofcHoverStringWhenInInv = lo.wo.translateDialogOrNarOrAnnotated("deduci qualcosa su {1}");
            //        ofcIsUseWithWhenInInv = false;
            //        ofcIsUseInLocationWhenInInv = false;
            //}
            else
            {
                throw new NotImplementedException();
            }
            //this.ofcFailureContinuations = lo.failureContinuations.Select(fa => fa.serId).ToArray();
            //ofcCannotBeUsed = lo.cannotBeUsed;
            //ofcNameMustAppearInGraphics = lo.nameMustAppearInGraphics;
        }

        private static ExplanationClient[] BuildCombineExplanations(LogicObj first, CombineHandler handler, XDocIndexed xdi)
        {
            if (handler.Explanation == null)
            {
                return Array.Empty<ExplanationClient>();
            }

            var group = first.CustomExplanations
                    ?? first.wo.getExplanationGroup(handler.Explanation)
                    ?? new[] { handler.Explanation };
            return BuildExplanationClients(first, group, xdi, handler.Explanation);
        }

        private static ExplanationClient[] BuildExplanationClients(
                LogicObj first,
                Explanation[] group,
                XDocIndexed xdi,
                Explanation requiredExplanation,
                bool ignoreGeneratedObjectExclusions = false)
        {
            var excluded = ignoreGeneratedObjectExclusions
                ? first.wo.explicitExplanationExclusionsOfLo(first.loId)
                : first.wo.explanationsToExcludeOfLo.itemOrEmpty(first.loId).ToHashSet();
            var visible = group
                    .Where(ex => first.wo.explanationIsVisible(ex));
            visible = visible.Where(ex => !excluded.Contains(ex.expId));
            var visibleList = visible
                    .ToList();

            if (visibleList.Count > 6)
            {
                visibleList = visibleList
                        .Where(ex => requiredExplanation != null && ex == requiredExplanation)
                        .Concat(visibleList.Where(ex => requiredExplanation == null || ex != requiredExplanation))
                        .Take(6)
                        .ToList();
            }

            return visibleList
                    .Select(ex => new ExplanationClient(
                            ex.expId,
                            first.wo.translateDialogOrNarOrAnnotated(ex.exName, xdi)))
                    .ToArray();
        }

        //public ObjForClient(/*bool ofcUseInLocation, */bool ofc_is_in_inv, bool ofc_can_be_remembered, string ofc_name_with_in, string loId, string ofc_name, int ofcUseMode, bool is_obvious_exit, bool ofc_is_character/*, bool ofcCouldPotentiallyBePickedUp*/,
        //        string[] failureContinuationsQtokIds, bool cannotBeUsed, bool nameMustAppearInGraphics, string ofcimagePortrait)
        //{
        //        //this.ofcCouldPotentiallyBePickedUp = ofcCouldPotentiallyBePickedUp;
        //        //this.ofcUseInLocation = ofcUseInLocation;
        //        this.ofc_is_in_inv = ofc_is_in_inv;
        //        this.ofc_can_be_remembered = ofc_can_be_remembered;
        //        this.ofc_name_with_in = ofc_name_with_in;
        //        this.loId = loId;
        //        this.ofc_name = ofc_name;
        //        this.ofcUseMode = ofcUseMode;
        //        this.is_obvious_exit = is_obvious_exit;
        //        this.ofc_is_character = ofc_is_character;
        //        this.ofcimagePortrait = ofcimagePortrait;
        //        this.ofcFailureContinuations = failureContinuationsQtokIds;
        //        ofcCannotBeUsed = cannotBeUsed;
        //        ofcNameMustAppearInGraphics = nameMustAppearInGraphics;
        //        //this.autoUseVerbWithId = autoUseVerbWithId;
        //}
    }


    //public class parHtmlClient
    //{
    //    public List<parHtmlElClient> elements = new List<parHtmlElClient>();


    //    public string textOnly()
    //    {
    //        var x = new List<string>();
    //        foreach (var el in elements)
    //        {
    //            x.Add(el.textOnlyVersion());
    //        }

    //        return x.Aggregate((a, b) => a + b);
    //    }

    //    //var r = MakeClickableRun(nomeOgg);

    //    ////r.Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 155));


    //    //r.PreviewMouseLeftButtonDown +=  (o, args) =>
    //    //{
    //    //    args.Handled = true;
    //    //    var lo = pairs.Where(pa => pa.pos == pos).Select(pa => pa.lo).Single();
    //    //    Debug.Assert(lo != null);
    //    //    if (qualcheTaskStaAspettandoClicSecondoOggetto != null)
    //    //    {
    //    //        qualcheTaskStaAspettandoClicSecondoOggetto.TrySetResult(lo);
    //    //    }
    //    //    else
    //    //    {
    //    //        // era il clic sul primo ogg
    //    //         ShowVerbMenuForObject(args, lo);
    //    //    }
    //    //};
    //    //return ret;

    //}

}
