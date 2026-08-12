using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    /// <summary>
    /// Come si raccoglie la firma per un tipo di intervento (Telefono, Sul Posto, Workshop...).
    /// <para>
    /// Una riga per ogni valore di <see cref="TypesSupport"/>, che e' un enum in codice e non una
    /// tabella: la chiave e' il valore dell'enum. Un tipo senza riga vale
    /// <see cref="SignatureRequirement.None"/>, cosi' un valore aggiunto in futuro non si mette a
    /// chiedere firme da solo.
    /// </para>
    /// <para>
    /// Prima non esisteva niente del genere: la firma remota valeva per tutto cio' che non era
    /// Sul Posto o Ufficio, quindi anche per il lavoro in officina.
    /// </para>
    /// </summary>
    public class SupportTypeSetting
    {
        /// <summary>Valore di <see cref="TypesSupport"/> a cui si riferisce la riga.</summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int SupportType { get; set; }

        [Display(Name = "Firma richiesta")]
        public SignatureRequirement SignatureRequirement { get; set; } = SignatureRequirement.None;

        [NotMapped]
        public TypesSupport SupportTypeEnum => (TypesSupport)SupportType;
    }
}
