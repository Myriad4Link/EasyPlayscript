using System.Collections.Generic;
using System.IO;
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
        var scripts = new Dictionary<string, ScriptBlock>();
        var texts = new Dictionary<string, ScriptBlock>();

        foreach (var file in SourceFiles)
        {
            var content = File.ReadAllText(file.ItemSpec);
            var blocks = PlayscriptStructureHelper.ParseStructure(content);

            foreach (var (identifier, name, rawContent, line, col) in blocks)
            {
                if (rawContent == null) continue;

                var trimmedContent = rawContent.Trim('\r', '\n');
                var (parser, contentErrors) = PlayscriptContentHelper.Parse(trimmedContent);
                var tree = parser.scriptContent();

                if (contentErrors.Count > 0)
                {
                    foreach (var error in contentErrors)
                        Log.LogWarning("Playscript", "SCPT002", null,
                            file.ItemSpec, error.Line, error.Col, 0, 0, error.Msg);
                    continue;
                }

                if (tree == null) continue;

                var builder = new PlayscriptCodeBuilder();
                builder.BuildFromContent(tree);
                var block = builder.ContentResult;

                if (block == null) continue;

                if (identifier == "script") scripts[name] = block;
                else if (identifier == "text") texts[name] = block;
            }
        }

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
