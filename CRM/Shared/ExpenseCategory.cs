using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CRM.Shared
{
    /// <summary>
    /// A che cosa e' stata spesa la somma. E' una voce di rimborso, non una descrizione:
    /// serve a rispondere a "quanto e' costato il vitto in quella trasferta" e, soprattutto,
    /// determina il trattamento fiscale (vitto e alloggio deducibili al 75%, rappresentanza a
    /// regime suo).
    /// <para>
    /// Non esiste un valore "nessuna": la tipologia mancante e' <c>null</c>, cioe' un campo da
    /// compilare. Stessa scelta fatta per la valuta - meglio un buco visibile che un valore
    /// inventato con la faccia di uno giusto, perche' quello nessuno lo va a correggere.
    /// </para>
    /// </summary>
    public enum ExpenseCategory
    {
        [Display(Name = "Vitto")]
        Meals = 1,

        [Display(Name = "Alloggio")]
        Lodging = 2,

        /// <summary>Treno, aereo, taxi, mezzi pubblici, noleggio, traghetto.</summary>
        [Display(Name = "Trasporti")]
        Travel = 3,

        [Display(Name = "Carburante")]
        Fuel = 4,

        [Display(Name = "Pedaggi")]
        Tolls = 5,

        [Display(Name = "Parcheggi")]
        Parking = 6,

        /// <summary>
        /// Spese di rappresentanza. Nessuna regola automatica la propone mai: ha il trattamento
        /// fiscale piu' stretto e la differenza fra un pranzo di lavoro e uno di rappresentanza
        /// sta nell'occasione, che sullo scontrino non c'e' scritta.
        /// </summary>
        [Display(Name = "Rappresentanza")]
        Entertainment = 7,

        [Display(Name = "Materiali e cancelleria")]
        Supplies = 8,

        [Display(Name = "Formazione")]
        Training = 9,

        [Display(Name = "Telefonia e connettivita'")]
        Telecom = 10,

        [Display(Name = "Altro")]
        Other = 99
    }

    /// <summary>
    /// Da dove viene la tipologia. Serve a due cose: dirlo a chi guarda (una proposta non e' un
    /// dato confermato) e misurare quanto ci prende ogni livello, confrontando
    /// <c>CategorySuggested</c> con la tipologia rimasta dopo la conferma.
    /// </summary>
    public enum ExpenseCategorySource
    {
        /// <summary>Scelta da una persona: e' l'unica che vale come conferma.</summary>
        [Display(Name = "Scelta manualmente")]
        Manual = 0,

        /// <summary>Sottotipo restituito dall'OCR (receipt.hotel, receipt.gas...).</summary>
        [Display(Name = "Tipo di documento riconosciuto")]
        DocumentType = 1,

        /// <summary>Regola su esercente o righe dello scontrino.</summary>
        [Display(Name = "Esercente riconosciuto")]
        MerchantRule = 2,

        /// <summary>Proposta del modello, quando i primi due livelli tacciono.</summary>
        [Display(Name = "Proposta AI")]
        Ai = 3
    }

    /// <summary>Etichette delle tipologie: le usano sia le pagine sia il prompt del modello.</summary>
    public static class ExpenseCategories
    {
        private static readonly Dictionary<ExpenseCategory, string> Labels = new()
        {
            [ExpenseCategory.Meals] = "Vitto",
            [ExpenseCategory.Lodging] = "Alloggio",
            [ExpenseCategory.Travel] = "Trasporti",
            [ExpenseCategory.Fuel] = "Carburante",
            [ExpenseCategory.Tolls] = "Pedaggi",
            [ExpenseCategory.Parking] = "Parcheggi",
            [ExpenseCategory.Entertainment] = "Rappresentanza",
            [ExpenseCategory.Supplies] = "Materiali e cancelleria",
            [ExpenseCategory.Training] = "Formazione",
            [ExpenseCategory.Telecom] = "Telefonia e connettività",
            [ExpenseCategory.Other] = "Altro"
        };

        /// <summary>Spiegazione di che cosa sta in ogni voce, per il prompt.</summary>
        private static readonly Dictionary<ExpenseCategory, string> Hints = new()
        {
            [ExpenseCategory.Meals] = "ristoranti, bar, pizzerie, pranzi e cene di lavoro, colazioni",
            [ExpenseCategory.Lodging] = "alberghi, b&b, affitti brevi",
            [ExpenseCategory.Travel] = "treno, aereo, taxi, mezzi pubblici, noleggio auto, traghetti",
            [ExpenseCategory.Fuel] = "rifornimenti di carburante",
            [ExpenseCategory.Tolls] = "pedaggi autostradali, Telepass",
            [ExpenseCategory.Parking] = "parcheggi, soste, autorimesse",
            [ExpenseCategory.Entertainment] = "spese di rappresentanza verso clienti (omaggi, ospitalità)",
            [ExpenseCategory.Supplies] = "cancelleria, materiali di consumo, minuteria",
            [ExpenseCategory.Training] = "corsi, seminari, certificazioni",
            [ExpenseCategory.Telecom] = "telefonia, traffico dati, connettività",
            [ExpenseCategory.Other] = "nessuna delle precedenti"
        };

        public static IReadOnlyList<ExpenseCategory> All { get; } = new List<ExpenseCategory>
        {
            ExpenseCategory.Meals,
            ExpenseCategory.Lodging,
            ExpenseCategory.Travel,
            ExpenseCategory.Fuel,
            ExpenseCategory.Tolls,
            ExpenseCategory.Parking,
            ExpenseCategory.Entertainment,
            ExpenseCategory.Supplies,
            ExpenseCategory.Training,
            ExpenseCategory.Telecom,
            ExpenseCategory.Other
        };

        public static string Label(ExpenseCategory category) =>
            Labels.TryGetValue(category, out var label) ? label : category.ToString();

        /// <summary>Etichetta di una tipologia che puo' non esserci: e' il caso normale in elenco.</summary>
        public static string Label(ExpenseCategory? category) =>
            category.HasValue ? Label(category.Value) : "Da indicare";

        public static string Hint(ExpenseCategory category) =>
            Hints.TryGetValue(category, out var hint) ? hint : string.Empty;

        public static string SourceLabel(ExpenseCategorySource? source) => source switch
        {
            ExpenseCategorySource.Manual => "scelta manualmente",
            ExpenseCategorySource.DocumentType => "dal tipo di documento riconosciuto",
            ExpenseCategorySource.MerchantRule => "dall'esercente riconosciuto",
            ExpenseCategorySource.Ai => "proposta dall'AI",
            _ => string.Empty
        };

        /// <summary>
        /// Traduce il nome scritto dal modello nella tipologia corrispondente. Un nome che non
        /// esiste vale come nessuna risposta: non si ripiega su "Altro", che sembrerebbe una
        /// classificazione riuscita.
        /// </summary>
        public static ExpenseCategory? Parse(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            return Enum.TryParse<ExpenseCategory>(name.Trim(), ignoreCase: true, out var parsed)
                   && Enum.IsDefined(typeof(ExpenseCategory), parsed)
                ? parsed
                : null;
        }
    }
}
