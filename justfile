# EasyPlayscript task runner
# Requires: https://github.com/casey/just
#
# Examples:
#   just pack-local
#   just pack-local configuration=Debug
#   just pack-local skip-cache-clear=true
#   just coverage

set windows-shell := ["pwsh.exe", "-NoLogo", "-Command"]
set shell := ["bash", "-cu"]

configuration := "Release"
scripts := justfile_directory() / "scripts"

# Rebuild and repack NuGet packages into nuget-local/, publish LSP to published/
[unix]
pack-local skip-cache-clear="false":
    #!/usr/bin/env bash
    set -euo pipefail
    cd "{{ justfile_directory() }}"
    args=("{{ configuration }}")
    if [[ "{{ skip-cache-clear }}" == "true" ]]; then
      args+=(--skip-cache-clear)
    fi
    bash "{{ scripts }}/pack-local.sh" "${args[@]}"

# Rebuild and repack NuGet packages into nuget-local/, publish LSP to published/
[windows]
pack-local skip-cache-clear="false":
    #!pwsh
    $ErrorActionPreference = 'Stop'
    Set-Location '{{ justfile_directory() }}'
    $params = @{ Configuration = '{{ configuration }}' }
    if ('{{ skip-cache-clear }}' -eq 'true') { $params['SkipCacheClear'] = $true }
    & '{{ scripts }}/pack-local.ps1' @params

# Run tests with coverage and open the HTML report (requires reportgenerator)
[unix]
coverage:
    bash "{{ scripts }}/coverage.sh"

# Run tests with coverage and open the HTML report (requires reportgenerator)
[windows]
coverage:
    #!pwsh
    $ErrorActionPreference = 'Stop'
    & '{{ scripts }}/coverage.ps1'
