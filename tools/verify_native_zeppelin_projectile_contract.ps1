#requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$CupheadDir,
    [string]$CecilPath = (Join-Path $env:USERPROFILE '.nuget/packages/mono.cecil/0.10.4/lib/net40/Mono.Cecil.dll'),
    [string]$ModPath
)

# Inspect native/optional compiled-mod IL without starting Unity or modifying
# the game. CupheadDir is the same locally detected reference used by deploy.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -Path $CecilPath
function Require($Condition, [string]$Message) { if (!$Condition) { throw $Message } }
function Calls($Method, [string]$Name) {
    @($Method.Body.Instructions | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq $Name
    })
}
$native = [Mono.Cecil.ModuleDefinition]::ReadModule(
    (Join-Path $CupheadDir 'Cuphead_Data/Managed/Assembly-CSharp.dll'))
try {
    $basic = $native.Types | Where-Object FullName -eq 'BasicProjectile'
    $enemy = $native.Types | Where-Object FullName -eq 'FlyingBlimpLevelEnemy'
    $projectile = $native.Types | Where-Object FullName -eq 'FlyingBlimpLevelEnemyProjectile'
    Require ($projectile.BaseType.FullName -eq $basic.FullName -and
        @($projectile.Methods | Where-Object Name -eq 'Create').Count -eq 0) 'Zeppelin bullets now override or bypass BasicProjectile.Create; review capture before shipping.'
    $create = @($basic.Methods | Where-Object {
        $_.Name -eq 'Create' -and $_.Parameters.Count -eq 3 -and
        $_.Parameters[0].ParameterType.FullName -eq 'UnityEngine.Vector2' -and
        $_.Parameters[1].ParameterType.FullName -eq 'System.Single' -and
        $_.Parameters[2].ParameterType.FullName -eq 'System.Single'
    })
    Require ($create.Count -eq 1 -and !$create[0].IsStatic -and $create[0].IsPublic -and
        $create[0].ReturnType.FullName -eq $basic.FullName) 'Native BasicProjectile.Create(Vector2,float,float) signature changed.'
    $cloneCalls = @(Calls $create[0] 'Create')
    Require ($cloneCalls.Count -eq 1 -and
        $cloneCalls[0].Operand.DeclaringType.FullName -eq 'AbstractProjectile' -and
        $cloneCalls[0].Operand.Parameters.Count -eq 2) 'Native basic projectile creation no longer returns the final initialized clone.'
    Require (@($create[0].Body.Instructions | Where-Object {
        $_.OpCode.Code -eq 'Stfld' -and $_.Operand.Name -eq 'Speed'
    }).Count -eq 1) 'Native Create no longer assigns its requested projectile speed.'
    foreach ($fieldName in @('projectilePrefab', 'parryablePrefab')) {
        $field = $enemy.Fields | Where-Object Name -eq $fieldName
        Require ($null -ne $field -and !$field.IsStatic -and
            $field.FieldType.FullName -eq $projectile.FullName) "Native zeppelin prefab field changed: $fieldName."
    }
    foreach ($entry in @(
        @{ Name = 'FireSingle'; Sites = 5; Ordinary = 3; Parryable = 2 },
        @{ Name = 'FireSpreadshot'; Sites = 3; Ordinary = 3; Parryable = 0 }
    )) {
        $fire = @($enemy.Methods | Where-Object Name -eq $entry.Name)
        Require ($fire.Count -eq 1 -and !$fire[0].IsStatic -and
            $fire[0].Parameters.Count -eq 0 -and $fire[0].ReturnType.FullName -eq 'System.Void') "Native synchronous fire entry point changed: $($entry.Name)."
        $creates = @(Calls $fire[0] 'Create')
        Require ($creates.Count -eq $entry.Sites) "Native projectile creation branches changed in $($entry.Name)."
        foreach ($call in $creates) {
            Require ($call.Operand.FullName -eq $create[0].FullName) "A zeppelin fire branch bypasses the exact captured Create overload: $($entry.Name)."
        }
        foreach ($fieldEntry in @(
            @{ Name = 'projectilePrefab'; Count = $entry.Ordinary },
            @{ Name = 'parryablePrefab'; Count = $entry.Parryable }
        )) {
            Require (@($fire[0].Body.Instructions | Where-Object {
                $_.OpCode.Code -eq 'Ldfld' -and $_.Operand.Name -eq $fieldEntry.Name
            }).Count -eq $fieldEntry.Count) "Native projectile source identity changed in $($entry.Name)."
        }
    }
    Write-Output 'PASS Native zeppelin single/parry/spread branches all return their final projectile through the captured BasicProjectile.Create overload (8 call sites).'
}
finally { $native.Dispose() }

