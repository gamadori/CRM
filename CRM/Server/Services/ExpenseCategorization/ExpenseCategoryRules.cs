using System.Text.RegularExpressions;
using CRM.Shared;

namespace CRM.Server.Services.ExpenseCategorization
{
    /// <summary>
    /// I due livelli deterministici: il sottotipo che l'OCR ha gia' riconosciuto e il dizionario
    /// di esercenti e parole delle righe.
    /// <para>
    /// Stanno prima del modello perche' non costano niente, rispondono sempre uguale e coprono
    /// il traffico vero: in trasferta gli scontrini sono benzina, autostrada, alberghi e
    /// ristoranti, cioe' proprio i casi che una tabella riconosce senza sbagliare.
    /// </para>
    /// <para>
    /// Nessuna regola propone mai <see cref="ExpenseCategory.Entertainment"/>: la differenza fra
    /// un pranzo di lavoro e uno di rappresentanza sta nell'occasione, che sullo scontrino non
    /// c'e' scritta, e sbagliarla cambia la deducibilita'. Quella voce resta una scelta di chi
    /// registra.
    /// </para>
    /// </summary>
    public static class ExpenseCategoryRules
    {
        /// <summary>Esercente riconosciuto: e' l'indizio piu' forte che si abbia senza chiedere.</summary>
        private const double MerchantConfidence = 0.9;

        /// <summary>
        /// Sottotipo dell'OCR: buono ma non quanto l'esercente. Azure distingue bene albergo,
        /// benzinaio e parcheggio; sul resto scivola verso il generico "pasto al dettaglio".
        /// </summary>
        private const double DocumentTypeConfidence = 0.8;

        /// <summary>
        /// Parola trovata nelle righe: vale meno, perche' una riga e' un pezzo della spesa e non
        /// per forza la spesa (il caffe' dentro il conto dell'albergo).
        /// </summary>
        private const double LineConfidence = 0.7;

        /// <summary>
        /// Livello 1 - il sottotipo che <c>prebuilt-receipt</c> restituisce gia' oggi e che
        /// finora finiva in una colonna senza essere letto da nessuno.
        /// <para>
        /// <c>receipt.creditCard</c> non c'e' apposta: e' la ricevuta del pagamento, dice come si
        /// e' pagato e non che cosa si e' comprato. <c>invoice</c> e <c>receipt</c> generico
        /// nemmeno: da soli non contengono nessuna informazione sulla natura della spesa.
        /// </para>
        /// </summary>
        private static readonly Dictionary<string, ExpenseCategory> DocumentTypes =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["receipt.hotel"] = ExpenseCategory.Lodging,
                ["receipt.gas"] = ExpenseCategory.Fuel,
                ["receipt.parking"] = ExpenseCategory.Parking,
                ["receipt.retailMeal"] = ExpenseCategory.Meals
            };

        private sealed record Rule(Regex Pattern, ExpenseCategory Category, bool MerchantOnly = false);

