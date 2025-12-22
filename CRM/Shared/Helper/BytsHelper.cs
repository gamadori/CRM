using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared.Helper
{
    public static class FileHelper
    {
        public static String FormatToHumanReadableFileSize(object value)
        {
            try
            {
                string[] suffixNames = { "bytes", "KB", "MB", "GB", "TB" };
                var counter = 0;
                decimal dValue = 0;

                Decimal.TryParse(value.ToString(), out dValue);

                while (Math.Round(dValue / 1024) >= 1 && counter < suffixNames.Length - 1)
                {
                    dValue /= 1024;
                    counter++;
                }

                return string.Format("{0:n1} {1}", dValue, suffixNames[counter]);
            }
            catch
            {
                //catch and handle the exception
                return string.Empty;
            }
        }
    }
}
