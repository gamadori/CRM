using CRM.Shared;
using CRM.Shared.DTOs;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.Generic;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace CRM.Client.Helpers
{
    public static class HubHelper
    {
        public static HubConnection HubConnection { get; set; } = null;
        public static MaintenanceNoticeDTO? CurrentMaintenanceNotice { get; private set; }
        public static event Action<MaintenanceNoticeDTO>? MaintenanceNoticeChanged;

        public static void SetMaintenanceNotice(MaintenanceNoticeDTO notice)
        {
            CurrentMaintenanceNotice = notice;
            MaintenanceNoticeChanged?.Invoke(notice);
        }

        public static void Timertick()
        {
            try
            {
                
                //StateHasChanged();
                if (HubConnection != null && HubConnection.State == HubConnectionState.Disconnected)
                {
                    //_logger?.LogError("Error, Attempting to Restart SignalR Service from within Timer");
                    HubConnection.StartAsync();
                }
            }
            catch (Exception e)
            {
                //_logger.LogError("Error in Timer Code " + e.Message);
            }
            
        }

        public static bool IsConnected()
        {
            return HubConnection.State == HubConnectionState.Connected;
        }
    }
}
