using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;

namespace CRM.Server.Helpers
{
    public static class SyncHelper
    {
        public static IQueryable<T> GetFilterPredicate<T>(IQueryable<T> data, string filter)
        {
            string predicate;

            if (filter != null)   //Filtering – Here you need to handle the filtering programmatically at server side, based on your Grid. 
            {

                var newfiltersplits = filter;

                var scripts = filter.Split("and");

                foreach (var script in scripts)
                {
                    string predicateCond;  // substringof
                    string predicateField;
                    string predicateValue;
                    var p = script;

                    if (p.StartsWith('(') && p.EndsWith(')'))
                    {
                        p = p.Substring(1, p.Length - 2);
                    }
                    var predicatesplits = p.Split('(', ')', ',', ' ');

                    if (p.StartsWith("startswith") || p.StartsWith("endswith"))
                    {

                        predicateCond = predicatesplits[0];  // substringof

                        if (predicateCond == "substringof")
                            predicateCond = "contains(@0)";
                        else
                            predicateCond += "(@0)";

                        predicateField = $"{predicatesplits[2]}.";
                        predicateValue = predicatesplits[4];
                    }
                    else if (p.StartsWith("substringof"))
                    {
                        predicateCond = predicatesplits[0];  // substringof

                        if (predicateCond == "substringof")
                            predicateCond = "contains(@0)";
                        else
                            predicateCond += "(@0)";

                        predicateField = $"{predicatesplits[3]}.";
                        predicateValue = predicatesplits[1];
                    }
                    else
                    {
                        predicateCond = predicatesplits[3];
                        if (predicateCond == "eq")
                            predicateCond = "== {0}";
                        else if (predicateCond == "ne")
                            predicateCond = "!= {0}";

                        predicateField = $"{predicatesplits[1]} ";
                        predicateValue = predicatesplits[4];

                    }
                    
                    predicate = $"{predicateField}{string.Format(predicateCond, "@0")}";

                    if (predicateValue.StartsWith("'") && predicateValue.EndsWith("'"))
                        predicateValue = predicateValue.Substring(1, predicateValue.Length - 2);

                    data = data.Where(predicate, new string[] { predicateValue });
                }
                
            }
            return data;
            
        }
    }
}
