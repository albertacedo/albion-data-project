using System.Windows;
using System.Windows.Threading;

namespace AlbionMarket.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            MessageBox.Show(args.ExceptionObject?.ToString() ?? "Error desconegut",
                "Albion Market", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Keep the app alive: a bad row from the API should not close the window.
        MessageBox.Show(e.Exception.Message, "Albion Market",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
