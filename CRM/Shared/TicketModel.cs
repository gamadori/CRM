using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CRM.Shared
{
    public class TicketModel
    {
        [Display(Name = nameof(Ticket.Id), ResourceType = typeof(Resources.Models.Ticket))]
        public int Id { get; set; }

        [Display(Name = nameof(Ticket.IdArticle), ResourceType = typeof(Resources.Models.Ticket))]        
        public string? Article { get; set; }

        [Display(Name = nameof(Ticket.IdProduct), ResourceType = typeof(Resources.Models.Ticket))]        
        public string Product { get; set; }

        public int? IdState { get; set; }

        [Display(Name = nameof(Ticket.IdState), ResourceType = typeof(Resources.Models.Ticket))]
        public string State { get; set; }

        
        public int IdType { get; set; }

        [Display(Name = nameof(Ticket.IdType), ResourceType = typeof(Resources.Models.Ticket))]

        public string DescType { get; set; }

        [Display(Name = nameof(Ticket.Priority), ResourceType = typeof(Resources.Models.Ticket))]
        public int Priority { get; set; }

        public int IdCompany { get; set; }
       
        [Display(Name = nameof(Ticket.IdCompany), ResourceType = typeof(Resources.Models.Ticket))]
        public string Company { get; set; }



        [Display(Name = nameof(Ticket.DateOpened), ResourceType = typeof(Resources.Models.Ticket))]
        public DateTime DateOpened { get; set; }


        [Display(Name = nameof(Ticket.Date), ResourceType = typeof(Resources.Models.Ticket))]
        public DateTime? Date { get; set; }

        [Display(Name = nameof(Ticket.Time), ResourceType = typeof(Resources.Models.Ticket))]
        public TimeOnly? Time { get; set; }

        [Display(Name = nameof(Ticket.DateEnd), ResourceType = typeof(Resources.Models.Ticket))]
        public DateTime? DateEnd { get; set; }

        [Display(Name = nameof(Ticket.DateClosed), ResourceType = typeof(Resources.Models.Ticket))]
        public DateTime? DateClosed { get; set; }

        [Display(Name = nameof(Ticket.DateExpired), ResourceType = typeof(Resources.Models.Ticket))]
        public DateTime? DateExpired { get; set; }

        [Display(Name = nameof(Ticket.Description), ResourceType = typeof(Resources.Models.Ticket))]
        public string Description { get; set; }

        [Display(Name = nameof(Ticket.MinuteWork), ResourceType = typeof(Resources.Models.Ticket))]
        public int MinuteWork { get; set; }

        [Display(Name = nameof(TicketModel.MinuteWorkFormatted), ResourceType = typeof(Resources.Models.TicketModel))]
        public string MinuteWorkFormatted { get; set; }
        
        [Display(Name = nameof(Ticket.IdUserOpened), ResourceType = typeof(Resources.Models.Ticket))]
        public string IdUserOpened { get; set; }

        [Display(Name = nameof(Ticket.IdUserOpened), ResourceType = typeof(Resources.Models.Ticket))]
        public string UserOpened { get; set; }


        [Display(Name = nameof(Ticket.IdUserCustomer), ResourceType = typeof(Resources.Models.Ticket))]
        public string UserCustomer { get; set; }

        public int IdContact { get; set; }

        [Display(Name = nameof(Ticket.IdContact), ResourceType = typeof(Resources.Models.Ticket))]
        public string ContactName { get; set; }

        [Display(Name = nameof(Ticket.IdUserAssigned), ResourceType = typeof(Resources.Models.Ticket))]
        public string IdUserAssigned { get; set; }

        
        public string UserAssigned { get; set; } = null;

        [Display(Name = nameof(Ticket.IdGroupAssigned), ResourceType = typeof(Resources.Models.Ticket))]
        public string GroupAssigned { get; set; }

        public int Progress { get; set; }

        public string Numero { get; set; }

        [Display(Name = nameof(Ticket.Support), ResourceType = typeof(Resources.Models.Ticket))]
        public int Support { get; set; }

        [Display(Name = nameof(Ticket.Closed), ResourceType = typeof(Resources.Models.Ticket))]
        public bool Closed { get; set; }

        [Display(Name = nameof(Ticket.CloseDescription), ResourceType = typeof(Resources.Models.Ticket))]
        public string CloseDescription { get; set; }

        [Display(Name = nameof(Ticket.CloseNote), ResourceType = typeof(Resources.Models.Ticket))]
        public string CloseNote { get; set; }

        [Display(Name = nameof(Ticket.IdUserClosed), ResourceType = typeof(Resources.Models.Ticket))]
        public string UserClosed { get; set; }

        
        [Display(Name = nameof(Ticket.IdProject), ResourceType = typeof(Resources.Models.Ticket))]
        public string Project { get; set; }

        public bool Invoiced { get; set; }
        public string StateColor { get; set; }

        public int Permits { get; set; }

        public int Rank { get; set; }

        public TicketType? TicketType { get; set; }  

    }

}
