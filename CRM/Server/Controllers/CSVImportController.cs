using CNM.Authorize;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Server.Controllers
{
    [AuthorizeRole(ePolicy.AdminRole)]
    [Route("api/[controller]")]
    [ApiController]
    public class CSVImportController : Controller
    {
        private readonly ApplicationDbContext _context;

        private readonly ILogEventService _logEventService;
        public CSVImportController(ApplicationDbContext context, ILogEventService logEventService)
        {
            _context = context;
            _logEventService = logEventService;
        }

        [HttpGet("{table}")]
        public async Task<ActionResult<List<CSVMapping>?>> GetMapping(string table)
        {
            try
            {
                var items = await _context.CSVMappings.Where(x => x.TableName == table).ToListAsync();
                return items;
            }
            catch(Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CSVImportController), nameof(GetMapping), LogEvent.EventsTypes.Error, ex.Message);
                return null;
            }

        }
        [HttpPost("Preview")]
        public ActionResult<List<string[]>> CSVPreview(CSVFile file)
        {
            List<string[]> data = new List<string[]>();

            var encodedTextBytes = Convert.FromBase64String(file.Content);
            string fileText = Encoding.UTF8.GetString(encodedTextBytes);

            string[] rows = fileText.Split("\r\n");
            
            foreach (string row in rows)
            {
                var fields = row.Split(file.Delimiter);
                data.Add(fields);
            }
            return data;
        }

        [HttpPost("Mapping/{table}")]
        public async Task<ActionResult> PostMapping(string table, List<CSVMapping> mapping)
        {
            try
            {
                var items = _context.CSVMappings.Where(x => x.TableName == table);

                foreach (var item in items)
                {
                    _context.CSVMappings.Remove(item);
                }
     



                
                foreach (var item in mapping.Where(x=>x.FieldName != null && x.FieldName.Length > 0))
                {
                    item.TableName = table;
                    await _context.CSVMappings.AddAsync(item);

                }
                
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                  await _logEventService.RegisterAsync(nameof(CSVImportController), nameof(PostMapping), LogEvent.EventsTypes.Error, ex.Message);
                return NoContent();
            }
        }
    }
}
