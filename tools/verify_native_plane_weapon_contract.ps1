param(
    [Parameter(Mandatory = $true)][string]$CupheadDir,
    [string]$CecilPath = (Join-Path $env:USERPROFILE '.nuget/packages/mono.cecil/0.10.4/lib/net40/Mono.Cecil.dll')
)
$ErrorActionPreference = 'Stop'
Add-Type -Path $CecilPath
$module = [Mono.Cecil.ModuleDefinition]::ReadModule((Join-Path $CupheadDir 'Cuphead_Data/Managed/Assembly-CSharp.dll'))
function Require($condition, [string]$message) { if (-not $condition) { throw $message } }
try {
    $manager = $module.Types | Where-Object Name -eq 'PlanePlayerWeaponManager'
    foreach ($name in @('currentWeapon', 'unshrunkWeapon')) {
        $field = $manager.Fields | Where-Object Name -eq $name
        Require ($field -and $field.IsPrivate -and $field.FieldType.Name -eq 'Weapon') "Weapon storage changed: $name"
    }
    $switch = $manager.Methods | Where-Object Name -eq 'SwitchWeapon'
    Require ($switch.IsPrivate -and $switch.Parameters.Count -eq 1 -and $switch.Parameters[0].ParameterType.Name -eq 'Weapon') 'SwitchWeapon signature changed'
    $ops = @($switch.Body.Instructions)
    Require ($ops[1].Operand.Name -eq 'EndBasic') 'SwitchWeapon must finish mini/basic fire before switching'
    Require (@($ops | Where-Object { $_.Operand.Name -eq 'StartBasic' }).Count -eq 1) 'SwitchWeapon no longer restarts held basic fire'
    Require (@($ops | Where-Object { $_.Operand.Name -eq 'EndEx' }).Count -eq 0) 'SwitchWeapon now interrupts EX'
    $start = ($manager.Methods | Where-Object Name -eq 'StartBasic').Body.Instructions
    Require (@($start | Where-Object { $_.OpCode.Code -eq 'Stfld' -and $_.Operand.Name -eq 'unshrunkWeapon' }).Count -eq 1) 'Mini-plane no longer saves full-size selection'
    $end = ($manager.Methods | Where-Object Name -eq 'EndBasic').Body.Instructions
    Require (@($end | Where-Object { $_.OpCode.Code -eq 'Stfld' -and $_.Operand.Name -eq 'currentWeapon' }).Count -eq 1) 'EndBasic no longer restores full-size selection'
    Require (@($end | Where-Object { $_.OpCode.Code -eq 'Stfld' -and $_.Operand.Name -eq 'unshrunkWeapon' }).Count -eq 1) 'EndBasic no longer clears mini selection'
    $player = $module.Types | Where-Object Name -eq 'PlanePlayerController'
    $busy = ($player.Methods | Where-Object Name -eq 'get_WeaponBusy').Body.Instructions
    Require (@($busy | Where-Object { $_.Operand.Name -eq 'get_CanInterupt' }).Count -eq 1) 'WeaponBusy no longer protects in-flight weapon animations'
    $basic = ($manager.Methods | Where-Object Name -eq 'CheckBasic').Body.Instructions
    Require (@($basic | Where-Object { $_.Operand.Name -eq 'SwitchWeapon' }).Count -eq 1) 'Relic randomization no longer passes through the shared restriction patch'
    Write-Output 'PASS Native plane weapon storage, switching, mini-plane restoration, EX/super protection and relic restriction hook.'
}
finally { $module.Dispose() }
