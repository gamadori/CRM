using CRM.Client.Services;
using CRM.Server.Models;
using CRM.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.ViewComponents
{
    public class LangSelector: ViewComponent
    {
        private readonly ILangSelectorService _langSelectorService;
        private readonly IHttpContextAccessor _contextAccessor;

        public LangSelector(ILangSelectorService langSelectorService, IHttpContextAccessor httpContextAccessor)
        {
            _langSelectorService = langSelectorService;
            _contextAccessor = httpContextAccessor;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            string? id = _contextAccessor.HttpContext?.Request.Cookies["lang"];


            

            LanguageSelectorModel lang = await _langSelectorService.OnGetAsync(id);
            return View(lang);
        }
    }
}
