using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CRM.Shared.DTOs
{
    // ---------------------------------------------------------------------------------------
    // Contratto con l'app da campo
    // ---------------------------------------------------------------------------------------
    // Le chiavi non stanno piu' qui: sono in ApiKeyDTO, condivise con gli altri ambiti.

    /// <summary>Risposta di "verifica connessione": conferma chiave, server e persona.</summary>
    public class FieldPingResponse
    {
        public bool Ok { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string? KeyName { get; set; }

        public DateTime? ExpiresAt { get; set; }
    }

    /// <summary>Una fiera fra cui scegliere nell'app: il minimo per riempire un elenco a tendina.</summary>
    public class FieldInitiativeDTO
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Location { get; set; }

        public DateTime DateFrom { get; set; }

        public DateTime DateTo { get; set; }

        /// <summary>Vero se oggi cade nel periodo: l'app puo' preselezionarla.</summary>
        public bool IsCurrent { get; set; }
    }

    /// <summary>
    /// Un biglietto inviato dall'app. La foto viaggia nella stessa richiesta multipart, non in una
    /// chiamata separata: due richieste che possono fallire indipendentemente lasciano allegati
    /// orfani o lead senza la loro fonte, e da un telefono in fiera succede spesso.
    /// </summary>
    public class FieldLeadRequest
    {
        public int IdInitiative { get; set; }

        public string? Name { get; set; }

        public string? CompanyName { get; set; }

        public string? JobTitle { get; set; }

        public string? Email { get; set; }

        public string? Phone { get; set; }

        /// <summary>Cosa voleva: il campo che a sera nessuno ricostruisce piu'.</summary>
        public string? Note { get; set; }

        /// <summary>0-100. L'app manda 80/50/20 per caldo/tiepido/freddo.</summary>
        public int Score { get; set; } = 50;

        /// <summary>
        /// Quando e' stato raccolto sul telefono. Serve perche' un biglietto puo' restare in coda
        /// giorni: la data del lead dev'essere quella della fiera, non quella dell'invio.
        /// </summary>
        public DateTime? CapturedAt { get; set; }

        /// <summary>
        /// Identificativo generato dall'app. Se l'invio riesce ma la risposta non arriva, il
        /// tentativo successivo non crea un doppione.
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// Chiede al server di leggere la foto e riempire i campi rimasti vuoti. Serve ai biglietti
        /// raccolti senza rete, per i quali l'OCR non e' stato possibile al momento dello scatto.
        /// </summary>
        public bool AutoFillFromCard { get; set; }
    }

    public class FieldLeadResponse
    {
        public bool Ok { get; set; }

        public int IdLead { get; set; }

        public string? Message { get; set; }

        /// <summary>Vero quando il lead esisteva gia' (stesso <c>ClientId</c>): non e' un errore.</summary>
        public bool Duplicate { get; set; }
    }
}
