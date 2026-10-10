// 密码库 —— 本地密码管理器
// Copyright (C) 2026 ZhangGe
//
// This program is free software: you can redistribute it and/or modify it
// under the terms of the GNU General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your
// option) any later version. This program is distributed WITHOUT ANY
// WARRANTY; see the GNU General Public License for more details.
// <https://www.gnu.org/licenses/>

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using 密码库.核心;

namespace 密码库.自检;

/// <summary>
/// 自检。不用任何测试框架，也就不用联网拉包——离线随时能跑。
///   dotnet run --project src\Vault.SelfTest [旧库路径 旧库主密码]
/// 带上后两个参数时，会额外验证「PowerShell 版建的库，C# 版能不能原样读出来」。
/// </summary>
internal static class 自检
{
    private static int _过, _败;

    private static void 查(string 名, bool 条件, string 说明 = "")
    {
        if (条件) { _过++; Console.WriteLine($"  [过] {名}"); }
        else { _败++; Console.WriteLine($"  [败] {名}{(说明.Length > 0 ? "：" + 说明 : "")}"); }
    }

    private static void 该抛<T>(string 名, Action 动作) where T : Exception
    {
        try { 动作(); 查(名, false, $"本该抛 {typeof(T).Name}，却没抛"); }
        catch (T) { 查(名, true); }
        catch (Exception 错) { 查(名, false, $"抛的是 {错.GetType().Name}：{错.Message}"); }
    }

    private static int Main(string[] 参数)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var 场地 = Path.Combine(Path.GetTempPath(), "vault-selftest-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(场地);
        Console.WriteLine($"自检场地：{场地}\n");

        try
        {
            基本读写(场地);
            换行与多行(场地);
            错密码与篡改(场地);
            换主密码(场地);
            备份与原子写(场地);
            密码生成检查();
            导出导入(场地);
            if (参数.Length >= 2) 读旧版库(参数[0], 参数[1]);
            else Console.WriteLine("\n(没给旧库路径，跳过 v1 兼容性检查)");
        }
        finally
        {
            try { Directory.Delete(场地, true); } catch { }
        }

        Console.WriteLine($"\n结果：{_过} 项通过，{_败} 项失败。");
        return _败 == 0 ? 0 : 1;
    }

    private static void 基本读写(string 场地)
    {
        Console.WriteLine("== 基本读写 ==");
        var 路径 = Path.Combine(场地, "a.vault");
        const string 口令 = "自检用的主密码-12345678";

        using (var 新库 = 库.新建(路径, 口令))
        {
            新库.放入(new 条目 { 标题 = "阿里云 WordPress", 用户名 = "root", 密码 = "p@ss word", 分类 = "服务器", 网址 = "203.0.113.1" });
            新库.放入(new 条目 { 标题 = "某银行", 用户名 = "zhangge", 密码 = "另一个密码", 备注 = "含中文的备注\n第二行" });
            新库.保存();
        }

        using var 再开 = 库.打开(路径, 口令);
        查("条目数对得上", 再开.条目表.Count == 2);
        var 甲 = 再开.条目表.FirstOrDefault(e => e.标题 == "阿里云 WordPress");
        查("字段原样读回", 甲 != null && 甲.用户名 == "root" && 甲.密码 == "p@ss word" && 甲.网址 == "203.0.113.1");
        查("中文备注不走样", 再开.条目表.Any(e => e.备注 == "含中文的备注\n第二行"));

        var 文本 = File.ReadAllText(路径);
        查("标题没有明文出现在文件里", !文本.Contains("阿里云") && !文本.Contains("某银行"),
            "整库加密的意义就在这儿");
        查("用户名没有明文出现在文件里", !文本.Contains("zhangge"));
        查("文件头留着必要的明文参数", 文本.Contains("\"iterations\"") && 文本.Contains("\"salt\""));
    }

