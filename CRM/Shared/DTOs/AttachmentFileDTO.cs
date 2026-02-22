using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared.DTOs
{
    public class AttachmentFileDTO
    {
        public int Id { get; set; }

        [ForeignKey("Attachment")]
        public int IdAttachment { get; set; }

        [Display(Name = "Nome File")]
        public string Name { get; set; }

        [Display(Name = "Tipo File")]
        [MaxLength(100)]
        public string ContentType { get; set; }

        public string Link { get; set; }

        public bool Selected { get; set; }

        public byte[] Bytes { get; set; }

        [Display(Name = "Dimensione")]
        public double Size { get; set; }

        public string Content { get; set; }

    }
}
