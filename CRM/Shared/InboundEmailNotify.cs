namespace CRM.Shared
{
    /// <summary>
    /// Notifica in tempo reale (SignalR) dell'arrivo di una email in ingresso. Recapitata agli
    /// operatori: MainLayout mostra il toast + suono, la Dashboard aggiorna il contatore.
    /// </summary>
    public class InboundEmailNotify : SerializedNotification
    {
        public int Id { get; set; }

        public string? From { get; set; }

        public string? Subject { get; set; }

        public InboundEmailNotify() { }
    }
}