    private static void 换行与多行(string 场地)
    {
        Console.WriteLine("\n== 多行内容（PEM 私钥这种）==");
        var 路径 = Path.Combine(场地, "b.vault");
        const string 口令 = "口令-abcdefgh";
        var pem = "-----BEGIN OPENSSH PRIVATE KEY-----\n第一行 line-one\n第二行 line-two\n-----END OPENSSH PRIVATE KEY-----";

        using (var 新库 = 库.新建(路径, 口令))
        {
            新库.放入(new 条目 { 标题 = "机器-002后端", 附加 = pem });
            新库.保存();
        }
        using var 再开 = 库.打开(路径, 口令);
        var 条 = 再开.条目表[0];
        查("多行内容逐字符一致", 条.附加 == pem, $"实得 {条.附加.Length} 字符，期望 {pem.Length}");
        查("行数没变", 条.附加.Split('\n').Length == 4);
    }

    private static void 错密码与篡改(string 场地)
    {
        Console.WriteLine("\n== 错密码 / 篡改 ==");
        var 路径 = Path.Combine(场地, "c.vault");
        const string 口令 = "正确口令-12345678";
        using (var 新库 = 库.新建(路径, 口令))
        {
            新库.放入(new 条目 { 标题 = "东西", 密码 = "秘密" });
            新库.保存();
        }

        该抛<打不开异常>("错的主密码打不开", () => 库.打开(路径, "错误口令-12345678").Dispose());

        // 改密文里一个字节
        var 原文 = File.ReadAllText(路径);
        var 改过 = 动一个base64字节(原文, "data");
        File.WriteAllText(路径 + ".tampered", 改过);
        该抛<打不开异常>("密文被改动能被发现", () => 库.打开(路径 + ".tampered", 口令).Dispose());

        // 把迭代次数从 60 万改成 1：文件头参与了认证，所以也必须被发现
        var 降级 = 原文.Replace("\"iterations\": 600000", "\"iterations\": 1");
        查("降级测试的前提成立（确实替换掉了）", 降级 != 原文);
        File.WriteAllText(路径 + ".weak", 降级);
        该抛<打不开异常>("偷偷调低迭代次数会被发现", () => 库.打开(路径 + ".weak", 口令).Dispose());
    }

