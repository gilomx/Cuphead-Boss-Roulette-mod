#requires -Version 7.2
Set-StrictMode -Version Latest

function Send-StreamSimulationOnce {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][ValidateRange(1, 65535)][int]$Port,
        [Parameter(Mandatory = $true)][string]$Path
    )

    # HttpClient can transparently retry GET after a connection failure. This
    # GET schedules work, so use exactly one socket/write attempt instead.
    # Ordinary read-only snapshots may continue to use HttpClient.
    $socket = [Net.Sockets.TcpClient]::new()
    try {
        $socket.ReceiveTimeout = 5000
        $socket.SendTimeout = 5000
        $connecting = $socket.ConnectAsync('127.0.0.1', $Port)
        if (!$connecting.Wait(5000)) { throw 'Timeout conectando al simulador local.' }
        $null = $connecting.GetAwaiter().GetResult()
        $stream = $socket.GetStream()
        $request = "GET /$Path HTTP/1.1`r`nHost: 127.0.0.1:$Port`r`nConnection: close`r`n`r`n"
        $bytes = [Text.Encoding]::ASCII.GetBytes($request)
        $stream.Write($bytes, 0, $bytes.Length)
        $reader = [IO.StreamReader]::new($stream, [Text.Encoding]::UTF8, $false, 1024, $true)
        try {
            $statusLine = $reader.ReadLine()
            if ($null -eq $statusLine -or $statusLine -notmatch '^HTTP/1\.[01] ([0-9]{3})(?: .*)?$') {
                throw 'El simulador cerro la conexion sin una respuesta HTTP valida.'
            }
            $statusCode = [int]$Matches[1]
            $headerLength = $statusLine.Length
            while ($true) {
                $line = $reader.ReadLine()
                if ($null -eq $line) { throw 'Respuesta HTTP incompleta del simulador.' }
                $headerLength += $line.Length + 2
                if ($headerLength -gt 65536) { throw 'Cabeceras del simulador demasiado grandes.' }
                if ($line.Length -eq 0) { break }
            }
            $body = [Text.StringBuilder]::new()
            $buffer = [char[]]::new(1024)
            while (($length = $reader.Read($buffer, 0, $buffer.Length)) -gt 0) {
                if ($body.Length + $length -gt 65536) { throw 'Respuesta del simulador demasiado grande.' }
                $null = $body.Append($buffer, 0, $length)
            }
            return [pscustomobject]@{ StatusCode = $statusCode; Body = $body.ToString() }
        }
        finally { $reader.Dispose() }
    }
    finally { $socket.Dispose() }
}

Export-ModuleMember -Function Send-StreamSimulationOnce
