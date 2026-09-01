using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;



namespace Seg
{
    public static class UtilsW
    {

        
        public static string EuroOfDecimal(decimal d)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("fr-FR", false);
            var s = d.ToString("c");
            return s;
        }

        //public static string toJson(object o)
        //{
        //    var jsonSerializer = new System.Web.Script.Serialization.JavaScriptSerializer();
        //    string json = jsonSerializer.Serialize(o);
        //    return json;
        //}

        public static string stringOfException(Exception e)
        {
            try
            {
                return stringOfExceptionRec(e, "");
            }
            catch
            {
                return "errore in stringOfexception";
            }
        }


        public static bool ContieneSqlException(Exception e, int sqlExcNumber)
        {
            if (e == null)
                return false;
            else
            {
                if (e.GetType().Name == "SqlException")
                {
                    var number = e.GetType().GetProperty("Number")?.GetValue(e) as int?;
                    if (number == sqlExcNumber)
                    {
                        return true;

                    }
                    else
                    {
                        return ContieneSqlException(e.InnerException, sqlExcNumber);
                    }
                }
                else
                {
                    return ContieneSqlException(e.InnerException, sqlExcNumber);

                }
            }
        }

        public static string HashString(string pwd)
        {
            HashAlgorithm hash = new SHA256Managed();
            byte[] plainTextBytes = System.Text.Encoding.UTF8.GetBytes(pwd);
            byte[] hashBytes = hash.ComputeHash(plainTextBytes);

            //in this string you got the encrypted password
            string pwdHash = Convert.ToBase64String(hashBytes);
            return pwdHash;
        }


        public static string HashStringCortoCheSoloIlWebServicePuoFare(string s)
        {

            var sPepe = "stringa-che-solo-il-web-service-conosce-fkov89339--" + s;
            var hash = HashString(sPepe);
            if (hash.Length > 20)
                hash = hash.Substring(0, 19);
            return hash;
        }

        //public static utente Autentica(pagiEntities db, string email, string pwdHash)
        //{
        //    //int? idUtente = null;
        //    return (from us in db.utente
        //            where us.email == email
        //            where us.passwordHash == pwdHash
        //            where us.emailConfermata
        //            select us).FirstOrDefault();
        //    //return idUtente;
        //}
        private static string stringOfExceptionRec(Exception e, string curString)
        {
            if (e != null)
            {
                var sqlStr = CreaStringaSql(e);

                //var dataValid = CreaStringaValidazioneDati(e);


                var s = curString + "_______________" + e.GetType().ToString() + sqlStr 
                    //+ dataValid 
                    + "----" + (e.Message ?? "") + "--------" + e.StackTrace;
                return stringOfExceptionRec(e.InnerException, s);
            }
            else
            {

                return curString;
            }

        }

        //private static string CreaStringaValidazioneDati(Exception e)
        //{
        //    var dataValid = "";
        //    if (e is DbEntityValidationException)
        //    {
        //        var de = (DbEntityValidationException)e;

        //        var entry = de.EntityValidationErrors.First();
        //        var error = entry.ValidationErrors.First();
        //        dataValid = "------data validation = " + error.ErrorMessage + "-----------";
        //    }
        //    return dataValid;
        //}

        private static string CreaStringaSql(Exception e)
        {
            int? sqlNumber = null;
            if (e.GetType().Name == "SqlException")
            {
                sqlNumber = e.GetType().GetProperty("Number")?.GetValue(e) as int?;
            }
            var sqlStr = (sqlNumber == null ? "" : "------sql number = " + sqlNumber.Value + "----------");
            return sqlStr;
        }


    }
}
