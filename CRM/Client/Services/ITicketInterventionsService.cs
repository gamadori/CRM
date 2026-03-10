using CRM.Shared;
using CRM.Shared.Models;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface ITicketInterventionsService : IBaseRestService<TicketIntervention, TicketInterventionFilter, int>
    {
        Task<bool> UploadReport(int id, UploadFilesModel item);

        Task<bool> CreateReport(int id, string? languageCode = null);

        Task<string?> GetReport(int id);

        Task<bool> SendReportEmail(int id, EmailViewModel email);

        Task<List<string>> GetCompanyEmailAddresses(int id);

        Task<HttpResponseMessage> AssignUsers(int id, List<string> userIds);

        Task<HttpResponseMessage> SaveSignature(int id, SignatureData signatureData);

        Task<HttpResponseMessage> SaveSignatureWithEmailConfirmation(int id, SignatureDataWithEmail signatureData);

        Task<HttpResponseMessage> ResendSignatureConfirmation(int id, ResendEmailRequest request);
    }
}
