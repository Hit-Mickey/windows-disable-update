# 生成引导页占位截图 guide.png（用户提供真实截图后替换 Assets\guide.png 并重新构建即可）
Add-Type -AssemblyName System.Drawing

$w = 800
$h = 450
$bmp = New-Object System.Drawing.Bitmap -ArgumentList $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = "AntiAlias"
$g.TextRenderingHint = "AntiAliasGridFit"
$g.Clear([System.Drawing.Color]::FromArgb(243, 243, 243))

# 虚线边框
$pen = New-Object System.Drawing.Pen -ArgumentList ([System.Drawing.Color]::FromArgb(200, 200, 200)), 2
$pen.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
$g.DrawRectangle($pen, 8, 8, $w - 16, $h - 16)

# 占位文字
$font1 = New-Object System.Drawing.Font -ArgumentList "Microsoft YaHei UI", 20, ([System.Drawing.FontStyle]::Bold)
$font2 = New-Object System.Drawing.Font -ArgumentList "Microsoft YaHei UI", 13
$brush1 = New-Object System.Drawing.SolidBrush -ArgumentList ([System.Drawing.Color]::FromArgb(93, 93, 93))
$brush2 = New-Object System.Drawing.SolidBrush -ArgumentList ([System.Drawing.Color]::FromArgb(130, 130, 130))
$fmt = New-Object System.Drawing.StringFormat
$fmt.Alignment = [System.Drawing.StringAlignment]::Center
$fmt.LineAlignment = [System.Drawing.StringAlignment]::Center

$g.DrawString("截图占位区域", $font1, $brush1, (New-Object System.Drawing.RectangleF -ArgumentList 0, 140, $w, 60), $fmt)
$g.DrawString("设置 → Windows 更新 → 暂停更新 → 选择日期", $font2, $brush2, (New-Object System.Drawing.RectangleF -ArgumentList 0, 210, $w, 40), $fmt)
$g.DrawString("（将实际截图保存为 Assets\guide.png 后重新构建即可替换）", $font2, $brush2, (New-Object System.Drawing.RectangleF -ArgumentList 0, 250, $w, 40), $fmt)

$g.Dispose()
$dir = Join-Path (Split-Path $PSScriptRoot -Parent) "src\WinUpdatePauser\Assets"
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$bmp.Save((Join-Path $dir "guide.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "guide.png generated at $dir"
