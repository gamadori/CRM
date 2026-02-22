using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen.Blazor;

namespace RedG.Client.Components
{
    public enum RGButtonType
    {
  
        New,
        Edit,
        Delete,
        Save,
        Submit,
        Cancel,
        Back,
        Download,
        DownloadPdf,
        View,
        Close,
        Confirm,
        Translate,
        AssignTicket,
        CloseTicket
    }
    public class RedGButton: RadzenButton
    {
        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public RGButtonType RGButtonType { get; set; } = RGButtonType.Save;

        [Parameter]
        public bool AutoLabel { get; set; } = false;

        protected override void OnInitialized()
        {
            base.OnInitialized();
            switch (RGButtonType)
            {
                case RGButtonType.New:
                    Icon = "add_circle";
                    ButtonStyle = Radzen.ButtonStyle.Primary;
                    break;
                case RGButtonType.Edit:
                    Icon = "edit";
                    if (AutoLabel)
                        Text = Localize["Edit"];
                    ButtonStyle = Radzen.ButtonStyle.Secondary;
                    break;
                case RGButtonType.Delete:
                    Icon = "delete";
                    ButtonStyle = Radzen.ButtonStyle.Danger;
                    break;
                case RGButtonType.Save:
                    Icon = "save";
                    ButtonStyle = Radzen.ButtonStyle.Success;
                    break;
                case RGButtonType.Submit:
                    Text = Localize["Save"];
                    Icon = "save";
                    ButtonStyle = Radzen.ButtonStyle.Success;
                    ButtonType = Radzen.ButtonType.Submit;
                    break;

                case RGButtonType.Cancel:
                    if (AutoLabel)
                        Text = Localize["Cancel"];
                    Icon = "cancel";
                    ButtonStyle = Radzen.ButtonStyle.Light;
                    break;

                case RGButtonType.Back:
                    Icon = "arrow_back_ios_new";
                    ButtonStyle = Radzen.ButtonStyle.Light;
                    break;
                case RGButtonType.Download:
                    Icon= "download";
                    ButtonStyle = Radzen.ButtonStyle.Warning;
                    break;

                case RGButtonType.DownloadPdf:
                    Icon = "picture_as_pdf";
                    ButtonStyle = Radzen.ButtonStyle.Warning;
                    break;


                case RGButtonType.View:

                    Icon = "visibility";
                    ButtonStyle = Radzen.ButtonStyle.Info;
                    break;
                case RGButtonType.Close:
                    Icon = "close";
                    ButtonStyle = Radzen.ButtonStyle.Light;
                    break;

                case RGButtonType.Confirm:
                    Icon = "check_circle";
                    ButtonStyle = Radzen.ButtonStyle.Info;
                    break;

                case RGButtonType.Translate:
                    Icon = "translate";
                    ButtonStyle = Radzen.ButtonStyle.Warning;
                    break;
                case RGButtonType.AssignTicket:
                    Icon= "assignment_ind";
                    ButtonStyle = Radzen.ButtonStyle.Info;
                    break;
                case RGButtonType.CloseTicket:
                    Icon = "assignment_turned_in";
                    ButtonStyle = Radzen.ButtonStyle.Success;
                    break;

            }
        }
    }
}
