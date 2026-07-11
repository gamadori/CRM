using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface ILeadService : IDataService<Lead, LeadDTO, int, LeadFilter, decimal>
    {
        Task<APIResponseMessage<DealDTO>> ConvertAsync(int id, ConvertLeadRequest request);
    }
}
