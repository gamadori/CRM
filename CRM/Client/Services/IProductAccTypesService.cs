using CRM.Shared;
using Contact = CRM.Shared.Contact;

namespace CRM.Client.Services
{
    public interface IProductAccTypesService : IRestClientModelService<ProductAccessoryType, ProductAccessoryTypeModel, ProductAccessoryTypeFilter, int>
    {
    }
}
