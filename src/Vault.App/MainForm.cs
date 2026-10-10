// 密码库 —— 本地密码管理器
// Copyright (C) 2026 ZhangGe
//
// This program is free software: you can redistribute it and/or modify it
// under the terms of the GNU General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your
// option) any later version. This program is distributed WITHOUT ANY
// WARRANTY; see the GNU General Public License for more details.
// <https://www.gnu.org/licenses/>

using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using 密码库.核心;

namespace 密码库.界面;

/// <summary>主窗口：搜索 + 列表 + 一列动作。</summary>
public sealed class 主窗 : Form
{
    private readonly 库 _库;
    private readonly 设置 _设置;

    private readonly TextBox _搜索 = new() { Font = 界面元素.常规, Dock = DockStyle.Fill };
    private readonly ListView _列表 = new()
    {
        View = View.Details,
        FullRowSelect = true,
        MultiSelect = false,
        HideSelection = false,
        Dock = DockStyle.Fill,
        Font = 界面元素.常规,
    };
    private readonly Label _状态 = 界面元素.标签("", 颜色: 界面元素.灰);
    private readonly Label _计数 = 界面元素.标签("", 颜色: 界面元素.灰);
    private readonly System.Windows.Forms.Timer _闲置计时 = new() { Interval = 20_000 };
    private DateTime _上次活动 = DateTime.Now;

    public 主窗(库 库对象, 设置 设置对象)
    {
        _库 = 库对象;
        _设置 = 设置对象;
        剪贴板.几秒后清 = _设置.剪贴板清除秒;

        Text = $"密码库 {界面元素.版本}".TrimEnd();
        Icon = 界面元素.程序图标;
        Font = 界面元素.常规;
        AutoScaleMode = AutoScaleMode.Font;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(this.尺(760), this.尺(480));
        ClientSize = new Size(
            _设置.窗宽 > 0 ? _设置.窗宽 : this.尺(940),
            _设置.窗高 > 0 ? _设置.窗高 : this.尺(600));

        _列表.Columns.Add("标题");
        _列表.Columns.Add("用户名");
        _列表.Columns.Add("分类");
        _列表.Columns.Add("网址");
        _列表.Columns.Add("更新");
        _列表.DoubleClick += (_, _) => 改这条();
        _列表.KeyDown += 列表按键;
        // 列宽按比例分，不写死：写死的话默认窗口就正好装不下，会冒出横向滚动条把「更新」列裁掉
        _列表.Resize += (_, _) => 排列列宽();

        _搜索.TextChanged += (_, _) => 刷新列表();

        var 外 = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(this.尺(12)),
        };
        外.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        外.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        外.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        外.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        外.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var 搜索行 = new TableLayoutPanel { ColumnCount = 3, Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        搜索行.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        搜索行.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        搜索行.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        搜索行.Controls.Add(界面元素.标签("搜索"), 0, 0);
        搜索行.Controls.Add(_搜索, 1, 0);
        搜索行.Controls.Add(_计数, 2, 0);

        外.Controls.Add(搜索行, 0, 0);
        外.SetColumnSpan(搜索行, 2);
        外.Controls.Add(_列表, 0, 1);
        外.Controls.Add(右边按钮(), 1, 1);
        外.Controls.Add(_状态, 0, 2);
        外.SetColumnSpan(_状态, 2);

        Controls.Add(外);

        _闲置计时.Tick += (_, _) => 查闲置();
        _闲置计时.Start();

        排列列宽();
        刷新列表();
        说("双击一条可以编辑。Ctrl+C 复制密码，Ctrl+Shift+C 复制用户名。");
    }

    private Control 右边按钮()
    {
        var 列 = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(this.尺(12), 0, 0, 0),
        };
        void 加(string 文, EventHandler 点, bool 主 = false)
        {
            var b = 界面元素.按钮(文, 点, 主);
            b.Width = this.尺(140);
            b.AutoSize = false;
            b.Height = this.尺(30);
            列.Controls.Add(b);
        }
        void 隔() => 列.Controls.Add(new Label { Height = this.尺(10), Width = this.尺(140), Margin = new Padding(0) });

