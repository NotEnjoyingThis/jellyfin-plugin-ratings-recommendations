<#
.SYNOPSIS
    Draws the placeholder catalog logo assets/logo.png (1280x720, 16:9): a stylised thumb up
    above four dots for the rating levels. Uses System.Drawing (Windows PowerShell 5.1 or
    PowerShell 7 on Windows). Replace logo.png with a real design whenever you like.
#>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$width = 1280; $height = 720
$bmp = New-Object System.Drawing.Bitmap($width, $height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
try {
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    $bgRect = New-Object System.Drawing.Rectangle(0, 0, $width, $height)
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush($bgRect, [System.Drawing.Color]::FromArgb(27, 16, 54), [System.Drawing.Color]::FromArgb(11, 58, 92), 45.0)
    $g.FillRectangle($bg, $bgRect)

    $thumbRect = New-Object System.Drawing.Rectangle(450, 150, 420, 400)
    $thumbBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($thumbRect, [System.Drawing.Color]::FromArgb(185, 140, 255), [System.Drawing.Color]::FromArgb(63, 179, 232), 90.0)

    # Cuff (wrist part)
    $cuff = New-Object System.Drawing.Drawing2D.GraphicsPath
    $cuff.AddArc(450, 300, 36, 36, 180, 90); $cuff.AddArc(504, 300, 36, 36, 270, 90)
    $cuff.AddArc(504, 494, 36, 36, 0, 90); $cuff.AddArc(450, 494, 36, 36, 90, 90)
    $cuff.CloseFigure()
    $g.FillPath($thumbBrush, $cuff)

    # Hand with raised thumb
    $hand = New-Object System.Drawing.Drawing2D.GraphicsPath
    $points = @(
        (New-Object System.Drawing.PointF(570, 310)), (New-Object System.Drawing.PointF(610, 300)),
        (New-Object System.Drawing.PointF(664, 180)), (New-Object System.Drawing.PointF(702, 160)),
        (New-Object System.Drawing.PointF(732, 190)), (New-Object System.Drawing.PointF(712, 320)),
        (New-Object System.Drawing.PointF(826, 320)), (New-Object System.Drawing.PointF(860, 360)),
        (New-Object System.Drawing.PointF(822, 500)), (New-Object System.Drawing.PointF(790, 530)),
        (New-Object System.Drawing.PointF(590, 530)), (New-Object System.Drawing.PointF(570, 510))
    )
    $hand.AddClosedCurve([System.Drawing.PointF[]]$points, 0.25)
    $g.FillPath($thumbBrush, $hand)

    # Four rating levels: -3, -1, +1, +3
    $alphas = @(64, 115, 180, 255); $sizes = @(28, 28, 28, 36); $x = 470
    for ($i = 0; $i -lt 4; $i++) {
        $dot = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb($alphas[$i], 255, 255, 255))
        $s = $sizes[$i]
        $g.FillEllipse($dot, $x - $s / 2, 610 - $s / 2, $s, $s)
        $dot.Dispose()
        $x += 110
    }

    $out = Join-Path $PSScriptRoot 'logo.png'
    $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Host "Written $out"
}
finally {
    $g.Dispose(); $bmp.Dispose()
}
