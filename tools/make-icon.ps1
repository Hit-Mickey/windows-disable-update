param(
    [string]$InputPath = (Join-Path $PSScriptRoot '..\src\WinUpdatePauser\Assets\app-icon.png'),
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\src\WinUpdatePauser\Assets\app-icon.ico'),
    [int[]]$Sizes = @(16, 24, 32, 48, 64, 128, 256)
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$resolvedInput = [System.IO.Path]::GetFullPath($InputPath)
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
if (-not [System.IO.File]::Exists($resolvedInput)) {
    throw "Icon source was not found: $resolvedInput"
}

$outputDirectory = [System.IO.Path]::GetDirectoryName($resolvedOutput)
if (-not [System.IO.Directory]::Exists($outputDirectory)) {
    [System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
}

$uniqueSizes = @($Sizes | Sort-Object -Unique)
if ($uniqueSizes.Count -eq 0 -or ($uniqueSizes | Where-Object { $_ -lt 1 -or $_ -gt 256 }).Count -gt 0) {
    throw 'Icon sizes must be between 1 and 256 pixels.'
}

$source = New-Object System.Drawing.Bitmap($resolvedInput)
$pngImages = New-Object System.Collections.Generic.List[byte[]]

try {
    foreach ($size in $uniqueSizes) {
        $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $bitmap.SetResolution(96, 96)
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.DrawImage($source, 0, 0, $size, $size)
            }
            finally {
                $graphics.Dispose()
            }

            $stream = New-Object System.IO.MemoryStream
            try {
                $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
                $pngImages.Add($stream.ToArray())
            }
            finally {
                $stream.Dispose()
            }
        }
        finally {
            $bitmap.Dispose()
        }
    }
}
finally {
    $source.Dispose()
}

$headerSize = 6
$entrySize = 16
$imageOffset = $headerSize + ($entrySize * $uniqueSizes.Count)
$output = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($output)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$uniqueSizes.Count)

    for ($index = 0; $index -lt $uniqueSizes.Count; $index++) {
        $size = $uniqueSizes[$index]
        $image = $pngImages[$index]
        $dimension = if ($size -eq 256) { 0 } else { $size }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$image.Length)
        $writer.Write([uint32]$imageOffset)
        $imageOffset += $image.Length
    }

    foreach ($image in $pngImages) {
        $writer.Write($image)
    }

    [System.IO.File]::WriteAllBytes($resolvedOutput, $output.ToArray())
}
finally {
    $writer.Dispose()
    $output.Dispose()
}

Write-Host ("Created {0} ({1} bytes; sizes: {2})" -f $resolvedOutput, (Get-Item -LiteralPath $resolvedOutput).Length, ($uniqueSizes -join ', '))
