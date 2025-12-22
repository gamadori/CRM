using BlazoringComponents.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazoringComponents
{
   

    public partial class BlazorButton : ComponentBase
    {

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.Buttons> Localize { get; set; }

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

        [Parameter]
        public bool Label { get; set; } = false;

        [Parameter]
        public bool Waiting { get; set; } = false;

        [Parameter]
        public string LabelWaiting { get; set; }

        [Parameter]
        public string CssClass { get; set; }

        [Parameter]
        public bool Disabled { get; set; }
        protected string _text;

        protected string _title;

        
        protected override void OnInitialized()
        {
            _text = GetText();
            _title = GetDesc();

            
        }

        protected string GetStyle()
        {
            switch (Template)
            {
                case TemplateButton.Cancel:
                    return "btn btn-secondary";

                case TemplateButton.Delete:
                    return "btn btn-danger";

                case TemplateButton.Back:
                case TemplateButton.Details:
                    return "btn btn-secondary";

                case TemplateButton.Submit:
                case TemplateButton.Edit:
                    return "btn btn-primary";

                case TemplateButton.Invitation:
                    return "btn btn-info";
                case TemplateButton.Next:
                case TemplateButton.Previous:
                    return "btn btn-primary";

                case TemplateButton.Filter:
                    return "btn btn-outline-secondary";

                case TemplateButton.CSVImport:
                case TemplateButton.Download:
                    return "btn btn-warning";

                case TemplateButton.Confirm:
                    return "btn btn-outline-secondary";
                case TemplateButton.Add:
                    return "btn btn-info";

                case TemplateButton.Report:
                    return "btn btn-success";
                case TemplateButton.Upload:
                    return "btn btn-success";
                case TemplateButton.SendEmail:
                    return "btn btn-primary";

                default:
                    string desc = Style.GetDisplayName();
                    string outline = (Outline) ? "outline-" : "";
                    return $"btn btn-{outline}{desc}";

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
                case TemplateButton.Invitation:
                case TemplateButton.Back:
                case TemplateButton.Filter:
                case TemplateButton.Previous:
                case TemplateButton.Next:
                case TemplateButton.Download:
                case TemplateButton.Confirm:
                case TemplateButton.Add:
                case TemplateButton.Report:
                case TemplateButton.Upload:
                case TemplateButton.SendEmail:
                case TemplateButton.CSVImport:
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
                case TemplateButton.New:
                    return "playlist_add";
                case TemplateButton.Invitation:
                    return "person_add";
                case TemplateButton.Back:
                    return "arrow_back";
                case TemplateButton.Filter:
                    return "filter_alt";
                case TemplateButton.Previous:
                    return "arrow_back_ios";
                case TemplateButton.Next:
                    return "arrow_forward_ios";
                case TemplateButton.Download:
                    return "file_download";
                case TemplateButton.Confirm:
                    return "check";
                case TemplateButton.Add:
                    return "add";
                case TemplateButton.Report:
                    return "description";
                case TemplateButton.Upload:
                    return "file_upload";
                case TemplateButton.SendEmail:
                    return "attach_email";
                case TemplateButton.CSVImport:
                    return "upload_file";
                default:
                    return Icon;

            }
        }

        protected string GetDesc()
        {
            switch (Template)
            {
                case TemplateButton.Edit:
                    return Localize["Modifica"];
                case TemplateButton.Delete:
                    return Localize["Elimina"];
                case TemplateButton.Details:
                    return Localize["Dettagli"];

                case TemplateButton.Submit:
                    return Localize["Salva"];
                case TemplateButton.Cancel:
                    return Localize["Cancel"];
                case TemplateButton.New:
                    return Localize["Nuovo"];
                case TemplateButton.Invitation:
                    return Localize["Invia Invito"];
                case TemplateButton.Download:
                    return Localize["Download"];
                case TemplateButton.Confirm:
                    return Localize["Conferma"];
                case TemplateButton.Add:
                    return Localize["Aggiungi"];
                case TemplateButton.Report:
                    return Localize["Report"];
                case TemplateButton.Upload:
                    return Localize["Upload"];
                case TemplateButton.CSVImport:
                    return Localize["CSV Import"];
                default:
                    return "";
            }
        }
        protected string GetText()
        {
            if (Text != null && Text.Length > 0)
                return Text;
            else if (!Label)
                return "";
            else
            {
                return GetDesc();
                
            }
        }

    }

    
}
