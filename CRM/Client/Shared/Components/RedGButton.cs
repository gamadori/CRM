using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen.Blazor;
using System.Threading.Tasks;

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

    /// <summary>
    /// RadzenButton con dei preset di icona e stile per i comandi ricorrenti del CRM.
    /// <para>
    /// Il preset vale solo come valore predefinito: quello che il chiamante passa
    /// esplicitamente vince sempre. Serve perche' <see cref="RGButtonType"/> vale Save quando
    /// non specificato, e un preset applicato incondizionatamente sovrascriveva Icon e
    /// ButtonStyle di ogni pulsante che non lo dichiarava.
    /// </para>
    /// </summary>
    public class RedGButton: RadzenButton
    {
        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public RGButtonType RGButtonType { get; set; } = RGButtonType.Save;

        [Parameter]
        public bool AutoLabel { get; set; } = false;

        // Quali parametri ha indicato il chiamante: si legge dalla ParameterView perche' le
        // proprieta' hanno comunque un valore predefinito e a posteriori non si distingue
        // "non passato" da "passato uguale al default".
        private bool _iconProvided;

        private bool _buttonStyleProvided;

        private bool _textProvided;

        public override Task SetParametersAsync(ParameterView parameters)
        {
            _iconProvided = parameters.TryGetValue<string>(nameof(Icon), out _);
            _buttonStyleProvided = parameters.TryGetValue<Radzen.ButtonStyle>(nameof(ButtonStyle), out _);
            _textProvided = parameters.TryGetValue<string>(nameof(Text), out _);

            return base.SetParametersAsync(parameters);
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();

            switch (RGButtonType)
            {
                case RGButtonType.New:
                    ApplyIcon("add_circle");
                    ApplyStyle(Radzen.ButtonStyle.Primary);
                    break;
                case RGButtonType.Edit:
                    ApplyIcon("edit");
                    if (AutoLabel)
                        ApplyText(Localize["Edit"]);
                    ApplyStyle(Radzen.ButtonStyle.Secondary);
                    break;
                case RGButtonType.Delete:
                    ApplyIcon("delete");
                    ApplyStyle(Radzen.ButtonStyle.Danger);
                    break;
                case RGButtonType.Save:
                    ApplyIcon("save");
                    ApplyStyle(Radzen.ButtonStyle.Success);
                    break;
                case RGButtonType.Submit:
                    ApplyText(Localize["Save"]);
                    ApplyIcon("save");
                    ApplyStyle(Radzen.ButtonStyle.Success);
                    ButtonType = Radzen.ButtonType.Submit;
                    break;

                case RGButtonType.Cancel:
                    if (AutoLabel)
                        ApplyText(Localize["Cancel"]);
                    ApplyIcon("cancel");
                    ApplyStyle(Radzen.ButtonStyle.Light);
                    break;

                case RGButtonType.Back:
                    ApplyIcon("arrow_back_ios_new");
                    ApplyStyle(Radzen.ButtonStyle.Light);
                    break;
                case RGButtonType.Download:
                    ApplyIcon("download");
                    ApplyStyle(Radzen.ButtonStyle.Warning);
                    break;

                case RGButtonType.DownloadPdf:
                    ApplyIcon("picture_as_pdf");
                    ApplyStyle(Radzen.ButtonStyle.Warning);
                    break;


                case RGButtonType.View:

                    ApplyIcon("visibility");
                    ApplyStyle(Radzen.ButtonStyle.Info);
                    break;
                case RGButtonType.Close:
                    ApplyIcon("close");
                    ApplyStyle(Radzen.ButtonStyle.Light);
                    break;

                case RGButtonType.Confirm:
                    ApplyIcon("check_circle");
                    ApplyStyle(Radzen.ButtonStyle.Info);
                    break;

                case RGButtonType.Translate:
                    ApplyIcon("translate");
                    ApplyStyle(Radzen.ButtonStyle.Warning);
                    break;
                case RGButtonType.AssignTicket:
                    ApplyIcon("assignment_ind");
                    ApplyStyle(Radzen.ButtonStyle.Info);
                    break;
                case RGButtonType.CloseTicket:
                    ApplyIcon("assignment_turned_in");
                    ApplyStyle(Radzen.ButtonStyle.Success);
                    break;

            }
        }

        private void ApplyIcon(string icon)
        {
            if (!_iconProvided)
                Icon = icon;
        }

        private void ApplyStyle(Radzen.ButtonStyle buttonStyle)
        {
            if (!_buttonStyleProvided)
                ButtonStyle = buttonStyle;
        }

        private void ApplyText(string text)
        {
            if (!_textProvided)
                Text = text;
        }
    }
}
