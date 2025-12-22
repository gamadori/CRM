using CRM.Shared;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Validators
{
    public class TicketInterventionValidator: AbstractValidator<TicketIntervention>
    {
        public TicketInterventionValidator()
        {
           
            RuleFor(x => x.EndDateTime).GreaterThan(x => x.StartDateTime).WithMessage("La Data di Fine deve essere maggiore della data iniziale");
           

        }
    }
}
