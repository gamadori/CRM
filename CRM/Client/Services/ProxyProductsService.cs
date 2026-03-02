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
    
    public class ProxyProductsService: ProxyRestClientService<Product, ProductDTO, int, ProductFilter, object>, IProductsService
    {
        public ProxyProductsService(HttpClient http) : base(http, ConstHelper.Products)
        {

        }

    }
}
