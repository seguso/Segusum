using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Seg
{

        public class Dialog
        {
                public bool askedQuestionsAreVisible = true;

                /// <summary>
                /// serve per serializzare e caricare lo stato visible/invisible delle domande del dialogo.
                /// </summary>
                public string id;

                public List<Question> questions;

                //public virtual void serialize(XElement xelDialog)
                //{
                //    foreach(var q in questions)
                //    {
                //        var xelq = new XElement("question");
                //        xelDialog.Add(xelq);


                //        xelq.Add(new XAttribute("id", q.id));
                //        xelq.Add(new XAttribute("isVisible", q.isVisible));
                //    }

                //}

                //public virtual void deserialize(XElement xelDialog)
                //{
                //    foreach (var qel in xelDialog.Elements("question"))
                //    {
                //        var questionId = qel.Attribute("id").Value;
                //        var isVisible = Boolean.Parse( qel.Attribute("isVisible").Value);

                //        var question = questions.Single(q => q.id == questionId);
                //        question.isVisible = isVisible;

                //    }

                //}


        }
}
