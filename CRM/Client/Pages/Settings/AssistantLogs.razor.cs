using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Settings
{
    public partial class AssistantLogs : ComponentBase
    {
        [Inject] ITicketsService Service { get; set; }

        private List<AssistantChatLogDTO> _items = new();

        private bool _loading;

        private string _search = string.Empty;

        private int? _voteFilter;

        private readonly List<VoteOption> _voteOptions = new()
        {
            new VoteOption { Text = "Tutti i voti", Value = null },
            new VoteOption { Text = "👍 Positivi", Value = 1 },
            new VoteOption { Text = "👎 Negativi", Value = -1 },
            new VoteOption { Text = "Senza voto", Value = 0 },
        };

        private int _countUp;
        private int _countDown;
        private int _countNone;
        private int _satisfaction;

        protected override async Task OnInitializedAsync()
        {
            await Load();
        }

        private async Task Load()
        {
            _loading = true;
            StateHasChanged();

            _items = await Service.GetAssistantLogs(new AssistantChatLogFilter
            {
                Search = string.IsNullOrWhiteSpace(_search) ? null : _search,
                Vote = _voteFilter
            });

            _countUp = _items.Count(i => i.Feedback == 1);
            _countDown = _items.Count(i => i.Feedback == -1);
            _countNone = _items.Count(i => i.Feedback == null);
            var voted = _countUp + _countDown;
            _satisfaction = voted > 0 ? (int)System.Math.Round(_countUp * 100.0 / voted) : 0;

            _loading = false;
            StateHasChanged();
        }

        private sealed class VoteOption
        {
            public string Text { get; set; } = string.Empty;
            public int? Value { get; set; }
        }
    }
}
