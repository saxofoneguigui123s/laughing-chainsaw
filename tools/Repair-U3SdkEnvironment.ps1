<#
.SYNOPSIS
  Checks and repairs the local Unity environment used by U3-SDK v3.26.3.12.

.DESCRIPTION
  The HLSLSupport.cginc errors that affect every stock U3 shader are caused by
  Unity being launched with the wrong/corrupt editor installation or by a
  stale project cache. This script validates the official editor version,
  verifies its built-in CG include directory, optionally removes only Unity's
  generated project caches, and opens the project with the validated editor.

  It never changes Assets, Packages, ProjectSettings, or content definitions.
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ProjectPath,

    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe',

    [switch]$CleanProjectCache,

    [switch]$OpenProject
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ExpectedEditorVersion = '2022.3.62f3'
$ExpectedPostProcessingVersion = '3.4.0'

function Fail([string]$Message) {
    Write-Error $Message
    exit 1
}

function Resolve-UnityExe([string]$Path) {
    $expanded = [Environment]::ExpandEnvironmentVariables($Path)
    if (Test-Path -LiteralPath $expanded -PathType Container) {
        $expanded = Join-Path $expanded 'Unity.exe'
    }

    if (-not (Test-Path -LiteralPath $expanded -PathType Leaf)) {
        Fail "Unity.exe não encontrado: $expanded"
    }

    return (Resolve-Path -LiteralPath $expanded).Path
}

$ProjectPath = [Environment]::ExpandEnvironmentVariables($ProjectPath)
if (-not (Test-Path -LiteralPath $ProjectPath -PathType Container)) {
    Fail "Projeto U3-SDK não encontrado: $ProjectPath"
}
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path

$projectVersionPath = Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt'
$manifestPath = Join-Path $ProjectPath 'Packages\manifest.json'
$lockPath = Join-Path $ProjectPath 'Packages\packages-lock.json'
foreach ($requiredPath in @($projectVersionPath, $manifestPath, $lockPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        Fail "Arquivo obrigatório do U3-SDK ausente: $requiredPath"
    }
}

$projectVersionText = Get-Content -LiteralPath $projectVersionPath -Raw
$match = [regex]::Match($projectVersionText, '(?m)^m_EditorVersion:\s*(?<version>\S+)\s*$')
if (-not $match.Success) {
    Fail "Não foi possível ler m_EditorVersion em $projectVersionPath"
}
if ($match.Groups['version'].Value -ne $ExpectedEditorVersion) {
    Fail "Este projeto exige Unity $ExpectedEditorVersion, mas ProjectVersion.txt informa $($match.Groups['version'].Value). Não faça downgrade/upgrade do projeto; instale e abra com a versão exata."
}

try {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
} catch {
    Fail "manifest.json inválido: $($_.Exception.Message)"
}
$packageVersion = $manifest.dependencies.'com.unity.postprocessing'
if ($packageVersion -ne $ExpectedPostProcessingVersion) {
    Fail "com.unity.postprocessing deve estar em $ExpectedPostProcessingVersion, mas está em '$packageVersion'. Restaure Packages/manifest.json e Packages/packages-lock.json da tag v3.26.3.12."
}

$UnityExe = Resolve-UnityExe $UnityPath
$EditorDataPath = Join-Path (Split-Path -Parent $UnityExe) 'Data'
$includeCandidates = @(
    (Join-Path $EditorDataPath 'Resources\CGIncludes\HLSLSupport.cginc'),
    (Join-Path $EditorDataPath 'CGIncludes\HLSLSupport.cginc')
)
$includePath = $includeCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if (-not $includePath) {
    $locations = $includeCandidates -join "`n  - "
    Fail @"
HLSLSupport.cginc não existe na instalação do Unity:
  - $locations

Isso não é um arquivo do projeto Nightfall nem deve ser copiado para Assets.
No Unity Hub, remova e reinstale exatamente o Editor $ExpectedEditorVersion. Depois execute este script novamente.
"@
}

Write-Host "Projeto: $ProjectPath" -ForegroundColor Cyan
Write-Host "Unity:   $UnityExe" -ForegroundColor Cyan
Write-Host "CGInclude validado: $includePath" -ForegroundColor Green
Write-Host "Pacote Post Processing validado: $packageVersion" -ForegroundColor Green

if ($CleanProjectCache) {
    # Library/Temp/obj are generated files. Removing them forces Unity to
    # reconstruct the shader and package caches without risking source assets.
    $cachePaths = @('Library', 'Temp', 'obj') | ForEach-Object { Join-Path $ProjectPath $_ }
    foreach ($cachePath in $cachePaths) {
        if (Test-Path -LiteralPath $cachePath) {
            if ($PSCmdlet.ShouldProcess($cachePath, 'Remover cache gerado pelo Unity')) {
                Remove-Item -LiteralPath $cachePath -Recurse -Force
                Write-Host "Cache removido: $cachePath" -ForegroundColor Yellow
            }
        }
    }
    Write-Host 'Abra o projeto uma vez e aguarde a reimportação terminar antes de iniciar um build.' -ForegroundColor Yellow
}

if ($OpenProject) {
    if ($PSCmdlet.ShouldProcess($ProjectPath, 'Abrir com o Unity validado')) {
        Start-Process -FilePath $UnityExe -ArgumentList @('-projectPath', "`"$ProjectPath`"")
    }
} else {
    Write-Host 'Verificação concluída. Para limpar o cache e abrir o projeto:' -ForegroundColor Green
    Write-Host ".\Repair-U3SdkEnvironment.ps1 -ProjectPath `"$ProjectPath`" -CleanProjectCache -OpenProject" -ForegroundColor Green
}
