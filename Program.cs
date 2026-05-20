namespace SimpleSync;

static class Program
{
    private const string SingleInstanceMutexName = "SimpleSync_1E3A44B2_6F6A_4B70_A8D6_4B5D8E0E0D50";

    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("simple sync가 이미 실행 중입니다.", "simple sync", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ShowUnhandledError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
            {
                ShowUnhandledError(exception);
            }
        };

        try
        {
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            ShowUnhandledError(ex);
        }
    }

    private static void ShowUnhandledError(Exception exception)
    {
        MessageBox.Show(
            exception.Message,
            "simple sync error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
