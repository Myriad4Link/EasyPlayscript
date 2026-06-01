using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EasyPlayscript.Tests.Utils;

public class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly ImmutableDictionary<string, string> _globalOptions;

    public TestAnalyzerConfigOptionsProvider(ImmutableDictionary<string, string> globalOptions)
    {
        _globalOptions = globalOptions;
    }

    public TestAnalyzerConfigOptionsProvider(params (string key, string value)[] options)
        : this(options.ToImmutableDictionary(o => o.key, o => o.value))
    {
    }

    public override AnalyzerConfigOptions GlobalOptions => new TestAnalyzerConfigOptions(_globalOptions);

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        => new TestAnalyzerConfigOptions(ImmutableDictionary<string, string>.Empty);

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        => new TestAnalyzerConfigOptions(ImmutableDictionary<string, string>.Empty);

    private class TestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly ImmutableDictionary<string, string> _options;

        public TestAnalyzerConfigOptions(ImmutableDictionary<string, string> options)
        {
            _options = options;
        }

        public override bool TryGetValue(string key, out string value)
        {
            var result = _options.TryGetValue(key, out var v);
            value = v!;
            return result;
        }
    }
}
