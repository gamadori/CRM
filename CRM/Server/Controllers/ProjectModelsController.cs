using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRM.Server.Data;
using CRM.Shared;
using Microsoft.Extensions.Primitives;
using System.Reflection;
using System.Linq.Expressions;
using System.Linq.Dynamic.Core;
using CRM.Server.Helpers;
using CRM.Server.Services;
using CRM.Client.Services;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProjectModelsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        private readonly ILogEventService _logEventService;
        public ProjectModelsController(ApplicationDbContext context, ILogEventService logEventService)
        {
            _context = context;
            _logEventService = logEventService; 
        }

        // GET: api/ProjectModels
        [HttpGet]
        public ActionResult<object> GetProjectModel()
        {
            try
            {
                string? filter = null;
                string order;

                var data = _context.ProjectModels.AsQueryable();

                var count = data.Count();
                var queryString = Request.Query;

                
                if (queryString.Keys.Contains("$filter"))
                {
                    filter = queryString["$filter"];

                    data = SyncHelper.GetFilterPredicate(data, filter);

                    
                }

                if (queryString.Keys.Contains("$orderby"))
                {
                    order = queryString["$orderby"];
                    data = data.OrderBy(order);

                }

                if (queryString.Keys.Contains("$inlinecount"))
                {
                    
                    StringValues Skip;
                    StringValues Take;
                    int skip = (queryString.TryGetValue("$skip", out Skip)) ? Convert.ToInt32(Skip[0]) : 0;
                    int top = (queryString.TryGetValue("$top", out Take)) ? Convert.ToInt32(Take[0]) : data.Count();

                    return new { Items = data.Skip(skip).Take(top), Count = count };
                }
                else
                {
                   
                    var l = data.ToList();
                    return l;
                }
            }
            catch(Exception ex)
            {
                _logEventService.Register(nameof(ProjectModelsController), nameof(GetProjectModel), LogEvent.EventsTypes.Error, ex.Message);
                return new { Items = new List<ProjectModel>(), Count = 0};
            }
        }

       

        // GET: api/ProjectModels/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ProjectModel>> GetProjectModel(int id)
        {
            var projectModel = await _context.ProjectModels.FindAsync(id);

            if (projectModel == null)
            {
                return NotFound();
            }

            return projectModel;
        }

        // PUT: api/ProjectModels/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutProjectModel(int id, ProjectModel projectModel)
        {
            if (id != projectModel.Id)
            {
                return BadRequest();
            }

            _context.Entry(projectModel).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProjectModelExists(id))
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

        // POST: api/ProjectModels
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<ProjectModel>> PostProjectModel(ProjectModel projectModel)
        {
            _context.ProjectModels.Add(projectModel);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetProjectModel", new { id = projectModel.Id }, projectModel);
        }

        // DELETE: api/ProjectModels/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProjectModel(int id)
        {
            var projectModel = await _context.ProjectModels.FindAsync(id);
            if (projectModel == null)
            {
                return NotFound();
            }

            _context.ProjectModels.Remove(projectModel);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ProjectModelExists(int id)
        {
            return _context.ProjectModels.Any(e => e.Id == id);
        }
    }
}
