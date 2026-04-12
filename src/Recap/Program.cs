using Recap.Tui;
using Velopack;

namespace Recap;

class Program
{
    static async Task Main(string[] args)
    {
        VelopackApp.Build().Run();

        var host = new TuiHost();
        await host.RunAsync();
    }
}
