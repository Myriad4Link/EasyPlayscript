#!/usr/bin/env bash
# Rebuilds and repacks all EasyPlayscript NuGet packages into the local feed.
#
# Usage:
#   ./scripts/pack-local.sh [Configuration] [--skip-cache-clear]
#
# Configuration defaults to Release.
# Pass --skip-cache-clear to leave NuGet global cache entries alone.
set -euo pipefail

CONFIGURATION="Release"
SKIP_CACHE_CLEAR=0

for arg in "$@"; do
  case "$arg" in
    Debug|Release)
      CONFIGURATION="$arg"
      ;;
    --skip-cache-clear)
      SKIP_CACHE_CLEAR=1
      ;;
    -h|--help)
      echo "Usage: $0 [Debug|Release] [--skip-cache-clear]"
      exit 0
      ;;
    *)
      echo "Unknown argument: $arg" >&2
      echo "Usage: $0 [Debug|Release] [--skip-cache-clear]" >&2
      exit 1
      ;;
  esac
done

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUTPUT_DIR="${REPO_ROOT}/nuget-local"

echo "==> Cleaning old packages"
rm -f "${OUTPUT_DIR}"/*.nupkg

echo "==> Building and packing EasyPlayscript.Core"
dotnet pack "${REPO_ROOT}/EasyPlayscript.Core" -c "${CONFIGURATION}" -o "${OUTPUT_DIR}"

echo "==> Building and packing EasyPlayscript.Generator"
dotnet pack "${REPO_ROOT}/EasyPlayscript.Generator" -c "${CONFIGURATION}" -o "${OUTPUT_DIR}"

echo "==> Building and packing EasyPlayscript.BuildTask"
dotnet pack "${REPO_ROOT}/EasyPlayscript.BuildTask" -c "${CONFIGURATION}" -o "${OUTPUT_DIR}"

echo "==> Publishing EasyPlayscript.LSP"
PUBLISH_DIR="${REPO_ROOT}/published/EasyPlayscript.LSP"
rm -rf "${PUBLISH_DIR}"
dotnet publish "${REPO_ROOT}/EasyPlayscript.LSP" -c "${CONFIGURATION}" -o "${PUBLISH_DIR}"
echo "  Published to ${PUBLISH_DIR}"

if [[ "${SKIP_CACHE_CLEAR}" -eq 0 ]]; then
  echo "==> Clearing NuGet global cache for EasyPlayscript packages"
  PACKAGES_DIR="${HOME}/.nuget/packages"
  for pkg in easyplayscript.core easyplayscript.generator easyplayscript.buildtask; do
    path="${PACKAGES_DIR}/${pkg}"
    if [[ -d "${path}" ]]; then
      rm -rf "${path}"
    fi
  done
fi

echo ""
echo "Done. Packages in ${OUTPUT_DIR}:"
shopt -s nullglob
for pkg in "${OUTPUT_DIR}"/*.nupkg; do
  echo "  ${pkg}"
done
