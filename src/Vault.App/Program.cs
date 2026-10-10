// 密码库 —— 本地密码管理器
// Copyright (C) 2026 ZhangGe
//
// This program is free software: you can redistribute it and/or modify it
// under the terms of the GNU General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your
// option) any later version. This program is distributed WITHOUT ANY
// WARRANTY; see the GNU General Public License for more details.
// <https://www.gnu.org/licenses/>

using System.Windows.Forms;
using 密码库.核心;

namespace 密码库.界面;

internal static class 程序
{
    [STAThread]
    private static void Main(string[] 参数)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var 设置对象 = 设置.读取();
        // 命令行给了路径就用它，方便同时管多个库
        if (参数.Length > 0 && !string.IsNullOrWhiteSpace(参数[0]))
            设置对象.库路径 = Path.GetFullPath(参数[0]);
        if (string.IsNullOrWhiteSpace(设置对象.库路径))
            设置对象.库路径 = 设置.默认库路径;

        // 锁定 → 解锁 → 再锁定…… 所以是个循环。
        // 锁定时主窗关闭、库被 Dispose（密钥当场抹掉），进程留着回到解锁界面；
        // 用户直接关窗口才是真的收工。
        string? 上次锁定原因 = null;
        while (true)
        {
            库? 打开的库 = null;
            try
            {
                打开的库 = 库.存在(设置对象.库路径)
                    ? 开一个已有的(设置对象, 上次锁定原因)
                    : 建一个新的(设置对象);
                if (打开的库 == null) return;   // 用户中途取消了

                设置对象.保存();
                var 窗 = new 主窗(打开的库, 设置对象);
                Application.Run(窗);

                if (!窗.要重新解锁) return;     // 是关窗口，不是锁定
                上次锁定原因 = 窗.锁定原因;
            }
            catch (Exception 错)
            {
                界面元素.警告($"出错了：{错.Message}");
                return;
            }
            finally
            {
                打开的库?.Dispose();   // 把内存里的密钥抹掉
            }
        }
    }

    private static 库? 开一个已有的(设置 设置对象, string? 开头的话 = null)
    {
        // 旧版（PowerShell 版）的库，先问要不要升级
        if (旧版.像是旧版(设置对象.库路径))
            return 升级旧库(设置对象);

        // 自动锁定回来时，先说清楚为什么被锁了——否则用户只看到窗口凭空变成了密码框
        var 前言 = string.IsNullOrEmpty(开头的话) ? "" : 开头的话 + "\n\n";

        while (true)
        {
            var 密码 = 密码窗.问一次(null, "解锁密码库",
                $"{前言}库：{设置对象.库路径}\n\n输入主密码。整个库都是加密的，不解锁看不到任何条目。");
            if (密码 == null) return null;
            try
            {
                return 库.打开(设置对象.库路径, 密码);
            }
            catch (打不开异常 错)
            {
                界面元素.警告(错.Message);
            }
        }
    }

    private static 库? 建一个新的(设置 设置对象)
    {
        var 要建 = MessageBox.Show(
            $"还没有密码库，现在建一个吗？\n\n位置：{设置对象.库路径}\n\n" +
            "下一步会让你设主密码。这个密码不存在任何地方——忘了，库里的东西谁也取不回来。",
            "第一次使用", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (要建 != DialogResult.Yes) return null;

        // 让用户自己挑地方：这文件以后装的是全部密码，得放在他知道、会备份的位置
        using (var 对话 = new SaveFileDialog
        {
            Title = "密码库文件放哪",
            Filter = "密码库|*.vault",
            FileName = Path.GetFileName(设置对象.库路径),
            InitialDirectory = Path.GetDirectoryName(设置对象.库路径),
            OverwritePrompt = true,
        })
        {
            if (对话.ShowDialog() != DialogResult.OK) return null;
            设置对象.库路径 = 对话.FileName;
        }

        var 密码 = 密码窗.问一次(null, "设置主密码",
            "这个库以后装的是你全部的密码和密钥。\n\n" +
            "主密码只存在你脑子里，没有找回的办法。建议用一句好记的长句子。", 二次确认: true);
        if (密码 == null) return null;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(设置对象.库路径)!);
            if (File.Exists(设置对象.库路径)) File.Delete(设置对象.库路径);
            var 新库 = 库.新建(设置对象.库路径, 密码);

            // 设密码那一步已经说过"没有找回办法"，但那时用户正忙着想密码，多半没往心里去。
            // 建完再拦一次：这是唯一能补救的时刻——密码刚输过，还记得。
            界面元素.提示(
                "库建好了。\n\n" +
                "现在请立刻把主密码记到一个安全的地方——纸质密码本、保险箱，" +
                "或者另一个你确定不会丢的地方。\n\n" +
                "它没有存在任何地方，也没有任何找回办法。忘了，库里的东西谁也取不回来。\n\n" +
                $"库文件：{设置对象.库路径}\n" +
                "（这个文件也请纳入你的备份范围。）",
                "记住你的主密码");
            return 新库;
        }
        catch (Exception 错)
        {
            界面元素.警告($"建库失败：{错.Message}");
            return null;
        }
    }

    /// <summary>把 PowerShell 版建的 v1 库转成 v2（整库加密）。原文件留着不动。</summary>
    private static 库? 升级旧库(设置 设置对象)
    {
        界面元素.提示(
            $"这个文件是旧版（PowerShell 版）的库：\n{设置对象.库路径}\n\n" +
            "旧版是逐条加密的，条目名和备注是明文。现在要把它转成新格式——整个库一起加密，" +
            "不解锁连有哪些条目都看不到。\n\n" +
            "原文件会原样留着，转换生成的是一个新文件。",
            "需要升级");

        var 密码 = 密码窗.问一次(null, "解锁旧库", "输入旧库的主密码：");
        if (密码 == null) return null;

        List<条目> 条目们;
        try
        {
            条目们 = 旧版.读出(设置对象.库路径, 密码);
        }
        catch (打不开异常 错)
        {
            界面元素.警告(错.Message);
            return null;
        }

        var 新路径 = Path.Combine(
            Path.GetDirectoryName(设置对象.库路径)!,
            Path.GetFileNameWithoutExtension(设置对象.库路径) + ".vault");
        if (File.Exists(新路径))
        {
            界面元素.警告($"目标文件已经存在了：{新路径}\n请先把它挪走。");
            return null;
        }

        var 新密码 = 密码窗.问一次(null, "设置主密码",
            $"读出了 {条目们.Count} 条。\n\n给新库设一个主密码（可以还用原来那个）：", 二次确认: true);
        if (新密码 == null) return null;

        try
        {
            var 新库 = 库.新建(新路径, 新密码);
            foreach (var e in 条目们) 新库.放入(e);
            新库.保存();
            设置对象.库路径 = 新路径;
            界面元素.提示(
                $"已升级：{条目们.Count} 条转到了\n{新路径}\n\n" +
                "旧文件没动，确认新库一切正常之后再自行删除。",
                "升级完成");
            return 新库;
        }
        catch (Exception 错)
        {
            界面元素.警告($"升级失败：{错.Message}");
            return null;
        }
    }
}
