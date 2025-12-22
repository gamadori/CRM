using CRM.Shared;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Linq.Dynamic.Core;

namespace CRM.Server.Helpers
{
    public class PagingHelper
    {
        public static void ResponsePaging<T, F>(HttpContext context, IQueryable<T> items, F args) where F: PagingParameterModel
        {
            int count = items.Count();
            

            if (args != null)
            {


                if (args.Filter != null)
                {

                    items = items.Where(args.Filter);
                }
                count = items.Count();

                if (args.OrderBy != null)
                    items = items.OrderBy(args.OrderBy);

                if (args.Skip != null && args.Top != null)
                {
                    items = items.Skip(args.Skip.Value).Take(args.Top.Value);
                }                

            }
            else
                count = items.Count();


            
          
            var paginationMetadata = new PagingHeaderModel()
            {
                TotalCount = count,
                TotalPage = 1,
                PageSize = 0
            };
            context.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));
        }
    }
}
