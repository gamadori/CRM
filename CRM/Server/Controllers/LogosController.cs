using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRM.Server.Data;
using CRM.Shared;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using Newtonsoft.Json;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LogosController : ControllerBase
    {
        private const string DirLoghi = "Loghi";

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public LogosController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: api/Loghi
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Logo>>> GetLoghi([FromQuery] LogosFilterModel args)
        {
            int totalPage;

            var logos = _context.Logos.AsQueryable();

            if (args.Codice != null && args.Codice?.Length > 0)
                logos = logos.Where(x => x.Codice.Contains(args.Codice));

            if (args.Descrizione != null && args.Descrizione?.Length > 0)
                logos = logos.Where(x => x.Descrizione.Contains(args.Descrizione));
            
            int count = logos.Count();

            if (args.PageSize > 0)
            {
                logos = logos.Skip((args.PageNumber - 1) * args.PageSize).Take(args.PageSize);
                totalPage = (int)Math.Ceiling(count / (double)args.PageSize);
            }
            else
                totalPage = 1;

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


            return await logos.ToListAsync();
        }

        // GET: api/Loghi/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Logo>> GetLogo(int id)
        {
            var logo = await _context.Logos.FindAsync(id);

            if (logo == null)
            {
                return NotFound();
            }

            return logo;
        }

        // PUT: api/Loghi/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutLogo(int id, Logo logo)
        {
            
            if (id != logo.Id)
            {
                return BadRequest();
            }

            

            _context.Entry(logo).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!LogoExists(id))
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

        // POST: api/Loghi
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Logo>> PostLogo(Logo logo)
        {
            
            _context.Logos.Add(logo);
            await _context.SaveChangesAsync();

            //SetLogo(logo.Id, logo.Ext, bytes);

            return CreatedAtAction("GetLogo", new { id = logo.Id }, logo);
        }

        // DELETE: api/Loghi/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteLogo(int id)
        {
            var logo = await _context.Logos.FindAsync(id);
            if (logo == null)
            {
                return NotFound();
            }

            _context.Logos.Remove(logo);
            await _context.SaveChangesAsync();
            
            return NoContent();
        }

        private bool LogoExists(int id)
        {
            return _context.Logos.Any(e => e.Id == id);
        }


        //private byte[] GetLogo(int id, string ext)
        //{
        //    string path = GetFileLogo(id, ext);
            
        //    if (System.IO.File.Exists(path))
        //        return System.IO.File.ReadAllBytes(path);
        //    else
        //        return null;
        //}

        //public bool SetLogo(int id, string ext, byte[] file)
        //{
        //    try
        //    {
        //        string path = GetFileLogo(id, ext);

        //        if (System.IO.File.Exists(path))
        //            System.IO.File.Delete(path);

        //        System.IO.File.WriteAllBytes(path, file);

        //        return true;

        //    }
        //    catch
        //    {
        //        return false;
        //    }
        //}

        //public void DeleteLogo(int id, string ext)
        //{
        //    string path = GetFileLogo(id, ext);

        //    if (System.IO.File.Exists(path))
        //        System.IO.File.Delete(path);
        //}
        //private string GetFileLogo(int id, string ext)
        //{
        //    string path = PathDirLoghi();
        //    path += $"\\{id}.{ext}";

        //    return path;
        //}
        //private string PathDirLoghi()
        //{
        //    string path = $"{_env.WebRootPath}\\{DirLoghi}";

        //    if (!Directory.Exists(path))
        //        Directory.CreateDirectory(path);

        //    return path;
        //}
    }
}
