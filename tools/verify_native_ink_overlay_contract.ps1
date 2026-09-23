param(
    [Parameter(Mandatory = $true)][string]$CupheadDir,
    [string]$CecilPath = (Join-Path $env:USERPROFILE '.nuget/packages/mono.cecil/0.10.4/lib/net40/Mono.Cecil.dll')
)
$ErrorActionPreference = 'Stop'
Add-Type -Path $CecilPath
$module = [Mono.Cecil.ModuleDefinition]::ReadModule((Join-Path $CupheadDir 'Cuphead_Data/Managed/Assembly-CSharp.dll'))
try {
    $overlay = $module.Types | Where-Object Name -eq 'PirateLevelSquidInkOverlay'
    $hit = $overlay.Methods | Where-Object Name -eq 'Hit'
    $fade = ($overlay.NestedTypes | Where-Object Name -like '*fade_cr*').Methods | Where-Object Name -eq 'MoveNext'
    if (-not $hit -or -not $fade) { throw 'Native ink overlay lifecycle was not found.' }
    foreach ($entry in @(@{ Method = $hit; Enabled = 'Ldc_I4_1' }, @{ Method = $fade; Enabled = 'Ldc_I4_0' })) {
        $instructions = @($entry.Method.Body.Instructions)
        $matched = $false
        for ($i = 2; $i -lt $instructions.Count; $i++) {
            if ($instructions[$i].Operand.Name -eq 'set_enabled' -and
                $instructions[$i - 1].OpCode.Code.ToString() -eq $entry.Enabled -and
                $instructions[$i - 2].Operand.Name -eq 'spriteRenderer') { $matched = $true }
        }
        if (-not $matched) { throw "The native ink renderer no longer signals its lifecycle in $($entry.Method.FullName)." }
    }
    'PASS Pirate ink enables its renderer immediately on Hit and disables it after the native fade; temporary rain can wait without altering native coroutines.'
}
finally { $module.Dispose() }
