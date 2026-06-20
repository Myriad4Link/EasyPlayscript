using System.Collections.Generic;
using System.IO;
using System.Linq;
using EasyPlayscript;
using EasyPlayscript.Parsing;
using MessagePack;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace EasyPlayscript.BuildTask;

public class PlayscriptBuildTask : Task
{
    [Required]
    public ITaskItem[] SourceFiles { get; set; }

    [Required]
    public string OutputPath { get; set; }

    public string AesKey { get; set; }

    public override bool Execute()
    {
        var hasErrors = false;
        var data = new PlayscriptCompilationData();
        AesKey ??= string.Empty;

        foreach (var file in SourceFiles)
        {
            var filePath = file.ItemSpec;
            var content = File.ReadAllText(filePath);
            var parseResult = PlayscriptStructureHelper.ParseStructure(content);

            foreach (var iface in parseResult.Interfaces)
            {
                iface.FilePath = filePath;
                data.Interfaces.Add(iface);
            }

            foreach (var (identifier, name, rawContent, line, col) in parseResult.Results)
            {
                if (rawContent == null) continue;

                var trimmedContent = rawContent.Trim('\r', '\n');

                if (identifier == BlockType.Script)
                {
                    var (parser, contentErrors) = PlayscriptContentHelper.Parse(trimmedContent);
                    var tree = parser.scriptContent();

                    if (contentErrors.Count > 0)
                    {
                        foreach (var error in contentErrors)
                            Log.LogError("Playscript", "SCPT002", null,
                                filePath, error.Line, error.Col, 0, 0, error.Msg);
                        hasErrors = true;
                        continue;
                    }

                    if (tree == null) continue;

                    var builder = new PlayscriptCodeBuilder();
                    builder.BuildScriptFromContent(tree);
                    var block = builder.ContentResult;

                    if (block == null) continue;

                    if (data.Scripts.ContainsKey(name))
                    {
                        var loc = data.ScriptLocations[name];
                        Log.LogError("Playscript", "SCPT004", null,
                            loc.filePath, loc.line, loc.col, 0, 0,
                            $"Duplicate script name \"{name}\"");
                        hasErrors = true;
                    }
                    else
                        data.ScriptLocations[name] = (filePath, line, col);

                    data.Scripts[name] = block;
                }
                else if (identifier == BlockType.Text)
                {
                    var (parser, contentErrors) = PlayscriptContentHelper.ParseText(trimmedContent);
                    var tree = parser.textContent();

                    if (contentErrors.Count > 0)
                    {
                        foreach (var error in contentErrors)
                            Log.LogError("Playscript", "SCPT002", null,
                                filePath, error.Line, error.Col, 0, 0, error.Msg);
                        hasErrors = true;
                        continue;
                    }

                    if (tree == null) continue;

                    var builder = new PlayscriptCodeBuilder();
                    builder.BuildTextFromContent(tree);
                    var block = builder.TextResult;

                    if (block == null) continue;

                    if (data.Texts.ContainsKey(name))
                    {
                        var loc = data.TextLocations[name];
                        Log.LogError("Playscript", "SCPT004", null,
                            loc.filePath, loc.line, loc.col, 0, 0,
                            $"Duplicate text name \"{name}\"");
                        hasErrors = true;
                    }
                    else
                        data.TextLocations[name] = (filePath, line, col);

                    data.Texts[name] = block;
                }
            }
        }

        var validationErrors = new List<ValidationDiagnostic>();
        validationErrors.AddRange(InterfaceValidator.ValidateUndeclaredCalls(data));
        validationErrors.AddRange(InterfaceValidator.ValidateDuplicateSignatures(data));
        validationErrors.AddRange(InterfaceValidator.ValidateArgumentTypes(data));

        foreach (var diag in validationErrors)
        {
            Log.LogError("Playscript", diag.Code, null,
                diag.FilePath, diag.Line, diag.Col, 0, 0, diag.Message);
            hasErrors = true;
        }

        if (hasErrors)
            return false;

        var playscriptData = new PlayscriptData { Scripts = data.Scripts, Texts = data.Texts };
        var bytes = MessagePackSerializer.Serialize(playscriptData);
        var encrypted = PlayscriptLoader.AesEncrypt(bytes, AesKey);

        var dir = Path.GetDirectoryName(OutputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllBytes(OutputPath, encrypted);

        return true;
    }
}
