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
        services.AddSingleton<WorkspaceIndex>();
        services.AddSingleton<DocumentStore>();
        services.AddSingleton<PlayscriptFormatter>();
    })
    .WithHandler<PlayscriptDocumentSyncHandler>()
    .WithHandler<PlayscriptSemanticTokensHandler>()
    .WithHandler<PlayscriptFormattingHandler>()
);

await server.WaitForExit;
