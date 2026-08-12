using System.Reflection;
using CRM.Server.Controllers;
using CRM.Shared;

namespace CRM.Tests;

/// <summary>
/// Scadenza del link di firma. Il token e' un GUID, quindi non si indovina, ma prima non moriva
/// mai: chi ritrovava l'email mesi dopo poteva ancora firmare un verbale di marzo.
/// <para>
/// La regola che conta e' quella scomoda: <b>scadenza vuota vale scaduta</b>. Se valesse "senza
/// scadenza", il primo percorso che dimenticasse di impostarla tornerebbe in silenzio al link
/// eterno, e nessuno se ne accorgerebbe finche' non capita.
/// </para>
/// </summary>
public class SignatureTokenExpiryTests
{
    private static readonly MethodInfo Valida = typeof(TicketInterventionsController)
        .GetMethod("SignatureTokenValid", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static bool TokenValido(TicketIntervention intervento, string? token)
        => (bool)Valida.Invoke(null, new object?[] { intervento, token })!;

    private static TicketIntervention ConToken(string token, DateTime? scadenza) => new()
    {
        SignatureConfirmationToken = token,
        SignatureTokenExpiry = scadenza
    };

    [Fact]
    public void Il_token_giusto_dentro_la_finestra_apre()
    {
        var intervento = ConToken("abc123", DateTime.Now.AddDays(3));

        Assert.True(TokenValido(intervento, "abc123"));
    }

    [Fact]
    public void Il_token_giusto_scaduto_non_apre_piu()
    {
        var intervento = ConToken("abc123", DateTime.Now.AddMinutes(-1));

        Assert.False(TokenValido(intervento, "abc123"));
    }

    [Fact]
    public void Senza_scadenza_il_token_e_considerato_scaduto()
    {
        var intervento = ConToken("abc123", null);

        Assert.False(TokenValido(intervento, "abc123"));
    }

    [Theory]
    [InlineData("abc124")]
    [InlineData("abc12")]
    [InlineData("")]
    [InlineData(null)]
    public void Un_token_diverso_non_apre(string? presentato)
    {
        var intervento = ConToken("abc123", DateTime.Now.AddDays(3));

        Assert.False(TokenValido(intervento, presentato));
    }

    [Fact]
    public void Un_intervento_senza_token_non_si_apre_con_niente()
    {
        var intervento = ConToken(string.Empty, DateTime.Now.AddDays(3));

        Assert.False(TokenValido(intervento, "abc123"));
    }
}
