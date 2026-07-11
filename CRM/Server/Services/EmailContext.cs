using CRM.Shared;

namespace CRM.Server.Services
{
    /// <summary>
    /// Contesto CRM opzionale di una email in uscita: collega la comunicazione a un'entità
    /// (Azienda/Contatto/Lead/Deal/Ticket) così da registrarla anche nella timeline Attività,
    /// oltre che nel registro delle email inviate. Se assente, l'email resta una semplice
    /// notifica transazionale (registrazione, promemoria, ...) senza aggancio alla scheda.
    /// </summary>
    public sealed class EmailContext
    {
        public EmailContext(ActivityEntityType entityType, int entityId)
        {
            EntityType = entityType;
            EntityId = entityId;
        }

        public ActivityEntityType EntityType { get; }

        public int EntityId { get; }
    }
}
