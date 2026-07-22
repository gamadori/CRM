using CRM.Shared;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Data
{
    /// <summary>
    /// Query condivise sull'azienda "madre" (l'entità che opera il CRM). Unica fonte di verità:
    /// la società con <see cref="CompanyTypes.HeadCompany"/>. L'installazione è single-tenant,
    /// quindi ne esiste una sola; a scopo deterministico si prende comunque la prima per Id.
    /// Sostituisce il vecchio doppione basato su <c>GlobalSetting.IdHeadQuarter</c>.
    /// </summary>
    public static class CompanyQueries
    {
        /// <summary>L'azienda madre (CompanyType = HeadCompany), o null se non ancora definita.</summary>
        public static Task<Company?> GetHeadCompanyAsync(
            this ApplicationDbContext context, bool tracking = false, CancellationToken ct = default)
            => (tracking ? context.Companies : context.Companies.AsNoTracking())
                .Where(c => c.CompanyType == CompanyTypes.HeadCompany)
                .OrderBy(c => c.Id)
                .FirstOrDefaultAsync(ct);

        /// <summary>Id dell'azienda madre, o null se non ancora definita.</summary>
        public static Task<int?> GetHeadCompanyIdAsync(
            this ApplicationDbContext context, CancellationToken ct = default)
            => context.Companies.AsNoTracking()
                .Where(c => c.CompanyType == CompanyTypes.HeadCompany)
                .OrderBy(c => c.Id)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync(ct);
    }
}
