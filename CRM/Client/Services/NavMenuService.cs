using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class NavMenuService: INavMenuService
    {
        public event Action RefreshRequest;
        public void CallRequestRefresh()
        {
            RefreshRequest?.Invoke();
        }
    }
}
