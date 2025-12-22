using CRM.Server.Data;
using CRM.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RolesController : ControllerBase
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;

        private readonly ApplicationDbContext _context;
        public RolesController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
            _roleManager = roleManager;
        }

        // GET: api/<RolesController>
        [HttpGet]
        public IEnumerable<IdentityRole> Get()
        {
            return _roleManager.Roles;
        }

        // GET api/<RolesController>/5
        [HttpGet("{id}")]
        public async Task<IdentityRole> Get(string id)
        {
            return await _roleManager.FindByIdAsync(id);
        }

        // POST api/<RolesController>
        [HttpPost]
        public void Post([FromBody] string value)
        {

        }

        // PUT api/<RolesController>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/<RolesController>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
