param(
    [Parameter(Mandatory = $false)]
    [string]$OutputPath = "src/ImessageExporterDesktop/Assets/App.ico"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function New-RoundedRectanglePath {
    param(
        [System.Drawing.RectangleF]$Rectangle,
        [float]$Radius
    )

    $diameter = $Radius * 2
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc($Rectangle.X, $Rectangle.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($Rectangle.Right - $diameter, $Rectangle.Y, $diameter, $diameter, 270, 90)
    $path.AddArc($Rectangle.Right - $diameter, $Rectangle.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($Rectangle.X, $Rectangle.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

$fullOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = [System.IO.Path]::GetDirectoryName($fullOutputPath)
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null

$bitmap = [System.Drawing.Bitmap]::new(256, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)

$outerRectangle = [System.Drawing.RectangleF]::new(8, 8, 240, 240)
$outerPath = New-RoundedRectanglePath -Rectangle $outerRectangle -Radius 52
$gradient = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
    $outerRectangle,
    [System.Drawing.Color]::FromArgb(255, 38, 132, 255),
    [System.Drawing.Color]::FromArgb(255, 22, 93, 220),
    45
)
$graphics.FillPath($gradient, $outerPath)

$bubbleRectangle = [System.Drawing.RectangleF]::new(45, 54, 166, 116)
$bubblePath = New-RoundedRectanglePath -Rectangle $bubbleRectangle -Radius 34
$whiteBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
$graphics.FillPath($whiteBrush, $bubblePath)

$tail = [System.Drawing.PointF[]]@(
    [System.Drawing.PointF]::new(72, 158),
    [System.Drawing.PointF]::new(65, 198),
    [System.Drawing.PointF]::new(108, 166)
)
$graphics.FillPolygon($whiteBrush, $tail)

$dotBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 38, 132, 255))
foreach ($x in @(79, 119, 159)) {
    $graphics.FillEllipse($dotBrush, $x, 101, 18, 18)
}

$temporaryPng = [System.IO.Path]::GetTempFileName()
$bitmap.Save($temporaryPng, [System.Drawing.Imaging.ImageFormat]::Png)
$pngBytes = [System.IO.File]::ReadAllBytes($temporaryPng)

$stream = [System.IO.File]::Create($fullOutputPath)
$writer = [System.IO.BinaryWriter]::new($stream)
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]1)
$writer.Write([Byte]0)
$writer.Write([Byte]0)
$writer.Write([Byte]0)
$writer.Write([Byte]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]32)
$writer.Write([UInt32]$pngBytes.Length)
$writer.Write([UInt32]22)
$writer.Write($pngBytes)
$writer.Flush()
$writer.Dispose()

Remove-Item $temporaryPng -Force
$dotBrush.Dispose()
$whiteBrush.Dispose()
$bubblePath.Dispose()
$gradient.Dispose()
$outerPath.Dispose()
$graphics.Dispose()
$bitmap.Dispose()

Write-Host "Created $fullOutputPath"
