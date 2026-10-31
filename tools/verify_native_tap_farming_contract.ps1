#requires -Version 7.2
[CmdletBinding()]
param(
    [string]$CupheadDir,
    [string]$CecilPath
)

# Read-only native IL contract: no game process, installation writes or
# packaged launcher data are involved. Deployment may pass its resolved game
# references; standalone runs also accept local configuration/Steam detection.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

function Resolve-ReferencePath([string]$Path) {
    return [IO.Path]::GetFullPath(
        [Environment]::ExpandEnvironmentVariables($Path), $repoRoot)
}

function Find-CupheadReferences {
    if ($env:CUPHEAD_DIR) { return Resolve-ReferencePath $env:CUPHEAD_DIR }
    $configPath = Join-Path $repoRoot 'launcher-dev.local.json'
    if (Test-Path -LiteralPath $configPath) {
        $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json -AsHashtable
        if ($config['cupheadDir']) { return Resolve-ReferencePath $config['cupheadDir'] }
    }
    $steamRoots = @(
        foreach ($key in @('HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam')) {
            if (!(Test-Path $key)) { continue }
            # Sandboxed shells may see a key while lacking read access.
            # Default Steam roots/VDFs below remain a read-only fallback.
            try { $values = Get-ItemProperty $key }
            catch { continue }
            foreach ($name in @('SteamPath', 'InstallPath')) {
                if ($values.PSObject.Properties[$name] -and $values.$name) { $values.$name }
            }
        }
        foreach ($programFilesRoot in @(${env:ProgramFiles(x86)}, $env:ProgramFiles)) {
            if ($programFilesRoot) {
                $steam = Join-Path $programFilesRoot 'Steam'
                if (Test-Path -LiteralPath $steam) { $steam }
            }
        }
    ) | Sort-Object -Unique
    $libraries = @($steamRoots)
    foreach ($steam in $steamRoots) {
        $vdf = Join-Path $steam 'steamapps/libraryfolders.vdf'
        if (!(Test-Path -LiteralPath $vdf)) { continue }
        foreach ($match in [regex]::Matches([IO.File]::ReadAllText($vdf), '"path"\s+"([^"]+)"')) {
            $libraries += $match.Groups[1].Value.Replace('\\', '\')
        }
    }
    $candidates = @(
        foreach ($library in ($libraries | Sort-Object -Unique)) {
            $manifest = Join-Path $library 'steamapps/appmanifest_268910.acf'
            if (!(Test-Path -LiteralPath $manifest)) { continue }
            $match = [regex]::Match([IO.File]::ReadAllText($manifest), '"installdir"\s+"([^"]+)"')
            if (!$match.Success) { continue }
            $candidate = Join-Path (Join-Path $library 'steamapps/common') $match.Groups[1].Value
            if (Test-Path -LiteralPath (Join-Path $candidate 'Cuphead_Data/Managed/Assembly-CSharp.dll')) {
                [IO.Path]::GetFullPath($candidate)
            }
        }
    ) | Sort-Object -Unique
    if (@($candidates).Count -ne 1) {
        throw 'Cannot identify unique Cuphead references. Pass -CupheadDir or set cupheadDir in launcher-dev.local.json.'
    }
    return $candidates
}

if (!$CupheadDir) { $CupheadDir = Find-CupheadReferences }
else { $CupheadDir = Resolve-ReferencePath $CupheadDir }
$nativePath = Join-Path $CupheadDir 'Cuphead_Data/Managed/Assembly-CSharp.dll'
if (!(Test-Path -LiteralPath $nativePath)) { throw 'Cuphead native reference assembly is missing.' }
if (!$CecilPath) {
    $packageRoot = $env:NUGET_PACKAGES
    if (!$packageRoot) { $packageRoot = Join-Path $env:USERPROFILE '.nuget/packages' }
    $cecilRoot = Join-Path $packageRoot 'mono.cecil'
    $CecilPath = @(
        if (Test-Path -LiteralPath $cecilRoot) {
            foreach ($version in (Get-ChildItem -LiteralPath $cecilRoot -Directory | Sort-Object Name -Descending)) {
                $candidate = Join-Path $version.FullName 'lib/net40/Mono.Cecil.dll'
                if (Test-Path -LiteralPath $candidate) { $candidate }
            }
        }
    ) | Select-Object -First 1
}
if (!$CecilPath -or !(Test-Path -LiteralPath $CecilPath)) {
    throw 'Mono.Cecil reference is missing. Pass -CecilPath from the clean loader or a restored Mono.Cecil package.'
}
Add-Type -Path $CecilPath

function Require($Condition, [string]$Message) {
    if (!$Condition) { throw $Message }
}
function All-Types($Types) {
    foreach ($type in $Types) {
        $type
        All-Types $type.NestedTypes
    }
}
function Calls($Method, [string]$Name) {
    @($Method.Body.Instructions | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq $Name
    })
}
function Assert-AuditedCalls($Actual, $Expected, [string]$Label) {
    $actualKeys = @($Actual | Group-Object { $_.caller + '|' + $_.family } |
        ForEach-Object { $_.Name + '|sites=' + $_.Count } | Sort-Object)
    $expectedKeys = @($Expected | ForEach-Object {
        $sites = if ($_.PSObject.Properties['sites']) { $_.sites } else { 1 }
        $_.caller + '|' + $_.family + '|sites=' + $sites
    } | Sort-Object)
    Require ($actualKeys.Count -gt 0 -and $expectedKeys.Count -gt 0) "$Label inventory is empty."
    $differences = @(Compare-Object -ReferenceObject $expectedKeys -DifferenceObject $actualKeys)
    Require ($differences.Count -eq 0) "$Label routes changed; review the native call sites before updating the audit baseline: $($differences | Out-String)"
}

