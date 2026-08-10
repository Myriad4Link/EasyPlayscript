#!/usr/bin/env bash
# Runs code coverage for both test projects and opens the HTML reports.
#
# Usage:
#   ./scripts/coverage.sh
#
# Requires `reportgenerator` on PATH.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REPORT_DIR="${REPO_ROOT}/coveragereport"
RESULTS_ROOT="${REPO_ROOT}/TestResults"
TEST_PROJECTS=(
  "EasyPlayscript.Tests"
  "EasyPlayscript.LSP.Tests"
)

cd "${REPO_ROOT}"

# Clean stale coverage data so old XMLs and HTMLs don't pollute the report.
rm -rf "${RESULTS_ROOT}" "${REPORT_DIR}"

for project in "${TEST_PROJECTS[@]}"; do
  echo ""
  echo "=== Running coverage for ${project} ==="
  dotnet test "${project}" --collect:"XPlat Code Coverage" --results-directory "TestResults/${project}"
done

xml_paths=()
for project in "${TEST_PROJECTS[@]}"; do
  results_dir="${RESULTS_ROOT}/${project}"
  xml_file="$(find "${results_dir}" -name 'coverage.cobertura.xml' 2>/dev/null | head -n 1 || true)"
  if [[ -n "${xml_file}" ]]; then
    xml_paths+=("${xml_file}")
  else
    echo "Warning: No coverage XML found for ${project}" >&2
  fi
done

if [[ "${#xml_paths[@]}" -eq 0 ]]; then
  echo "No coverage data found. Ensure tests ran successfully." >&2
  exit 1
fi

# reportgenerator accepts ';' as the report path separator on all platforms.
reports_joined="$(IFS='; echo "${xml_paths[*]}")"
reports_arg="-reports:${reports_joined}"

echo ""
echo "=== Generating HTML report ==="
reportgenerator "${reports_arg}" "-targetdir:${REPORT_DIR}" "-reporttypes:Html"

html_path="${REPORT_DIR}/index.html"
if [[ -f "${html_path}" ]]; then
  echo "Opening ${html_path}"
  case "$(uname -s)" in
    Darwin)
      open "${html_path}"
      ;;
    *)
      if command -v xdg-open >/dev/null 2>&1; then
        xdg-open "${html_path}" >/dev/null 2>&1 || true
      else
        echo "Report written to ${html_path} (no opener found)"
      fi
      ;;
  esac
fi
