using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared.DTOs
{
    public class FolderLanguageDTO
    {
        public int Id { get; set; }
        public int IdFolder { get; set; }
        public int IdLanguage { get; set; }
        public string Name { get; set; }

        public string Language { get; set; }
    }
}
