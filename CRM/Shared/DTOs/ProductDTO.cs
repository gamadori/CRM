using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared.DTOs
{
    public class ProductDTO
    {
        public int Id { get; set; }

        [Display(Name = nameof(Product.Name), ResourceType = typeof(Resources.Models.Product))]
        public string Name { get; set; }

        [Display(Name = nameof(Product.Description), ResourceType = typeof(Resources.Models.Product))]
        public string Description { get; set; }

        [Display(Name = nameof(Product.Code), ResourceType = typeof(Resources.Models.Product))]
        public string? Code { get; set; }

        [Display(Name = nameof(Product.IdProductType), ResourceType = typeof(Resources.Models.Product))]
        public int? IdProductType { get; set; }

        [Display(Name = nameof(Product.Price), ResourceType = typeof(Resources.Models.Product))]
        public decimal Price { get; set; }

        public bool IsArchived { get; set; }

        public DateTime? ArchivedAt { get; set; }

        public string? ArchivedReason { get; set; }

        [Display(Name = "Template Gantt")]
        public int? IdGanttPlan { get; set; }

        [Display(Name = "Template Gantt")]
        public string GanttPlanName { get; set; } = string.Empty;

        [Display(Name = "Capo commessa predefinito")]
        public string? IdDefaultCommessaResponsible { get; set; }

        [Display(Name = "Capo commessa predefinito")]
        public string DefaultCommessaResponsibleName { get; set; } = string.Empty;
        
        [Display(Name = nameof(Product.IdProductType), ResourceType = typeof(Resources.Models.Product))]
        public string ProductTypeName { get; set; }

        [Display(Name = nameof(Product.IdCompany), ResourceType = typeof(Resources.Models.Product))]
        public string CompanyName { get; set; }
        
        [Display(Name = nameof(Product.IdCompany), ResourceType = typeof(Resources.Models.Product))]
        public int? IdCompany { get; set; }

    }

    public static class ProductHelper
    {
        public static ProductDTO ToDTO(this Product product)
        {
            if (product == null) return null;
            return new ProductDTO
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Code = product.Code,
                IdProductType = product.IdProductType,
                Price = product.Price,
                IsArchived = product.IsArchived,
                ArchivedAt = product.ArchivedAt,
                ArchivedReason = product.ArchivedReason,
                IdGanttPlan = product.IdGanttPlan,
                GanttPlanName = product.GanttPlan != null ? product.GanttPlan.Name : string.Empty,
                IdDefaultCommessaResponsible = product.IdDefaultCommessaResponsible,
                DefaultCommessaResponsibleName = product.DefaultCommessaResponsible != null ? product.DefaultCommessaResponsible.NameComplete : string.Empty,
                ProductTypeName = product.ProductType != null ? product.ProductType.Name : string.Empty,
                CompanyName = product.Company != null ? product.Company.RagioneSociale : string.Empty,
                IdCompany = product.IdCompany
            };
        }

        public static Product ToEntity(this ProductDTO dto)
        {
            if (dto == null) return null;
            return new Product
            {
                Id = dto.Id,
                Name = dto.Name,
                Description = dto.Description,
                Code = dto.Code,
                IdProductType = dto.IdProductType,
                Price = dto.Price,
                IsArchived = dto.IsArchived,
                ArchivedAt = dto.ArchivedAt,
                ArchivedReason = dto.ArchivedReason,
                IdGanttPlan = dto.IdGanttPlan,
                IdDefaultCommessaResponsible = dto.IdDefaultCommessaResponsible,
                IdCompany = dto.IdCompany
            };
        }
    }
}
