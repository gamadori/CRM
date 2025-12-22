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
    public class UserRolesController : ControllerBase
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;

        private readonly ApplicationDbContext _context;
        public UserRolesController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
            _roleManager = roleManager;
        }

        // GET: api/<RolesController>
        [HttpGet]
        public async Task<IEnumerable<UserRoles>> Get()
        {
            List<UserRoles> model = new List<UserRoles>();
            var users = _userManager.Users.ToList();

            foreach (var user in users)
            {
                UserRoles userRoles = new UserRoles() { Id = user.Id, Roles = (await _userManager.GetRolesAsync(user)).ToList() };

                model.Add(userRoles);
            }
            return model;
        }

        // GET api/<RolesController>/5
        [HttpGet("{id}")]
        public async Task<UserRoles> Get(string id)
        {
            ApplicationUser user;

            if (id == null || id.Length == 0)
            {
                user = await _userManager.FindByNameAsync(HttpContext.User.Identity.Name);
            }
            else
                user = await _userManager.FindByIdAsync(id);

            UserRoles model = new UserRoles() {Id = user.Id, Roles = (await _userManager.GetRolesAsync(user)).ToList() };

            return model;
        }

        // POST api/<RolesController>
        [HttpPost]
        public void Post([FromBody] ApplicationUser value)
        {

        }

        // PUT api/<RolesController>/5
        [HttpPut("{id}")]
        public async Task Put(string id, [FromBody] UserRoles model)
        {
            ApplicationUser user = await _userManager.FindByIdAsync(id);

            var roles = await _userManager.GetRolesAsync(user);

            await _userManager.RemoveFromRolesAsync(user, roles);
            
            await _userManager.AddToRolesAsync(user, model.Roles);


        }

        // DELETE api/<RolesController>/5
        [HttpDelete("{id}")]
        public void Delete(string id)
        {
        }
    }
}
