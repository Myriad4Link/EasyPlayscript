using System.IO;
using System.Text;
using EasyPlayscript.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace EasyPlayscript;

[Generator]
public class PlayscriptGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var scptProvider = context.AdditionalTextsProvider
            .Where(static f => f.Path.EndsWith(".scpt"))
            .Select(static (file, ct) =>
            {
                var text = file.GetText(ct);
                return new
                {
                    Name = Path.GetFileNameWithoutExtension(file.Path),
                    Content = text?.ToString() ?? string.Empty
                };
            });

        context.RegisterSourceOutput(scptProvider, static (spc, fileData) =>
        {
            var (parser, errors) = PlayscriptParserHelper.Parse(fileData.Content);
            var tree = parser.playscript();
            var builder = new PlayscriptCodeBuilder(fileData.Name);
            var code = builder.Visit(tree);
            spc.AddSource($"{fileData.Name}.g.cs", SourceText.From(code, Encoding.UTF8));
        });
    }
}
