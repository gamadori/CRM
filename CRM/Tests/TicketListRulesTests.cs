using CRM.Server.Services;
using CRM.Shared;

namespace CRM.Tests;

/// <summary>
/// Le regole con cui l'elenco ticket decide stato e pulsanti. Sono state estratte dal servizio
/// quando la lista e' stata resa veloce: prima vivevano dentro il ciclo che interrogava il
/// database una riga alla volta, e non erano verificabili senza un database attorno.
/// <para>
/// Qui interessa soprattutto cio' che NON deve accadere: un pulsante di presa in carico o di
/// blocco a chi non ne ha diritto non da' errore a nessuno, semplicemente c'e'.
/// </para>
/// </summary>
public class TicketListRulesTests
{
    private static readonly DateTime Oggi = new(2026, 8, 12);

    private static eTicketStates Stato(
        bool closed = false,
        bool isClient = false,
        bool hasAssignedUser = false,
        DateTime? dateExpired = null,
        bool hasAssignee = false)
        => TicketListRules.ResolveState(closed, isClient, hasAssignedUser, dateExpired, Oggi, hasAssignee);

    [Fact]
    public void Un_ticket_chiuso_e_chiuso_qualunque_altra_cosa_sia_vera()
    {
        Assert.Equal(eTicketStates.Closed, Stato(closed: true, dateExpired: Oggi.AddDays(-10), hasAssignee: true));
    }

    [Fact]
    public void Il_cliente_vede_sempre_in_lavorazione()
    {
        // Gli stati interni (creato, assegnato, scaduto) sono affari nostri.
        Assert.Equal(eTicketStates.Processing, Stato(isClient: true, dateExpired: Oggi.AddDays(-10)));
        Assert.Equal(eTicketStates.Processing, Stato(isClient: true));
    }

    /// <summary>
    /// Dentro l'azienda "in lavorazione" non esiste piu': un ticket assegnato e' un ticket su cui si
    /// lavora. Cambia anche una conseguenza: prima chi ci stava lavorando non risultava in ritardo,
    /// ora un ticket oltre la scadenza si vede scaduto anche se qualcuno lo ha in mano - ed e' vero,
    /// perche' in ritardo lo e'.
    /// </summary>
    [Fact]
    public void In_lavorazione_non_e_piu_uno_stato_a_se()
    {
        Assert.Equal(eTicketStates.Assigned, Stato(hasAssignedUser: true, hasAssignee: true));

        Assert.Equal(eTicketStates.Expired, Stato(
            hasAssignedUser: true, dateExpired: Oggi.AddDays(-3), hasAssignee: true));
    }

    [Fact]
    public void La_scadenza_di_oggi_non_e_ancora_un_ritardo()
    {
        Assert.Equal(eTicketStates.Created, Stato(dateExpired: Oggi));
        Assert.Equal(eTicketStates.Expired, Stato(dateExpired: Oggi.AddDays(-1)));
    }

    [Fact]
    public void Senza_scadenza_conta_solo_se_e_assegnato()
    {
        Assert.Equal(eTicketStates.Assigned, Stato(hasAssignee: true));
        Assert.Equal(eTicketStates.Created, Stato(hasAssignee: false));
    }

    private static bool Claim(
        bool hasCurrentUser = true,
        bool closed = false,
        bool assignedToCurrentUser = false,
        bool isAdminOrSuperUser = false,
        bool belongsToHeadCompany = true,
        int? idGroupAssigned = null,
        bool currentUserInAssignedGroup = false,
        bool currentUserAmongTypeAssignees = false)
        => TicketListRules.CanClaim(hasCurrentUser, closed, assignedToCurrentUser, isAdminOrSuperUser,
            belongsToHeadCompany, idGroupAssigned, currentUserInAssignedGroup, currentUserAmongTypeAssignees);

    [Fact]
    public void Non_si_prende_in_carico_un_ticket_chiuso_o_gia_proprio()
    {
        Assert.False(Claim(closed: true, isAdminOrSuperUser: true));
        Assert.False(Claim(assignedToCurrentUser: true, isAdminOrSuperUser: true));
        Assert.False(Claim(hasCurrentUser: false, isAdminOrSuperUser: true));
    }

    [Fact]
    public void Chi_non_e_di_casa_non_prende_in_carico_niente()
    {
        Assert.False(Claim(belongsToHeadCompany: false, currentUserAmongTypeAssignees: true));
        Assert.False(Claim(belongsToHeadCompany: false, idGroupAssigned: 7, currentUserInAssignedGroup: true));
    }

    [Fact]
    public void Sul_ticket_di_un_gruppo_decide_l_appartenenza_al_gruppo()
    {
        Assert.True(Claim(idGroupAssigned: 7, currentUserInAssignedGroup: true));

        // Fuori dal gruppo non basta essere fra gli assegnabili del tipo.
        Assert.False(Claim(idGroupAssigned: 7, currentUserInAssignedGroup: false, currentUserAmongTypeAssignees: true));
    }

    [Fact]
    public void Senza_gruppo_decide_chi_e_assegnabile_per_quel_tipo()
    {
        Assert.True(Claim(currentUserAmongTypeAssignees: true));
        Assert.False(Claim(currentUserAmongTypeAssignees: false));
    }

    [Fact]
    public void Admin_e_superuser_prendono_in_carico_anche_fuori_dal_gruppo()
    {
        Assert.True(Claim(isAdminOrSuperUser: true, belongsToHeadCompany: false, idGroupAssigned: 7));
    }

    private static bool Block(
        bool canAccessCompany = true,
        bool isAdminOrSuperUser = false,
        bool belongsToHeadCompany = true,
        bool assignedToCurrentUser = false,
        bool commessaResponsibleIsCurrentUser = false)
        => TicketListRules.CanManageBlock(canAccessCompany, isAdminOrSuperUser, belongsToHeadCompany,
            assignedToCurrentUser, commessaResponsibleIsCurrentUser);

    [Fact]
    public void Il_blocco_richiede_prima_di_tutto_di_poter_vedere_l_azienda()
    {
        // Nemmeno l'amministratore blocca un ticket di un'azienda fuori dal suo perimetro.
        Assert.False(Block(canAccessCompany: false, isAdminOrSuperUser: true));
    }

    [Fact]
    public void Blocca_chi_ha_il_ticket_in_carico_o_ne_e_responsabile_in_commessa()
    {
        Assert.True(Block(assignedToCurrentUser: true));
        Assert.True(Block(commessaResponsibleIsCurrentUser: true));
        Assert.False(Block());
    }

    [Fact]
    public void Chi_non_e_di_casa_non_blocca_nemmeno_il_proprio_ticket()
    {
        Assert.False(Block(belongsToHeadCompany: false, assignedToCurrentUser: true));
    }
}
