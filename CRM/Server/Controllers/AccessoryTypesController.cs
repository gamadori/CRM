using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRM.Server.Data;
using CRM.Shared;
using System.Linq.Dynamic.Core;
using CRM.Client.Pages.DashBoard;
using Newtonsoft.Json;
using CRM.Server.Services;
using Microsoft.CodeAnalysis.Host;
using System.Drawing;
using Microsoft.CodeAnalysis.CSharp;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccessoryTypesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogEventService _logEventService;
        private readonly IPermitsService _permits;
        private readonly ILanguagesService _languageService;
        public AccessoryTypesController(ApplicationDbContext context, ILogEventService logEventService, IPermitsService permits, ILanguagesService languageService)
        {
            _context = context;
            _logEventService = logEventService;
            _permits = permits;
            _languageService = languageService; 
        }

        // GET: api/Talks
        [HttpGet]
        public async Task<ActionResult<List<AccessoryTypeModel>>> Get([FromQuery]AccessoryTypeFilter args)
        {
            try
            {
                IQueryable<AccessoryTypeModel> items;

                var idLanguage = await _languageService.GetIdLanguage();


                if (args != null && args.Translate)
                {
                    items =
                            from types in _context.AccessoryTypes
                            join lang in _context.AccessoryTypeLanguages.Where(x => x.IdLanguage == idLanguage) on types.Id equals lang.IdAccessoryType
                            into AccessoryTranslate
                            from trad in AccessoryTranslate.DefaultIfEmpty()
                            select new AccessoryTypeModel()
                            {
                                Id = types.Id,
                                Name = types.Name,
                                Language = trad != null && trad.Name != null ? trad.Name : types.Name
                            };

                }
                else
                {
                    items = _context.AccessoryTypes.Select(x => new AccessoryTypeModel()
                    {
                        Id = x.Id,
                        Name = x.Name,
                        Language = x.Name
                    });
                }
                        
              
                if (args.Filter != null)
                {
                    items = items.Where(args.Filter);
                }

                if (args.OrderBy != null)
                {
                    items = items.OrderBy(args.OrderBy);
                }
                else
                    items = items.OrderBy(x => x.Name);

                int count = items.Count();
                
                if (items != null && args?.Skip != null && args.Top != null)
                {
                    items = items.Skip(args.Skip.Value).Take(args.Top.Value);
                }


                var paginationMetadata = new
                {
                    totalCount = count,
                };

                HttpContext.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));


                var list = items != null ? await items.ToListAsync() : new List<AccessoryTypeModel>();





                return list;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(AccessoryTypesController), nameof(Get), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        // GET: api/Talks/5
        [HttpGet("{id}")]
        public async Task<ActionResult<AccessoryType>> GetAccessory(int id)
        {
            var accessoryType = await _context.AccessoryTypes.FindAsync(id);


            if (accessoryType == null)
            {
                return NotFound();
            }

            accessoryType.Permits = await _permits.AccesoryTypePermits();

            return accessoryType;
        }

        // GET: api/Talks/5
        [HttpGet("details/{id}")]
        public async Task<ActionResult<AccessoryTypeModel>> GetAccessoryDetails(int id)
        {
            var items =
                      from types in _context.AccessoryTypes.Where(x=>x.Id== id)
                      join lang in _context.AccessoryTypeLanguages.DefaultIfEmpty() on types.Id equals lang.IdAccessoryType                      
                      into AccessoryTranslate
                      from trad in AccessoryTranslate.DefaultIfEmpty()
                      select new AccessoryTypeModel()
                      {
                          Id = types.Id,
                          Name = types.Name,
                          Language = trad != null && trad.Name != null ? trad.Name : types.Name,
                          Description = types.Description,
                      };

            var item = items.FirstOrDefault();

            if (item != null)
                item.Permits = await _permits.AccesoryTypePermits();

            return item != null ? item : new AccessoryTypeModel();
        }



        // PUT: api/Talks/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAccessoryType(int id, AccessoryType accessoryType)
        {
            try
            {
                if (id != accessoryType.Id)
                {
                    return BadRequest();
                }

                if (!await _permits.CanEditAccessoryType())
                    return Problem(GlobalMessages.PermitsErrors);

                
                _context.Entry(accessoryType).State = EntityState.Modified;
            }
            catch(Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(AccessoryTypesController), nameof(PutAccessoryType), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AccessoryTypeExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Talks
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<AccessoryType>> PostAccessoryType(AccessoryType accessoryType)
        {
            if (await _permits.CanInsertAccessoryType())
            {
                accessoryType.Date = DateTime.Now;
                accessoryType.IdUser = await _permits.IdUser();
                _context.AccessoryTypes.Add(accessoryType);
                await _context.SaveChangesAsync();

                return CreatedAtAction("GetAccessory", new { id = accessoryType.Id }, accessoryType);
            }
            else
                return Problem(GlobalMessages.PermitsErrors);
        }

        // DELETE: api/Talks/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAccessoryType(int id)
        {
            var accessoryType = await _context.AccessoryTypes.FindAsync(id);
            if (accessoryType == null)
            {
                return NotFound();
            }

            _context.AccessoryTypes.Remove(accessoryType);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool AccessoryTypeExists(int id)
        {
            return _context.AccessoryTypes.Any(e => e.Id == id);
        }
    }
}
