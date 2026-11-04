param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("install", "uninstall")]
    [string]$Action = "install"
)

$DistPath = Resolve-Path "$PSScriptRoot\..\bin\dist\Omnidas.Gui.exe" -ErrorAction SilentlyContinue

if (-not (Test-Path $DistPath) -and $Action -eq "install") {
    Write-Host "Executavel do Omnidas nao encontrado em $DistPath. Rodando dotnet publish..." -ForegroundColor Yellow
    dotnet publish "$PSScriptRoot\..\src\Omnidas.Gui" -c Release -o "$PSScriptRoot\..\bin\dist" | Out-Null
    $DistPath = Resolve-Path "$PSScriptRoot\..\bin\dist\Omnidas.Gui.exe"
}

$RegistryLocations = @(
    "HKCU:\Software\Classes\*\shell\Omnidas",
    "HKCU:\Software\Classes\Directory\shell\Omnidas"
)

if ($Action -eq "uninstall") {
    foreach ($loc in $RegistryLocations) {
        if (Test-Path $loc) {
            Remove-Item -Path $loc -Recurse -Force
            Write-Host "Removido: $loc" -ForegroundColor Cyan
        }
    }
    Write-Host "Omnidas desinstalado com sucesso do menu de contexto!" -ForegroundColor Green
    return
}

foreach ($loc in $RegistryLocations) {
    New-Item -Path $loc -Force | Out-Null
    Set-ItemProperty -Path $loc -Name "(Default)" -Value "Converter com Omnidas..."
    Set-ItemProperty -Path $loc -Name "Icon" -Value "$DistPath,0"

    $cmdLoc = "$loc\command"
    New-Item -Path $cmdLoc -Force | Out-Null
    Set-ItemProperty -Path $cmdLoc -Name "(Default)" -Value "`"$DistPath`" `"%1`""
}

Write-Host "Omnidas registrado com sucesso no Menu de Contexto do Windows!" -ForegroundColor Green
