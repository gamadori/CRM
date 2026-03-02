using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;
using Contact = CRM.Shared.Contact;

namespace CRM.Client.Services
{
   
    public interface IContactsService : IDataService<Contact, ContactDTO, int, ContactFilter, object>
    {
       
    }

}
