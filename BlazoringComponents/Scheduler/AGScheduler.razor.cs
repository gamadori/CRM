using AGUtility.Extensions;
using BlazoringComponents.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Radzen;
using CRM.Shared;
using System.Runtime.CompilerServices;

namespace BlazoringComponents.Scheduler
{
    public enum SchedulerViews
    {
        Day,
        Week,
        Month
    }

    public enum SchedulerViewMode
    {
        Sheduler,
        Calendar
    }
    public partial class AGScheduler<TItem>: ComponentBase
    {
       

        [Inject]
        private IJSRuntime JSRuntime { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        private IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        DialogService DialogService { get; set; }   
        


        [Parameter]
        public SchedulerViews CurrentView { get; set; } = SchedulerViews.Month;

        [Parameter]
        public EventCallback<SchedulerViews> CurrentViewChanged { get; set; }

        [Parameter]
        public string DateProperty { get; set; }
        [Parameter]
        public string TimeProperty { get; set; }

        [Parameter]
        public string DateEndProperty { get; set; }

        [Parameter]
        public string UserProperty { get; set; }
        [Parameter]
        public string CompanyProperty { get; set; }
        [Parameter]
        public string BackColorProperty { get; set; }

        [Parameter]
        public string DescriptionProperty { get; set; }

        [Parameter]
        public DateTime DateCurrent 
        {
            get { return _dateCurrent; } 
            set 
            { 
                if (_dateCurrent != value)
                {
                    _dateCurrent = value;
                    DateCurrentChanged.InvokeAsync(_dateCurrent);
                }
            } 
        }
        
        [Parameter]
        public EventCallback<DateTime> DateCurrentChanged { get; set; }

        [Parameter]
        public SchedulerViewMode ViewMode { get; set; } = SchedulerViewMode.Sheduler;

        [Parameter]
        public IEnumerable<TItem> Items { get; set; }

        [Parameter] 
        public RenderFragment Filter { get; set; }

        [Parameter]
        public RenderFragment<object> Details { get; set; }

        [Parameter] 
        public  Func<Task<List<TItem>>> Loader { get; set; }

        [Parameter]
        public Action<DateTime, DateTime, DateTime> UpdatePeriod { get; set; }

        [Parameter]
        public bool EnableSelection { get; set; }

        [Parameter]
        public bool DateLocked { get; set; }

        [Parameter]
        public EventCallback<DateTime> OnSelect { get; set; }

        [Parameter]
        public EventCallback<DateTime> OnNewTicket { get; set; }

        [Parameter]
        public bool Loading { get; set; } = true;

        protected string _period;

        protected DateTime _dateStart;

        protected DateTime _dateEnd;

        private DateTime _dateStartLast;

        private DateTime _dateEndLast;

        protected string _activeDay = "";
        protected string _activeWeek = "";
        protected string _activeMonth = "";

        private object _id = null;

        private DateTime _dateCurrent = DateTime.Today;

        private bool _isMobile = false;

        public async Task Update()
        {
            await Period(true);
            StateHasChanged();
        }

        protected override async Task OnInitializedAsync()
        {
            await CheckIfMobile();
            await Period();
            await base.OnInitializedAsync();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await CheckIfMobile();
                if (_isMobile && CurrentView == SchedulerViews.Month)
                {
                    CurrentView = SchedulerViews.Week;
                    await CurrentViewChanged.InvokeAsync(CurrentView);
                    await Period();
                    StateHasChanged();
                }
            }
            await base.OnAfterRenderAsync(firstRender);
        }

        private async Task CheckIfMobile()
        {
            try
            {
                _isMobile = await JSRuntime.InvokeAsync<bool>("eval", "window.innerWidth <= 768");
            }
            catch
            {
                _isMobile = false;
            }
        }

        protected override async Task OnParametersSetAsync()
        {
           

            await base.OnParametersSetAsync();
        }
        
        protected async Task ViewSelected(SchedulerViews view)
        {
            CurrentView = view;

            await CurrentViewChanged.InvokeAsync(CurrentView);

            await Period();

            StateHasChanged();
        }

