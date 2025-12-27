using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Services;
using CRM.Shared;
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
    [Route("api/[controller]")]
    [ApiController]
    public class LanguagesController : ControllerBase
    {
        private readonly ILogEventService _logEventService;
        private readonly ILanguagesService _languagesService;
        public LanguagesController(ILogEventService logEventService, ILanguagesService languagesService)
        {
            _logEventService = logEventService;
            _languagesService = languagesService;
        }

        // GET: api/Companies
        [HttpGet]
        public async Task<PagingResponse<Language>?> GetPage([FromQuery] LanguageFilter? args = null)
        {
            try
            {
                var companies = await _languagesService.GetPagingAsync(args);
                return companies;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LanguagesController), nameof(GetItems), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }

        [HttpGet("list")]
        public async Task<IEnumerable<Language?>> GetItems([FromQuery] LanguageFilter? args = null)
        {
            try
            {

                var companies = await _languagesService.GetListAsync(args);

                if (companies == null)
                {
                    return Enumerable.Empty<Language>();
                }

                return companies;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LanguagesController), nameof(GetItems), LogEvent.EventsTypes.Error, ex);
                return Enumerable.Empty<Language>();
            }
        }

        
        [HttpGet("{id}")]
        public async Task<ActionResult<Language>> Get(int id)
        {
            var item = await _languagesService.GetItemAsync(id);

            if (item == null)
            {
                return NotFound();
            }

            return item;
        }
        
        // PUT: api/Companies/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        
        [HttpPut("{id}")]
        public async Task<ActionResult<APIResponseMessage<Language>>> Put(int id, Language item)
        {
            if (id != item.Id)
            {
                return BadRequest();
            }

            var resp = await _languagesService.PostAsync(item);

            if (resp == null)
                return Problem("Error saving settings");

            return Ok(resp);
        }

        // POST: api/Companies
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754

        [HttpPost]
        public async Task<ActionResult<APIResponseMessage<Language>>> Post(Language item)
        {
            var resp = await _languagesService.PostAsync(item);

            if (resp == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Post return null");

            return Ok(resp);
        }

        [HttpGet("GetIdLanguage")]
        public async Task<ActionResult<Language>> GetIdLanguage()
        {
            try
            {
                var id = await _languagesService.GetIdLanguage();
                if (id.HasValue)
                {
                    return Ok(id.Value);
                }
                else
                {
                    return NotFound("No language ID set.");
                }
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LanguagesController), nameof(GetIdLanguage), LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "Error retrieving language ID.");
            }
        }

        [HttpPost("SetIdLanguage")]
        public async Task<IActionResult> SetIdLanguage([FromBody] int id)
        {
            try
            {
                var result = await _languagesService.SetIdLanguage(id);
                if (result)
                {
                    return Ok("Language ID set successfully.");
                }
                else
                {
                    return BadRequest("Failed to set language ID.");
                }
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LanguagesController), nameof(SetIdLanguage), LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "Error setting language ID.");
            }
        }

        [HttpPost("SeCodeLanguage")]
        public async Task<IActionResult> SetCodeLanguage([FromBody] string code)
        {
            try
            {
                var result = await _languagesService.SetCodeLanguage(code);
                if (result)
                {
                    return Ok("Language ID set successfully.");
                }
                else
                {
                    return BadRequest("Failed to set language ID.");
                }
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LanguagesController), nameof(SetCodeLanguage), LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "Error setting language ID.");
            }
        }

        // DELETE: api/Companies/5
        //[AuthorizeRole(ePolicy.AdminRole)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var resp = await _languagesService.DeleteAsync(id);
            
            if (!resp)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Error On Deleded Language");
            }
            else
                return NoContent();
        }

       

    }
}
