using CRM.Client.Helpers;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace CRM.Client.Pages.TicketChats
{
    public partial class TestIndex: ComponentBase
    {
        public List<TicketChatViewModel> ChatList { get; set; } = new List<TicketChatViewModel>();
        public int TotalSize { get; set; }

        [Inject]
        public HttpClient HttpClient { get; set; }


        private async Task GetProducts(PagingParameterModel param)
        {
            var virtualizeResult = await RestClientHelper.Get<TicketChatViewModel>(HttpClient, $"{ConstHelper.TicketChatsPath}", param);
            ChatList = virtualizeResult.Items;
            TotalSize = virtualizeResult.MetaData.TotalCount;
        }
    }
}
