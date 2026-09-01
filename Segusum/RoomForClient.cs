using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Seg
{

        public class RoomForClient
        {
                public List<ObjForClient> rfc_objects;
                public string rfc_img;


        public int rfc_bg_wt { get; set; }
        public int rfc_bg_ht { get; set; }
        public string rfcNameTextMode { get; set; }
        //public LayerForClient[] rfc_layers;

        public RoomForClient(List<ObjForClient> rfc_objects, string rfc_img, string nameTextMode, int rfc_bg_wt, int rfc_bg_ht)
        {
            this.rfc_objects = rfc_objects;
            this.rfc_img = rfc_img;
            rfcNameTextMode = nameTextMode;
            this.rfc_bg_wt = rfc_bg_wt;
            this.rfc_bg_ht = rfc_bg_ht;
            //this.rfc_layers = rfc_layers;
        }
    }
}
