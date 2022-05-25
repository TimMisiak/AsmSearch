using AsmSearch;
using DbgX;
using DbgX.Interfaces.Services;
using DbgX.Requests;
using DbgX.Requests.Initialization;
using Nito.AsyncEx;


if (args.Length != 1)
{
    Console.WriteLine("Usage: asmsearch <NameOfBinary>");
    return;
}

// DbgX was designed to be used in a UI context. If you're using it outside
// of a UI, make sure to establish a SynchronizationContext.
AsyncContext.Run(async () =>
{
    DebugEngine engine = new DebugEngine();
    engine.DmlOutput += Engine_DmlOutput;
    DisassemblyCache cache = new DisassemblyCache();
    await cache.DisassembleModule(engine, args[0]);
});

void Engine_DmlOutput(object? sender, OutputEventArgs e)
{
    Console.Write(e.Output);
}