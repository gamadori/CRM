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
using Newtonsoft.Json;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TasksProjectsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TasksProjectsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/TasksProjects
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TaskProject>>> GetTaskProject([FromQuery] TaskProjectFilter args)
        {
            
            try
            {
                int totalPage;
                DateTime dateTo;

                var tasks = _context.TasksProject.AsQueryable();

                if (args.Name != null && args.Name.Length > 0)
                    tasks = tasks.Where(x => x.Name.Contains(args.Name));

                if (args.IdProject != null)
                {
                    tasks = tasks.Where(x => x.IdProject == args.IdProject);
                }

                int count = tasks != null ? tasks.Count() : 0;

                if (args.PageSize > 0)
                {
                    tasks = tasks.Skip((args.PageNumber - 1) * args.PageSize).Take(args.PageSize);
                    totalPage = (int)Math.Ceiling(count / (double)args.PageSize);
                }
                else
                {
                    totalPage = 1;

                }
                bool nextPage = args.PageNumber < totalPage;
                bool previousPage = args.PageNumber > 1;

                var paginationMetadata = new
                {
                    totalCunt = count,
                    pageSize = args.PageSize,
                    currentPage = args.PageNumber,
                    totalPage = totalPage,
                    previousPage = previousPage,
                    nextPage = nextPage
                };
                HttpContext.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));

                return await tasks.ToListAsync();
            }
            catch (Exception ex)
            {
                return null;
            }


        }
    

        // GET: api/TasksProjects/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TaskProject>> GetTaskProject(int id)
        {
            var taskProject = await _context.TasksProject.FindAsync(id);

            if (taskProject == null)
            {
                return NotFound();
            }
        

            return taskProject;
        }

        // PUT: api/TasksProjects/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTaskProject(int id, TaskProject taskProject)
        {
            if (id != taskProject.Id)
            {
                return BadRequest();
            }

            _context.Entry(taskProject).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TaskProjectExists(id))
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

        // POST: api/TasksProjects
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<TaskProject>> PostTaskProject([FromBody] TaskProject taskProject)
        {
            try
            {
                

                _context.TasksProject.Add(taskProject);
                await _context.SaveChangesAsync();

                return CreatedAtAction("GetTaskProject", new { id = taskProject.Id }, taskProject);
            }
            catch(Exception ex)
            {
                return null;
            }
        }

        // DELETE: api/TasksProjects/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTaskProject(int id)
        {
            var taskProject = await _context.TasksProject.FindAsync(id);
            if (taskProject == null)
            {
                return NotFound();
            }

            _context.TasksProject.Remove(taskProject);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool TaskProjectExists(int id)
        {
            return _context.TasksProject.Any(e => e.Id == id);
        }
    }
}
