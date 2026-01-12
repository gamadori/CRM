using CRM.Shared;
using FluentValidation;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Validators
{
    public class TicketValidator: AbstractValidator<Ticket>
    {
        
        public TicketValidator(IStringLocalizer<CRM.Shared.Resources.App> localize)
        {
            RuleFor(x => x.IdCompany).NotEmpty().WithMessage(localize["Select the Client"]);
            RuleFor(x => x.IdType).NotEmpty().When(x => x.Step == TicketCreateSteps.TypeTicket).WithMessage(localize["Select the type of Ticket"]);

            RuleFor(x => x.IdArticle).NotEmpty().When(x => x.Step == TicketCreateSteps.ProductTicket && x.IdProduct == null && x.TicketType?.IdProdotto == (int)PropertyStates.Required).WithMessage(localize["Select the Product"]);
            RuleFor(x => x.IdProduct).NotEmpty().When(x => x.Step == TicketCreateSteps.ProductTicket && x.IdArticle == null && x.TicketType?.IdArticolo == (int)PropertyStates.Required).WithMessage(localize["Select the article"]);
            
            RuleFor(x => x.Date).NotEmpty().When(x => x.Step == TicketCreateSteps.DateTicket && x.TicketType?.Date == (int)PropertyStates.Required).WithMessage(localize["Set the date of the event"]);
            RuleFor(x => x.Time).NotEmpty().When(x => x.Step == TicketCreateSteps.DateTicket && x.TicketType?.Time == (int)PropertyStates.Required).WithMessage(localize["Set the time of the event"]);
            RuleFor(x => x.Description).NotEmpty().When(x => x.Step == TicketCreateSteps.DescriptionTicket).WithMessage(localize["Inserire la descrizione del Ticket"]);
            
        }
    }
}