        加("新建条目", (_, _) => 新建一条(), 主: true);
        加("编辑", (_, _) => 改这条());
        加("复制密码", (_, _) => 复制密码());
        加("复制用户名", (_, _) => 复制用户名());
        加("打开网址", (_, _) => 打开网址());
        加("删除", (_, _) => 删这条());
        隔();
        加("生成密码", (_, _) =>
        {
            var p = 生成窗.来一个(this);
            if (p != null) 说(剪贴板.放上去(p, "生成的密码"));
        });
        隔();
        加("锁定", (_, _) => 锁定());
        加("改主密码", (_, _) => 改主密码());
        加("导出…", (_, _) => 导出一份());
        return 列;
    }

    // ---------- 列表 ----------

    /// <summary>
    /// 按比例分配列宽，让五列正好填满列表可视区。
    /// <para>
    /// 不写死像素值：写死的话五列合计会超出默认窗口的可用宽度，底下冒出横向滚动条，
    /// 「更新」列被裁得只剩「20」。用 ClientSize.Width（已经扣掉纵向滚动条）算，
    /// 再留几个像素余量，缩放到任何 DPI、任何窗口大小都不会溢出。
    /// </para>
    /// </summary>
    private void 排列列宽()
    {
        var 可用 = _列表.ClientSize.Width - this.尺(4);
        if (可用 <= 0 || _列表.Columns.Count == 0) return;

        // 标题最宽（列表就是靠它找东西的），更新日期最窄
        double[] 占比 = [0.30, 0.21, 0.13, 0.24, 0.12];
        var 最后 = _列表.Columns.Count - 1;

        // 包在 BeginUpdate/EndUpdate 里：ListView 改完列宽不会自己重算滚动范围，
        // 结果是列明明装得下、底下还挂着一条横向滚动条。EndUpdate 会强制它重算。
        _列表.BeginUpdate();
        try
        {
            var 已分 = 0;
            for (var i = 0; i < 最后; i++)
            {
                var 宽 = (int)Math.Round(可用 * 占比[i]);
                _列表.Columns[i].Width = 宽;
                已分 += 宽;
            }
            // 最后一列吃掉四舍五入的零头，保证总和不多不少
            _列表.Columns[最后].Width = Math.Max(可用 - 已分, this.尺(60));
        }
        finally
        {
            _列表.EndUpdate();
        }
    }

    /// <summary>
    /// 布局结束后再排列宽。放在这里而不是只挂 Resize：Resize 触发时子控件的几何
    /// 还可能是上一轮的，按旧宽度分完列就会超出新的可视区，又把滚动条招回来。
    /// </summary>
    protected override void OnLayout(LayoutEventArgs 事件)
    {
        base.OnLayout(事件);
        排列列宽();
    }

    private void 刷新列表(string? 选中编号 = null)
    {
        选中编号 ??= 选中的()?.编号;
        _列表.BeginUpdate();
        _列表.Items.Clear();
        var 词 = _搜索.Text;
        foreach (var e in _库.条目表.Where(e => e.命中(词)))
        {
            var 项 = new ListViewItem(e.标题) { Tag = e };
            项.SubItems.Add(e.用户名);
            项.SubItems.Add(e.分类);
            项.SubItems.Add(e.网址);
            项.SubItems.Add(e.更新.ToString("yyyy-MM-dd"));
            _列表.Items.Add(项);
            if (e.编号 == 选中编号) { 项.Selected = true; 项.EnsureVisible(); }
        }
        _列表.EndUpdate();

        var 总数 = _库.条目表.Count;
        var 显示 = _列表.Items.Count;
        _计数.Text = 总数 == 显示 ? $"共 {总数} 条" : $"{显示} / {总数} 条";
        if (总数 == 0) 说("库是空的。点「新建条目」加第一条。");
    }

    private 条目? 选中的()
        => _列表.SelectedItems.Count > 0 ? _列表.SelectedItems[0].Tag as 条目 : null;

    private 条目? 要选中()
    {
        var e = 选中的();
        if (e == null) 说("先在左边选中一条。", 警告: true);
        return e;
    }

    private void 列表按键(object? 发送者, KeyEventArgs 事件)
    {
        if (事件.Control && 事件.Shift && 事件.KeyCode == Keys.C) { 复制用户名(); 事件.Handled = true; }
        else if (事件.Control && 事件.KeyCode == Keys.C) { 复制密码(); 事件.Handled = true; }
        else if (事件.KeyCode == Keys.Delete) { 删这条(); 事件.Handled = true; }
        else if (事件.KeyCode == Keys.Enter) { 改这条(); 事件.Handled = true; }
    }

    // ---------- 动作 ----------

    private void 新建一条()
    {
        记活动();
        var 新的 = 条目窗.编辑(this, new 条目(), _库.全部分类());
        if (新的 == null) return;
        _库.放入(新的);
        存盘();
        刷新列表(新的.编号);
        说($"已存入「{新的.标题}」。");
    }

    private void 改这条()
    {
        记活动();
        var e = 要选中(); if (e == null) return;
        var 改后 = 条目窗.编辑(this, e, _库.全部分类());
        if (改后 == null) return;
        _库.放入(改后);
        存盘();
        刷新列表(改后.编号);
        说($"已保存「{改后.标题}」。");
    }

    private void 删这条()
    {
        记活动();
        var e = 要选中(); if (e == null) return;
        if (!界面元素.问(
            $"确定删掉「{e.标题}」？\n\n删了找不回来。\n" +
            "如果这是某台机器唯一的密钥，删之前先确认你还有别的进得去的办法。",
            "删除条目")) return;
        _库.删除(e.编号);
        存盘();
        刷新列表();
        说($"已删除「{e.标题}」。备份还留在 备份\\ 里。");
    }

    private void 复制密码()
    {
        记活动();
        var e = 要选中(); if (e == null) return;
        if (e.密码.Length == 0) { 说("这条没有密码字段。内容可能在「附加」里，编辑打开看。", 警告: true); return; }
        说(剪贴板.放上去(e.密码, "密码"));
    }

    private void 复制用户名()
    {
        记活动();
        var e = 要选中(); if (e == null) return;
        if (e.用户名.Length == 0) { 说("这条没填用户名。", 警告: true); return; }
        说(剪贴板.放上去(e.用户名, "用户名"));
    }

    private void 打开网址()
    {
        记活动();
        var e = 要选中(); if (e == null) return;
        var 网址 = e.网址.Trim();
        if (网址.Length == 0) { 说("这条没填网址。", 警告: true); return; }
        if (!网址.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !网址.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            网址 = "https://" + 网址;
        try
        {
            Process.Start(new ProcessStartInfo(网址) { UseShellExecute = true });
            说($"已在浏览器打开 {网址}");
        }
        catch (Exception 错)
        {
            说($"打不开：{错.Message}", 警告: true);
        }
    }

    private void 改主密码()
    {
        记活动();
        var 旧 = 密码窗.问一次(this, "改主密码", "先输入当前的主密码：");
        if (旧 == null) return;
        if (!_库.主密码对吗(旧)) { 界面元素.警告("当前主密码不对。"); return; }

        var 新 = 密码窗.问一次(this, "改主密码", "设置新的主密码。整个库会用新密码重新加密。", 二次确认: true);
        if (新 == null) { 说("没改。"); return; }
        try
        {
            _库.换主密码(新);
            说("主密码已更换，整库已重新加密。旧库的备份在 备份\\ 里，用新密码验证没问题后可以删掉。");
        }
        catch (Exception 错)
        {
            界面元素.警告($"改主密码失败：{错.Message}");
        }
    }

    private void 导出一份()
    {
        记活动();
        if (!界面元素.问(
            "导出的文件是**明文**的，谁拿到谁就能看全部密码。\n\n" +
            "它的用途是搬家和离线备份——用完请立刻删掉，或者放进加密盘。\n\n继续？",
            "导出前请确认")) return;

        var 密码 = 密码窗.问一次(this, "导出", "再输一次主密码确认是你本人：");
        if (密码 == null) return;
        if (!_库.主密码对吗(密码)) { 界面元素.警告("主密码不对。"); return; }

        using var 对话 = new SaveFileDialog
        {
            Title = "导出（明文！）",
            Filter = "JSON（能再导回来）|*.json|CSV（Excel / 其它密码软件）|*.csv",
            FileName = $"密码库导出-{DateTime.Now:yyyyMMdd}",
        };
        if (对话.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            if (对话.FilterIndex == 2) 导出.存成CSV(_库.条目表, 对话.FileName);
            else 导出.存成JSON(_库.条目表, 对话.FileName);
            说($"已导出到 {对话.FileName}（明文，权限已收紧。用完记得删）。");
        }
        catch (Exception 错)
        {
            界面元素.警告($"导出失败：{错.Message}");
        }
    }

    private void 存盘()
    {
        try { _库.保存(); }
        catch (Exception 错) { 界面元素.警告($"存盘失败：{错.Message}\n\n改动还在内存里，先别关窗口。"); }
    }

    // ---------- 锁定 ----------

    private void 查闲置()
    {
        if ((DateTime.Now - _上次活动).TotalMinutes >= _设置.自动锁定分钟)
        {
            锁定(自动: true);
        }
    }

    private void 记活动() => _上次活动 = DateTime.Now;

    protected override void OnActivated(EventArgs 事件)
    {
        base.OnActivated(事件);
        记活动();
    }

    /// <summary>
    /// 关窗的原因是「锁定」而不是「退出」。程序据此决定回到解锁界面还是收工。
    /// </summary>
    public bool 要重新解锁 { get; private set; }

    /// <summary>自动锁定时，给解锁界面带一句话说明为什么被锁了。手动锁定为 null。</summary>
    public string? 锁定原因 { get; private set; }

    /// <summary>
    /// 锁定：关掉主窗，让程序把库 Dispose 掉（密钥当场抹零），然后回到输主密码的界面。
    /// <para>
    /// 不在内存里维持一个"锁住的"状态——那样密钥还在进程里。这里是真的把它扔了，
    /// 解锁时重新从主密码派生。代价是要重算一次 60 万轮 PBKDF2，值得。
    /// </para>
    /// </summary>
    private void 锁定(bool 自动 = false)
    {
        _闲置计时.Stop();
        要重新解锁 = true;
        // 别用底部状态栏说这句：窗口紧接着就关了，那行字用户根本看不到。
        // 交给解锁界面去显示。
        锁定原因 = 自动 ? $"闲置超过 {_设置.自动锁定分钟} 分钟，已自动锁定。" : null;
        Close();
    }

    private void 说(string 文, bool 警告 = false)
    {
        _状态.Text = 文;
        _状态.ForeColor = 警告 ? 界面元素.红 : 界面元素.灰;
    }

    protected override void OnFormClosing(FormClosingEventArgs 事件)
    {
        _设置.窗宽 = ClientSize.Width;
        _设置.窗高 = ClientSize.Height;
        _设置.保存();
        剪贴板.清掉();
        base.OnFormClosing(事件);
    }
}
