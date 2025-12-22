using CRM.Server.Data;
using CRM.Shared;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Services
{
    public class TranslateService
    {
        private readonly ApplicationDbContext _context;

        public TranslateService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<string> GetTxtAsync(int idLanguage, string module, string field)
        {
            var item = await _context.Translates.FirstOrDefaultAsync(x => x.IdLanguage == idLanguage && x.Module == module && x.Field == field);

            if (item != null)
                return item.Text;
            else
                return field;
        }
    }
}
