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
    public class ProductParenChildController : ControllerBase
    {


        private readonly ApplicationDbContext _context;
        public ProductParenChildController(ApplicationDbContext context)
        {
            _context = context;
        }

        // PUT: api/Customers/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<ApiResponseModel>> Post(ProductParentChildModel model)
        {
            

            var parent = await _context.Products.Include(x=>x.Childs).Where(x => x.Id == model.IdParent).SingleOrDefaultAsync();
            var child = await _context.Products.Where(x => x.Id == model.IdChild).SingleOrDefaultAsync();

            if (parent == null)
                return new ApiResponseModel() { State = false, Message = "Tipo Prodotto Inesistente" };


            if (child == null)
                return new ApiResponseModel() { State = false, Message = "Sotto Parte insesistente" };

            try
            {
                parent.Childs.Add(child);
                await _context.SaveChangesAsync();

                
            }
            catch (DbUpdateConcurrencyException ex)
            {
                    return new ApiResponseModel() { State = false, Message = ex.Message };
                
            }

            return new ApiResponseModel() { State = true };
        }
        [HttpDelete]
        public async Task<IActionResult> Delete([FromQuery] ProductParentChildModel model)
        {
            var product = await _context.Products.Include(x=>x.Childs).Where(x=>x.Id == model.IdParent).FirstOrDefaultAsync();
            if (product == null)
            {
                return NotFound();
            }

            var user = await _context.Products.FindAsync(model.IdChild);

            product.Childs.Remove(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }

       
    }
}