        private async Task ViewChanged()
        {
            await CheckIfMobile();
            
            if (_isMobile && CurrentView == SchedulerViews.Month)
            {
                CurrentView = SchedulerViews.Week;
            }

            await CurrentViewChanged.InvokeAsync(CurrentView);

            await Period();

            StateHasChanged();
        }
        protected async Task Period(bool forceUpdate = false)
        {
            try
            {
                _activeDay = "";
                _activeWeek = "";
                _activeMonth = "";
                switch (CurrentView)
                {
                    case SchedulerViews.Day:

                        _dateStart = DateCurrent;
                        _dateEnd = DateCurrent;
                        _period = DateCurrent.ToShortDateString();
                        _activeDay = "active";
                        break;

                    case SchedulerViews.Week:
                        _dateStart = DateCurrent.Date.GetFirstDayOfWeek();
                        _dateEnd = _dateStart.AddDays(6);
                        _period = $"{Localize["From"]} {_dateStart.ToShortDateString()} {Localize["To"]} {_dateEnd.ToShortDateString()}";
                        _activeWeek = "active";
                        break;

                    case SchedulerViews.Month:
                        _dateStart = DateCurrent.Date.GetFirstDayOfMonth().GetFirstDayOfWeek();
                        _dateEnd = DateCurrent.Date.GetLastDayOfMonth().GetLastDayOfWeek();

                        _period = $"{DateCurrent.Date.GetMonthName()} {DateCurrent.Year}";
                        _activeMonth = "active";
                    
                        break;

                }
                if (_dateStart != _dateStartLast || _dateEnd != _dateEndLast || forceUpdate)
                {

                    UpdatePeriod?.Invoke(_dateStart, _dateEnd, DateCurrent);
                    _dateStartLast = _dateStart;
                    _dateEndLast = _dateEnd;
                   
                    Items = await Loader();
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }

        protected async Task Next()
        {
            switch (CurrentView)
            {
                case SchedulerViews.Week:
                    NextWeek();
                    break;

                case SchedulerViews.Day:
                    NextDay();
                    break;

                case SchedulerViews.Month:
                    NextMonth();
                    break;
            }
            await Period ();
            
        }

        protected async Task Previous()
        {
            switch (CurrentView)
            {
                case SchedulerViews.Week:
                    PreviousWeek();
                    break;
                case SchedulerViews.Day:
                    PreviousDay();
                    break;

                case SchedulerViews.Month:
                    PreviousMonth();
                    break;

            }
            await Period ();
        }

        protected void PreviousDay()
        {
            DateCurrent = DateCurrent.AddDays(-1);
        }

        protected void NextDay()
        {
            DateCurrent = DateCurrent.AddDays(1);
        }

        protected void PreviousWeek()
        {
            DateCurrent = DateCurrent.AddDays(-7);
        }

        
        protected void NextWeek()
        {
            DateCurrent = DateCurrent.AddDays(7);
            
        }

        protected void PreviousMonth()
        {
            DateCurrent = DateCurrent.AddMonths(-1);
        }


        protected void NextMonth()
        {
            DateCurrent = DateCurrent.AddMonths(1);
        }


        protected async Task Today()
        {
            DateCurrent = DateTime.Today;
            await Period();
            
        }

        protected async void OpenModal(string id)
        {
            _id = id;

            //await JSRuntime.InvokeVoidAsync("ShowModal", "modalDetails");
            StateHasChanged();
            await JSRuntime.InvokeVoidAsync("ShowCanvas", "modalDetails");
            
        }

        protected async Task OpenDaylyScheduler(DateTime date)
        {
            DateCurrent = date;
            await ViewSelected(SchedulerViews.Day);
            StateHasChanged();
        }
        protected async Task OnChangeDate(Microsoft.AspNetCore.Components.ChangeEventArgs args)
        {
            if (DateTime.TryParse((string)args.Value, out DateTime date))
            {
                DateCurrent = date;
                await Period();
            }
        }


        private async void OnSelectDate(DateTime date)
        {

            DateCurrent = date;
            
        }

        protected async void OpenTicket(object id)
        {
            await JSRuntime.InvokeVoidAsync("CloseModal", "modalDetails");
            NavigationManager.NavigateTo($"/Tickets/{id}");
        }

        
    }
}
