using CRM.Shared;
using Contact = CRM.Shared.Contact;

namespace CRM.Client.Services
{
    public interface IAccessoryTypesService: IRestClientModelService<AccessoryType, AccessoryTypeModel, AccessoryTypeFilter, int>
    {
    }
}
