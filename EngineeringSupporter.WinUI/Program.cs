using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System.Threading;
using Velopack;

namespace EngineeringSupporter.WinUI;

public static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // 1. Velopackの初期化（インストーラーのイベント等を処理）
        VelopackApp.Build().Run();

        // 2. 通常のWinUI起動プロセス
        Microsoft.UI.Xaml.Application.Start((p) =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }
}
