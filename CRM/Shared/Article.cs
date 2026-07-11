using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CRM.Shared; // aggiunto per ItemStatus

namespace CRM.Shared
{
    public class Article
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
        [ForeignKey("Product")]
        [Display(Name = nameof(Article.IdProduct), ResourceType = typeof(Resources.Models.Article))]
        public int? IdProduct { get; set; }

        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
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

        [Display(Name = nameof(Article.Property1), ResourceType = typeof(Resources.Models.Article))]
        public string Property1 { get; set; }

        [ForeignKey("Company")]
        [Display(Name = nameof(Article.IdCompany), ResourceType = typeof(Resources.Models.Article))]
        public int? IdCompany { get; set; }

        [Display(Name = nameof(Article.Country), ResourceType = typeof(Resources.Models.Article))]
        public string Country { get; set; }

        [ForeignKey("RecipientCompany")]
        [Display(Name = nameof(Article.IdRecipientCompany), ResourceType = typeof(Resources.Models.Article))]
        public int? IdRecipientCompany { get; set; }

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
        public string CompleteName { get { return $"{this.Product?.Name} - {this.SerialNumber}"; } }

        [NotMapped]
        public string NameComplete { get { return $"{Product?.Name} {SerialNumber}"; } }

        [NotMapped]
        public virtual List<ArticleAccessory> Accessories { get; set; }

        public virtual Product Product { get; set; }

        public virtual Company Company { get; set; }

        public virtual Company RecipientCompany { get; set; }

        public virtual ICollection<ArticleDomainState> StatesCurrent { get; set; }
    }

    public class ArticleFilter : PagingParameterModel
    {

        public string SerialNumber { get; set; }

        public int? IdProduct { get; set; }

        public int? IdCompany { get; set; }

        public int? Year { get; set; }

        public bool UserLogged { get; set; }

        public bool IncludeArchived { get; set; }
    }

    public class TicketInterventionProductModel
    {
        public bool Selected { get; set; }

        public string SerialNumber { get; set; }

        public string Year { get; set; }
    }
}
