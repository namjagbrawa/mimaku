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

/// <summary>输主密码的小窗。设新密码时要求输两遍。</summary>
public sealed class 密码窗 : Form
{
    private readonly TextBox _框1 = 界面元素.文本框(遮住: true);
    private readonly TextBox? _框2;
    private readonly bool _二次确认;

    public string 密码 => _框1.Text;

    public 密码窗(string 标题, string 说明, bool 二次确认 = false)
    {
        _二次确认 = 二次确认;
        Text = 标题;
        Font = 界面元素.常规;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Font;

        var 表 = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(this.尺(16)),
        };

        var 说明标 = 界面元素.标签(说明);
        说明标.MaximumSize = new Size(this.尺(400), 0);
        表.Controls.Add(说明标);
        表.Controls.Add(_框1);

        if (二次确认)
        {
            _框2 = 界面元素.文本框(遮住: true);
            表.Controls.Add(界面元素.标签("再输一次确认"));
            表.Controls.Add(_框2);
        }

        var 钮条 = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, this.尺(8), 0, 0),
        };
        var 取消 = 界面元素.按钮("取消", (_, _) => { DialogResult = DialogResult.Cancel; Close(); });
        var 确定 = 界面元素.按钮("确定", (_, _) => 试着确定(), 主要: true);
        钮条.Controls.Add(取消);
        钮条.Controls.Add(确定);
        表.Controls.Add(钮条);

        Controls.Add(表);
        AcceptButton = 确定;
        CancelButton = 取消;
        ClientSize = new Size(this.尺(430), 表.PreferredSize.Height);
        _框1.Select();
    }

    private void 试着确定()
    {
        if (string.IsNullOrEmpty(_框1.Text))
        {
            界面元素.警告("主密码不能为空。");
            return;
        }
        if (_二次确认)
        {
            if (!string.Equals(_框1.Text, _框2!.Text, StringComparison.Ordinal))
            {
                界面元素.警告("两次输入不一致，重来。");
                _框1.Clear(); _框2.Clear(); _框1.Select();
                return;
            }
            var 熵 = 密码生成.估算熵(_框1.Text);
            if (_框1.Text.Length < 12 || 熵 < 60)
            {
                var 接着 = 界面元素.问(
                    $"这个主密码强度是「{密码生成.强度说法(熵)}」（{_框1.Text.Length} 位）。\n\n" +
                    "它是整个库唯一的锁，而且没有找回的办法。建议用一句好记的长句子，" +
                    "比办公室里那种「大小写数字符号」的短密码强得多。\n\n仍然使用这个密码？",
                    "主密码偏弱");
                if (!接着) { _框1.Clear(); _框2.Clear(); _框1.Select(); return; }
            }
        }
        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void Dispose(bool 释放托管)
    {
        if (释放托管)
        {
            _框1.Clear();
            _框2?.Clear();
        }
        base.Dispose(释放托管);
    }

    /// <summary>弹一次，返回输入的密码；点取消返回 null。</summary>
    public static string? 问一次(IWin32Window? 父, string 标题, string 说明, bool 二次确认 = false)
    {
        using var 窗 = new 密码窗(标题, 说明, 二次确认);
        return 窗.ShowDialog(父) == DialogResult.OK ? 窗.密码 : null;
    }
}
