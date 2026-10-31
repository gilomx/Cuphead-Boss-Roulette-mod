#requires -Version 7.2
[CmdletBinding()]
param(
    [string]$CupheadDir,
    [string]$CecilPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$testRepoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$testCacheRoot = Join-Path $testRepoRoot '.deployment-cache'
$testRunDirectory = Join-Path $testCacheRoot ('tap-audit-negative-' + [guid]::NewGuid().ToString('N'))
$testVerifier = Join-Path $PSScriptRoot 'verify_native_tap_farming_contract.ps1'

function Assert-TestContainedPath([string]$Path) {
    $target = [IO.Path]::GetFullPath($Path)
    $cache = [IO.Path]::GetFullPath($testCacheRoot)
    $prefix = $cache.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (!$target.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Native-audit test path escaped the workspace deployment cache.'
    }
    # Reject junctions/symlinks before creating, writing or recursively cleaning
    # a case. Lexical containment alone does not prove physical containment.
    $current = $target
    while ($current.Length -ge $cache.Length) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw 'Native-audit test paths must not contain reparse points.'
            }
        }
        if ($current.Equals($cache, [StringComparison]::OrdinalIgnoreCase)) { break }
        $current = [IO.Path]::GetDirectoryName($current)
    }
}

function New-MutantCopy([string]$Case, [string]$Mutation) {
    $caseRoot = Join-Path $testRunDirectory $Case
    $copyPath = Join-Path $caseRoot 'Cuphead_Data/Managed/Assembly-CSharp.dll'
    Assert-TestContainedPath $copyPath
    $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($copyPath))
    Assert-TestContainedPath $copyPath
    Copy-Item -LiteralPath $testSourceNativePath -Destination $copyPath

    # Read the COPY through a memory stream so writing its mutated image cannot
    # conflict with a file-backed reader. The installation is never a writer.
    $assemblyStream = [IO.MemoryStream]::new([IO.File]::ReadAllBytes($copyPath), $false)
    $resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
    $resolver.AddSearchDirectory([IO.Path]::GetDirectoryName($testSourceNativePath))
    $reader = [Mono.Cecil.ReaderParameters]::new()
    $reader.AssemblyResolver = $resolver
    $mutant = [Mono.Cecil.ModuleDefinition]::ReadModule($assemblyStream, $reader)
    try {
        $mutantTypes = @(All-Types $mutant.Types)
        if ($Mutation -eq 'unexpected_damage') {
            $robot = $mutantTypes | Where-Object FullName -eq 'LevelProperties/Robot'
            $helihead = ($mutantTypes | Where-Object FullName -eq 'RobotLevelHelihead').Methods |
                Where-Object Name -eq 'OnDamageTaken'
            $damageTarget = (Calls $helihead 'DealDamage' | Where-Object {
                $_.Operand.DeclaringType.Name -eq 'AbstractLevelProperties`3'
            })[0].Operand
            $flags = [Mono.Cecil.MethodAttributes]::Private -bor
                [Mono.Cecil.MethodAttributes]::Static -bor [Mono.Cecil.MethodAttributes]::HideBySig
            $unused = [Mono.Cecil.MethodDefinition]::new(
                'TapAuditUnexpectedDamage', $flags, $mutant.TypeSystem.Void)
            $unused.Parameters.Add([Mono.Cecil.ParameterDefinition]::new(
                'properties', [Mono.Cecil.ParameterAttributes]::None, $robot))
            $processor = $unused.Body.GetILProcessor()
            $processor.Append([Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
            $processor.Append([Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, [float]1))
            $processor.Append([Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Callvirt, $damageTarget))
            $processor.Append([Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Ret))
            ($mutantTypes | Where-Object FullName -eq 'RobotLevel').Methods.Add($unused)
        }
        elseif ($Mutation -eq 'duplicate_advance') {
            $iterator = $mutantTypes | Where-Object FullName -eq 'AirplaneLevel/<check_terriers_cr>c__Iterator2'
            $moveNext = $iterator.Methods | Where-Object Name -eq 'MoveNext'
            $advance = (Calls $moveNext 'DealDamageToNextNamedState')[0]
            # Duplicate an EXISTING caller's site while preserving its method
            # signature/family. A set-only audit would incorrectly accept this.
            # The properties instance is already on the stack at this point.
            $processor = $moveNext.Body.GetILProcessor()
            $processor.InsertBefore($advance,
                [Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Dup))
            $processor.InsertBefore($advance,
                [Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Callvirt, $advance.Operand))
        }
        else { throw 'Unknown native-audit test mutation.' }
        Assert-TestContainedPath $copyPath
        $mutant.Write($copyPath)
    }
    finally {
        $mutant.Dispose()
        $resolver.Dispose()
        $assemblyStream.Dispose()
    }
    return $caseRoot
}

function Assert-MutantRejected([string]$Directory, [string]$ExpectedMessage) {
    $rejected = $false
    try { & $testVerifier -CupheadDir $Directory -CecilPath $CecilPath | Out-Null }
    catch {
        if (!$_.Exception.Message.Contains($ExpectedMessage, [StringComparison]::Ordinal)) { throw }
        $rejected = $true
    }
    if (!$rejected) { throw "Native audit accepted a mutant expected to fail: $ExpectedMessage" }
    Write-Output "PASS Mutated copy rejected: $ExpectedMessage"
}

# Positive control also resolves portable paths and loads Cecil using the same
# code as ordinary contract runs. Dot-sourcing exposes its read-only helpers.
$testArguments = @{}
if ($CupheadDir) { $testArguments['CupheadDir'] = $CupheadDir }
if ($CecilPath) { $testArguments['CecilPath'] = $CecilPath }
. $testVerifier @testArguments
$testSourceNativePath = [IO.Path]::GetFullPath($nativePath)
$testSourceHash = (Get-FileHash -LiteralPath $testSourceNativePath -Algorithm SHA256).Hash
try {
    Assert-TestContainedPath $testRunDirectory
    $damageCopy = New-MutantCopy 'unexpected-damage' 'unexpected_damage'
    Assert-MutantRejected $damageCopy 'Nonstandard boss damage routes changed'
    $advanceCopy = New-MutantCopy 'duplicate-advance' 'duplicate_advance'
    Assert-MutantRejected $advanceCopy 'Scripted named-state advancement routes changed'
}
finally {
    if (Test-Path -LiteralPath $testRunDirectory) {
        Assert-TestContainedPath $testRunDirectory
        Remove-Item -LiteralPath $testRunDirectory -Recurse -Force
    }
    if ((Get-FileHash -LiteralPath $testSourceNativePath -Algorithm SHA256).Hash -ne $testSourceHash) {
        throw 'Native source hash changed during audit tests.'
    }
}
Write-Output 'PASS Negative native audit tests: unknown damage and duplicate phase-advance sites fail; temporary copies cleaned; original reference hash unchanged.'
