using CRM.Shared;

namespace CRM.Client.Helpers
{
    public static class TicketHelper
    {
        public static TypeMessage GetTypeMessage(TicketChat ticketChat,int? idCompany)
        {
            try
            {
                if (ticketChat.User?.IdCompany == idCompany)
                    return TypeMessage.Sended;
                else
                    return TypeMessage.Receved;
            }
            catch
            {
                return TypeMessage.Receved;
            }
        }
    }
}
