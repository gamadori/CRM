using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using CRM.Server.Data;
using CRM.Shared;
using Microsoft.Extensions.Localization;
using NuGet.Common;
using System.Globalization;
using CRM.Server.Services;
using CRM.Server.Controllers;
using CRM.Shared.DTOs;

namespace CRM.Server.Pages.Reports
{
    public class TicketReportModel : PageModel
    {
        private readonly CRM.Server.Data.ApplicationDbContext _context;

        public TicketReportModel(CRM.Server.Data.ApplicationDbContext context)
        {
            _context = context;
            
        }

        public TicketDTO Ticket { get; set; } = default!; 

        public int IdLanguage { get; set; }

       
        public async Task<IActionResult> OnGetAsync(int? id, int idLanguage)
        {
            

            if (id == null || _context.Tickets == null)
            {
                return NotFound();
            }

            IdLanguage = idLanguage;

            var ticket = await GetTicket(id.Value);


            if (ticket == null)
            {
                return NotFound();
            }
            else 
            {
                Ticket = ticket;
            }

           
            return Page();
        }

        private string ListToString(List<string> list)
        {
            string s = "";
            int l = list.Count();

            if (l > 0)
            {
                s = list[0];

                if (l > 1)
                    s += $" / {list[1]}";
            }
            return s;
        }

        private async Task<TicketDTO?> GetTicket(int id)
        {
            var tickets = _context.Tickets.Where(x => x.Id == id).Include(x => x.UserOpened).AsQueryable();
        
            var ticketModel = await tickets.Select(x => new TicketDTO()
            {
                Id = x.Id,
                Date = x.Date,
                DateEnd = x.DateEnd,
                DateOpened = x.DateOpened,
                DateClosed = x.DateClosed,
                Time = x.Time,
                Company = x.Company.RagioneSociale,
                Product = (x.Product != null) ? x.Product.Name : "",
                Article = (x.Article != null) ? x.Article.SerialNumber : "",
                Project = (x.Project != null) ? x.Project.Name : "",
                IdUserAssigned = x.IdUserAssigned,
                IdCompany = x.IdCompany,
                IdState = x.IdState,
                IdUserOpened = x.IdUserOpened,
                UserOpened = (x.UserOpened != null) ? x.UserOpened.NameComplete : "",
                UserAssigned = (x.UserAssigned != null) ? x.UserAssigned.NameComplete : "",
                UserClosed = (x.UserClosed != null) ? x.UserClosed.NameComplete : "",
                MinuteWork = x.TicketInterventions.Sum(y => y.Minute),
                Description = x.Description,
                DescType = (x.TicketType.Languages.Where(x => x.IdLanguage == IdLanguage).Any()) ? x.TicketType.Languages.Where(x => x.IdLanguage == IdLanguage).FirstOrDefault().Name : "",
                TicketType = x.TicketType,
                ContactName = x.Contact != null ? x.Contact.NameComplete : "",
                CloseDescription = x.CloseDescription

            }).FirstOrDefaultAsync();


            return ticketModel;
        }
        
    }



    
}

