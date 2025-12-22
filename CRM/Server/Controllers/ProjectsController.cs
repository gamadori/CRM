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
using CRM.Server.Helpers;
using Microsoft.Extensions.Primitives;
using CRM.Shared.Helper;
using CRM.Server.Services;
using Newtonsoft.Json;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProjectsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private ILogEventService _logEventService;
        private IPermitsService _permitsService;    
        public ProjectsController(ApplicationDbContext context, ILogEventService logEventService, IPermitsService permitsService)
        {
            _context = context;
            _logEventService = logEventService;
            _permitsService = permitsService;   
        }

        // GET: api/ProjectModels
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Project>>>GetProjects([FromQuery] ProjectFilter? args = null)
        {
            try
            {
                int totalPage = 1;

                
                var projects = _context.Projects.Include(x=>x.UserLeader).AsQueryable();

               
                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                {
                    projects = projects.OrderBy(args.OrderBy);
                }
                else
                    projects = projects.OrderBy(x => x.StartDate);

                if (args?.Filter != null && args.Filter.Any())
                {
                    projects = projects.Where(args.Filter);
                }

                

                int count = projects.Count();

                if (args?.Skip != null && args.Top != null)
                {
                    projects = projects.Skip(args.Skip.Value).Take(args.Top.Value);
                }
                else
                {
                    totalPage = 1;

                }

                bool nextPage = args?.PageNumber < totalPage;
                bool previousPage = args?.PageNumber > 1;

                var paginationMetadata = new
                {
                    totalCount = count,
                    pageSize = args != null ? args.PageSize : 0,
                    currentPage = args != null ? args.PageNumber : 0,
                    totalPage = totalPage,
                    previousPage = previousPage,
                    nextPage = nextPage
                };
                HttpContext.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));
                // var list = await companies.ToListAsync();
                var list = await projects.ToListAsync();

                foreach (var item in list)
                {
                    item.Permits = await _permitsService.ObjectPermits(null, item.IdUserCreate);
                }
                return list;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProjectsController), nameof(GetProjects), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }


        // GET: api/Projects/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Project>> GetProject(int id)
        {
            var project = await _context.Projects.FindAsync(id);

            if (project == null)
            {
                return NotFound();
            }
            project.UserLeader = await _context.Users.FindAsync(project.IdUserLeader);
            project.UserCreate = await _context.Users.FindAsync(project.IdUserCreate);
            project.Permits = await _permitsService.ObjectPermits(null, project.IdUserCreate);
            return project;
        }

        // PUT: api/Projects/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutProject(int id, Project project)
        {
            if (id != project.Id)
            {
                return BadRequest();
            }

            _context.Entry(project).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProjectExists(id))
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

        // POST: api/Projects
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Project>> PostProject(Project project)
        {
            try
            {
                project.StartDate = DateTime.Now;
                project.IdUserCreate = await _permitsService.IdUser();

                _context.Projects.Add(project);
                
                await _context.SaveChangesAsync();

                return CreatedAtAction("GetProject", new { id = project.Id }, project);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProjectsController), nameof(PostProject), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }

        }

        [HttpPost("adduser")]
        public async Task<IActionResult> AddUser(ProjectUser item)
        {
            var user = await _context.ProjectUsers.FirstOrDefaultAsync(x=>x.IdProject == item.IdProject && x.IdUser == item.IdUser);

            if (user == null)
            {
                user = new ProjectUser();
                user.IdUser = item.IdUser;
                user.IdProject = item.IdProject;
                _context.Add(user);

                await _context.SaveChangesAsync();
                return NoContent();
            }
            else
                return BadRequest();
        }

        [HttpPost("removecustomer")]
        public async Task<IActionResult> RemoveUser(ProjectUser item)
        {
            var user = await _context.ProjectUsers.FindAsync(item.Id);

            if (user != null)
            {
                _context.ProjectUsers.Remove(user);
                await _context.SaveChangesAsync();
                return NoContent();
            }
            else
                return BadRequest();
        }

        // DELETE: api/Projects/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProject(int id)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null)
            {
                return NotFound();
            }

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();

            return NoContent();
        }


        private bool ProjectExists(int id)
        {
            return _context.Projects.Any(e => e.Id == id);
        }

        
    }
}
