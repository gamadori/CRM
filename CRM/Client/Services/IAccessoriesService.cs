using CRM.Shared;
using Contact = CRM.Shared.Contact;

namespace CRM.Client.Services
{
    public interface IAccessoriesService: IRestClientModelService<Accessory, Accessory, AccessoryFilter, int>
    {
    }
}
