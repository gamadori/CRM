using CRM.Server.Helpers;
using CRM.Server.Models;
using CRM.Shared;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.DotNet.Scaffolding.Shared.Project;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Asn1.Ocsp;
using System.Globalization;

namespace CRM.Server.Services
{
    public class LangSelectorService: ILangSelectorService
    {
        private readonly CRM.Server.Data.ApplicationDbContext _context;

        private readonly IHttpContextAccessor _httpContextAccessor;

        private readonly IPermitsService _permitsService;
        

        public LangSelectorService(CRM.Server.Data.ApplicationDbContext context, IPermitsService permitsService, IHttpContextAccessor httpContextAccessor)
        {

            _context = context;
            _permitsService = permitsService;
            _httpContextAccessor = httpContextAccessor;

        }

        public async Task<LanguageSelectorModel> OnGetAsync(string? lang = null)
        {
            string? code;
            var model = new LanguageSelectorModel();

            model.Languages = await _context.Languages.OrderBy(x => x.Index).ToListAsync();


            if (lang != null)
            {
                code = lang;
            }
            else
            {
                var user = await _permitsService.GetUser();

                if (user != null)
                {

                    code = user.LanguageCode;

                }
                else
                {
                    var requestCulture = _httpContextAccessor.HttpContext?.Features.Get<IRequestCultureFeature>();

                    code = requestCulture?.RequestCulture.UICulture.Name;


                }
            }
            model.Language = await _context.Languages.FirstOrDefaultAsync(x => x.LanguageCode == code);
           

            if (model.Language == null)
                model.Language = _context.Languages.FirstOrDefault();
            return model;
        }
    }
}
