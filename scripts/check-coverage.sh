#!/usr/bin/env bash
set -euo pipefail

THRESHOLD="${1:?uso: check-coverage.sh <umbral-de-lineas>}"
DOTNET="${DOTNET:-dotnet}"
REPORT_DIR="${COVERAGE_DIR:-coverage}/gate"

"$DOTNET" tool restore >/dev/null
"$DOTNET" tool run reportgenerator "-reports:coverage/**/coverage.cobertura.xml" \
  "-targetdir:$REPORT_DIR" "-reporttypes:Xml" >/dev/null

python3 - "$REPORT_DIR/summary.xml" "$THRESHOLD" <<'PY'
import sys
import xml.etree.ElementTree as ET

path, threshold = sys.argv[1], float(sys.argv[2])
root = ET.parse(path).getroot()
summary = root.find("Summary")
line_pct = float(summary.findtext("Linecoverage"))

print(f"Líneas cubiertas: {line_pct:.2f}% (umbral: {threshold:g}%)")
if line_pct < threshold:
    sys.exit(
        f"ERROR: la cobertura de líneas ({line_pct:.2f}%) está por debajo del umbral "
        f"({threshold:g}%)."
    )
PY