param([string]$Path = '/', [string]$OutFile)
$ErrorActionPreference = 'Stop'

$client = New-Object System.Net.Sockets.TcpClient('localhost', 8099)
$stream = $client.GetStream()
$request = "GET $Path HTTP/1.1`r`nHost: localhost:8099`r`nConnection: close`r`n`r`n"
$bytes = [System.Text.Encoding]::ASCII.GetBytes($request)
$stream.Write($bytes, 0, $bytes.Length)

$ms = New-Object System.IO.MemoryStream
$buffer = New-Object byte[] 65536
while (($n = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) { $ms.Write($buffer, 0, $n) }
$client.Close()

$raw = $ms.ToArray()
if ($OutFile) { [System.IO.File]::WriteAllBytes($OutFile, $raw) }
[System.Text.Encoding]::GetEncoding('ISO-8859-1').GetString($raw)
