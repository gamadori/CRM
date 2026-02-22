using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class FolderLanguage
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Folder))]
        public int FolderId { get; set; }

        [ForeignKey(nameof(Language))]
        public int LanguageId { get; set; }

        public string Name { get; set; }

        public virtual Folder Folder { get; set; }

        public virtual Language Language { get; set; }
    }

    public class FolderLanguageFilter : PagingParameterModel
    {
        public string? Name { get; set; }
        public int? LanguageId { get; set; }

        public int? FolderId { get; set; }
    }
}
