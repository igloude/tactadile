namespace Tactadile;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        using var mutex = new Mutex(true, @"Global\Tactadile_SingleInstance", out bool createdNew);
        if (!createdNew)
            return; // Another instance is already running

        // For self-contained unpackaged apps, the Windows App SDK auto-initializes.
        // No Bootstrap.Initialize() call needed.

        Microsoft.UI.Xaml.Application.Start((p) =>
        {
            var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            System.Threading.SynchronizationContext.SetSynchronizationContext(context);

            new App();
        });
    }
}
