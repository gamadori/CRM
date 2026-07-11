using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared.DTOs
{
    public class ArticleDTO
    {
        public int Id { get; set; }

        [Display(Name = nameof(Article.IdProduct), ResourceType = typeof(Resources.Models.Article))]
        public int? IdProduct { get; set; }

        [Display(Name = nameof(Article.IdProduct), ResourceType = typeof(Resources.Models.Article))]
        public string ProductName { get; set; }

        [Display(Name = nameof(Article.SerialNumber), ResourceType = typeof(Resources.Models.Article))]
        public string SerialNumber { get; set; }

        [Display(Name = nameof(Article.Name), ResourceType = typeof(Resources.Models.Article))]
        public string Name { get; set; }

        [Display(Name = nameof(Article.Year), ResourceType = typeof(Resources.Models.Article))]
        public int Year { get; set; }

        [Display(Name = nameof(Article.SaleDate), ResourceType = typeof(Resources.Models.Article))]
        public DateTime? SaleDate { get; set; }

        [Display(Name = nameof(Article.Description), ResourceType = typeof(Resources.Models.Article))]
        public string Description { get; set; }

            
        [Display(Name = nameof(Article.IdCompany), ResourceType = typeof(Resources.Models.Article))]
        public int? IdCompany { get; set; }

        [Display(Name = nameof(Article.IdCompany), ResourceType = typeof(Resources.Models.Article))]
        public string CompanyName { get; set; }

        [Display(Name = nameof(Article.Country), ResourceType = typeof(Resources.Models.Article))]
        public string Country { get; set; }

        [Display(Name = nameof(Article.IdRecipientCompany), ResourceType = typeof(Resources.Models.Article))]
        public int? IdRecipientCompany { get; set; }

        [Display(Name = nameof(Article.IdRecipientCompany), ResourceType = typeof(Resources.Models.Article))]
        public string RecipientCompanyName { get; set; }

        [Display(Name = nameof(Article.RecipientCountry), ResourceType = typeof(Resources.Models.Article))]
        public string RecipientCountry { get; set; }

        [Display(Name = nameof(Article.TestDate), ResourceType = typeof(Resources.Models.Article))]
        public DateTime? TestDate { get; set; }

        [Display(Name = nameof(Article.DeliveryDate), ResourceType = typeof(Resources.Models.Article))]
        public DateTime? DeliveryDate { get; set; }

        [Display(Name = nameof(Article.Note), ResourceType = typeof(Resources.Models.Article))]
        public string Note { get; set; }

        public bool IsArchived { get; set; }

        public DateTime? ArchivedAt { get; set; }

        public string? ArchivedReason { get; set; }

        [NotMapped]
        [Display(Name = nameof(Article.Name), ResourceType = typeof(Resources.Models.Article))]
        public string CompleteName { get; set; }

    }

    public static class ArticleHelper
    {
        public static ArticleDTO ToDTO(this Article article)
        {
            if (article == null) return null;
            return new ArticleDTO   
            {
                Id = article.Id,
                IdProduct = article.IdProduct,
                ProductName = article.Product?.Name,
                SerialNumber = article.SerialNumber,
                Name = article.Name,
                Year = article.Year,
                SaleDate = article.SaleDate,
                Description = article.Description,
                IdCompany = article.IdCompany,
                CompanyName = article.Company?.RagioneSociale,
                Country = article.Country,
                IdRecipientCompany = article.IdRecipientCompany,
                RecipientCompanyName = article.RecipientCompany?.RagioneSociale,
                RecipientCountry = article.RecipientCountry,
                TestDate = article.TestDate,
                DeliveryDate = article.DeliveryDate,
                Note = article.Note,
                IsArchived = article.IsArchived,
                ArchivedAt = article.ArchivedAt,
                ArchivedReason = article.ArchivedReason,
                CompleteName = $"{article.Name} - {article.SerialNumber}",
                

            };
        }

        public static Article ToEntity(this ArticleDTO dto)
        {
            if (dto == null) return null;
            return new Article
            {
                Id = dto.Id,
                IdProduct = dto.IdProduct,
                SerialNumber = dto.SerialNumber,
                Name = dto.Name,
                Year = dto.Year,
                SaleDate = dto.SaleDate,
                Description = dto.Description,
                IdCompany = dto.IdCompany,
                Country = dto.Country,
                IdRecipientCompany = dto.IdRecipientCompany,
                RecipientCountry = dto.RecipientCountry,
                TestDate = dto.TestDate,
                DeliveryDate = dto.DeliveryDate,
                Note = dto.Note,
                IsArchived = dto.IsArchived,
                ArchivedAt = dto.ArchivedAt,
                ArchivedReason = dto.ArchivedReason,
            

            };
        }
    }
}
