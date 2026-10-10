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
using 密码库.核心;

namespace 密码库.界面;

/// <summary>
/// 新建 / 编辑一条记录。
/// 多行内容（PEM 私钥、恢复码）就靠下面那个等宽大框，整段粘进来。
/// </summary>
public sealed class 条目窗 : Form
{
    private readonly 条目 _条;
    private readonly TextBox _标题 = 界面元素.文本框();
    private readonly ComboBox _分类;
    private readonly TextBox _用户名 = 界面元素.文本框();
    private readonly TextBox _密码 = 界面元素.文本框(遮住: true);
    private readonly TextBox _网址 = 界面元素.文本框();
    private readonly TextBox _备注 = new()
    {
        Font = 界面元素.常规,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
    };
    private readonly TextBox _附加 = new()
    {
        Font = 界面元素.等宽,
        Multiline = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = false,
        AcceptsReturn = true,
        AcceptsTab = true,
        Dock = DockStyle.Fill,
    };
    private readonly Label _附加信息 = 界面元素.标签("", 颜色: 界面元素.灰);
    private readonly string _原密码;

    /// <summary>编辑完的结果。点了保存才有意义。</summary>
    public 条目 结果 => _条;

    public 条目窗(条目 待编辑, IEnumerable<string> 已有分类)
    {
        _条 = 待编辑.复制();
        _原密码 = _条.密码;

        Text = string.IsNullOrEmpty(_条.标题) ? "新建条目" : $"编辑：{_条.标题}";
        Icon = 界面元素.程序图标;
        Font = 界面元素.常规;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        MinimumSize = new Size(this.尺(560), this.尺(520));

        _分类 = new ComboBox
        {
            Font = 界面元素.常规,
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDown,
            Margin = new Padding(0, 0, 0, 8),
        };
        _分类.Items.AddRange(已有分类.Cast<object>().ToArray());

        _标题.Text = _条.标题;
        _分类.Text = _条.分类;
        _用户名.Text = _条.用户名;
        _密码.Text = _条.密码;
        _网址.Text = _条.网址;
        _备注.Text = 转CRLF(_条.备注);
        _附加.Text = 转CRLF(_条.附加);

        var 表 = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(this.尺(16)),
        };
        // 行高：标签自动，输入框自动，两个多行框按比例吃掉剩余空间
        void 加(Control c, SizeType 类型 = SizeType.AutoSize, float 值 = 0)
        {
            表.RowStyles.Add(new RowStyle(类型, 值));
            表.Controls.Add(c);
        }

        加(界面元素.标签("标题"));
        加(_标题);
        加(界面元素.标签("分类（可留空）"));
        加(_分类);
        加(界面元素.标签("用户名"));
        加(_用户名);

        加(界面元素.标签("密码"));
        加(密码那一行());

        加(界面元素.标签("网址"));
        加(_网址);

        加(界面元素.标签("备注"));
        加(_备注, SizeType.Percent, 30f);

        加(附加那一行());
        加(_附加, SizeType.Percent, 70f);

        加(底部按钮());

        Controls.Add(表);
        ClientSize = new Size(this.尺(660), this.尺(620));

