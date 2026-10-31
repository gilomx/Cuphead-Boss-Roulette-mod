#requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ModPath,
    [string]$CecilPath = (Join-Path $env:USERPROFILE '.nuget/packages/mono.cecil/0.10.4/lib/net40/Mono.Cecil.dll')
)

# Compiled UI wiring check complements the Unity-independent layout policy
# tests. It reads the mod image only, without opening or changing the game.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -Path $CecilPath
function Require($Condition, [string]$Message) { if (!$Condition) { throw $Message } }
function Calls($Method, [string]$Name) {
    @($Method.Body.Instructions | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq $Name
    })
}
$module = [Mono.Cecil.ModuleDefinition]::ReadModule($ModPath)
try {
    $plugin = $module.Types | Where-Object FullName -eq 'Gilomx.CupheadBossRoulette.Plugin'
    $cache = $module.Types | Where-Object FullName -eq 'Gilomx.CupheadBossRoulette.NativeChallengePromptLayoutCache'
    Require ($null -ne $plugin -and $null -ne $cache) 'Native manual prompt layout cache is missing.'
    $apply = $plugin.Methods | Where-Object Name -eq 'ApplyNativeChallengePromptText'
    $needs = @(Calls $apply 'NeedsApply')
    $setText = @(Calls $apply 'set_text')
    $flush = @(Calls $apply 'ForceUpdateCanvases')
    $commit = @(Calls $apply 'MarkApplied')
    Require ($needs.Count -eq 1 -and $setText.Count -eq 1 -and $flush.Count -eq 1 -and $commit.Count -eq 1 -and
        $needs[0].Offset -lt $setText[0].Offset -and $setText[0].Offset -lt $flush[0].Offset -and
        $flush[0].Offset -lt $commit[0].Offset) 'Prompt UI writes are not guarded and committed after successful layout.'
    Require ($needs[0].Next.OpCode.Code -in @('Brtrue', 'Brtrue_S') -and
        $needs[0].Next.Next.OpCode.Code -eq 'Ret') 'Stable manual-prompt layout no longer returns before UI writes/canvas flush.'
    foreach ($metric in @('get_scaleFactor', 'get_width', 'get_height', 'get_font', 'get_fontSize', 'get_fontStyle',
        'get_resizeTextForBestFit', 'get_resizeTextMinSize', 'get_resizeTextMaxSize')) {
        Require (@(Calls $apply $metric).Count -ge 1) "Prompt layout invalidation metric is no longer supplied: $metric."
    }
    $update = $plugin.Methods | Where-Object Name -eq 'UpdateNativeChallengePrompt'
    Require (@(Calls $update 'get_CurrentLanguage').Count -eq 1 -and
        @(Calls $update 'LocalizedChallengeLabel').Count -eq 1 -and
        @($update.Body.Instructions | Where-Object {
            $_.OpCode.Code -eq 'Stfld' -and $_.Operand.Name -eq 'nativeChallengePromptLabelLanguage'
        }).Count -eq 1) 'Manual prompt localization is no longer cached by the current language.'
    $equipped = $plugin.Methods | Where-Object Name -eq 'UpdateEquippedChallengePrompt'
    $gate = @(Calls $equipped 'CanPrepareTemplate')
    $prepare = @(Calls $equipped 'PrepareNativeChallengePrompt')
    Require ($gate.Count -eq 1 -and $prepare.Count -eq 1 -and $gate[0].Offset -lt $prepare[0].Offset) 'Manual prompt template discovery lost its map/active-manual gate.'
    foreach ($owner in @('PrepareNativeChallengePrompt', 'DestroyNativeChallengePrompt')) {
        $method = $plugin.Methods | Where-Object Name -eq $owner
        Require (@(Calls $method 'Reset').Count -eq 1) "Manual prompt layout is not invalidated on UI lifecycle change: $owner."
    }
    $reset = $cache.Methods | Where-Object Name -eq 'Reset'
    Require (@($reset.Body.Instructions | Where-Object {
        $_.OpCode.Code -eq 'Initobj' -and $_.Operand.Name -eq 'NativeChallengePromptLayoutInput'
    }).Count -eq 1) 'Prompt layout reset retains references to the previous canvas/font/text.'
    $query = $cache.Methods | Where-Object Name -eq 'NeedsApply'
    Require (@($query.Body.Instructions | Where-Object {
        $_.OpCode.Code -in @('Newobj', 'Newarr', 'Box')
    }).Count -eq 0) 'Stable prompt layout queries now allocate.'
    Write-Output 'PASS Compiled manual prompt wiring: stable-layout early return, localization and display-metric invalidation, map/manual discovery gate, successful-layout commit and reference-clearing teardown.'
}
finally { $module.Dispose() }
