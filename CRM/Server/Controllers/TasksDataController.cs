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
using CRM.Shared.Helper;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TasksDataController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TasksDataController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/TasksProjects
        [HttpGet]
        public async Task<IEnumerable<TaskData>> GetTaskProject([FromQuery] TaskDataFilter filter)
        {
            
            try
            {
                int totalPage;

                var tasks = _context.TasksProject.AsQueryable();

                if (filter.Name != null && filter.Name.Length > 0)
                    tasks = tasks.Where(x => x.Name.Contains(filter.Name));

                if (filter.IdProject != null)
                {
                    tasks = tasks.Where(x => x.IdProject == filter.IdProject);
                }

                

                int count = tasks != null ? tasks.Count() : 0;

                if (filter.PageSize > 0)
                {
                    tasks = tasks.Skip((filter.PageNumber - 1) * filter.PageSize).Take(filter.PageSize);
                    totalPage = (int)Math.Ceiling(count / (double)filter.PageSize);
                }
                else
                {
                    totalPage = 1;

                }
                bool nextPage = filter.PageNumber < totalPage;
                bool previousPage = filter.PageNumber > 1;

                var paginationMetadata = new
                {
                    totalCunt = count,
                    pageSize = filter.PageSize,
                    currentPage = filter.PageNumber,
                    totalPage = totalPage,
                    previousPage = previousPage,
                    nextPage = nextPage
                };
                HttpContext.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));

                var list = (await tasks.ToListAsync());

                if (list != null)
                    return list.ToTaskDataList();
                else
                    return new List<TaskData>();
            }
            catch (Exception ex)
            {
                return null;
            }


        }
    

        // GET: api/TasksProjects/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TaskData>> GetTaskProject(int id)
        {
            var taskProject = await _context.TasksProject.FindAsync(id);

            if (taskProject == null)
            {
                return NotFound();
            }
            
          
            return new TaskData(taskProject);
        }

        // PUT: api/TasksProjects/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTaskProject(int id, TaskData task)
        {
            if (id != task.Id)
            {
                return BadRequest();
            }

            TaskProject taskProject = new TaskProject(task);

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
            catch (Exception ex)
            {

            }

            return NoContent();
        }

        // POST: api/TasksProjects
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<TaskProject>> PostTaskProject([FromBody] TaskData task)
        {
            try
            {

                TaskProject taskProject = new TaskProject(task);

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
