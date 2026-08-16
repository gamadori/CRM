using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    /// <summary>
    /// Quanto ha lavorato e quanto ha prodotto una macchina in un giorno.
    /// <para>
    /// La macchina manda i <b>totalizzatori</b> (ore e pezzi da quando esiste); la differenza col
    /// giorno precedente la calcola il CRM. Cosi' un invio saltato non e' un giorno perso: al
    /// collegamento successivo il conto torna lo stesso, perche' il totale se lo porta dietro.
    /// </para>
    /// </summary>
    public class MachineDailyReading
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Article))]
        public int IdArticle { get; set; }

        /// <summary>Il giorno a cui si riferisce la lettura. Uno solo per macchina.</summary>
        [Column(TypeName = "date")]
        [Display(Name = "Giorno")]
        public DateTime Day { get; set; }

        [Display(Name = "Ore totali della macchina")]
        public decimal? TotalHours { get; set; }

        [Display(Name = "Pezzi totali della macchina")]
        public long? TotalPieces { get; set; }

        /// <summary>Ore lavorate quel giorno: differenza con la lettura precedente.</summary>
        [Display(Name = "Ore del giorno")]
        public decimal? Hours { get; set; }

        /// <summary>Pezzi prodotti quel giorno: differenza con la lettura precedente.</summary>
        [Display(Name = "Pezzi del giorno")]
        public long? Pieces { get; set; }

        /// <summary>
        /// Il totalizzatore e' sceso rispetto alla lettura di prima. Succede dopo una sostituzione
        /// della scheda o un azzeramento in assistenza. Si segna invece di calcolare una differenza
        /// negativa, che sarebbe una produzione negativa in mezzo ai grafici.
        /// </summary>
        [Display(Name = "Contatore azzerato")]
        public bool CounterReset { get; set; }

        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

        public Article? Article { get; set; }
    }
}
