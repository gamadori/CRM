using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class Folder
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = nameof(Folder.Name), ResourceType = typeof(Resources.Models.Folder))]
        public string Name { get; set; }

        [Display(Name = nameof(Folder.Description), ResourceType = typeof(Resources.Models.Folder))]
        public string Description { get; set; }


    }

    public class FolderFilter: PagingParameterModel
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }
}
