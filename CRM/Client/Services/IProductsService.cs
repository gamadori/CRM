using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CRM.Shared;
using CRM.Shared.DTOs;

namespace CRM.Client.Services
{
    
    public interface IProductsService : IDataService<Product, ProductDTO, int, ProductFilter, object>
    {
       
    }
}
