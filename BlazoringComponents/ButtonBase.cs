using BlazoringComponents.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazoringComponents
{
    public enum TemplateButton
    {
        Null,
        [Display(Name = "submit")]
        Submit,
        [Display(Name = "cancel")]
        Cancel,
        [Display(Name = "edit")]
        Edit,
        [Display(Name = "details")]
        Details,
        [Display(Name = "delete")]
        Delete,
        [Display(Name = "new")]
        New,
        [Display(Name = "back")]
        Back,
        [Display(Name = "invitation")]
        Invitation,
        [Display(Name = "Filtro")]
        Filter,
        [Display(Name = "previous")]
        Previous,
        [Display(Name = "next")]
        Next,
        [Display(Name = "Download")]
        Download,
        [Display(Name = "Conferma")]
        Confirm,
        [Display(Name = "Add")]
        Add,
        [Display(Name = "Report")]
        Report,
        [Display(Name = "Upload")]
        Upload,
        [Display(Name = "CSV Imnport")]
        CSVImport,
        [Display(Name = "send-email")]
        SendEmail
    }
    public enum BtnStyle
    {
        [Display(Name = "primary")]
        Primary,

        [Display(Name = "secondary")]
        Secondary,

        [Display(Name = "success")]
        Success,

        [Display(Name = "danger")]
        Danger,

        [Display(Name = "warning")]
        Warning,

        [Display(Name = "info")]
        Info,

        [Display(Name = "light")]
        Light,

        [Display(Name = "dark")]
        Dark


    }

    public enum BtnType
    {
        [Display(Name = "button")]
        Button,

        [Display(Name = "submit")]
        Submit,

        [Display(Name = "Reset")]
        Reset,


    }

    public class ButtonBase: ComponentBase
    {
        [Parameter]
        public string Icon { get; set; }

        [Parameter]
        public string Text { get; set; }

        [Parameter]
        public BtnStyle Style { get; set; } = BtnStyle.Light;

        [Parameter]
        public bool Outline { get; set; } = false;

        [Parameter]
        public BtnType Type { get; set; } = BtnType.Button;

        [Parameter]
        public EventCallback<MouseEventArgs> OnClick { get; set; }

        [Parameter]
        public TemplateButton Template { get; set; }

        protected string GetStyle()
        {
            switch (Template)
            {
                case TemplateButton.Cancel:
                    return "btn btn-secondary mx-2";

                case TemplateButton.Delete:
                    return "btn btn-danger mx-2";

                case TemplateButton.Details:
                case TemplateButton.Back:
                    return "btn btn-secondary mx-2";

                case TemplateButton.Submit:
                case TemplateButton.Edit:
                    return "btn btn-primary mx-2";

                case TemplateButton.Filter:
                    return "btn btn-outline-secondary mx-2";

                case TemplateButton.Next:
                case TemplateButton.Previous:
                    return "btn btn-outline-secondary mx-2";

                case TemplateButton.Download:
                    return "btn btn-warning mx-2";

                case TemplateButton.Confirm:
                    return "btn btn--outline-secondary mx-2";
                case TemplateButton.Add:
                    return "btn btn-info mx-2";
                default:
                    string desc = Style.GetDisplayName();
                    string outline = (Outline) ? "outline-" : "";
                    return $"btn btn-{outline}{desc} mx-2";

            }

        }

        protected string GetButtonType()
        {
            switch (Template)
            {
                case TemplateButton.Submit:
                    return "submit";
                case TemplateButton.Cancel:
                case TemplateButton.Delete:
                case TemplateButton.Details:
                case TemplateButton.Edit:
                case TemplateButton.Back:
                case TemplateButton.Filter:
                case TemplateButton.Next:
                case TemplateButton.Previous:
                case TemplateButton.Download:
                case TemplateButton.Add:
                    return "button";
                default:
                    return Type.GetDisplayName();
            }

        }



        protected string GetIcon()
        {
            switch (Template)
            {
                case TemplateButton.Submit:
                    return "save";
                case TemplateButton.Cancel:
                    return "cancel";
                case TemplateButton.Delete:
                    return "delete";
                case TemplateButton.Details:
                    return "list";
                case TemplateButton.Edit:
                    return "create";
                case TemplateButton.Back:
                    return "arrow_back";
                case TemplateButton.Filter:
                    return "filter_alt";
                case TemplateButton.Next:
                    return "arrow_forward_ios";
                case TemplateButton.Previous:
                    return "arrow_back_ios";
                case TemplateButton.Download:
                    return "file_download";
                case TemplateButton.Confirm:
                    return "check";
                case TemplateButton.Add:
                    return "add";
                default:
                    return Icon;

            }
        }
    }
}
