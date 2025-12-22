using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Radzen;
using Radzen.Blazor;

namespace CRM.Client.Shared.Components
{
    public partial class AGButton: ComponentBase
    {
        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public EventCallback<MouseEventArgs> OnClickEvent { get; set; }

        [Parameter]
        public string? Text { get; set; } = null;

        public enum ButtonStyles
        {
            ButtonDetails,
            ButtonEdit,
            ButtonConfirm,
            ButtonCancel,
            ButtonDownload,
            ButtonDelete,
            ButtonNew,
            ButtonPrint
        }

        [Parameter]
        public ButtonStyles ButtonStyle { get; set; }   

        private Radzen.ButtonStyle GetButtonStyle()
        {
            switch (ButtonStyle)
            {
                case ButtonStyles.ButtonDetails:
                    return Radzen.ButtonStyle.Secondary;

                case ButtonStyles.ButtonEdit:
                    return Radzen.ButtonStyle.Primary;

                case ButtonStyles.ButtonConfirm:
                    return Radzen.ButtonStyle.Success;

                case ButtonStyles.ButtonCancel:
                    return Radzen.ButtonStyle.Secondary;

                case ButtonStyles.ButtonDownload:
                    return Radzen.ButtonStyle.Warning;

                case ButtonStyles.ButtonDelete:
                    return Radzen.ButtonStyle.Danger;
                case ButtonStyles.ButtonNew:
                    return Radzen.ButtonStyle.Light;

                default:
                    return Radzen.ButtonStyle.Info;
            }
        }

        private string GetIcon()
        {
            switch (ButtonStyle)
            {
                case ButtonStyles.ButtonDetails:
                    return "format_list_bulleted";
                case ButtonStyles.ButtonEdit:
                    return "edit";
                case ButtonStyles.ButtonConfirm:
                    return "done";
                case ButtonStyles.ButtonCancel:
                    return "cancel";
                case ButtonStyles.ButtonDownload:
                    return "download";
                case ButtonStyles.ButtonDelete:
                    return "delete";
                case ButtonStyles.ButtonNew:
                    return "playlist_add";

                case ButtonStyles.ButtonPrint:
                    return "print";
                default:
                    return "";

            }
        }

        private string GetText()
        {
            if (Text == null)
                return Localize[ButtonStyle.ToString()];
            else
                return Text;    
        }

    }
}
