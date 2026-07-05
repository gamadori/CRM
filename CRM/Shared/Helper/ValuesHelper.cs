using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared.Helper
{
    public enum CompanyViews
    {
        Company,
        Users,
        Articles,
        Ticket,
        Contacts,
        Contracts,
        Customers,
        Activities
    }

    public enum CSVTable
    {
        Company,
        Category,
        Article
    }
    public enum ProductTypeViews
    {
        ProductTypes,
        Products,
        Ticket
    }

    public static class ValuesHelper
    {
        

        public const string BreadcrumbHeader = "Breadcrumb-Header";
    }
}
