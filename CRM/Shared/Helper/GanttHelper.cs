using CRM.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CRM.Shared.Helper
{
    public static class GanttHelper
    {
        public static TaskDependency GetDependency(string s)
        {
            TypesDependency t;
            TaskDependency model = new TaskDependency();

           var v = Regex.Match(s, "\\d+").Value;

            if (int.TryParse(v, out int n))
            {
                model.Id = n;

                v = s.Substring(v.Length);

                if (Enum.TryParse(v, out t))
                {
                    model.Type = t;

                    return model;
                }
            }
            return null;
        }

        public static int GetDuration(string duration)
        {
            var v = Regex.Match(duration, "\\d+").Value;

            if (int.TryParse(v, out int n))
            {
                return n;
            }
            else
                return 0;
        }
    }
}
