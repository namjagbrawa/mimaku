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

/// <summary>密码生成器。</summary>
public sealed class 生成窗 : Form
{
    private readonly TextBox _结果 = new()
    {
        Font = 界面元素.等宽,
        Dock = DockStyle.Fill,
        ReadOnly = true,
        BackColor = Color.White,
        Margin = new Padding(0, 0, 0, 6),
    };

    private readonly NumericUpDown _长度 = new() { Minimum = 8, Maximum = 128, Value = 20, Font = 界面元素.常规 };
    private readonly CheckBox _小写 = new() { Text = "小写 a-z", Checked = true, AutoSize = true, Font = 界面元素.常规 };
    private readonly CheckBox _大写 = new() { Text = "大写 A-Z", Checked = true, AutoSize = true, Font = 界面元素.常规 };
    private readonly CheckBox _数字 = new() { Text = "数字 0-9", Checked = true, AutoSize = true, Font = 界面元素.常规 };
    private readonly CheckBox _符号 = new() { Text = "符号 !@#$", Checked = true, AutoSize = true, Font = 界面元素.常规 };
    private readonly CheckBox _避混 = new() { Text = "避开容易看错的字符（I O l 0 o 1）", AutoSize = true, Font = 界面元素.常规 };
    private readonly Label _强度 = 界面元素.标签("", 颜色: 界面元素.灰);

    public string 密码 => _结果.Text;

    public 生成窗()
    {
        Text = "生成密码";
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

        表.Controls.Add(_结果);
        表.Controls.Add(_强度);

        var 长度行 = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, this.尺(6), 0, 0) };
        长度行.Controls.Add(界面元素.标签("长度"));
        长度行.Controls.Add(_长度);
        表.Controls.Add(长度行);

        foreach (var 勾 in new[] { _小写, _大写, _数字, _符号, _避混 })
        {
            勾.CheckedChanged += (_, _) => 重生成();
            表.Controls.Add(勾);
        }
        _长度.ValueChanged += (_, _) => 重生成();

        var 钮条 = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, this.尺(10), 0, 0),
        };
        var 取消 = 界面元素.按钮("取消", (_, _) => { DialogResult = DialogResult.Cancel; Close(); });
        var 用它 = 界面元素.按钮("用这个", (_, _) => { DialogResult = DialogResult.OK; Close(); }, 主要: true);
        var 再来 = 界面元素.按钮("换一个", (_, _) => 重生成());
        钮条.Controls.Add(取消);
        钮条.Controls.Add(用它);
        钮条.Controls.Add(再来);
        表.Controls.Add(钮条);

        Controls.Add(表);
        AcceptButton = 用它;
        CancelButton = 取消;
        ClientSize = new Size(this.尺(420), 表.PreferredSize.Height);
        重生成();
    }

    private void 重生成()
    {
        try
        {
            _结果.Text = 密码生成.生成(new 密码生成.选项
            {
                长度 = (int)_长度.Value,
                要小写 = _小写.Checked,
                要大写 = _大写.Checked,
                要数字 = _数字.Checked,
                要符号 = _符号.Checked,
                避开易混 = _避混.Checked,
            });
            var 熵 = 密码生成.估算熵(_结果.Text);
            _强度.Text = $"强度：{密码生成.强度说法(熵)}（约 {熵:F0} bit）";
            _强度.ForeColor = 熵 >= 90 ? 界面元素.绿 : 熵 >= 65 ? 界面元素.灰 : 界面元素.红;
        }
        catch (ArgumentException 错)
        {
            _结果.Text = "";
            _强度.Text = 错.Message;
            _强度.ForeColor = 界面元素.红;
        }
    }

    /// <summary>弹一次，返回生成的密码；取消返回 null。</summary>
    public static string? 来一个(IWin32Window? 父)
    {
        using var 窗 = new 生成窗();
        return 窗.ShowDialog(父) == DialogResult.OK && 窗.密码.Length > 0 ? 窗.密码 : null;
    }
}
