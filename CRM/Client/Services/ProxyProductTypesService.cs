using CRM.Client.Helpers;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Models;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    
    public class ProxyProductTypesService: ProxyRestClientService<ProductType, ProductTypeDTO, int, ProductTypeFilter, object>, IProductTypesService
    {
        public ProxyProductTypesService(HttpClient http) : base(http, ConstHelper.ProductsTypesPath)
        {

        }

    }
}
