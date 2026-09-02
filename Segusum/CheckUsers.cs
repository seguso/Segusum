using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
#pragma warning disable 219

namespace Seg
{
        [DataContract]
        internal class CheckUsers
        {
                [DataMember]
                public string A_Uname { get; set; }
                [DataMember]
                public DateTime? DateModified { get; set; }
                [DataMember]
                public string SavegameXml { get; set; }
                

                public CheckUsers(DateTime? dateModified, string savegameXml, string uname)
                {
                        DateModified = dateModified;
                        SavegameXml = savegameXml;
                        A_Uname = uname;
                }

                public CheckUsers()
                {
                }

                public override bool Equals(object obj)
                {
                        return obj is CheckUsers other &&
                               DateModified == other.DateModified &&
                               SavegameXml == other.SavegameXml &&
                               A_Uname == other.A_Uname;
                }

                public override int GetHashCode()
                {
                        int hashCode = -365753256;
                        hashCode = hashCode * -1521134295 + DateModified.GetHashCode();
                        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(SavegameXml);
                        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(A_Uname);
                        return hashCode;
                }
        }
}
