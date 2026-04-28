using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ManipuladorFotos;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        ShowSafeErrorMessage(e.Exception);
    }

    private static void OnCurrentDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            ShowSafeErrorMessage(ex);
            return;
        }

        System.Windows.MessageBox.Show(
            "Ocorreu um erro inesperado. Reinicie o aplicativo para continuar com segurança.",
            "Erro inesperado",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        ShowSafeErrorMessage(e.Exception);
    }

    private static void ShowSafeErrorMessage(Exception ex)
    {
        var message = ex.Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            message = "Erro desconhecido.";
        }

        System.Windows.MessageBox.Show(
            $"Ocorreu um erro inesperado: {message}\n\nA aplicação tentou continuar sem fechar.",
            "Erro inesperado",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning);
    }
}
