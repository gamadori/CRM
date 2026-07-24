using CRM.Shared;

namespace CRM.Client.Helpers
{
    /// <summary>Helper di presentazione per gli stati di commessa e fase.</summary>
    public static class CommessaUi
    {
        public static string StateText(CommessaStates s) => s switch
        {
            CommessaStates.Planned => "Pianificata",
            CommessaStates.InProgress => "In lavorazione",
            CommessaStates.Suspended => "Sospesa",
            CommessaStates.Testing => "Collaudo",
            CommessaStates.Completed => "Completata",
            CommessaStates.Delivered => "Consegnata",
            CommessaStates.Cancelled => "Annullata",
            _ => s.ToString()
        };

        public static string StateBadgeClass(CommessaStates s) => s switch
        {
            CommessaStates.Planned => "bg-secondary",
            CommessaStates.InProgress => "bg-primary",
            CommessaStates.Suspended => "bg-warning text-dark",
            CommessaStates.Testing => "bg-info text-dark",
            CommessaStates.Completed => "bg-success",
            CommessaStates.Delivered => "bg-success",
            CommessaStates.Cancelled => "bg-danger",
            _ => "bg-light text-dark"
        };

        public static string FaseStateText(CommessaFaseStates s) => s switch
        {
            CommessaFaseStates.Pending => "Da fare",
            CommessaFaseStates.InProgress => "In corso",
            CommessaFaseStates.Done => "Completata",
            _ => s.ToString()
        };

        public static string RowStatusText(RowProductionStatus s) => s switch
        {
            RowProductionStatus.None => "Da avviare",
            RowProductionStatus.Ready => "Pronto",
            RowProductionStatus.InProduction => "In produzione",
            RowProductionStatus.Closed => "Chiusa",
            _ => s.ToString()
        };

        public static string RowStatusBadgeClass(RowProductionStatus s) => s switch
        {
            RowProductionStatus.None => "bg-light text-dark border",
            RowProductionStatus.Ready => "bg-success",
            RowProductionStatus.InProduction => "bg-primary",
            RowProductionStatus.Closed => "bg-secondary",
            _ => "bg-light text-dark"
        };
    }
}