        _附加.TextChanged += (_, _) => 刷新附加信息();
        刷新附加信息();
        _标题.Select();
    }

    private Control 密码那一行()
    {
        var 行 = new TableLayoutPanel
        {
            ColumnCount = 4,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 8),
        };
        行.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        for (int i = 0; i < 3; i++) 行.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var 看 = new CheckBox { Text = "显示", AutoSize = true, Font = 界面元素.常规, Margin = new Padding(this.尺(6), this.尺(3), 0, 0) };
        看.CheckedChanged += (_, _) => _密码.UseSystemPasswordChar = !看.Checked;

        var 生成 = 界面元素.按钮("生成…", (_, _) =>
        {
            var 新的 = 生成窗.来一个(this);
            if (新的 != null) { _密码.Text = 新的; 看.Checked = true; }
        });
        var 复制 = 界面元素.按钮("复制", (_, _) =>
        {
            if (_密码.Text.Length > 0) 剪贴板.放上去(_密码.Text);
        });

        _密码.Margin = new Padding(0);
        行.Controls.Add(_密码, 0, 0);
        行.Controls.Add(看, 1, 0);
        行.Controls.Add(生成, 2, 0);
        行.Controls.Add(复制, 3, 0);
        return 行;
    }

    private Control 附加那一行()
    {
        var 行 = new TableLayoutPanel
        {
            ColumnCount = 3,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };
        行.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        行.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        行.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var 导入 = 界面元素.按钮("从文件导入…", (_, _) => 从文件读());
        行.Controls.Add(界面元素.标签("附加内容（多行，比如 PEM 私钥、恢复码）"), 0, 0);
        行.Controls.Add(_附加信息, 1, 0);
        行.Controls.Add(导入, 2, 0);
        return 行;
    }

    private Control 底部按钮()
    {
        var 条 = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, this.尺(10), 0, 0),
        };
        var 取消 = 界面元素.按钮("取消", (_, _) => { DialogResult = DialogResult.Cancel; Close(); });
        var 保存 = 界面元素.按钮("保存", (_, _) => 试着保存(), 主要: true);
        条.Controls.Add(取消);
        条.Controls.Add(保存);
        CancelButton = 取消;
        return 条;
    }

    private void 从文件读()
    {
        using var 对话 = new OpenFileDialog
        {
            Title = "选一个文本文件",
            Filter = "密钥和文本文件|*.pem;*.key;*.txt;*.md;*.pub|所有文件|*.*",
        };
        if (对话.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var 内容 = File.ReadAllText(对话.FileName);
            if (内容.Contains('\0'))
            {
                界面元素.警告("这看起来是二进制文件（.pfx / .p12 / .jks 之类），本程序只存文本。");
                return;
            }
            _附加.Text = 转CRLF(内容);
            if (_标题.Text.Length == 0)
                _标题.Text = Path.GetFileNameWithoutExtension(对话.FileName);
        }
        catch (Exception 错)
        {
            界面元素.警告($"读不了这个文件：{错.Message}");
        }
    }

    private void 刷新附加信息()
    {
        var 文 = _附加.Text;
        _附加信息.Text = 文.Length == 0
            ? ""
            : $"{文.Split('\n').Length} 行 / {文.Length} 字符";
    }

    private void 试着保存()
    {
        if (string.IsNullOrWhiteSpace(_标题.Text))
        {
            界面元素.警告("标题不能为空——列表里就是靠它找东西的。");
            _标题.Select();
            return;
        }

        _条.标题 = _标题.Text.Trim();
        _条.分类 = _分类.Text.Trim();
        _条.用户名 = _用户名.Text.Trim();
        _条.密码 = _密码.Text;
        _条.网址 = _网址.Text.Trim();
        _条.备注 = 转LF(_备注.Text);
        _条.附加 = 转LF(_附加.Text);

        if (_条.密码 != _原密码) _条.密码更新 = DateTimeOffset.Now;

        DialogResult = DialogResult.OK;
        Close();
    }

    // 库里一律存 LF；WinForms 的多行框只认 CRLF，不转的话 PEM 会挤成一行。
    private static string 转LF(string 文) => 文.Replace("\r\n", "\n").Replace("\r", "\n");
    private static string 转CRLF(string 文) => 转LF(文).Replace("\n", "\r\n");

    /// <summary>弹一次，返回编辑后的条目；取消返回 null。</summary>
    public static 条目? 编辑(IWin32Window? 父, 条目 待编辑, IEnumerable<string> 已有分类)
    {
        using var 窗 = new 条目窗(待编辑, 已有分类);
        return 窗.ShowDialog(父) == DialogResult.OK ? 窗.结果 : null;
    }
}
