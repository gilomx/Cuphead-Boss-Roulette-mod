param(
    [ValidateSet('Server', 'Follow', 'Bits', 'Subscription', 'Gifted', 'Resubscription', 'Reward')]
    [string]$Event = 'Server',
    [ValidateRange(1, 1000000)][int]$Amount = 100,
    [ValidateRange(1, 1000)][int]$Quantity = 1,
    [ValidateRange(1, 2147483647)][int]$ViewerId = 1001,
    [ValidateLength(1, 64)][string]$RewardName = 'Mini jefe'
)

$ErrorActionPreference = 'Stop'
$cliVersion = '1.1.24'
$cache = Join-Path (Split-Path -Parent $PSScriptRoot) '.deployment-cache/twitch-cli'
$null = [IO.Directory]::CreateDirectory($cache)
$archive = Join-Path $cache 'cli.zip'
$expectedHash = '6053F3FAB02AA1349F25EFA550D14E11033C10C775787367CA4FD88D96B1CE70'
$cli = Join-Path $cache "twitch-cli_${cliVersion}_Windows_x86_64/twitch.exe"
if (-not (Test-Path -LiteralPath $archive)) {
    Invoke-WebRequest "https://github.com/twitchdev/twitch-cli/releases/download/v$cliVersion/twitch-cli_${cliVersion}_Windows_x86_64.zip" -OutFile $archive
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'El ZIP de Twitch CLI no coincide con la publicación oficial.'
}
if (-not (Test-Path -LiteralPath $cli)) { Expand-Archive -LiteralPath $archive -DestinationPath $cache -Force }
if ((Get-FileHash -LiteralPath $cli -Algorithm SHA256).Hash -ne '98FCAB8242674B72A9B36A9E523C71AC42DFC196C34AC2FE02DDF4076F252C58') {
    throw 'El ejecutable de Twitch CLI no coincide con la publicación oficial.'
}
$config = Join-Path $cache 'local-test.env'
if (-not (Test-Path -LiteralPath $config)) { [IO.File]::WriteAllText($config, '') }

if ($Event -eq 'Server') {
    Write-Host 'Servidor local de Twitch. En Configuración > Twitch pulsa Probar con Twitch CLI (Dev).'
    & $cli event websocket start-server --ip 127.0.0.1 --port 8080 --require-subscription --config $config
} else {
    $eventType = @{ Follow = 'channel.follow'; Bits = 'channel.cheer'; Subscription = 'channel.subscribe';
        Gifted = 'channel.subscription.gift'; Resubscription = 'channel.subscription.message';
        Reward = 'channel.channel_points_custom_reward_redemption.add' }[$Event]
    $cliArguments = @('event', 'trigger', $eventType, '--transport', 'websocket', '--no-config',
        '--to-user', '42', '--from-user', "$ViewerId")
    if ($Event -eq 'Follow') { $cliArguments += @('--version', '2') }
    if ($Event -eq 'Bits') { $cliArguments += @('--cost', "$Amount") }
    if ($Event -eq 'Gifted') { $cliArguments += @('--cost', "$Quantity") }
    if ($Event -eq 'Reward') { $cliArguments += @('--cost', "$Amount", '--item-name', $RewardName, '--item-id', 'pichi-test-reward') }
    & $cli @cliArguments
}
if ($LASTEXITCODE -ne 0) { throw "Twitch CLI terminó con código $LASTEXITCODE." }
