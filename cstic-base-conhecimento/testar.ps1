$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
& dotnet run --project tests/Cstic.KnowledgeBase.Tests -c Release
exit $LASTEXITCODE
