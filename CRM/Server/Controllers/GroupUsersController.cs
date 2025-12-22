using CRM.Server.Data;
using CRM.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Math.EC.Rfc7748;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GroupUsersController : ControllerBase
    {


        private readonly ApplicationDbContext _context;
        public GroupUsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // PUT: api/Customers/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<ApiResponseModel>> PostUser(UserGroupModel model)
        {
            

            var group = await _context.Groups.Where(x => x.Id == model.IdGroup).Include(x=>x.Users).SingleOrDefaultAsync();
            var user = await _context.Users.Where(x => x.Id == model.IdUser).SingleOrDefaultAsync();

            if (group == null)
                return new ApiResponseModel() { State = false, Message = "Utente insesistente" };


            if (user == null)
                return new ApiResponseModel() { State = false, Message = "Utente insesistente" };

            try
            {
                group.Users.Add(user);
                await _context.SaveChangesAsync();

                
            }
            catch (DbUpdateConcurrencyException ex)
            {
                    return new ApiResponseModel() { State = false, Message = ex.Message };
                
            }

            return new ApiResponseModel() { State = true };
        }
        [HttpDelete]
        public async Task<IActionResult> Delete([FromQuery] UserGroupModel model)
        {
            var group = await _context.Groups.Include(x=>x.Users).Where(x=>x.Id == model.IdGroup).FirstOrDefaultAsync();
            if (group == null)
            {
                return NotFound();
            }

            var user = await _context.Users.FindAsync(model.IdUser);

            group.Users.Remove(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }

       
    }
}
