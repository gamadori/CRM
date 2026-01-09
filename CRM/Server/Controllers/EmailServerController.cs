
using CNM.Authorize;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [AuthorizeRole(ePolicy.StandardRole)]
    [Route("api/[controller]")]
    [ApiController]

    public class EmailServerController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        private readonly IEmailSenderPlus _emailService;

        private readonly IPermitsService _permitsService;

        private readonly ILogEventService _logEventService;

        public EmailServerController(ApplicationDbContext context, EmailService emailService, PermitsService permitsService, ILogEventService logEventService)
        {
            _context = context;
            _emailService = emailService;
            _permitsService = permitsService;
            _logEventService = logEventService;
        }


       
    }
}
