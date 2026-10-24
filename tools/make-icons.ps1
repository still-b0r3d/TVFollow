# Draws TVFollow's icons into assets\. Run it again after changing the drawing:
#   powershell -ExecutionPolicy Bypass -File tools\make-icons.ps1
# Each .ico holds 16-48 px frames as plain bitmaps (for the taskbar corner and old code)
# and a 256 px PNG frame (for Explorer and the Apps list).
param([string]$OutDir = (Join-Path $PSScriptRoot '..\assets'))
Add-Type -AssemblyName System.Drawing

$Sizes = 16, 20, 24, 32, 40, 48, 64, 256

function RoundRect($g, $brush, $x, $y, $w, $h, $r) {
    $p = New-Object Drawing.Drawing2D.GraphicsPath
    $p.AddArc($x, $y, $r * 2, $r * 2, 180, 90)
    $p.AddArc($x + $w - $r * 2, $y, $r * 2, $r * 2, 270, 90)
    $p.AddArc($x + $w - $r * 2, $y + $h - $r * 2, $r * 2, $r * 2, 0, 90)
    $p.AddArc($x, $y + $h - $r * 2, $r * 2, $r * 2, 90, 90)
    $p.CloseFigure()
    $g.FillPath($brush, $p)
}

# The TV, drawn on a 256 x 256 grid and scaled to $size. Small sizes skip the glare and
# the badge text, which only turn to mush.
function Draw-Tv([int]$size, [string]$screen, [string]$badge, [string]$label) {
    $bmp = New-Object Drawing.Bitmap $size, $size, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.Clear([Drawing.Color]::Transparent)
    $g.ScaleTransform($size / 256.0, $size / 256.0)
    $dark = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 34, 34, 40))
    if ($size -ge 24) {
        RoundRect $g $dark 98 196 60 26 6
        RoundRect $g $dark 70 214 116 18 9
        RoundRect $g $dark 8 30 240 172 18
        RoundRect $g (New-Object Drawing.SolidBrush ([Drawing.ColorTranslator]::FromHtml($screen))) 22 44 212 144 8
    } else {
        # chunkier for the taskbar corner: thicker frame, bigger screen, short stand
        RoundRect $g $dark 84 200 88 36 8
        RoundRect $g $dark 0 24 256 184 24
        RoundRect $g (New-Object Drawing.SolidBrush ([Drawing.ColorTranslator]::FromHtml($screen))) 24 48 208 136 10
    }
    if ($size -ge 32) {
        $glare = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(40, 255, 255, 255))
        $g.FillPolygon($glare, [Drawing.Point[]]@((New-Object Drawing.Point 22, 44), (New-Object Drawing.Point 140, 44), (New-Object Drawing.Point 60, 188), (New-Object Drawing.Point 22, 188)))
    }
    if ($badge) {
        $g.FillEllipse($dark, 140, 118, 116, 116)
        $g.FillEllipse((New-Object Drawing.SolidBrush ([Drawing.ColorTranslator]::FromHtml($badge))), 148, 126, 100, 100)
        if ($size -ge 48) {
            $font = New-Object Drawing.Font 'Segoe UI', 30, ([Drawing.FontStyle]::Bold), ([Drawing.GraphicsUnit]::Pixel)
            $fmt = New-Object Drawing.StringFormat
            $fmt.Alignment = 'Center'
            $fmt.LineAlignment = 'Center'
            $g.DrawString($label, $font, [Drawing.Brushes]::White, (New-Object Drawing.RectangleF 148, 128, 100, 100), $fmt)
        }
    }
    $g.Dispose()
    return $bmp
}

# A 32-bit bitmap frame: header, bottom-up BGRA rows, then an all-clear AND mask.
function Bmp-Frame([Drawing.Bitmap]$bmp) {
    $n = $bmp.Width
    $ms = New-Object IO.MemoryStream
    $w = New-Object IO.BinaryWriter $ms
    $maskRow = [int]([math]::Ceiling($n / 32.0) * 4)
    $w.Write([UInt32]40); $w.Write([Int32]$n); $w.Write([Int32]($n * 2))
    $w.Write([UInt16]1); $w.Write([UInt16]32); $w.Write([UInt32]0)
    $w.Write([UInt32]($n * $n * 4 + $maskRow * $n))
    $w.Write([Int32]0); $w.Write([Int32]0); $w.Write([UInt32]0); $w.Write([UInt32]0)
    $data = $bmp.LockBits((New-Object Drawing.Rectangle 0, 0, $n, $n), 'ReadOnly', ([Drawing.Imaging.PixelFormat]::Format32bppArgb))
    $row = New-Object byte[] ($n * 4)
    for ($y = $n - 1; $y -ge 0; $y--) {
        [Runtime.InteropServices.Marshal]::Copy([IntPtr]($data.Scan0.ToInt64() + $y * $data.Stride), $row, 0, $n * 4)
        $w.Write($row)
    }
    $bmp.UnlockBits($data)
    $w.Write((New-Object byte[] ($maskRow * $n)))
    $w.Flush()
    return , $ms.ToArray()  # the comma keeps PowerShell from unrolling the bytes
}

function Png-Frame([Drawing.Bitmap]$bmp) {
    $ms = New-Object IO.MemoryStream
    $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
    return , $ms.ToArray()
}

function Save-Icon([string]$name, [string]$screen, [string]$badge, [string]$label) {
    $frames = foreach ($s in $Sizes) {
        $bmp = Draw-Tv $s $screen $badge $label
        if ($s -ge 256) { , (Png-Frame $bmp) } else { , (Bmp-Frame $bmp) }
        $bmp.Dispose()
    }
    $path = Join-Path $OutDir $name
    $fs = [IO.File]::Create($path)
    $w = New-Object IO.BinaryWriter $fs
    $w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$Sizes.Count)
    $offset = 6 + 16 * $Sizes.Count
    for ($i = 0; $i -lt $Sizes.Count; $i++) {
        $s = $Sizes[$i]
        $w.Write([byte]($s % 256)); $w.Write([byte]($s % 256)); $w.Write([byte]0); $w.Write([byte]0)
        $w.Write([UInt16]1); $w.Write([UInt16]32)
        $w.Write([UInt32]([byte[]]$frames[$i]).Length); $w.Write([UInt32]$offset)
        $offset += $frames[$i].Length
    }
    foreach ($f in $frames) { $w.Write([byte[]]$f) }
    $w.Close()
    "wrote $path"
}

Save-Icon 'tv.ico'        '#2a6fdb' $null     ''
Save-Icon 'tv-paused.ico' '#5b6170' '#c68a00' 'II'
Save-Icon 'tv-on.ico'     '#2a6fdb' '#2e9e4f' 'ON'
Save-Icon 'tv-off.ico'    '#3a3a44' '#c0392b' 'OFF'
Save-Icon 'tv-pc.ico'     '#2a6fdb' '#7b4fd6' 'PC'
