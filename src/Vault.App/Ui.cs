// 密码库 —— 本地密码管理器
// Copyright (C) 2026 ZhangGe
//
// This program is free software: you can redistribute it and/or modify it
// under the terms of the GNU General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your
// option) any later version. This program is distributed WITHOUT ANY
// WARRANTY; see the GNU General Public License for more details.
// <https://www.gnu.org/licenses/>

using System.Drawing;
using System.Windows.Forms;

namespace 密码库.界面;

/// <summary>界面上反复用到的字体、颜色和几个小工厂。</summary>
internal static class 界面元素
{
    public static readonly Font 常规 = 挑字体("Microsoft YaHei UI", "Segoe UI", 9.75f, FontStyle.Regular);
    public static readonly Font 粗体 = new(常规, FontStyle.Bold);
    public static readonly Font 等宽 = 挑字体("Consolas", "Courier New", 10.5f, FontStyle.Regular);
    public static readonly Font 小字 = 挑字体("Microsoft YaHei UI", "Segoe UI", 8.5f, FontStyle.Regular);

    public static readonly Color 灰 = Color.FromArgb(110, 110, 110);
    public static readonly Color 红 = Color.FromArgb(180, 40, 40);
    public static readonly Color 绿 = Color.FromArgb(40, 120, 60);
    public static readonly Color 淡底 = Color.FromArgb(246, 246, 246);

    /// <summary>
    /// 程序图标。<c>ApplicationIcon</c> 只设置 exe 的 Win32 图标资源（资源管理器和任务栏用那个），
    /// **窗体标题栏不会自动用它**——<see cref="Form.Icon"/> 不赋值就是 WinForms 的通用图标。
    /// 所以把同一个 .ico 嵌成资源，在这里读出来给各窗体用。
    /// 读不到就返回 null（用通用图标），不值得为此让程序打不开。
    /// </summary>
    public static readonly Icon? 程序图标 = 读图标();

    /// <summary>
    /// 程序版本，形如 "1.0.0"。显示在标题栏——**「我手上跑的是哪一版」必须一眼看得见**，
    /// 否则拷出去的副本和项目里的新版本没法对账。取不到就返回空串。
    /// </summary>
    public static string 版本 => typeof(界面元素).Assembly.GetName().Version is { } v
        ? $"{v.Major}.{v.Minor}.{v.Build}"
        : "";

    private static Icon? 读图标()
    {
        try
        {
            var 程序集 = typeof(界面元素).Assembly;
            var 名 = 程序集.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(".ico", StringComparison.OrdinalIgnoreCase));
            if (名 == null) return null;
            using var 流 = 程序集.GetManifestResourceStream(名);
            return 流 == null ? null : new Icon(流);
        }
        catch
        {
            return null;
        }
    }

    private static Font 挑字体(string 首选, string 备选, float 磅, FontStyle 样式)
    {
        try
        {
            var f = new Font(首选, 磅, 样式);
            // 字体不存在时 GDI+ 会悄悄换成别的，这里核对一下真拿到的是不是首选
            if (string.Equals(f.Name, 首选, StringComparison.OrdinalIgnoreCase)) return f;
            f.Dispose();
        }
        catch { }
        return new Font(备选, 磅, 样式);
    }

    /// <summary>
    /// 按当前窗口的 DPI 换算像素。
    /// 布局尽量交给 TableLayoutPanel / Dock / AutoSize 去做（那些跟着字体走，天然适配 DPI），
    /// 只有确实要写死的尺寸才用这个。
    /// </summary>
    public static int 尺(this Control 控件, int 九六DPI下的值)
        => (int)Math.Round(九六DPI下的值 * 控件.DeviceDpi / 96.0);

    public static Label 标签(string 文, bool 粗 = false, Color? 颜色 = null)
        => new()
        {
            Text = 文,
            AutoSize = true,
            Font = 粗 ? 粗体 : 常规,
            ForeColor = 颜色 ?? SystemColors.ControlText,
            Margin = new Padding(0, 6, 0, 2),
        };

    public static Button 按钮(string 文, EventHandler 点击, bool 主要 = false)
    {
        var b = new Button
        {
            Text = 文,
            Font = 主要 ? 粗体 : 常规,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlatStyle = FlatStyle.System,
            Margin = new Padding(0, 0, 0, 6),
        };
        b.Click += 点击;
        return b;
    }

    public static TextBox 文本框(string 文 = "", bool 遮住 = false)
        => new()
        {
            Text = 文,
            Font = 常规,
            UseSystemPasswordChar = 遮住,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 8),
        };

    public static void 提示(string 文, string 标题 = "密码库")
        => MessageBox.Show(文, 标题, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static void 警告(string 文, string 标题 = "密码库")
        => MessageBox.Show(文, 标题, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static bool 问(string 文, string 标题 = "确认")
        => MessageBox.Show(文, 标题, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
}