    /// <summary>
    /// 把库文件里某个 base64 字段的第一个字节翻一位。
    /// 走 JsonNode 而不是切字符串：System.Text.Json 默认编码器会把 base64 里的
    /// '+' 写成它的 JSON 转义形式（反斜杠 + u002B），对原文直接 FromBase64String 必炸。
    /// </summary>
    private static string 动一个base64字节(string 文本, string 字段)
    {
        var 根 = JsonNode.Parse(文本) ?? throw new InvalidOperationException("库文件不是合法 JSON。");
        var 值 = 根[字段]?.GetValue<string>()
                 ?? throw new InvalidOperationException($"库文件里没有 {字段} 字段。");
        var 原 = Convert.FromBase64String(值);
        原[0] ^= 1;
        根[字段] = Convert.ToBase64String(原);
        return 根.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static void 换主密码(string 场地)
    {
        Console.WriteLine("\n== 换主密码 ==");
        var 路径 = Path.Combine(场地, "d.vault");
        const string 旧 = "旧口令-12345678";
        const string 新 = "新口令-87654321";
        using (var 新库 = 库.新建(路径, 旧))
        {
            新库.放入(new 条目 { 标题 = "条目甲", 密码 = "值甲", 附加 = "多行\n内容" });
            新库.保存();
            查("换之前旧密码是对的", 新库.主密码对吗(旧));
            新库.换主密码(新);
            查("换之后旧密码不认了", !新库.主密码对吗(旧));
            查("换之后新密码是对的", 新库.主密码对吗(新));
        }
        该抛<打不开异常>("换完之后旧密码开不了", () => 库.打开(路径, 旧).Dispose());
        using var 再开 = 库.打开(路径, 新);
        查("换完之后内容完好", 再开.条目表.Count == 1 && 再开.条目表[0].附加 == "多行\n内容");
    }

    private static void 备份与原子写(string 场地)
    {
        Console.WriteLine("\n== 备份 ==");
        var 路径 = Path.Combine(场地, "e.vault");
        const string 口令 = "口令-12345678";
        using (var 新库 = 库.新建(路径, 口令))
        {
            for (int i = 0; i < 3; i++)
            {
                新库.放入(new 条目 { 标题 = $"第 {i} 条" });
                新库.保存();
                Thread.Sleep(1100);   // 备份文件名精确到秒，间隔开才看得出多份
            }
        }
        var 备份目录 = Path.Combine(场地, "备份");
        查("生成了备份", Directory.Exists(备份目录) && Directory.GetFiles(备份目录).Length >= 2,
            Directory.Exists(备份目录) ? $"只有 {Directory.GetFiles(备份目录).Length} 份" : "备份目录都没有");
        查("没留下临时文件", !File.Exists(路径 + ".tmp"));
    }

    private static void 密码生成检查()
    {
        Console.WriteLine("\n== 密码生成 ==");
        var 设置 = new 密码生成.选项 { 长度 = 32 };
        var 一批 = Enumerable.Range(0, 200).Select(_ => 密码生成.生成(设置)).ToList();
        查("长度对", 一批.All(p => p.Length == 32));
        查("200 个互不相同", 一批.Distinct().Count() == 200);
        查("四类字符都出现", 一批.All(p =>
            p.Any(char.IsLower) && p.Any(char.IsUpper) && p.Any(char.IsDigit) && p.Any(c => !char.IsLetterOrDigit(c))));

        var 避混 = 密码生成.生成(new 密码生成.选项 { 长度 = 60, 要符号 = false, 避开易混 = true });
        查("避开易混字符时确实没有 I O l 0 o 1", !避混.Any(c => "IOl0o1".Contains(c)));
        查("熵估算大致合理", 密码生成.估算熵(new string('a', 20)) is > 90 and < 96);
    }

    private static void 导出导入(string 场地)
    {
        Console.WriteLine("\n== 导出 / 导入 ==");
        var 路径 = Path.Combine(场地, "f.vault");
        const string 口令 = "口令-12345678";
        using var 新库 = 库.新建(路径, 口令);
        新库.放入(new 条目 { 标题 = "带,逗号和\"引号", 密码 = "值", 备注 = "跨\n行" });
        新库.放入(new 条目 { 标题 = "普通一条", 用户名 = "u", 密码 = "p" });
        新库.保存();

        var json = Path.Combine(场地, "out.json");
        导出.存成JSON(新库.条目表, json);
        var 读回 = 导出.从JSON读入(json);
        查("JSON 往返条目数对", 读回.Count == 2);
        查("JSON 往返内容完好", 读回.Any(e => e.标题 == "带,逗号和\"引号" && e.备注 == "跨\n行"));
        查("导入会重新发编号（不会覆盖现有条目）",
            读回.All(e => 新库.条目表.All(旧 => 旧.编号 != e.编号)));

        var csv = Path.Combine(场地, "out.csv");
        导出.存成CSV(新库.条目表, csv);
        var csv文本 = File.ReadAllText(csv);
        查("CSV 有表头", csv文本.StartsWith("﻿group,title,username", StringComparison.Ordinal)
                      || csv文本.StartsWith("group,title,username", StringComparison.Ordinal));
        查("CSV 里逗号和引号被正确转义", csv文本.Contains("\"带,逗号和\"\"引号\"\"\"")
                                      || csv文本.Contains("\"带,逗号和\"\"引号\""));
    }

    private static void 读旧版库(string 旧库路径, string 旧库口令)
    {
        Console.WriteLine("\n== 兼容 PowerShell 版建的 v1 库 ==");
        查("认得出这是旧版", 旧版.像是旧版(旧库路径));
        try
        {
            var 条目们 = 旧版.读出(旧库路径, 旧库口令);
            查("能读出条目", 条目们.Count > 0, $"读出 {条目们.Count} 条");
            foreach (var e in 条目们)
                Console.WriteLine($"       · {e.标题}  密码{(e.密码.Length > 0 ? "有" : "无")}  附加 {e.附加.Split('\n').Length} 行");
            该抛<打不开异常>("旧库用错密码也打不开", () => 旧版.读出(旧库路径, "肯定不对的密码"));
        }
        catch (Exception 错)
        {
            查("读旧库", false, 错.Message);
        }
    }
}
