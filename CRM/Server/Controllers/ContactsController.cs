using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;

using CRM.Server.Data;
using CRM.Shared;
using Newtonsoft.Json;
using CRM.Server.Services;
using CRM.Client.Services;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ContactsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        private readonly IPermitsService _permitsService;

        private readonly ILogEventService _logEventService;
        public ContactsController(ApplicationDbContext context, IPermitsService permitsService, ILogEventService logEventService)
        {
            _context = context;
            _permitsService = permitsService;
            _logEventService = logEventService; 
        }

        // GET: api/Contacts
        // GET: api/Products
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Contact>>> GetContacts([FromQuery] ContactFilter args)
        {
            try
            {
                var contacts = _context.Contacts.Include(x => x.Company).Include(x => x.Company).AsQueryable();

                if (!await _permitsService.BelongsToMainCompany() && !await _permitsService.BelongsToReseller())
                {
                    return new List<Contact>();
                }

               

                if (args.Filter != null && args.Filter.Trim().Length > 0)
                {
                    contacts = contacts.Where(args.Filter);
                }

                if (args.IdCompany != null)
                {
                    contacts = contacts.Where(x => x.IdCompany == args.IdCompany);
                }

                if (args.Name != null && args.Name.Trim().Length > 0)
                {
                    string filterName = args.Name.Replace(" ", "") ;
                   

                    contacts = contacts.Where(x=>x.Name.Contains(args.Name) || x.Surname.Contains(args.Name) || (x.Surname + x.Name).Contains(filterName)  || (x.Name + x.Surname).Contains(filterName));  
                }

                if (args.OrderBy != null && args.OrderBy.Length > 0)
                {
                    contacts = contacts.OrderBy(args.OrderBy);
                }
                else
                    contacts = contacts.OrderBy(x => x.Surname).ThenBy(x => x.Name);

                int count = contacts.Count();
                int totalPage = 0;

                if (args.Skip != null && args.Top != null)
                {
                    contacts = contacts.Skip(args.Skip.Value).Take(args.Top.Value);
                }
                else
                {
                    totalPage = 1;

                }
                bool nextPage = args.PageNumber < totalPage;
                bool previousPage = args.PageNumber > 1;

                var paginationMetadata = new
                {
                    totalCount = count,
                    
                };
                HttpContext.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));

                return await contacts.Select(x=>new Contact()
                {
                    Id = x.Id,
                    IdCompany = x.IdCompany,
                    Name = x.Name,
                    Surname = x.Surname,
                    Email = x.Email,
                    Mobile = x.Mobile,
                    Phone = x.Phone,
                    Company = x.Company
                    

                }).ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ContactsController), nameof(GetContacts), LogEvent.EventsTypes.Error, ex.Message);
                return new List<Contact>();
            }
        }

        // GET: api/Contacts/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Contact>> GetContact(int id)
        {
            var contact = await _context.Contacts.Include(x=>x.Company).FirstOrDefaultAsync(x=>x.Id == id);

            if (contact == null)
            {
                return NotFound();
            }
            
            return contact;
        }

        // PUT: api/Contacts/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutContact(int id, Contact contact)
        {
            if (id != contact.Id)
            {
                return BadRequest();
            }

            _context.Entry(contact).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ContactExists(id))
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

        // POST: api/Contacts
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Contact>> PostContact(Contact contact)
        {
            _context.Contacts.Add(contact);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetContact", new { id = contact.Id }, contact);
        }

        // DELETE: api/Contacts/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteContact(int id)
        {
            var contact = await _context.Contacts.FindAsync(id);
            if (contact == null)
            {
                return NotFound();
            }

            _context.Contacts.Remove(contact);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ContactExists(int id)
        {
            return _context.Contacts.Any(e => e.Id == id);
        }
    }
}
