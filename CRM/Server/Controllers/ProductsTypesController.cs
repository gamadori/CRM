using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Helper;
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
    public class ProductsTypesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        private readonly ILogEventService _logEventService;

        private readonly IProductTypesService _service;
        public ProductsTypesController(ApplicationDbContext context, ILogEventService logEventService, IProductTypesService service)
        {
            _context = context;
            _logEventService = logEventService;
            _service = service;
        }

        [HttpGet]
        public async Task<PagingResponse<ProductTypeDTO>?> GetPage([FromQuery] ProductTypeFilter? args = null)
        {
            try
            {
                var items = await _service.GetPagingAsync(args);
                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductsTypesController), nameof(GetPage), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }
        [HttpGet("list")]
        public async Task<IEnumerable<ProductTypeDTO>?> GetItems([FromQuery] ProductTypeFilter? args = null)
        {
            try
            {
                var items = await _service.GetListAsync(args);
                if (items == null)
                {
                    return Enumerable.Empty<ProductTypeDTO>();
                }
                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductsTypesController), nameof(GetItems), LogEvent.EventsTypes.Error, ex);
                return Enumerable.Empty<ProductTypeDTO>();
            }
        }
        [HttpGet("{id}")]
        public async Task<ActionResult<ProductTypeDTO?>> GetItem(int id)
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
                await _logEventService.RegisterAsync(nameof(ProductsTypesController), nameof(GetItem), LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }
        [HttpPut("{id}")]
        public async Task<ActionResult<APIResponseMessage<ProductTypeDTO>>> Put(int id, ProductType item)
        {
            if (id != item.Id)
            {
                return BadRequest();
            }
            var resp = await _service.PostAsync(item);

            if (resp == null)
                return Problem("Error saving product type");

            return Ok(resp);
        }

        [HttpPost]
        public async Task<ActionResult<APIResponseMessage<ProductTypeDTO>>> Post(ProductType item)
        {
            var resp = await _service.PostAsync(item);

            if (resp == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Post return null");

            return Ok(resp);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var resp = await _service.DeleteAsync(id);

            if (!resp)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Error on deleting Product Type");
            }
            else
                return NoContent();
        }

    }
}
