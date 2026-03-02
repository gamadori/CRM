using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    public class ContactsController : ControllerBase
    {
        private IContactsService _service;

        private readonly IPermitsService _permitsService;

        private readonly ILogEventService _logEventService;
        public ContactsController(IContactsService service, IPermitsService permitsService, ILogEventService logEventService)
        {
            _service = service;
            _permitsService = permitsService;
            _logEventService = logEventService; 
        }


        [HttpGet]
        public async Task<PagingResponse<ContactDTO>?> GetPage([FromQuery] ContactFilter? args = null)
        {
            try
            {
                var items = await _service.GetPagingAsync(args);
                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ContactsController), nameof(GetPage), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }

        [HttpGet("list")]
        public async Task<IEnumerable<ContactDTO>?> GetItems([FromQuery] ContactFilter? args = null)
        {
            try
            {
                var items = await _service.GetListAsync(args);
                if (items == null)
                {
                    return Enumerable.Empty<ContactDTO>();
                }
                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ContactsController), nameof(GetItems), LogEvent.EventsTypes.Error, ex);
                return Enumerable.Empty<ContactDTO>();
            }
        }


        [HttpGet("{id}")]
        public async Task<ActionResult<ContactDTO?>> GetItem(int id)
        {
            try
            {
                var item = await _service.GetItemAsync(id);
                if (item == null)
                {
                    return NotFound();
                }
                return Ok(item);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ContactsController), nameof(GetItem), LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<APIResponseMessage<ContactDTO>>> Put(int id, Contact item)
        {
            if (id != item.Id)
            {
                return BadRequest();
            }
            var resp = await _service.PostAsync(item);

            if (resp == null)
                return Problem("Error saving contact");

            return Ok(resp);
        }

        [HttpPost]
        public async Task<ActionResult<APIResponseMessage<ContactDTO>>> Post(Contact item)
        {
            var resp = await _service.PostAsync(item);

            if (resp == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Post return null");

            return Ok(resp);
        }

        // DELETE: api/Contacts/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var resp = await _service.DeleteAsync(id);

            if (!resp)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Error on deleting contact");
            }
            else
                return NoContent();
        }
    }
}
