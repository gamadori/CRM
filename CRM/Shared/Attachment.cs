using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public enum AttachmentTypes
    {
        Ticket,
        Project,
        Deal,
        ContractType,
        Contract,
        Product,
        Article,
        DXF,
        Service
    }

   
    public enum FileTypes
    {
        Zip,
        Image,
        Document,
        Spreadsheet,
        CAD,
        Other
    }

    public enum AttachmentVisibilities
    {
        Private,
        Public,
    }

    public class Attachment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int IdParent { get; set; }

        public AttachmentTypes AttchmentType { get; set; }


        [Required]
        [Display(Name = "Nome")]
        [MaxLength(100)]
        public string Name { get; set; }


        [Display(Name = "Descrizione")]
        [MaxLength(100)]
        public string Description { get; set; }

        [Display(Name = nameof(Attachment.FolderId), ResourceType = typeof(Resources.Models.Attachment))]
        [ForeignKey(nameof(Folder))]
        public int? FolderId { get; set; }

        [Display(Name = "Data")]
        public DateTime? CreatedOn { get; set; }

        [Required]
        [Display(Name = "User")]
        [ForeignKey(nameof(User))]
        public string IdUser { get; set; }

        [NotMapped]
        public bool CanEdit { get; set; }

        [NotMapped]
        public bool CanDelete { get; set; }
    
        public AttachmentVisibilities Visibility { get; set; }
        
        [JsonIgnore]
        public virtual ApplicationUser User { get; set; }

        public virtual ICollection<AttachmentFile> Files { get; set; }

        public virtual Folder? Folder { get; set; }

    }



   
    public class AttachmentFile
    {
        public int Id { get; set; }

        [ForeignKey("Attachment")]
        public int IdAttachment { get; set; }

        [Display(Name = "Nome File")]
        public string Name { get; set; }

        [Display(Name = "Tipo File")]
        [MaxLength(100)]
        public string ContentType { get; set; }

        public string FileType { get; set; }

        public string Link { get; set; }
       
        [NotMapped]
        public bool Selected { get; set; }

        [NotMapped]
        public byte[] Bytes { get; set; }

        [Display(Name = "Dimensione")]
        public double Size { get; set; }

        [NotMapped]
        public string Content { get; set; }

        public virtual Attachment Attachment { get; set; }

       
    }
    
    
    public class AttachmentsFilter: PagingParameterModel
    {
        public int? IdParant { get; set; }

        public AttachmentTypes AttchmentType { get; set; }

        public int? FolderId { get; set; }

        public string FileType { get; set; }

        public string Name { get; set; }
    }

    public class AttachmentResponse
    {
        public string Name { get; set; }

        public string ContentType { get; set; }

        public bool HasPermits { get; set; }
    }
}

public class UploadFilesModel
{ 
    public int IdOwner { get; set; }

    public List<AttachmentFileModel> Files { get; set; }
}
public class AttachmentFileModel
{
    public string Content { get; set; }

    public string ContentType { get; set; }

    public int Id { get; set; }

}