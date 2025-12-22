using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared.Helper
{
    public static class AvatarsHelper
    {
        public static string AvatarTxt(string surname, string name)
        {
            string txt = "";

            if (surname != null && surname.Length > 0)
            {
                txt = surname.Substring(0, 1);
            }

            if (name != null && name.Length > 0)
            {
                txt += name.Substring(0, 1);
            }
            return txt;
        }
    }
}
