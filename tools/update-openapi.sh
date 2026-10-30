#!/usr/bin/env bash
# Pobiera aktualną specyfikację OpenAPI API szkoleniowego do repo. Klient (FieldServiceApiClient) generuje się z niej
# przy każdym `dotnet build` – zmiana kontraktu = zmiana tego pliku, widoczna w `git diff` i w PR.
#   tools/update-openapi.sh                      # API szkoleniowe (Swagger: https://mobilny.dev/maui/architektura/swagger)
#   tools/update-openapi.sh http://localhost:5090/swagger/v1/swagger.json
set -euo pipefail
URL="${1:-https://mobilny.dev/maui/architektura/swagger/v1/swagger.json}"
OUT="$(cd "$(dirname "$0")/.." && pwd)/src/FieldService.Infrastructure/Api/openapi/fieldservice.v1.json"
curl -fsS "$URL" | python3 -c 'import json, sys; json.dump(json.load(sys.stdin), sys.stdout, ensure_ascii=False, indent=2); print()' > "$OUT.tmp"
mv "$OUT.tmp" "$OUT"
echo "Zapisano $OUT – git diff pokaże zmiany kontraktu, dotnet build wygeneruje klienta."
