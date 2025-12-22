using CRM.Shared;
using Microsoft.AspNetCore.Mvc;
using QLNet;
using Syncfusion.Blazor;
using System.Reflection;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AboutController : ControllerBase
    {
        // GET: api/<AboutController>
        [HttpGet]
        public AboutModel Get()
        {
            AboutModel model = new AboutModel();
            model.Version = GetInformationalVersion();
            model.Name = "CRM";  //GetName();
            model.Description = "Customer Relationship Management"; //GetDescription();
            model.Date = new DateTime(2024, 4, 26);
            return model;
        }

        //public static string? GetInformationalVersion() =>
        //Assembly
        //    .GetEntryAssembly()
        //    ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        //    ?.InformationalVersion;

        public static string? GetInformationalVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString();

        public static string? GetName() =>
            Assembly.GetExecutingAssembly().GetName().Name;

        public static string? GetDescription()
        {
            var asm = Assembly.GetExecutingAssembly();
            return ((AssemblyProductAttribute)asm.GetCustomAttributes(typeof(AssemblyProductAttribute)).First()).Product;
        }

    }
}
