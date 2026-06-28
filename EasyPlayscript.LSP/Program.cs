using EasyPlayscript.LSP.Parsing;
using EasyPlayscript.LSP.Services;
using EasyPlayscript.LSP.Sync;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Server;

var server = await LanguageServer.From(options => options
    .WithInput(Console.OpenStandardInput())
    .WithOutput(Console.OpenStandardOutput())
    .WithServices(services =>
    {
        services.AddSingleton<PlayscriptDocumentParser>();
        services.AddSingleton<DocumentStore>();
    })
    .WithHandler<PlayscriptDocumentSyncHandler>()
    .WithHandler<PlayscriptSemanticTokensHandler>()
);

await server.WaitForExit;