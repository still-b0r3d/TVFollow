param([string]$OutDir)
Add-Type -AssemblyName System.Drawing

function Save-Ico([System.Drawing.Bitmap]$bmp, [string]$path) {
    $ms = New-Object IO.MemoryStream
    $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
    $png = $ms.ToArray()
    $fs = [IO.File]::Create($path)
    $w = New-Object IO.BinaryWriter $fs
    $w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]1)      # header: icon, 1 image
    $w.Write([byte]0); $w.Write([byte]0); $w.Write([byte]0); $w.Write([byte]0) # 256x256, no palette
    $w.Write([UInt16]1); $w.Write([UInt16]32)                             # planes, bpp
    $w.Write([UInt32]$png.Length); $w.Write([UInt32]22)                   # size, offset
    $w.Write($png)
    $w.Close()
}

function RoundRect($g, $brush, $x, $y, $wd, $ht, $r) {
    $p = New-Object Drawing.Drawing2D.GraphicsPath
    $p.AddArc($x, $y, $r*2, $r*2, 180, 90)
    $p.AddArc($x+$wd-$r*2, $y, $r*2, $r*2, 270, 90)
    $p.AddArc($x+$wd-$r*2, $y+$ht-$r*2, $r*2, $r*2, 0, 90)
    $p.AddArc($x, $y+$ht-$r*2, $r*2, $r*2, 90, 90)
    $p.CloseFigure()
    $g.FillPath($brush, $p)
}

function Make-Tv([string]$screen, [string]$badge, [string]$label, [string]$path) {
    $bmp = New-Object Drawing.Bitmap 256, 256
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.Clear([Drawing.Color]::Transparent)
    # stand
    RoundRect $g (New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255,40,40,46))) 98 196 60 26 6
    RoundRect $g (New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255,40,40,46))) 70 214 116 18 9
    # bezel + screen
    RoundRect $g (New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255,28,28,32))) 8 30 240 172 18
    RoundRect $g (New-Object Drawing.SolidBrush ([Drawing.ColorTranslator]::FromHtml($screen))) 22 44 212 144 8
    # screen glare
    $glare = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(40,255,255,255))
    $g.FillPolygon($glare, [Drawing.Point[]]@((New-Object Drawing.Point 22,44),(New-Object Drawing.Point 140,44),(New-Object Drawing.Point 60,188),(New-Object Drawing.Point 22,188)))
    if ($badge) {
        $g.FillEllipse((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255,20,20,24))), 140, 118, 116, 116)
        $g.FillEllipse((New-Object Drawing.SolidBrush ([Drawing.ColorTranslator]::FromHtml($badge))), 148, 126, 100, 100)
        $font = New-Object Drawing.Font 'Segoe UI', 30, ([Drawing.FontStyle]::Bold), ([Drawing.GraphicsUnit]::Pixel)
        $fmt = New-Object Drawing.StringFormat; $fmt.Alignment = 'Center'; $fmt.LineAlignment = 'Center'
        $g.DrawString($label, $font, [Drawing.Brushes]::White, (New-Object Drawing.RectangleF 148, 128, 100, 100), $fmt)
    }
    $g.Dispose()
    Save-Ico $bmp $path
    $bmp.Dispose()
}

Make-Tv '#2a6fdb' $null    ''    (Join-Path $OutDir 'tv.ico')
Make-Tv '#2a6fdb' '#2e9e4f' 'ON'  (Join-Path $OutDir 'tv-on.ico')
Make-Tv '#3a3a44' '#c0392b' 'OFF' (Join-Path $OutDir 'tv-off.ico')
Make-Tv '#2a6fdb' '#7b4fd6' 'PC'  (Join-Path $OutDir 'tv-pc.ico')
"icons written"
