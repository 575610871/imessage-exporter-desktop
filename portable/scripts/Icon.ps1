param([string]$Destination = "portable/Suoh.App/Assets/Suoh.ico")
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase
$drawing = [System.Windows.Media.DrawingVisual]::new()
$dc = $drawing.RenderOpen()
$suoh = [System.Windows.Media.BrushConverter]::new().ConvertFromString('#992C49')
$dc.DrawRoundedRectangle($suoh, $null, [System.Windows.Rect]::new(8,8,240,240), 52, 52)
$dc.DrawRoundedRectangle([System.Windows.Media.Brushes]::White, $null, [System.Windows.Rect]::new(50,59,156,114), 28, 28)
$tail = [System.Windows.Media.Geometry]::Parse('M 70,156 L 65,199 L 114,165 Z')
$dc.DrawGeometry([System.Windows.Media.Brushes]::White, $null, $tail)
foreach ($x in @(88,128,168)) { $dc.DrawEllipse($suoh, $null, [System.Windows.Point]::new($x,116), 8, 8) }
$dc.Close()
$bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new(256,256,96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
$bitmap.Render($drawing)
$encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
$encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$png = [System.IO.MemoryStream]::new()
$encoder.Save($png)
$bytes = $png.ToArray()
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($Destination))) | Out-Null
$writer = [System.IO.BinaryWriter]::new([System.IO.File]::Create([System.IO.Path]::GetFullPath($Destination)))
try {
  $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
  $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0)
  $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$bytes.Length); $writer.Write([uint32]22)
  $writer.Write($bytes)
} finally { $writer.Dispose(); $png.Dispose() }
