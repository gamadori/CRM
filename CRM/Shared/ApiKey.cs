using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    /// <summary>
    /// A che cosa serve una chiave. E' la parte che rende sicura la tabella unica: una chiave vale
    /// per il suo ambito e per nessun altro, quindi quella di una macchina non puo' scrivere lead
    /// e quella di un telefono in fiera non puo' leggere i backup.
    /// </summary>
    public enum ApiKeyScope
    {
        /// <summary>Macchine e sistemi esterni che leggono o caricano backup.</summary>
        MachineBackup = 1,

        /// <summary>Cliente esterno che apre e consulta i propri ticket.</summary>
        ExternalTicket = 2,

        /// <summary>App di cattura biglietti in fiera.</summary>
        Field = 3
    }

    /// <summary>
    /// Cosa puo' fare la chiave. Oggi distingue qualcosa solo per i backup; sugli altri ambiti la
    /// distinzione fra leggere e scrivere non ha ancora un significato, e resta <c>ReadWrite</c>.
    /// </summary>
    public enum ApiKeyPermission
    {
        ReadOnly = 1,
        ReadWrite = 2
    }

    /// <summary>
    /// Una chiave di accesso all'API, per tutti gli usi che non passano dal login.
    /// <para>
    /// Nasce dall'unificazione di tre tabelle quasi identiche (backup macchina, ticket esterni,
    /// app fiera): stessa generazione, stessa impronta, stessi metadati, e come unica vera
    /// differenza <b>a cosa sono intestate</b>. Tenerle separate significava tre punti di verifica
    /// da ricordare di correggere insieme, e - nel caso peggiore - una pagina di gestione che ne
    /// copriva solo una su tre.
    /// </para>
    /// <para>
    /// Della chiave si conserva solo l'impronta SHA-256: chi la perde ne genera un'altra, non la
    /// recupera. Il prefisso serve a riconoscerla nell'elenco senza poterla ricostruire, e la sua
    /// sigla dice l'ambito (<c>crmbk</c>, <c>crmtk</c>, <c>crmfd</c>).
    /// </para>
    /// </summary>
    public class ApiKey
    {
        [Key]
        public int Id { get; set; }

        public ApiKeyScope Scope { get; set; }

        /// <summary>A chi o a cosa e' intestata: "Linea collaudo", "Portale Rossi", "Telefono di Marco".</summary>
        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(64)]
        public string KeyHash { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? KeyPrefix { get; set; }

        public ApiKeyPermission Permission { get; set; } = ApiKeyPermission.ReadWrite;

        /// <summary>Azienda per conto della quale la chiave opera. Obbligatoria nell'ambito ticket esterni.</summary>
        [ForeignKey(nameof(Company))]
        public int? IdCompany { get; set; }

        /// <summary>
        /// Persona per conto della quale la chiave scrive. Obbligatoria nell'ambito fiera: i
        /// biglietti raccolti devono avere un proprietario, altrimenti a sera nessuno sa di chi
        /// siano i contatti.
        /// </summary>
        [ForeignKey(nameof(User))]
        [MaxLength(450)]
        public string? IdUser { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ExpiresAt { get; set; }

        public DateTime? LastUsedAt { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public virtual Company? Company { get; set; }

        public virtual ApplicationUser? User { get; set; }
    }
}
