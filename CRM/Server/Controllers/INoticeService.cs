namespace CRM.Server.Controllers
{
    public interface INoticeService
    {
        Task SendNoticeNewUserToAdmins(string idUser);
    }
}
