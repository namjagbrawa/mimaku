// 密码库 —— 本地密码管理器
// Copyright (C) 2026 ZhangGe
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Drawing;
using System.Windows.Forms;

namespace 密码库.界面;

/// <summary>每次启动都能选择收到的副本，不依赖已保存的个人库路径。</summary>
public sealed class 启动窗 : Form
{
    public string? 选定路径 { get; private set; }
    public bool 要新建 { get; private set; }

    public 启动窗(string 上次路径)
    {
        Text = $"密码库 {界面元素.版本}";
        Icon = 界面元素.程序图标;
        Font = 界面元素.常规;
        AutoScaleMode = AutoScaleMode.Font;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        var 表 = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(this.尺(16)),
        };
        var 提示 = 界面元素.标签("收到别人分享的密码库或需要恢复备份时，选择“打开已有库”，再输入那份文件的口令。");
        提示.MaximumSize = new Size(this.尺(480), 0);
        表.Controls.Add(提示);
        var 路径 = 界面元素.标签($"上次的库：{上次路径}", 颜色: 界面元素.灰);
        路径.MaximumSize = new Size(this.尺(480), 0);
        表.Controls.Add(路径);
        var 继续 = 界面元素.按钮("继续上次", (_, _) => 完成(上次路径), 主要: true);
        继续.Enabled = File.Exists(上次路径);
        var 打开 = 界面元素.按钮("打开已有库…", (_, _) =>
        {
            using var 对话 = new OpenFileDialog
            {
                Title = "打开收到的密码库或已有备份",
                Filter = "密码库文件|*.vault;*.enc|所有文件|*.*",
                CheckFileExists = true, Multiselect = false,
            };
            if (对话.ShowDialog(this) == DialogResult.OK) 完成(对话.FileName);
        });
        var 新建 = 界面元素.按钮("新建密码库", (_, _) => { 要新建 = true; 完成(null); });
        var 取消 = 界面元素.按钮("退出", (_, _) => { DialogResult = DialogResult.Cancel; Close(); });
        foreach (var 按钮 in new[] { 继续, 打开, 新建, 取消 })
        {
            按钮.AccessibleName = 按钮.Text;
            按钮.Dock = DockStyle.Fill;
            表.Controls.Add(按钮);
        }
        Controls.Add(表);
        AcceptButton = 继续.Enabled ? 继续 : 打开;
        CancelButton = 取消;
        ClientSize = new Size(this.尺(520), 表.PreferredSize.Height);
    }

    private void 完成(string? 路径)
    {
        选定路径 = 路径;
        DialogResult = DialogResult.OK;
        Close();
    }
}
