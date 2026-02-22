using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared.DTOs
{
    public class AttachmentDTO
    {
        
        public int Id { get; set; }

        public int IdParent { get; set; }

        public AttachmentTypes AttchmentType { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public int? FolderId { get; set; }

        public DateTime? CreatedOn { get; set; }

        public string IdUser { get; set; }

        [NotMapped]
        public bool CanEdit { get; set; }

        [NotMapped]
        public bool CanDelete { get; set; }

        public string? NameUser { get; set; }


        public string Folder { get; set; }
    }
}
