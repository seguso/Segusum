using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Seg
{
        /// <summary>
        /// The action that has just been executed. The concrete record identifies
        /// the action shape and carries all of its operands.
        /// </summary>
        public abstract record ActionContext
        {
                public virtual bool IsMove => false;

                public virtual bool WasPerformedOn(LogicObj target) => false;
        }

        public sealed record CombineActionContext(
                LogicObj Tool,
                LogicObj Target,
                Explanation? Explanation) : ActionContext
        {
                public override bool WasPerformedOn(LogicObj target) => Target == target;
        }

        public sealed record UseForActionContext(
                LogicObj Object,
                Objective Objective,
                Explanation? Explanation) : ActionContext
        {
                public override bool WasPerformedOn(LogicObj target) => Object == target;
        }

        public sealed record IsActuallyActionContext(
                LogicObj Object,
                Explanation Explanation1,
                Explanation Explanation2) : ActionContext
        {
                public override bool WasPerformedOn(LogicObj target) => Object == target;
        }

        public sealed record UseInComposerActionContext(
                LogicObj Object,
                cinComposer[] Parts,
                Template Template,
                Filler Filler1,
                Filler Filler2) : ActionContext
        {
                public override bool WasPerformedOn(LogicObj target) => Object == target;
        }

        public sealed record PickUpActionContext(LogicObj Object) : ActionContext
        {
                public override bool WasPerformedOn(LogicObj target) => Object == target;
        }

        public sealed record UseHereActionContext(LogicObj Object) : ActionContext
        {
                public override bool WasPerformedOn(LogicObj target) => Object == target;
        }

        public sealed record CancelTextInputActionContext(TextInput TextInput) : ActionContext;

        public sealed record SubmitTextInputActionContext(
                TextInput TextInput,
                string ChosenText,
                string ChosenText2,
                string ChosenExplanationId) : ActionContext;

        public sealed record LookActionContext(LogicObj Object) : ActionContext
        {
                public override bool WasPerformedOn(LogicObj target) => Object == target;
        }

        public sealed record MoveActionContext(Room From, Room To) : ActionContext
        {
                public override bool IsMove => true;
        }

        public sealed record TalkHereActionContext(Room Room) : ActionContext;

        public class HandlerInput
        {
                //public List<cutSceneToken> cs = new List<cutSceneToken>();
                public bool timeMustAdvance = true;

                public Dialog dialogToStart = null;

                public TextInput textInputToShow = null;

                //public bool giveGenericErrorMessage = false;

                public bool? makesNoSenseAtThisTime = null;

                public bool gameFinished { get; set; } = false;
        }

        public class PickUpHandlerInput : HandlerInput
        {
                
                // non serve perche' il pickup non è mai automatico, nell'handler devi scrivere olivia.pickup object
                //public bool cancelPickup= false;

                public bool dontSayDefaultTextIfCsEmpty = false;
        }

        public class TextHandlerInput : HandlerInput
        {
                public string chosenText;

                public string chosenText2;

                public string explSerId { get; set; }
        }
}
