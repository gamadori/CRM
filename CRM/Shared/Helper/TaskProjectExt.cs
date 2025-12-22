using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared.Helper
{
    public static class TaskProjectExt
    {
        public static List<TaskData> ToTaskDataList(this List<TaskProject> items)
        {
            List<TaskData> list = new List<TaskData>();

           
            foreach (var item in items)
            {
                list.Add(new TaskData(item));
            }
            return list;
        }
    }
}
