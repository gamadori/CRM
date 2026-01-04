using CRM.Shared;
using System.Collections.Generic;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Models
{
    public class PageHeaderModel
    {
        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
        public List<BreadcrumbItem> BreadcrumbItems { get; set; } = new();
        public string Icon { get; set; } = string.Empty;

        public PageModality PageMode { get; set; } =  PageModality.Visualization;

        public string? DialogTitle { get; set; }
    }
}