        private static Regex R(string pattern) =>
            new(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Livello 2. Le voci marcate <c>MerchantOnly</c> valgono solo sul nome dell'esercente:
        /// come parola in una riga vorrebbero dire un'altra cosa - "ATM" su uno scontrino e' il
        /// bancomat molto piu' spesso dell'azienda dei trasporti milanese.
        /// </summary>
        private static readonly Rule[] Rules =
        {
            // ── Carburante ───────────────────────────────────────────────────
            new(R(@"\bq8\b"), ExpenseCategory.Fuel, MerchantOnly: true),
            new(R(@"\beni\b"), ExpenseCategory.Fuel, MerchantOnly: true),
            new(R(@"\bagip\b"), ExpenseCategory.Fuel, MerchantOnly: true),
            new(R(@"\btamoil\b"), ExpenseCategory.Fuel, MerchantOnly: true),
            new(R(@"\besso\b"), ExpenseCategory.Fuel, MerchantOnly: true),
            new(R(@"\bshell\b"), ExpenseCategory.Fuel, MerchantOnly: true),
            new(R(@"\berg\b"), ExpenseCategory.Fuel, MerchantOnly: true),
            new(R(@"\brepsol\b"), ExpenseCategory.Fuel, MerchantOnly: true),
            new(R(@"\btotal(erg)?\b"), ExpenseCategory.Fuel, MerchantOnly: true),
            new(R(@"\bip\s+(station|gruppo)"), ExpenseCategory.Fuel, MerchantOnly: true),
            new(R(@"carburant"), ExpenseCategory.Fuel),
            new(R(@"benzina"), ExpenseCategory.Fuel),
            new(R(@"gasolio"), ExpenseCategory.Fuel),
            new(R(@"\bdiesel\b"), ExpenseCategory.Fuel),
            new(R(@"rifornimento"), ExpenseCategory.Fuel),
            new(R(@"\bfuel\b"), ExpenseCategory.Fuel),
            new(R(@"\bgpl\b"), ExpenseCategory.Fuel),

            // ── Pedaggi ──────────────────────────────────────────────────────
            new(R(@"telepass"), ExpenseCategory.Tolls),
            new(R(@"autostrad"), ExpenseCategory.Tolls),
            new(R(@"pedagg"), ExpenseCategory.Tolls),
            new(R(@"viacard"), ExpenseCategory.Tolls),
            new(R(@"\btoll\b"), ExpenseCategory.Tolls),

            // ── Parcheggi ────────────────────────────────────────────────────
            new(R(@"parcheggi"), ExpenseCategory.Parking),
            new(R(@"\bparking\b"), ExpenseCategory.Parking),
            new(R(@"autorimessa"), ExpenseCategory.Parking),
            new(R(@"autosilo"), ExpenseCategory.Parking),
            new(R(@"\bsosta\b"), ExpenseCategory.Parking),
            new(R(@"\bpark\b"), ExpenseCategory.Parking, MerchantOnly: true),

            // ── Trasporti ────────────────────────────────────────────────────
            new(R(@"trenitalia"), ExpenseCategory.Travel),
            new(R(@"\bitalo\b"), ExpenseCategory.Travel, MerchantOnly: true),
            new(R(@"frecciaross|frecciargento|frecciabianca"), ExpenseCategory.Travel),
            new(R(@"trenord"), ExpenseCategory.Travel),
            new(R(@"\batm\b|\batac\b|\bgtt\b|\btper\b|\bamt\b"), ExpenseCategory.Travel, MerchantOnly: true),
            new(R(@"\btaxi\b"), ExpenseCategory.Travel),
            new(R(@"\buber\b|free\s?now"), ExpenseCategory.Travel, MerchantOnly: true),
            new(R(@"flixbus|itabus"), ExpenseCategory.Travel),
            new(R(@"ryanair|easyjet|wizz\s?air|lufthansa|air\s?france|\bklm\b|alitalia|ita\s+airways|british\s+airways"), ExpenseCategory.Travel),
            new(R(@"aeroport|\bairport\b"), ExpenseCategory.Travel),
            new(R(@"hertz|\bavis\b|europcar|\bsixt\b|maggiore\s+rent|car\s?rental"), ExpenseCategory.Travel, MerchantOnly: true),
            new(R(@"noleggio"), ExpenseCategory.Travel),
            new(R(@"traghett|grimaldi\s+lines|\btirrenia\b"), ExpenseCategory.Travel),
            new(R(@"biglietto|biglietteria"), ExpenseCategory.Travel),
            new(R(@"\bmetropolitana\b|\bautobus\b"), ExpenseCategory.Travel),

            // ── Alloggio ─────────────────────────────────────────────────────
            new(R(@"\bhotel\b|\bhotels\b"), ExpenseCategory.Lodging),
            new(R(@"albergo"), ExpenseCategory.Lodging),
            new(R(@"\bb\s?&\s?b\b|bed\s*(and|&)\s*breakfast"), ExpenseCategory.Lodging),
            new(R(@"residence|agriturismo|\bmotel\b|\bhostel\b|ostello|\bresort\b"), ExpenseCategory.Lodging),
            new(R(@"booking\.com|airbnb"), ExpenseCategory.Lodging, MerchantOnly: true),
            new(R(@"pernottament|\bmezza\s+pensione\b"), ExpenseCategory.Lodging),

            // ── Vitto ────────────────────────────────────────────────────────
            new(R(@"ristorant"), ExpenseCategory.Meals),
            new(R(@"trattoria|osteria|pizzeri|braceria|birreria|paninoteca|rosticceria|gastronomia"), ExpenseCategory.Meals),
            new(R(@"\bbar\b"), ExpenseCategory.Meals, MerchantOnly: true),
            new(R(@"caff(e|è)"), ExpenseCategory.Meals),
            new(R(@"autogrill|chef\s?express|mcdonald|burger\s?king"), ExpenseCategory.Meals, MerchantOnly: true),
            new(R(@"tavola\s+calda|self\s?service"), ExpenseCategory.Meals),
            new(R(@"\bcoperto\b|\bmen(u|ù)\b|\bpranzo\b|\bcena\b|colazione"), ExpenseCategory.Meals),

            // ── Materiali e cancelleria ──────────────────────────────────────
            new(R(@"cartoleria|cancelleria"), ExpenseCategory.Supplies),
            new(R(@"ferramenta|utensileria|minuteria"), ExpenseCategory.Supplies),
            new(R(@"leroy\s?merlin|\bbrico\b|office\s?depot|\bstaples\b"), ExpenseCategory.Supplies, MerchantOnly: true),

            // ── Telefonia ────────────────────────────────────────────────────
            new(R(@"vodafone|windtre|wind\s?tre|\biliad\b|fastweb|telecom\s+italia"), ExpenseCategory.Telecom),
            new(R(@"\btim\b"), ExpenseCategory.Telecom, MerchantOnly: true),
            new(R(@"ricarica\s+telefonic|traffico\s+dati"), ExpenseCategory.Telecom),

            // ── Formazione ───────────────────────────────────────────────────
            new(R(@"formazione|seminario|\bcors[oi]\b|certificazione"), ExpenseCategory.Training),
            new(R(@"academy|udemy|coursera"), ExpenseCategory.Training, MerchantOnly: true)
        };

        /// <summary>
        /// I due livelli deterministici, in ordine di attendibilita': esercente, poi sottotipo
        /// del documento, poi righe.
        /// <para>
        /// L'esercente viene prima del sottotipo perche' e' piu' specifico: su uno scontrino Q8
        /// l'OCR dice spesso "pasto al dettaglio" (l'area di servizio vende anche panini) mentre
        /// il nome dell'esercente e' inequivocabile.
        /// </para>
        /// </summary>
        public static ExpenseCategorySuggestion Apply(ExpenseCategoryRequest request)
        {
            var fromMerchant = FromMerchant(request.MerchantName);
            if (fromMerchant.HasCategory)
                return fromMerchant;

            var fromDocumentType = FromDocumentType(request.DocumentType);
            if (fromDocumentType.HasCategory)
                return fromDocumentType;

            return FromLines(request.Lines);
        }

        /// <summary>Livello 1: il sottotipo riconosciuto dall'OCR.</summary>
        public static ExpenseCategorySuggestion FromDocumentType(string? documentType)
        {
            if (string.IsNullOrWhiteSpace(documentType))
                return ExpenseCategorySuggestion.None;

            // Un PDF multi-documento porta i tipi uniti da virgola ("receipt.hotel, receipt.gas"):
            // tipi diversi vogliono dire spese diverse, e la testata non ne ha una sola.
            var types = documentType
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (types.Count != 1 || !DocumentTypes.TryGetValue(types[0], out var category))
                return ExpenseCategorySuggestion.None;

            return new ExpenseCategorySuggestion(
                category,
                DocumentTypeConfidence,
                $"Il documento è riconosciuto come «{types[0]}».",
                ExpenseCategorySource.DocumentType);
        }

        /// <summary>Livello 2, prima parte: il nome dell'esercente.</summary>
        public static ExpenseCategorySuggestion FromMerchant(string? merchantName)
        {
            if (string.IsNullOrWhiteSpace(merchantName))
                return ExpenseCategorySuggestion.None;

            var match = Rules.FirstOrDefault(rule => rule.Pattern.IsMatch(merchantName));
            if (match == null)
                return ExpenseCategorySuggestion.None;

            return new ExpenseCategorySuggestion(
                match.Category,
                MerchantConfidence,
                $"L'esercente «{merchantName.Trim()}» è riconosciuto come {ExpenseCategories.Label(match.Category)}.",
                ExpenseCategorySource.MerchantRule);
        }

        /// <summary>
        /// Livello 2, seconda parte: le descrizioni delle righe. Vince la tipologia che compare
        /// in piu' righe - il conto dell'albergo ha una riga di caffe' e cinque di pernottamento,
        /// e la spesa e' l'albergo.
        /// </summary>
        public static ExpenseCategorySuggestion FromLines(IReadOnlyList<string>? lines)
        {
            if (lines == null || lines.Count == 0)
                return ExpenseCategorySuggestion.None;

            var hits = new Dictionary<ExpenseCategory, (int Count, string Line)>();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var match = Rules.FirstOrDefault(rule => !rule.MerchantOnly && rule.Pattern.IsMatch(line));
                if (match == null)
                    continue;

                hits[match.Category] = hits.TryGetValue(match.Category, out var current)
                    ? (current.Count + 1, current.Line)
                    : (1, line.Trim());
            }

            if (hits.Count == 0)
                return ExpenseCategorySuggestion.None;

            var ordered = hits.OrderByDescending(hit => hit.Value.Count).ToList();

            // Due tipologie a pari merito su righe diverse: e' una spesa mista, e sceglierne una
            // a caso sarebbe peggio che lasciar decidere. Si passa la mano al livello dopo.
            if (ordered.Count > 1 && ordered[0].Value.Count == ordered[1].Value.Count)
                return ExpenseCategorySuggestion.None;

            var winner = ordered[0];

            return new ExpenseCategorySuggestion(
                winner.Key,
                LineConfidence,
                $"La riga «{Shorten(winner.Value.Line)}» indica {ExpenseCategories.Label(winner.Key)}.",
                ExpenseCategorySource.MerchantRule);
        }

        private static string Shorten(string value) =>
            value.Length <= 60 ? value : value.Substring(0, 57) + "...";
    }
}
