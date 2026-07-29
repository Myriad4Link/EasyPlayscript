import * as path from "path";
import { workspace, ExtensionContext } from "vscode";
import {
  LanguageClient,
  LanguageClientOptions,
  ServerOptions,
  TransportKind,
} from "vscode-languageclient/node";

let client: LanguageClient;

export function activate(context: ExtensionContext) {
  const config = workspace.getConfiguration("easyplayscript");
  let serverCommand: string;
  let serverArgs: string[];

  const customPath: string = config.get("server.path") ?? "";
  if (customPath) {
    serverCommand = customPath;
    serverArgs = [];
  } else {
    serverCommand = "dotnet";
    serverArgs = [
      context.asAbsolutePath(path.join("server", "EasyPlayscript.LSP.dll")),
    ];
  }

  const serverOptions: ServerOptions = {
    run: { command: serverCommand, args: serverArgs, transport: TransportKind.stdio },
    debug: { command: serverCommand, args: serverArgs, transport: TransportKind.stdio },
  };

  const clientOptions: LanguageClientOptions = {
    documentSelector: [{ scheme: "file", language: "playscript" }],
    synchronize: {
      fileEvents: workspace.createFileSystemWatcher("**/*.scpt"),
    },
  };

  client = new LanguageClient(
    "easyplayscript",
    "EasyPlayscript",
    serverOptions,
    clientOptions
  );

  client.start();
}

export function deactivate(): Thenable<void> | undefined {
  if (!client) {
    return undefined;
  }
  return client.stop();
}
