using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Server.Services
{
    public interface IPermitsService
    {
        Task<string> IdUser();

        Task<ApplicationUser> GetUser();

        Task<bool> CanAccessOtherCompany();

        Task<int?> GetIdCompany();

        Task<List<int>> GetIdCompanies();

        Task<List<int>> GetIdCompanies(int idCompany);

        /// <summary>
        /// Id delle aziende di cui l'utente corrente può vedere i dati, oppure <c>null</c> se
        /// può vederli tutti (utente dell'azienda madre). Serve a filtrare per riga le query dei
        /// moduli commerciali. Lista vuota = nessun dato visibile (utente senza azienda).
        /// </summary>
        Task<List<int>?> GetVisibleCompanyIds();

        Task<bool> CanAccessCompany(int? idCompany);

        Task<bool> CanWriteCompanyData(int? idCompany);

        Task<bool> CanManageSettings();

        Task<Company?> GetCompany();

        Task<List<ApplicationUser>> GetAdmins();

        Task<bool> CanGetObject(int? objIdCompany);

        Task<bool> CanEditObject(string objIdOwner);

        Task<bool> CanDeleteObject(string objIdOwner);

        Task<int> ObjectPermits(int? objIdCompany, string objIdOwner);

        Task<bool> CanViewInternalData();

        Task<AuthorizationResult> CheckPolicy(ePolicy policy);

        Task<bool> CheckPolicy(string idUser, ePolicy policy);

        Task<ePolicy?> GetPolicy();

        Task<bool> IsClient();

        Task<bool> IsAdmin(string? idUser = null);

        Task<bool> IsSuperUser();

        Task<bool> IsSuperUser(string idUser);

        Task<bool> IsStandardUser();

        Task<bool> IsStandardUser(string idUser);

        /// <summary>True se l'utente appartiene all'azienda madre (CompanyType = HeadCompany).</summary>
        Task<bool> BelongsToHeadCompany();

        Task<bool> BelongsToHeadCompany(ApplicationUser user);

        Task<bool> BelongsToHeadCompany(string idUser);

        Task<bool> BelongsToReseller();

        Task<List<ApplicationUser>> GetUsersCanAssignTicket(int idTicket);

        Task<List<ApplicationUser>> GetUsersCanAssignTicketType(int idType);

        Task<int> TicketPermits(int idTicket, int IdCompany, string idUserAssigned);

        /// <summary>Come sopra, per chi ha gia' letto stato e assegnazione del ticket.</summary>
        Task<int> TicketPermits(int idTicket, int IdCompany, string idUserAssigned,
            bool isAssignedToCurrentUser, bool isClosed);

        Task<bool> CanGetTicket(int idTicket);

        Task<bool> CanReceveTicket(int idTicket, string idUser);

        Task<bool> CanAssignTicket(string? idUser = null);

       

        Task<bool> CanReOpenTicket(int idTicket);

        Task<bool> CanEditTicket();

        Task<bool> CanCloseTicket(int idTicket);

        /// <summary>True se il ticket e' assegnato all'utente corrente (assegnazione singola o multipla).</summary>
        Task<bool> IsAssignedToTicket(int idTicket);

        /// <summary>True se l'utente puo' registrare interventi sul ticket: assegnatario, Admin o SuperUser.</summary>
        Task<bool> CanAddTicketIntervention(int idTicket);

        /// <summary>True se la richiesta di assegnazione e' consentita: Admin e SuperUser sempre,
        /// gli altri solo per assegnare o togliere se stessi, e solo su ticket a loro assegnabili.</summary>
        Task<bool> CanAssignTicketUsers(int idTicket, IEnumerable<string>? userIds);

        Task<bool> CanInsertTicketChat(int idTicket);

        Task<bool> CanEditTicketChat(int id);

        Task<bool> CanReadTicketChat(int idTicket, string? idUser = null);

        Task<string[]> GetUsersCanReadTicketChat(int idTicket);

        Task<bool> CanDownloadInterventionReport(int idIntervention);

        Task<bool> CanDeleteAttachment(string owner);

        Task<bool> CanEditAttachment(string owner);

        Task<bool> CanDeleteAttachment(int idAttachment);

        Task<bool> CanGetDeal(int idDeal);

        Task<bool> CanInsertDeal();

        Task<bool> CanEditDeal(int idDeal);

        Task<bool> CanDeleteDeal(int idDeal);

        Task<int> DealPermits(int idDeal);

        bool CanGetAccessoryType();

        Task<bool> CanInsertAccessoryType();

        Task<bool> CanEditAccessoryType();

        Task<bool> CanDeleteAccessoryType();

        Task<int> AccesoryTypePermits();

        bool CanGetAccessory();

        Task<bool> CanInsertAccessory();

        Task<bool> CanEditAccessory();

        Task<bool> CanDeleteAccessory();

        Task<int> AccesoryPermits();

        bool CanGetContractType();

        Task<bool> CanInsertContractType();


        Task<bool> CanEditContractType();


        Task<bool> CanDeleteContractType();


        Task<int> ContractTypePermits();

        Task<int> ProductPermits();

        Task<int> ArticlePermits();

        Task<IQueryable<Article>> ArticlesQuery(IQueryable<Article> query, string? idUser = null);

        Task<bool> ArticleCanAccess(int? idArticle);
        Task<int> CompanyPermits();

        Task<PermitResponse> CompanyCanAccess(int? idCompany);

        Task<CompanyTypes?> CompanyType();

        Task<int> UserPermits();




        Task<int> AttachmentPermits(int idAttachment);

        Task<List<string>?> GetUserSendTicketChatAlert(int idTicketChat);


    }
}
