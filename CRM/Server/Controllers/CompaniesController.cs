using CNM.Authorize;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Client.Shared;
using CRM.Server.Data;
using CRM.Server.Helpers;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Helper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;



namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class CompaniesController : ControllerBase
    {

        private readonly IPermitsService _permitsService;
        private readonly ILogEventService _logEventService;
        private readonly ICompaniesService _companiesService;

        public CompaniesController(IPermitsService permitsService, ILogEventService logEventService, ICompaniesService companiesService)
        {
            _permitsService = permitsService;
            _logEventService = logEventService;
            _companiesService = companiesService;
        }


        [HttpGet]
        public async Task<PagingResponse<CompanyDTO>?> GetPage([FromQuery] CompanyFilter? args = null)
        {
            try
            {
                var items = await _companiesService.GetPagingAsync(args);
                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CompaniesController), nameof(GetPage), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }
        [HttpGet("list")]
        public async Task<IEnumerable<CompanyDTO>?> GetItems([FromQuery] CompanyFilter? args = null)
        {
            try
            {
                var items = await _companiesService.GetListAsync(args);
                if (items == null)
                {
                    return Enumerable.Empty<CompanyDTO>();
                }
                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CompaniesController), nameof(GetItems), LogEvent.EventsTypes.Error, ex);
                return Enumerable.Empty<CompanyDTO>();
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CompanyDTO?>> GetItem(int id)
        {
            try
            {
                var item = await _companiesService.GetItemAsync(id);
                if (item == null)
                {
                    return NotFound();
                }
                return Ok(item);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CompaniesController), nameof(GetItem), LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>Azienda madre attuale: serve alla UI per avvisare prima di cambiarla.</summary>
        [HttpGet("headcompany")]
        public async Task<ActionResult<CompanyDTO?>> GetHeadCompany()
        {
            return Ok(await _companiesService.GetHeadCompanyAsync());
        }

        [AuthorizeRole(ePolicy.StandardRole)]
        [HttpPut("{id}")]
        public async Task<ActionResult<APIResponseMessage<CompanyDTO>>> Put(int id, Company item)
        {
            if (id != item.Id)
            {
                return BadRequest();
            }
            var resp = await _companiesService.PostAsync(item);

            if (resp == null)
                return Problem("Error saving company");

            return Ok(resp);
        }

        [AuthorizeRole(ePolicy.StandardRole)]
        [HttpPost]
        public async Task<ActionResult<APIResponseMessage<CompanyDTO>>> Post(Company item)
        {
            var resp = await _companiesService.PostAsync(item);

            if (resp == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Post return null");

            return Ok(resp);
        }

        [AuthorizeRole(ePolicy.StandardRole)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var resp = await _companiesService.DeleteAsync(id);

            if (!resp)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Error on deleting Company");
            }
            else
                return NoContent();
        }


        // GET: api/Companies/5
        [HttpGet("user")]
        public async Task<ActionResult<Company>> GetUserCompany()
        {
            var company = await _companiesService.GetUserCompany();
            if (company == null)
            {
                return NotFound();
            }
            else
                return Ok(company);

        }



        [AuthorizeRole(ePolicy.StandardRole)]
        [HttpPost("removecustomer")]
        public async Task<IActionResult> RemoveCustomer(CustomerModel item)
        {
            var resp = await _companiesService.RemoveCustomer(item);

            if (resp)
                return NoContent();
            else
                return BadRequest();

        }

        [AuthorizeRole(ePolicy.StandardRole)]
        [HttpPost("addcustomer")]
        public async Task<IActionResult> AddCustomer(CustomerModel item)
        {
            var resp = await _companiesService.AddCustomer(item);

            if (resp)
                return NoContent();
            else
                return BadRequest();

        }




        [HttpGet("emailaddresses/{idCompany}")]
        public async Task<ActionResult<IEnumerable<string>>> GetEmailAddress(int idCompany)
        {

            var emailAddresses = await _companiesService.GetEmailAddress(idCompany);
            if (emailAddresses == null || !emailAddresses.Any())
            {
                return NotFound();
            }
            else
                return Ok(emailAddresses);
        }

        [HttpGet("logo/{idCompany}")]
        public async Task<ActionResult<string>> GetLogo(int idCompany)
        {

            var logo = await _companiesService.GetLogo(idCompany);
            if (logo == null)
            {
                return Ok("");
            }
            else
                return Ok(logo);
        }

        [HttpGet("tree")]
        public async Task<ActionResult<List<CompanyTreeNodeDTO>>> GetTree([FromQuery] int? idCompany = null)
        {
            try
            {
                var tree = await _companiesService.GetTreeAsync(idCompany);
                return Ok(tree);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CompaniesController), nameof(GetTree), LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
