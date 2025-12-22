using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface INavMenuService
    {
        event Action RefreshRequest;

        void CallRequestRefresh();
    }
}
