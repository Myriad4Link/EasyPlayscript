using System.IO;
using EasyPlayscript.Parsing;
using JetBrains.Annotations;
using MessagePack;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace EasyPlayscript.BuildTask;

public class PlayscriptBuildTask : Task
{
    [Required] public ITaskItem[] SourceFiles { get; [UsedImplicitly] set; }

    [Required] public string OutputPath { get; [UsedImplicitly] set; }

    // ReSharper disable once MemberCanBePrivate.Global
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
            var (structureResult, structureErrors) = PlayscriptStructureHelper.ParseStructureWithErrors(content);

            var diagnostics = PlayscriptPipeline.ProcessFile(structureResult, data, filePath);

            foreach (var error in structureErrors)
            {
                Log.LogError("Playscript",
                    error.IsLexer ? DiagnosticCodes.UnexpectedToken : DiagnosticCodes.MismatchedInput, null,
                    filePath, error.Line, error.Col, 0, 0, error.Msg);
                hasErrors = true;
            }

            foreach (var diag in diagnostics)
            {
                Log.LogError("Playscript", diag.Code, null,
                    diag.FilePath, diag.Line, diag.Col, 0, 0, diag.Message);
                hasErrors = true;
            }
        }

        foreach (var diag in PlayscriptPipeline.Validate(data))
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