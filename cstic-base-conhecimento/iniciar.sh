#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
porta="${PORTA:-5080}"
endereco="${ENDERECO:-localhost}"
dotnet_exec="${DOTNET_BINARY:-dotnet}"
if ! command -v "$dotnet_exec" >/dev/null 2>&1; then
  if [ -x /workspace/.dotnet/dotnet ]; then
    dotnet_exec=/workspace/.dotnet/dotnet
    export DOTNET_CLI_HOME=/workspace/.dotnet-home
  else
    echo 'Instale o SDK .NET 10. Consulte docs/GUIA-INSTALACAO.md.' >&2
    exit 1
  fi
fi
exec "$dotnet_exec" run --project src/Cstic.KnowledgeBase -c Release --no-launch-profile --urls "http://$endereco:$porta"
