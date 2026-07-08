# Generates app.ico - a simple "active user" icon:
#   a white person silhouette on a green (online/active) circle.
# Small sizes are written as uncompressed DIB (BMP) entries so the .NET
# tray reader handles them cleanly; the 256px entry is PNG-compressed.

Add-Type -AssemblyName System.Drawing

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = [double]$size
    $pad = $s * 0.03

    # Green "active" circle background.
    $green = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 46, 204, 113))
    $g.FillEllipse($green, $pad, $pad, $s - 2*$pad, $s - 2*$pad)

    # White person silhouette.
    $white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)

    # Head.
    $headD = $s * 0.30
    $g.FillEllipse($white, ($s - $headD) / 2.0, $s * 0.20, $headD, $headD)

    # Shoulders / body (top hump of an ellipse).
    $bodyW = $s * 0.62
    $bodyH = $s * 0.55
    $g.FillPie($white, ($s - $bodyW) / 2.0, $s * 0.54, $bodyW, $bodyH, 180, 180)

    $g.Dispose()
    return $bmp
}

function Get-DibBytes($bmp, [int]$size) {
    $rect = New-Object System.Drawing.Rectangle 0, 0, $size, $size
    $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride = $data.Stride
    $buffer = New-Object byte[] ($stride * $size)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $buffer, 0, $buffer.Length)
    $bmp.UnlockBits($data)

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)

    # BITMAPINFOHEADER (height doubled for XOR + AND masks).
    $bw.Write([UInt32]40)
    $bw.Write([Int32]$size)
    $bw.Write([Int32]($size * 2))
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $bw.Write([UInt32]0)   # BI_RGB
    $bw.Write([UInt32]0)
    $bw.Write([Int32]0); $bw.Write([Int32]0)
    $bw.Write([UInt32]0); $bw.Write([UInt32]0)

    # XOR (color) data, bottom-up.
    for ($y = $size - 1; $y -ge 0; $y--) {
        $bw.Write($buffer, $y * $stride, $size * 4)
    }

    # AND mask: all zero (alpha channel handles transparency), rows padded to 4 bytes.
    $maskRowBytes = [int]([math]::Floor(($size + 31) / 32) * 4)
    $maskRow = New-Object byte[] $maskRowBytes
    for ($y = 0; $y -lt $size; $y++) { $bw.Write($maskRow, 0, $maskRowBytes) }

    $bw.Flush()
    $result = $ms.ToArray()
    $bw.Close()
    return ,$result
}

function Get-PngBytes($bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $result = $ms.ToArray()
    $ms.Dispose()
    return ,$result
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)

$images = @()
foreach ($sz in $sizes) {
    $bmp = New-IconBitmap $sz
    if ($sz -ge 256) {
        $images += ,(Get-PngBytes $bmp)
    } else {
        $images += ,(Get-DibBytes $bmp $sz)
    }
    $bmp.Dispose()
}

$outPath = Join-Path $PSScriptRoot 'app.ico'
$fs = New-Object System.IO.FileStream($outPath, [System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter($fs)

# ICONDIR.
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$sizes.Count)

$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]
    $bytes = $images[$i]
    $dim = if ($sz -ge 256) { 0 } else { $sz }

    $bw.Write([Byte]$dim)
    $bw.Write([Byte]$dim)
    $bw.Write([Byte]0)
    $bw.Write([Byte]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $bw.Write([UInt32]$bytes.Length)
    $bw.Write([UInt32]$offset)
    $offset += $bytes.Length
}

foreach ($bytes in $images) { $bw.Write($bytes) }

$bw.Flush()
$bw.Close()
$fs.Close()

Write-Host "Created $outPath"
