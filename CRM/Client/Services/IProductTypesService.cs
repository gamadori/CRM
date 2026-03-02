using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CRM.Shared;
using CRM.Shared.DTOs;

namespace CRM.Client.Services
{
    
    public interface IProductTypesService : IDataService<ProductType, ProductTypeDTO, int, ProductTypeFilter, object>
    {
       
    }
}