if ($ModPath) {
    $mod = [Mono.Cecil.ModuleDefinition]::ReadModule($ModPath)
    try {
        $patch = $mod.Types | Where-Object FullName -eq 'Gilomx.CupheadBossRoulette.CreatorToolsZeppelinProjectilePresentation'
        Require ($null -ne $patch) 'Compiled zeppelin projectile capture patch is missing.'
        $context = $patch.Fields | Where-Object Name -eq 'currentFire'
        Require ($context.IsStatic -and @($context.CustomAttributes | Where-Object {
            $_.AttributeType.FullName -eq 'System.ThreadStaticAttribute'
        }).Count -eq 1) 'Compiled zeppelin fire context is not thread-local.'
        foreach ($method in $patch.Methods) {
            if (!$method.HasBody) { continue }
            Require (@($method.Body.Instructions | Where-Object {
                $_.Operand -is [Mono.Cecil.MethodReference] -and
                $_.Operand.Name -in @('FindObjectsOfTypeAll', 'FindObjectsOfType', 'FindObjectOfType')
            }).Count -eq 0) 'Compiled zeppelin fire patch still enumerates loaded/scene objects.'
        }
        foreach ($exitName in @('EndFirePostfix', 'EndFireFinalizer')) {
            $exit = $patch.Methods | Where-Object Name -eq $exitName
            Require ($null -ne $exit -and @(Calls $exit 'EndFire').Count -eq 1) "Compiled zeppelin scope cleanup is missing: $exitName."
        }
        $prepare = $patch.Methods | Where-Object Name -eq 'PrepareCreatedProjectilePostfix'
        foreach ($name in @('MatchGameplayBodyScale', 'BringActorToFront')) {
            Require (@(Calls $prepare $name).Count -eq 1) "Compiled zeppelin result presentation hook changed: $name."
        }
        $executor = $mod.Types | Where-Object FullName -eq 'Gilomx.CupheadBossRoulette.ZeppelinInteractionExecutor'
        $available = $executor.Methods | Where-Object Name -eq 'get_Available'
        $cacheChecks = @(Calls $available 'Ready')
        $find = @(Calls $available 'FindBlimpLady')
        Require ($cacheChecks.Count -eq 2 -and $find.Count -eq 1 -and
            $cacheChecks[1].Offset -lt $find[0].Offset) 'Compiled zeppelin availability no longer checks cached prefabs before global Hilda discovery.'
        $lanes = $executor.Methods | Where-Object Name -eq 'ActiveLanes'
        Require (@(Calls $lanes 'FindObjectsOfType').Count -eq 0 -and
            @($lanes.Body.Instructions | Where-Object {
                $_.OpCode.Code -eq 'Ldsfld' -and $_.Operand.Name -eq 'Instances' -and
                $_.Operand.DeclaringType.Name -eq 'CreatorToolsDonorLabel'
            }).Count -eq 1) 'Compiled zeppelin lane collection no longer uses its existing donor-label registry.'
        $cacheType = $mod.Types | Where-Object FullName -eq 'Gilomx.CupheadBossRoulette.NativeZeppelinCache'
        $cacheCamera = $cacheType.Methods | Where-Object Name -eq 'FindGameplayCamera'
        foreach ($cameraUser in @($lanes, $cacheCamera)) {
            Require (@($cameraUser.Body.Instructions | Where-Object {
                $_.Operand -is [Mono.Cecil.MethodReference] -and
                $_.Operand.DeclaringType.Name -eq 'CreatorToolsGameplayCameraCache' -and
                $_.Operand.Name -eq 'Get'
            }).Count -eq 1) 'Compiled zeppelin spawning bypasses the shared gameplay-camera cache.'
        }
        Write-Output 'PASS Compiled zeppelin capture avoids fire scans, restores scopes, checks cached availability, reuses label registry and shares camera discovery.'
    }
    finally { $mod.Dispose() }
}
