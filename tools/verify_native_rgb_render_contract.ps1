param(
    [Parameter(Mandatory = $true)][string]$CupheadDir,
    [string]$CecilPath = (Join-Path $env:USERPROFILE '.nuget/packages/mono.cecil/0.10.4/lib/net40/Mono.Cecil.dll')
)
$ErrorActionPreference = 'Stop'
Add-Type -Path $CecilPath
$module = [Mono.Cecil.ModuleDefinition]::ReadModule((Join-Path $CupheadDir 'Cuphead_Data/Managed/Assembly-CSharp.dll'))
try {
    foreach ($entry in @(
        @{ Name = 'ChromaticAberrationFilmGrain'; Fields = @('r', 'g', 'b') },
        @{ Name = 'BlurGamma'; Fields = @('blurSize') }
    )) {
        $type = $module.Types | Where-Object Name -eq $entry.Name
        $render = $type.Methods | Where-Object Name -eq 'OnRenderImage'
        if (-not $render -or $render.Parameters.Count -ne 2) { throw "Native render signature changed: $($entry.Name)" }
        foreach ($name in $entry.Fields) {
            $reads = @($render.Body.Instructions | Where-Object { $_.OpCode.Code -in @('Ldfld','Ldflda') -and $_.Operand.Name -eq $name })
            $writes = @($render.Body.Instructions | Where-Object { $_.OpCode.Code -eq 'Stfld' -and $_.Operand.Name -eq $name })
            if ($reads.Count -eq 0 -or $writes.Count -ne 0) { throw "Native renderer no longer treats $name as an input" }
        }
    }
    $renderer = $module.Types | Where-Object Name -eq 'CupheadRenderer'
    $fuzzy = $renderer.Methods | Where-Object Name -eq 'TouchFuzzy'
    if (@($fuzzy.Body.Instructions | Where-Object { $_.Operand.Name -eq 'PsychedelicEffect' }).Count -ne 1) { throw 'Native pollen color entry point changed' }
    if (@($fuzzy.Body.Instructions | Where-Object { $_.Operand.Name -eq 'change_blur_cr' }).Count -ne 1) { throw 'Native pollen blur entry point changed' }
    'PASS RGB and blur render methods read temporary inputs without changing their native state; pollen bridge verified.'
}
finally { $module.Dispose() }
