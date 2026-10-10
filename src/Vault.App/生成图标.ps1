# 密码库 —— 本地密码管理器
# Copyright (C) 2026 ZhangGe
#
# This program is free software: you can redistribute it and/or modify it
# under the terms of the GNU General Public License as published by the
# Free Software Foundation, either version 3 of the License, or (at your
# option) any later version. This program is distributed WITHOUT ANY
# WARRANTY; see the GNU General Public License for more details.
# <https://www.gnu.org/licenses/>

# 生成 密码库.ico（C# 版的图标）。改了造型就重跑一次：
#   pwsh -NoProfile -File .\生成图标.ps1
#
# 造型：金色圆角方块底 + 深色挂锁。金底深锁的对比度最强，
# 16 像素（任务栏、文件列表用的就是这个尺寸）下仍能一眼认出来。
#
# ICO 里塞 256/48/32/16 四个尺寸——只给一个大图的话，Windows 缩小时会糊。
# 注意 ICO 目录项里尺寸字段是 1 字节，256 要写成 0。

Add-Type -AssemblyName System.Drawing

$出 = Join-Path $PSScriptRoot '密码库.ico'
$金 = [Drawing.Color]::FromArgb(232, 179, 65)
$深 = [Drawing.Color]::FromArgb(31, 41, 55)

function 圆角路径 {
    param([double]$x, [double]$y, [double]$w, [double]$h, [double]$r)
    $p = New-Object Drawing.Drawing2D.GraphicsPath
    $p.AddArc($x, $y, $r * 2, $r * 2, 180, 90)
    $p.AddArc($x + $w - $r * 2, $y, $r * 2, $r * 2, 270, 90)
    $p.AddArc($x + $w - $r * 2, $y + $h - $r * 2, $r * 2, $r * 2, 0, 90)
    $p.AddArc($x, $y + $h - $r * 2, $r * 2, $r * 2, 90, 90)
    $p.CloseFigure()
    return $p
}

function 画 {
    param([int]$边)
    $b = New-Object Drawing.Bitmap $边, $边
    $g = [Drawing.Graphics]::FromImage($b)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([Drawing.Color]::Transparent)
    $s = $边 / 256.0

    # 金色底
    $g.FillPath((New-Object Drawing.SolidBrush $金), (圆角路径 0 0 (256 * $s) (256 * $s) (56 * $s)))

    # 锁梁（半圆弧，圆头笔）
    $笔 = New-Object Drawing.Pen $深, (30 * $s)
    $笔.StartCap = 'Round'; $笔.EndCap = 'Round'
    $g.DrawArc($笔, (78 * $s), (46 * $s), (100 * $s), (108 * $s), 180, 180)

    # 锁体
    $g.FillPath((New-Object Drawing.SolidBrush $深), (圆角路径 (54 * $s) (114 * $s) (148 * $s) (108 * $s) (24 * $s)))

    # 钥匙孔（挖成金色，和底色同色）
    $g.FillEllipse((New-Object Drawing.SolidBrush $金), (114 * $s), (144 * $s), (28 * $s), (28 * $s))
    $g.FillRectangle((New-Object Drawing.SolidBrush $金), (121 * $s), (163 * $s), (14 * $s), (32 * $s))

    $笔.Dispose(); $g.Dispose()
    return $b
}

# 按 ICO 格式手工拼：图标头 + 目录项 + 每个尺寸的 PNG 数据
$尺寸 = 256, 48, 32, 16
$图数据 = foreach ($n in $尺寸) {
    $bm = 画 $n
    $ms = New-Object IO.MemoryStream
    $bm.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
    $bm.Dispose()
    , $ms.ToArray()
}

$流 = New-Object IO.MemoryStream
$写 = New-Object IO.BinaryWriter $流
$写.Write([uint16]0); $写.Write([uint16]1); $写.Write([uint16]$尺寸.Count)
$偏移 = 6 + 16 * $尺寸.Count
for ($i = 0; $i -lt $尺寸.Count; $i++) {
    $n = $尺寸[$i]
    $写.Write([byte]$(if ($n -ge 256) { 0 } else { $n }))   # 256 写 0
    $写.Write([byte]$(if ($n -ge 256) { 0 } else { $n }))
    $写.Write([byte]0); $写.Write([byte]0)
    $写.Write([uint16]1); $写.Write([uint16]32)
    $写.Write([uint32]$图数据[$i].Length)
    $写.Write([uint32]$偏移)
    $偏移 += $图数据[$i].Length
}
foreach ($d in $图数据) { $写.Write($d) }
$写.Flush()
[IO.File]::WriteAllBytes($出, $流.ToArray())
$写.Dispose(); $流.Dispose()

# 校验：直接解析 ICO 目录项 + 每张嵌入 PNG 的 IHDR，核对真实尺寸。
#
# 不要用 System.Drawing.Icon 来验！它请求 256 会返回 48（GDI+ 不肯返回 PNG 压缩的
# 256 档），会让人以为图标坏了。Windows 资源管理器和任务栏读得好好的。
# 006 那份老图标行为完全一样——它的脚本只是"打印了异常值但忘了置 false"，
# 所以一直报"通过"，属于校验本身失效。
$ok = $true
$字节 = [IO.File]::ReadAllBytes($出)
$项数 = [BitConverter]::ToUInt16($字节, 4)
if ($项数 -ne $尺寸.Count) { "  目录项数 $项数，期望 $($尺寸.Count)"; $ok = $false }
for ($i = 0; $i -lt $项数; $i++) {
    $o = 6 + $i * 16
    $记的宽 = if ($字节[$o] -eq 0) { 256 } else { [int]$字节[$o] }
    $长 = [BitConverter]::ToUInt32($字节, $o + 8)
    $偏 = [BitConverter]::ToUInt32($字节, $o + 12)

    if ($偏 + $长 -gt $字节.Length) { "  第 $i 项数据越界"; $ok = $false; continue }
    # PNG 签名
    if ($字节[$偏] -ne 0x89 -or $字节[$偏 + 1] -ne 0x50) { "  第 $i 项不是 PNG"; $ok = $false; continue }
    # IHDR 里的宽高是大端，位于 PNG 起点 +16 / +20
    $真宽 = [BitConverter]::ToInt32(($字节[($偏 + 16)..($偏 + 19)][3..0]), 0)
    $真高 = [BitConverter]::ToInt32(($字节[($偏 + 20)..($偏 + 23)][3..0]), 0)
    if ($真宽 -ne $记的宽 -or $真高 -ne $记的宽) {
        "  第 $i 项：目录说 $记的宽，实际 ${真宽}x${真高}"; $ok = $false
    }
}
"图标：$出  $([math]::Round((Get-Item $出).Length / 1kb)) KB  四尺寸($($尺寸 -join '/'))校验：$(if ($ok) { '通过' } else { '有问题' })"
