using EasyPlayscript.LSP.Parsing;
using EasyPlayscript.Parsing;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace EasyPlayscript.LSP.Services;

/// <summary>
///     Aggregates parsed <c>.scpt</c> files in the workspace and produces cross-file
///     validation diagnostics. Per-file data comes from
///     <see cref="PlayscriptDocumentParser" /> (Pass 2) and is merged into a single
///     <see cref="PlayscriptCompilationData" /> to run
///     <see cref="PlayscriptPipeline.Validate" /> (SCPT004–SCPT008). Cross-file
///     diagnostics are routed back to the originating file via
///     <see cref="ValidationDiagnostic.FilePath" />, which the pipeline sets to the
///     <c>DocumentUri</c> string used at registration time.
/// </summary>
/// <remarks>
///     <para>
///         Implementation-side diagnostics (SCPT009–SCPT013) are intentionally <b>not</b>
///         reported here — the LSP process has no access to user <c>[Implementation]</c>
///         methods. The Roslyn source generator and the build task still surface them at
///         compile time. Re-evaluate when the LSP gains a workspace symbol index.
///     </para>
///     <para>
///         Single-root only: <c>DocumentUri</c> strings are used as the canonical key.
///         Multi-root workspaces would need a folder-relative key.
///     </para>
///     <para>
///         Iteration order over registered files is insertion order. This matters because
///         <see cref="PlayscriptCompilationData.MergeFrom" /> attributes SCPT004 (duplicate
///         script/text name) to the first-merged file's location. Re-registering a URI
///         updates the entry in place (preserving its position).
///     </para>
/// </remarks>
internal class WorkspaceIndex
{
    private readonly Lock _lock = new();

    // Insertion-ordered. Keyed by DocumentUri.ToString() so it matches the FilePath
    // baked into the per-file data by PlayscriptPipeline.ProcessFile. We use a
    // regular Dictionary (not ConcurrentDictionary) so iteration order is stable.
    private readonly Dictionary<DocumentUri, FileCompilation> _files = new();

    // Cached per-file merged diagnostics, keyed by file path (matching
    // ValidationDiagnostic.FilePath). Each entry contains the file's own
    // per-file diagnostics plus any cross-file diagnostics routed to it.
    // Recomputed on any register/remove.
    private Dictionary<string, List<ValidationDiagnostic>> _diagnosticsByFile = new();

    /// <summary>
    ///     Replaces the workspace entry for <paramref name="uri" /> and re-runs the
    ///     cross-file validator against the full aggregate.
    /// </summary>
    /// <param name="uri">The document URI (used as the canonical key).</param>
    /// <param name="perFileDiagnostics">
    ///     Validation diagnostics produced by per-file Pass 2 processing (SCPT002/003/004).
    /// </param>
    /// <param name="compilationData">
    ///     Per-file parsed data: scripts, texts, interfaces, and their locations.
    /// </param>
    public void Register(DocumentUri uri, IReadOnlyList<ValidationDiagnostic> perFileDiagnostics,
        PlayscriptCompilationData compilationData)
    {
        lock (_lock)
        {
            // Re-registering preserves the existing insertion position so SCPT004
            // attribution remains stable across edits.
            _files[uri] = new FileCompilation(uri.ToString(), perFileDiagnostics, compilationData);
            Recompute();
        }
    }

    /// <summary>
    ///     Removes a file from the workspace and re-runs cross-file validation.
    /// </summary>
    public void Remove(DocumentUri uri)
    {
        lock (_lock)
        {
            _files.Remove(uri);
            Recompute();
        }
    }

    /// <summary>
    ///     Returns the combined diagnostics for <paramref name="uri" />: its per-file
    ///     diagnostics plus all cross-file diagnostics routed to its file path.
    /// </summary>
    public IReadOnlyList<ValidationDiagnostic> GetAllDiagnostics(DocumentUri uri)
    {
        lock (_lock)
        {
            return _diagnosticsByFile.TryGetValue(uri.ToString(), out var diags)
                ? diags
                : [];
        }
    }

    /// <summary>
    ///     Returns the per-file parsed data for <paramref name="uri" />, or <c>null</c>
    ///     if the file is not in the workspace.
    /// </summary>
    public PlayscriptCompilationData? GetCompilationData(DocumentUri uri)
    {
        lock (_lock)
        {
            return _files.TryGetValue(uri, out var file) ? file.CompilationData : null;
        }
    }

    /// <summary>
    ///     Number of files currently tracked. Used by tests.
    /// </summary>
    public int FileCount
    {
        get
        {
            lock (_lock) return _files.Count;
        }
    }

    private void Recompute()
    {
        // Caller holds _lock. Re-aggregation is O(aggregate size); for typical
        // workspaces (tens of files) this is sub-millisecond.

        // Start with the per-file diagnostics captured at registration.
        var byFile = new Dictionary<string, List<ValidationDiagnostic>>();
        foreach (var kvp in _files) byFile[kvp.Value.FilePath] = [.. kvp.Value.PerFileDiagnostics];

        // Merge each file's compilation data into an aggregate, folding the
        // resulting SCPT004 (duplicate script/text name) diagnostics into the
        // per-file pool. MergeFrom attributes the duplicate to the first-seen
        // file's location, which is exactly the file we want to surface the
        // error on. Iteration order is insertion order, so "first-seen" is
        // "first-registered".
        var aggregate = new PlayscriptCompilationData();
        foreach (var d in from kvp in _files
                 select aggregate.MergeFrom(kvp.Value.CompilationData)
                 into mergeDiags
                 from d in mergeDiags
                 where !string.IsNullOrEmpty(d.FilePath)
                 select d)
        {
            if (!byFile.TryGetValue(d.FilePath, out var list))
            {
                list = [];
                byFile[d.FilePath] = list;
            }

            list.Add(d);
        }

        // SCPT009–SCPT013 are deferred: the LSP has no access to user
        // [Implementation] methods, so reporting them would always produce
        // false positives. See the class-level remarks.
        var crossFile = PlayscriptPipeline.Validate(aggregate);
        foreach (var d in crossFile.Where(d => !string.IsNullOrEmpty(d.FilePath)))
        {
            if (!byFile.TryGetValue(d.FilePath, out var list))
            {
                list = [];
                byFile[d.FilePath] = list;
            }

            list.Add(d);
        }

        _diagnosticsByFile = byFile;
    }

    private sealed record FileCompilation(
        string FilePath,
        IReadOnlyList<ValidationDiagnostic> PerFileDiagnostics,
        PlayscriptCompilationData CompilationData);
}