using CRM.Mobile.Pages;

namespace CRM.Mobile;

public partial class App : Application
{
    private readonly CapturePage _capture;
    private readonly SettingsPage _settings;

    public App(CapturePage capture, SettingsPage settings)
    {
        InitializeComponent();
        _capture = capture;
        _settings = settings;
    }

    /// <summary>
    /// Due schede: si raccoglie in una, si configura nell'altra.
    /// <para>
    /// La separazione non e' estetica. Allo stand si tocca una schermata sola, e l'indirizzo del
    /// server non deve stare sotto le dita mentre si ha davanti una persona - e' il modo piu'
    /// veloce per cancellarlo per sbaglio a meta' fiera.
    /// </para>
    /// </summary>
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var tabs = new TabbedPage
        {
            BarBackgroundColor = Theme.Brand,
            BarTextColor = Colors.White,
            SelectedTabColor = Colors.White,
            UnselectedTabColor = Color.FromArgb("#A7D5D0"),
            Children =
            {
                new NavigationPage(_capture) { Title = "Cattura", BarBackgroundColor = Theme.Brand, BarTextColor = Colors.White },
                new NavigationPage(_settings) { Title = "Configurazione", BarBackgroundColor = Theme.Brand, BarTextColor = Colors.White }
            }
        };

        return new Window(tabs);
    }
}