$module = [Mono.Cecil.ModuleDefinition]::ReadModule($nativePath)
try {
    $types = @(All-Types $module.Types)
    $properties = $types | Where-Object FullName -eq 'AbstractLevelProperties`3'
    Require ($null -ne $properties) 'Native generic level properties type changed.'
    $deal = @($properties.Methods | Where-Object Name -eq 'DealDamage')
    Require ($deal.Count -eq 1 -and $deal[0].IsPublic -and !$deal[0].IsStatic -and
        $deal[0].Parameters.Count -eq 1 -and
        $deal[0].Parameters[0].ParameterType.FullName -eq 'System.Single' -and
        $deal[0].ReturnType.FullName -eq 'System.Void') 'Native DealDamage(float) signature changed.'

    $advanceMethods = @($properties.Methods | Where-Object Name -like 'DealDamageToNext*')
    Require ($advanceMethods.Count -eq 1 -and
        $advanceMethods[0].Name -eq 'DealDamageToNextNamedState') 'Native scripted advancement entry points changed; audit all helpers before excluding them.'
    $advance = $advanceMethods[0]
    Require ($advance.IsPublic -and !$advance.IsStatic -and !$advance.HasGenericParameters -and
        $advance.Parameters.Count -eq 0 -and $advance.ReturnType.FullName -eq 'System.Void') 'Scripted named-state advance signature changed.'
    $synthetic = @(Calls $advance 'DealDamage')
    Require ($synthetic.Count -eq 1 -and $synthetic[0].Previous.OpCode.Code -eq 'Ldc_R4' -and
        [float]$synthetic[0].Previous.Operand -eq [float]1 -and
        $synthetic[0].Operand.DeclaringType.Name -eq 'AbstractLevelProperties`3') 'Named-state advance no longer uses synthetic DealDamage(1f).'
    $boundedLoop = @($advance.Body.Instructions | Where-Object {
        $_.OpCode.Code -in @('Blt', 'Blt_S') -and
        $_.Previous.OpCode.Code -eq 'Ldfld' -and $_.Previous.Operand.Name -eq 'TotalHealth' -and
        $_.Operand.Offset -lt $synthetic[0].Offset
    })
    Require ($boundedLoop.Count -eq 1) 'Native synthetic advancement loop is no longer bounded by TotalHealth.'
    Require (@($advance.Body.Instructions | Where-Object {
        $_.OpCode.Code -eq 'Ldstr' -and $_.Operand -eq 'Generic'
    }).Count -eq 1) 'Named-state advance no longer skips Generic state names.'

    $robotProperties = $types | Where-Object FullName -eq 'LevelProperties/Robot'
    Require ($robotProperties.BaseType -is [Mono.Cecil.GenericInstanceType] -and
        $robotProperties.BaseType.ElementType.FullName -eq $properties.FullName) 'Robot properties no longer expose the closed generic health methods.'
    $chest = ($types | Where-Object FullName -eq 'RobotLevelRobotChest').Methods | Where-Object Name -eq 'OnDamageTaken'
    $chestAdvance = @(Calls $chest 'DealDamageToNextNamedState')
    $destroyedStore = @($chest.Body.Instructions | Where-Object {
        $_.OpCode.Code -eq 'Stfld' -and $_.Operand.Name -eq 'destroyed' -and
        $_.Previous.OpCode.Code -eq 'Ldc_I4_1'
    })
    Require ($chestAdvance.Count -eq 1 -and $destroyedStore.Count -eq 1 -and
        $chestAdvance[0].Offset -lt $destroyedStore[0].Offset) 'Robot heart no longer advances once before locking its destroyed state.'
    Require (@(Calls $chest 'DealDamage' | Where-Object {
        $_.Operand.DeclaringType.Name -eq 'AbstractLevelProperties`3'
    }).Count -eq 0) 'Robot heart now applies ordinary global boss damage; audit local component HP separately.'
    $body = ($types | Where-Object FullName -eq 'RobotLevelRobotBodyPart').Methods | Where-Object Name -eq 'OnDamageTaken'
    Require (@(Calls $body 'DealDamage').Count -eq 1 -and
        (Calls $body 'DealDamage')[0].Operand.DeclaringType.FullName -eq 'Level/Timeline') 'Robot primary components no longer damage only their local health/timeline.'

    $helihead = ($types | Where-Object FullName -eq 'RobotLevelHelihead').Methods | Where-Object Name -eq 'OnDamageTaken'
    $realHits = @(Calls $helihead 'DealDamage' | Where-Object {
        $_.Operand.DeclaringType.Name -eq 'AbstractLevelProperties`3'
    })
    Require ($realHits.Count -eq 1 -and $realHits[0].Previous.Operand.Name -eq 'get_damage' -and
        @(Calls $helihead 'DealDamageToNextNamedState').Count -eq 0) 'Robot helihead no longer forwards real player hit damage directly to boss properties.'
    $stateChanged = ($types | Where-Object FullName -eq 'RobotLevel').Methods | Where-Object Name -eq 'OnStateChanged'
    Require (@(Calls $stateChanged 'TriggerPhaseTwo').Count -eq 1) 'Robot phase-two transition entry point changed.'

    # These synchronous death callbacks demonstrate that the helper is a
    # generic control-flow operation, not damage unique to Dr. Kahl's heart.
    foreach ($entry in @(
        @{ Type = 'SlimeLevelSlime'; Method = 'Die' },
        @{ Type = 'DicePalaceBoozeLevelBossBase'; Method = 'AllDead' }
    )) {
        $caller = ($types | Where-Object FullName -eq $entry.Type).Methods | Where-Object Name -eq $entry.Method
        Require (@(Calls $caller 'DealDamageToNextNamedState').Count -eq 1) "Scripted advancement caller changed: $($entry.Type).$($entry.Method)."
    }

    # Check all families accepted by the production installer, including DLC,
    # against every boss exposed by this checkout's roulette catalog.
    $familyTypes = @{}
    $levelProperties = $types | Where-Object FullName -eq 'LevelProperties'
    foreach ($family in $levelProperties.NestedTypes) {
        Require ($family.BaseType -is [Mono.Cecil.GenericInstanceType] -and
            $family.BaseType.ElementType.FullName -eq $properties.FullName) "Audit unsupported property inheritance before enabling tap absorption: $($family.FullName)."
        Require (@($family.Methods | Where-Object Name -in @(
            'DealDamage', 'DealDamageToNextNamedState')).Count -eq 0) "Boss family overrides guarded health methods: $($family.FullName)."
        $familyTypes[$family.Name] = $family
    }
    $bossSource = [IO.File]::ReadAllText((Join-Path $repoRoot 'RouletteData.cs'))
    $bosses = @([regex]::Matches($bossSource,
        '(?m)^\s*new BossEntry\([^\r\n]*?Levels\.([A-Za-z0-9_]+),') |
        ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
    Require ($bosses.Count -gt 0) 'Cannot inspect the roulette boss catalog.'
    foreach ($boss in $bosses) {
        Require ($familyTypes.ContainsKey($boss)) "Roulette boss has no guarded native health family: $boss."
    }

    $namedCalls = [Collections.Generic.List[object]]::new()
    $otherDamageCalls = [Collections.Generic.List[object]]::new()
    $ordinaryDamageCalls = 0
    foreach ($type in $types) {
        foreach ($method in $type.Methods) {
            if (!$method.HasBody) { continue }
            foreach ($instruction in $method.Body.Instructions) {
                $target = $instruction.Operand
                if ($target -isnot [Mono.Cecil.MethodReference] -or
                    $target.DeclaringType.Name -ne $properties.Name -or
                    $target.Name -notin @('DealDamage', 'DealDamageToNextNamedState')) { continue }
                if ($method -eq $advance) { continue } # The synthetic loop checked above.
                Require ($target.DeclaringType -is [Mono.Cecil.GenericInstanceType]) "Unresolved health call needs review: $($method.FullName)."
                $familyName = $target.DeclaringType.GenericArguments[0].DeclaringType.Name
                Require ($familyTypes.ContainsKey($familyName)) "Health caller is outside the guarded families: $($method.FullName)."
                $site = [pscustomobject]@{ caller = $method.FullName; family = $familyName }
                if ($target.Name -eq 'DealDamageToNextNamedState') {
                    $namedCalls.Add($site)
                    continue
                }
                # Only direct, unmodified DamageInfo.damage reads are
                # classified mechanically. Constants, local variables,
                # wrappers, parries and animation/coroutine credits must have
                # a reviewed baseline entry, never an inferred exemption.
                $producer = $instruction.Previous.Operand
                if ($producer -is [Mono.Cecil.MethodReference] -and
                    $producer.DeclaringType.FullName -eq 'DamageDealer/DamageInfo' -and
                    $producer.Name -eq 'get_damage') {
                    $ordinaryDamageCalls++
                }
                else { $otherDamageCalls.Add($site) }
            }
        }
    }
    $namedAudit = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'tap-farming-native-audit.json') -Raw | ConvertFrom-Json
    $damageAudit = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'tap-farming-native-damage-audit.json') -Raw | ConvertFrom-Json
    Require ($namedAudit.schemaVersion -eq 1 -and $damageAudit.schemaVersion -eq 1) 'Unknown Tap Farming audit baseline schema.'
    Assert-AuditedCalls $namedCalls $namedAudit.namedStateAdvances 'Scripted named-state advancement'
    Assert-AuditedCalls $otherDamageCalls $damageAudit.entries 'Nonstandard boss damage'
    Write-Output "PASS Tap Farming audit: $($bosses.Count) roulette bosses, $($familyTypes.Count) native health families, $($namedCalls.Count) guarded phase-advance sites, $ordinaryDamageCalls direct player-damage sites and $($otherDamageCalls.Count) reviewed nonstandard sites."
    Write-Output 'PASS Native Tap Farming contract: bounded synthetic named-state damage, Robot heart destruction/phase transition, local body HP, real helihead hits, and shared scripted-death callers.'
}
finally { $module.Dispose() }
