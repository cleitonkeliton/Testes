param(
    [ValidateRange(1024, 65535)][int]$Porta = 5080,
    [switch]$Rede
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Instale o SDK .NET 10 e abra novamente o PowerShell. Consulte docs/GUIA-INSTALACAO.md.'
}
$endereco = if ($Rede) { '0.0.0.0' } else { 'localhost' }
Write-Host "Iniciando CSTIC POP na porta $Porta. Use Ctrl+C para parar."
& dotnet run --project src/Cstic.KnowledgeBase -c Release --no-launch-profile --urls "http://${endereco}:$Porta"
exit $LASTEXITCODE
