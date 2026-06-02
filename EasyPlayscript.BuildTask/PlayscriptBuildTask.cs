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

    [Required]
    public string AesKey { get; set; }

    public override bool Execute()
    {
        var hasErrors = false;
        var scripts = new Dictionary<string, ScriptBlock>();
        var texts = new Dictionary<string, ScriptBlock>();
        var scriptLocations = new Dictionary<string, (string filePath, int line, int col)>();
        var textLocations = new Dictionary<string, (string filePath, int line, int col)>();
        var allInterfaces = new List<InterfaceDeclaration>();

        foreach (var file in SourceFiles)
        {
            var filePath = file.ItemSpec;
            var content = File.ReadAllText(filePath);
            var parseResult = PlayscriptStructureHelper.ParseStructure(content);

            foreach (var iface in parseResult.Interfaces)
            {
                iface.FilePath = filePath;
                allInterfaces.Add(iface);
            }

            foreach (var (identifier, name, rawContent, line, col) in parseResult.Results)
            {
                if (rawContent == null) continue;

                var trimmedContent = rawContent.Trim('\r', '\n');
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
                builder.BuildFromContent(tree);
                var block = builder.ContentResult;

                if (block == null) continue;

                if (identifier == BlockType.Script)
                {
                    if (scripts.ContainsKey(name))
                    {
                        var loc = scriptLocations[name];
                        Log.LogError("Playscript", "SCPT004", null,
                            loc.filePath, loc.line, loc.col, 0, 0,
                            $"Duplicate script name \"{name}\"");
                        hasErrors = true;
                    }
                    else
                        scriptLocations[name] = (filePath, line, col);

                    scripts[name] = block;
                }
                else if (identifier == BlockType.Text)
                {
                    if (texts.ContainsKey(name))
                    {
                        var loc = textLocations[name];
                        Log.LogError("Playscript", "SCPT004", null,
                            loc.filePath, loc.line, loc.col, 0, 0,
                            $"Duplicate text name \"{name}\"");
                        hasErrors = true;
                    }
                    else
                        textLocations[name] = (filePath, line, col);

                    texts[name] = block;
                }
            }
        }

        var validationErrors = new List<ValidationDiagnostic>();
        validationErrors.AddRange(InterfaceValidator.ValidateUndeclaredCalls(
            allInterfaces, scripts, scriptLocations, texts, textLocations));
        validationErrors.AddRange(InterfaceValidator.ValidateDuplicateSignatures(allInterfaces));
        validationErrors.AddRange(InterfaceValidator.ValidateArgumentTypes(
            allInterfaces, scripts, scriptLocations, texts, textLocations));

        foreach (var diag in validationErrors)
        {
            Log.LogError("Playscript", diag.Code, null,
                diag.FilePath, diag.Line, diag.Col, 0, 0, diag.Message);
            hasErrors = true;
        }

        if (hasErrors)
            return false;

        var data = new PlayscriptData { Scripts = scripts, Texts = texts };
        var bytes = MessagePackSerializer.Serialize(data);
        var encrypted = PlayscriptLoader.AesEncrypt(bytes, AesKey);

        var dir = Path.GetDirectoryName(OutputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllBytes(OutputPath, encrypted);

        return true;
    }
}
