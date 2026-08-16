namespace CRM.Server.Services
{
    /// <summary>Una macchina che ha smesso di mandare il backup.</summary>
    public sealed class MacchinaInSilenzio
    {
        public int IdArticle { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public string? ProductName { get; set; }
        public string? CompanyName { get; set; }
        public DateTime UltimoBackup { get; set; }
        public int GiorniDiSilenzio { get; set; }
    }

    /// <summary>
    /// Chi va segnalato perche' ha smesso di mandare il backup.
    /// <para>
    /// Il valore di un backup e' accorgersi che manca <b>prima</b> del guasto, non dopo. Finora la
    /// data dell'ultimo backup c'era ma non la guardava nessuno.
    /// </para>
    /// <para>
    /// Si guardano solo le macchine che un backup lo hanno gia' mandato. Le altre non sono mai
    /// state sotto backup: segnalarle vorrebbe dire ricevere ogni giorno l'elenco di tutto il parco
    /// macchine, e un avviso che arriva sempre non lo legge piu' nessuno.
    /// </para>
    /// </summary>
    public static class MachineBackupSilenceRules
    {
        /// <summary>
        /// Le macchine in silenzio da piu' giorni della soglia, le piu' silenziose per prime.
        /// Con soglia a zero non se ne segnala nessuna: la sorveglianza e' spenta.
        /// </summary>
        public static List<MacchinaInSilenzio> InSilenzio(
            IEnumerable<MacchinaInSilenzio> macchine,
            DateTime adesso,
            int giorniDiSoglia)
        {
            if (giorniDiSoglia <= 0)
                return new List<MacchinaInSilenzio>();

            var risultato = new List<MacchinaInSilenzio>();

            foreach (var macchina in macchine)
            {
                var giorni = (int)(adesso.Date - macchina.UltimoBackup.Date).TotalDays;
                if (giorni < giorniDiSoglia)
                    continue;

                macchina.GiorniDiSilenzio = giorni;
                risultato.Add(macchina);
            }

            return risultato.OrderByDescending(x => x.GiorniDiSilenzio).ToList();
        }

        /// <summary>
        /// I destinatari del riepilogo: piu' indirizzi separati da punto e virgola, o da virgola,
        /// perche' chi li scrive usa l'uno o l'altra. Spazi e doppioni si perdono per strada, cosi'
        /// un elenco scritto male non manda la stessa email due volte alla stessa persona.
        /// </summary>
        public static List<string> Destinatari(string? impostazione)
        {
            if (string.IsNullOrWhiteSpace(impostazione))
                return new List<string>();

            return impostazione
                .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Il testo del riepilogo: una riga per macchina, la piu' silenziosa in cima.</summary>
        public static string Riepilogo(IReadOnlyList<MacchinaInSilenzio> macchine, int giorniDiSoglia)
        {
            var righe = macchine.Select(x =>
                $"<tr><td>{x.SerialNumber}</td><td>{x.ProductName}</td><td>{x.CompanyName}</td>" +
                $"<td>{x.UltimoBackup:dd/MM/yyyy}</td><td>{x.GiorniDiSilenzio}</td></tr>");

            return $"<p>Macchine senza backup da almeno {giorniDiSoglia} giorni: <b>{macchine.Count}</b>.</p>" +
                   "<table border=\"1\" cellpadding=\"6\" cellspacing=\"0\">" +
                   "<tr><th>Matricola</th><th>Modello</th><th>Cliente</th><th>Ultimo backup</th><th>Giorni</th></tr>" +
                   string.Join("", righe) +
                   "</table>";
        }
    }
}
