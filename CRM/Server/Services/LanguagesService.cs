using CRM.Client.Shared;
using CRM.Server.Data;
using CRM.Server.Models;
using CRM.Shared;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Services
{
    public class LanguagesService: ILanguagesService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermitsService _permitsService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LanguagesService(ApplicationDbContext context, IPermitsService permitsService, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _permitsService = permitsService;
            _httpContextAccessor = httpContextAccessor; 
        }

        public async Task<int?> GetIdLanguage()
        {
            var user = await _permitsService.GetUser();
            if (user != null)
            {
                var language = _context.Languages.FirstOrDefault(x => x.LanguageCode == user.LanguageCode);

                return language?.Id;
            }
            else
                return null;
        }

        
    }
}
