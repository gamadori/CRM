using CRM.Shared;

namespace CRM.Server.Services
{
    /// <summary>
    /// Le decisioni che l'elenco ticket prende su ogni riga: che stato mostrare, se si puo'
    /// prendere in carico, se si puo' gestire il blocco.
    /// <para>
    /// Stanno qui, separate dalle query, per due motivi. Il primo e' che sono le stesse regole per
    /// una riga di lista e per la scheda di un ticket: un'unica scrittura non puo' divergere. Il
    /// secondo e' che senza database attorno si possono provare - e sono regole di permesso, dove
    /// sbagliare non da' errore, da' un pulsante in mano a chi non doveva averlo.
    /// </para>
    /// </summary>
    public static class TicketListRules
    {
        /// <summary>
        /// Lo stato da mostrare.
        /// <para>
        /// Dentro l'azienda "in lavorazione" non esiste piu' come stato a se': un ticket assegnato
        /// a qualcuno e' un ticket su cui si sta lavorando, e distinguere le due cose voleva dire
        /// raccontare chi aveva premuto quale pulsante invece di dire a che punto e' il lavoro.
        /// Chi lo ha in mano lo si legge dall'assegnatario, quanto ci ha lavorato dagli interventi.
        /// </para>
        /// <para>
        /// Il cliente resta un caso a parte: vede sempre e solo "in lavorazione", perche' i nostri
        /// stati interni - aperto, assegnato, scaduto - non lo riguardano.
        /// </para>
        /// </summary>
        public static eTicketStates ResolveState(
            bool closed,
            bool isClient,
            bool hasAssignedUser,
            DateTime? dateExpired,
            DateTime today,
            bool hasAssignee)
        {
            if (closed)
                return eTicketStates.Closed;

            if (isClient)
                return eTicketStates.Processing;

            if (dateExpired != null && today > dateExpired)
                return eTicketStates.Expired;

            if (hasAssignee)
                return eTicketStates.Assigned;

            return eTicketStates.Created;
        }

        /// <summary>
        /// Se l'utente corrente puo' prendere in carico il ticket. Un ticket chiuso non si prende,
        /// e nemmeno uno che si ha gia': il pulsante servirebbe a nulla. Admin e SuperUser sempre;
        /// gli altri devono essere di casa e, se il ticket e' su un gruppo, appartenere a quel
        /// gruppo. Senza gruppo si guarda chi e' assegnabile per quel tipo di ticket.
        /// </summary>
        public static bool CanClaim(
            bool hasCurrentUser,
            bool closed,
            bool assignedToCurrentUser,
            bool isAdminOrSuperUser,
            bool belongsToHeadCompany,
            int? idGroupAssigned,
            bool currentUserInAssignedGroup,
            bool currentUserAmongTypeAssignees)
        {
            if (!hasCurrentUser || closed || assignedToCurrentUser)
                return false;

            if (isAdminOrSuperUser)
                return true;

            if (!belongsToHeadCompany)
                return false;

            if (idGroupAssigned != null)
                return currentUserInAssignedGroup;

            return currentUserAmongTypeAssignees;
        }

        /// <summary>
        /// Se l'utente corrente puo' bloccare o sbloccare il ticket. Serve vederlo (l'azienda),
        /// essere di casa, e averci a che fare: averlo in carico o essere il responsabile della
        /// commessa da cui nasce. Admin e SuperUser sempre.
        /// </summary>
        public static bool CanManageBlock(
            bool canAccessCompany,
            bool isAdminOrSuperUser,
            bool belongsToHeadCompany,
            bool assignedToCurrentUser,
            bool commessaResponsibleIsCurrentUser)
        {
            if (!canAccessCompany)
                return false;

            if (isAdminOrSuperUser)
                return true;

            if (!belongsToHeadCompany)
                return false;

            return assignedToCurrentUser || commessaResponsibleIsCurrentUser;
        }
    }
}
