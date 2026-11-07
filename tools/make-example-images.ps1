# Draws the example app's pictures into examples\tvfollow-example\images: its tile on the
# TV's home screen (540 x 405, and 290 x 218 for HD TVs) and the picture while it starts
# (1920 x 1080 and 1280 x 720). Run it again after changing the drawing:
#   & .\tools\make-example-images.ps1
param([string]$OutDir = (Join-Path $PSScriptRoot '..\examples\tvfollow-example\images'))
Add-Type -AssemblyName System.Drawing

$Blue = [Drawing.ColorTranslator]::FromHtml('#2a6fdb')   # TVFollow's blue
$Navy = [Drawing.ColorTranslator]::FromHtml('#14213d')   # the app's background
$Frame = [Drawing.Color]::FromArgb(255, 34, 34, 40)
$Screen = [Drawing.ColorTranslator]::FromHtml('#9cc0ff')

function RoundRect($g, $brush, $x, $y, $w, $h, $r) {
    $p = New-Object Drawing.Drawing2D.GraphicsPath
    $p.AddArc($x, $y, $r * 2, $r * 2, 180, 90)
    $p.AddArc($x + $w - $r * 2, $y, $r * 2, $r * 2, 270, 90)
    $p.AddArc($x + $w - $r * 2, $y + $h - $r * 2, $r * 2, $r * 2, 0, 90)
    $p.AddArc($x, $y + $h - $r * 2, $r * 2, $r * 2, 90, 90)
    $p.CloseFigure()
    $g.FillPath($brush, $p)
}

# TVFollow's TV (the same drawing as its icon, on a 256 x 256 grid), $size wide at $x, $y,
# with a play mark on its screen
function Draw-Tv($g, [float]$x, [float]$y, [float]$size) {
    $state = $g.Save()
    $g.TranslateTransform($x, $y)
    $g.ScaleTransform($size / 256.0, $size / 256.0)
    $dark = New-Object Drawing.SolidBrush $Frame
    RoundRect $g $dark 98 196 60 26 6
    RoundRect $g $dark 70 214 116 18 9
    RoundRect $g $dark 8 30 240 172 18
    RoundRect $g (New-Object Drawing.SolidBrush $Screen) 22 44 212 144 8
    $glare = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(40, 255, 255, 255))
    $g.FillPolygon($glare, [Drawing.PointF[]]@((New-Object Drawing.PointF 22, 44), (New-Object Drawing.PointF 140, 44), (New-Object Drawing.PointF 60, 188), (New-Object Drawing.PointF 22, 188)))
    $play = New-Object Drawing.SolidBrush $Blue
    $g.FillPolygon($play, [Drawing.PointF[]]@((New-Object Drawing.PointF 108, 82), (New-Object Drawing.PointF 160, 116), (New-Object Drawing.PointF 108, 150)))
    $g.Restore($state)
}

function New-Canvas([int]$w, [int]$h, $color) {
    $bmp = New-Object Drawing.Bitmap $w, $h, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.Clear($color)
    return $bmp, $g
}

function Write-Text($g, [string]$text, [float]$px, [float]$top, [int]$width, [bool]$bold, $color) {
    $style = if ($bold) { [Drawing.FontStyle]::Bold } else { [Drawing.FontStyle]::Regular }
    $font = New-Object Drawing.Font 'Segoe UI', $px, $style, ([Drawing.GraphicsUnit]::Pixel)
    $fmt = New-Object Drawing.StringFormat
    $fmt.Alignment = 'Center'
    $g.DrawString($text, $font, (New-Object Drawing.SolidBrush $color), (New-Object Drawing.RectangleF 0, $top, $width, ($px * 1.6)), $fmt)
    $font.Dispose()
}

# the tile: the TV on TVFollow's blue, the app's name under it
function Save-Tile([int]$w, [int]$h, [string]$name) {
    $bmp, $g = New-Canvas $w $h $Blue
    $tv = $w * 0.42
    Draw-Tv $g (($w - $tv) / 2) ($h * 0.07) $tv
    Write-Text $g 'TVFollow example' ($h * 0.105) ($h * 0.70) $w $true ([Drawing.Color]::White)
    $g.Dispose()
    $path = Join-Path $OutDir $name
    $bmp.Save($path, [Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    "wrote $path"
}

# the start picture: the TV on the app's navy, with its name
function Save-Splash([int]$w, [int]$h, [string]$name) {
    $bmp, $g = New-Canvas $w $h $Navy
    $tv = $h * 0.36
    Draw-Tv $g (($w - $tv) / 2) ($h * 0.22) $tv
    Write-Text $g 'TVFollow example' ($h * 0.065) ($h * 0.60) $w $true ([Drawing.Color]::White)
    $g.Dispose()
    $path = Join-Path $OutDir $name
    $bmp.Save($path, [Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    "wrote $path"
}

New-Item -ItemType Directory -Force $OutDir | Out-Null
Save-Tile 540 405 'icon_focus_fhd.png'
Save-Tile 290 218 'icon_focus_hd.png'
Save-Splash 1920 1080 'splash_fhd.png'
Save-Splash 1280 720 'splash_hd.png'
