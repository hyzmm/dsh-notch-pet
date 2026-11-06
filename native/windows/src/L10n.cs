using System;
using System.Globalization;

namespace DshNotchPet
{
    internal static class L10n
    {
        public static bool IsChinese
        {
            get
            {
                string forced = Environment.GetEnvironmentVariable("DSH_NOTCH_PET_LOCALE");
                if (!string.IsNullOrEmpty(forced))
                {
                    string value = forced.ToLowerInvariant();
                    if (value.StartsWith("zh", StringComparison.Ordinal)) return true;
                    if (value.StartsWith("en", StringComparison.Ordinal)) return false;
                }
                string name = CultureInfo.CurrentUICulture.Name.ToLowerInvariant();
                return name.StartsWith("zh", StringComparison.Ordinal);
            }
        }

        public static string T(string english, string chinese)
        {
            return IsChinese ? chinese : english;
        }

        public static string Phase(PetPhase phase)
        {
            switch (phase)
            {
                case PetPhase.Running:
                case PetPhase.Thinking:
                case PetPhase.Tool:
                    return T("RUNNING", "运行中");
                case PetPhase.Waiting:
                    return T("WAITING", "等待中");
                case PetPhase.Done:
                    return T("DONE", "完成");
                case PetPhase.Error:
                    return T("ERROR", "出错");
                default:
                    return T("IDLE", "待命");
            }
        }

        public static string Activity(SessionFocus focus)
        {
            if (focus == null) return T("Resting at the top", "在顶部待命");
            PetPhase phase = PetPhaseExtensions.Parse(focus.phase);
            switch (phase)
            {
                case PetPhase.Tool:
                    return T("Executing", "正在执行");
                case PetPhase.Waiting:
                    return T("Waiting for you", "等你回复");
                case PetPhase.Thinking:
                    return T("Thinking", "思考中");
                case PetPhase.Done:
                    return T("Turn complete", "本轮完成");
                case PetPhase.Error:
                    return T("Something went wrong", "遇到错误");
                case PetPhase.Running:
                    return T("Working", "执行中");
                default:
                    return T("Resting at the top", "在顶部待命");
            }
        }

        public static string TaskTitle(SessionFocus focus)
        {
            string title = focus == null ? string.Empty : (focus.title ?? string.Empty).Trim();
            return title.Length == 0 ? T("DSH session", "DSH 会话") : title;
        }

        /// <summary>
        /// The narrow line under the title. It mirrors the live step, not the
        /// finished assistant reply, exactly like the macOS panel.
        /// </summary>
        public static string LiveProgress(SessionFocus focus, bool compact)
        {
            if (focus == null) return T("Ready", "就绪");
            PetPhase phase = PetPhaseExtensions.Parse(focus.phase);
            if (phase == PetPhase.Error)
            {
                string message = (focus.lastError ?? string.Empty).Trim();
                return message.Length == 0 ? T("Something went wrong", "遇到错误") : message;
            }
            if (phase == PetPhase.Waiting)
            {
                if (focus.activity != null && focus.activity.kind == "questions")
                {
                    string line = ActivityLine(focus.activity, compact);
                    if (line != null) return line;
                }
                return T("Waiting for your action", "等待你的操作");
            }
            if (focus.activity != null)
            {
                string line = ActivityLine(focus.activity, compact);
                if (line != null) return line;
            }
            if (phase.IsBusy() && !string.IsNullOrEmpty(focus.reasoning)
                && focus.reasoning.Trim().Length > 0)
            {
                return T("Thinking", "正在分析请求") + " · " + focus.reasoning.Trim();
            }
            switch (phase)
            {
                case PetPhase.Thinking:
                case PetPhase.Running:
                    return T("Thinking", "正在分析请求");
                case PetPhase.Done:
                    return T("Turn complete", "本轮完成");
                case PetPhase.Idle:
                    return T("Ready", "就绪");
                default:
                    return Activity(focus);
            }
        }

        private static string ActivityLine(ActivityState step, bool compact)
        {
            string verb;
            switch (step.kind ?? "tools")
            {
                case "edit": verb = T("Editing file", "正在编辑文件"); break;
                case "write": verb = T("Writing file", "正在写入文件"); break;
                case "read": verb = T("Reading file", "正在读取文件"); break;
                case "readImage": verb = T("Reading image", "正在读取图片"); break;
                case "search": verb = T("Searching code", "正在搜索代码"); break;
                case "commands": verb = T("Running command", "正在运行命令"); break;
                case "code": verb = T("Running code", "正在运行代码"); break;
                case "webSearch": verb = T("Searching the web", "正在搜索网页"); break;
                case "webFetch": verb = T("Visiting webpage", "正在访问网页"); break;
                case "subagents": verb = T("Coordinating subagents", "正在协调子智能体"); break;
                case "plan": verb = T("Updating plan", "正在更新计划"); break;
                case "questions": verb = T("Waiting for you", "等待你的操作"); break;
                default: verb = T("Calling tool", "正在调用工具"); break;
            }
            string raw = compact ? (step.detailShort ?? step.detail) : (step.detail ?? step.detailShort);
            string detail = (raw ?? string.Empty).Trim();
            if (detail.Length == 0) return verb;
            return verb + " · " + detail;
        }

        public static string ProgressLine(SessionFocus focus)
        {
            if (focus == null || focus.progress == null
                || !focus.progress.total.HasValue || focus.progress.total.Value <= 0)
                return T("Live progress", "实时进度");
            int completed = focus.progress.completed ?? 0;
            int total = focus.progress.total.Value;
            if (focus.progress.kind == "goal")
                return T("Goal " + completed + "/" + total + " rounds",
                    "目标 " + completed + "/" + total + " 轮");
            return T("Tasks " + completed + "/" + total, "任务 " + completed + "/" + total);
        }

        public static string Footer(SessionFocus focus, int activeCount)
        {
            if (focus == null)
                return T("Start a DSH task and I will wake up", "开始 DSH 任务，我就会醒来");
            PetPhase phase = PetPhaseExtensions.Parse(focus.phase);
            if (phase == PetPhase.Error && !string.IsNullOrEmpty(focus.lastError))
                return focus.lastError;
            if (focus.goal != null && !string.IsNullOrEmpty(focus.goal.objective))
                return focus.goal.objective;
            if (activeCount > 1)
                return T(activeCount + " agents are working", activeCount + " 个 agent 正在工作");
            switch (phase)
            {
                case PetPhase.Waiting:
                    return T("Take your time — I will wait", "慢慢来，我等你");
                case PetPhase.Done:
                    return T("Nice. Ready for the next turn.", "干得漂亮，随时开始下一轮。");
                case PetPhase.Idle:
                    return T("Start a DSH task and I will wake up", "开始 DSH 任务，我就会醒来");
                default:
                    return T("DSH is working", "DSH 正在工作");
            }
        }

        public static string Elapsed(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) return string.Empty;
            long total = (long)Math.Floor(seconds);
            if (total < 60) return total + "s";
            if (total < 3600) return (total / 60) + "m " + (total % 60) + "s";
            return (total / 3600) + "h " + (total % 3600 / 60) + "m";
        }

        /// <summary>Session total in DSH's compact token style: 517 / 12.2K / 517K / 1.2M.</summary>
        public static string TokenUsage(TokenUsage usage)
        {
            return CompactTokenCount(usage.TotalTokens) + " tok";
        }

        public static string CacheHit(string percent)
        {
            return T(percent + " hit", "命中 " + percent);
        }

        public static string UsageDetail(TokenUsage usage)
        {
            string total = usage.TotalTokens.ToString("N0", CultureInfo.CurrentCulture);
            string input = usage.UncachedInputTokens.ToString("N0", CultureInfo.CurrentCulture);
            string cacheRead = usage.CacheReadTokens.ToString("N0", CultureInfo.CurrentCulture);
            string output = usage.OutputTokens.ToString("N0", CultureInfo.CurrentCulture);
            string hit = usage.CacheHitText();
            string cached = hit == null ? string.Empty
                : T(" · " + hit + " cached", " · 缓存 " + hit);
            return T(
                "Session tokens " + total + " · uncached input " + input
                    + " · cache read " + cacheRead + " · output " + output + cached,
                "会话 Token " + total + " · 未缓存输入 " + input
                    + " · 缓存读取 " + cacheRead + " · 输出 " + output + cached);
        }

        private static string CompactTokenCount(long value)
        {
            if (value < 1000) return value.ToString(CultureInfo.InvariantCulture);
            bool millions = value >= 1000000;
            double scaled = millions ? value / 1000000.0 : value / 1000.0;
            double rounded = scaled >= 100 ? Math.Round(scaled, MidpointRounding.AwayFromZero)
                : Math.Round(scaled * 10, MidpointRounding.AwayFromZero) / 10.0;
            long tenths = (long)Math.Round(rounded * 10, MidpointRounding.AwayFromZero);
            string number = tenths % 10 == 0
                ? (tenths / 10).ToString(CultureInfo.InvariantCulture)
                : (tenths / 10).ToString(CultureInfo.InvariantCulture)
                    + "." + (tenths % 10).ToString(CultureInfo.InvariantCulture);
            return number + (millions ? "M" : "K");
        }

        public static string AccentPresetName(string id)
        {
            switch (id)
            {
                case "aurora": return T("Aurora", "极光蓝");
                case "glacier": return T("Glacier", "冰川青");
                case "iris": return T("Iris", "鸢尾紫");
                case "sakura": return T("Sakura", "樱花粉");
                case "tangerine": return T("Tangerine", "落日橙");
                case "mint": return T("Mint", "薄荷绿");
                default: return id;
            }
        }

        public static readonly string AppName = T("DSH Notch Pet", "DSH 刘海宠物");
        public static readonly string OpenDSH = T("Open DSH", "打开 DSH");
        public static readonly string Open = T("Open", "打开");
        public static readonly string QuitPet = T("Quit Notch Pet", "退出刘海宠物");
        public static readonly string Pin = T("Pin panel", "固定面板");
        public static readonly string Unpin = T("Unpin panel", "取消固定");
        public static readonly string Disconnected = T("Disconnected", "已断开");
        public static readonly string Settings = T("Settings…", "设置…");
        public static readonly string SettingsTitle = T("Notch Pet", "刘海宠物");
        public static readonly string SettingsHint = T("Accent color and behavior", "主色调与显示方式");
        public static readonly string AccentColor = T("Accent color", "主色调");
        public static readonly string OnlyWhileWorking = T("Only while working", "仅在有任务时显示");
        public static readonly string OnlyWhileWorkingHint = T(
            "Hide the panel when no task is running",
            "无进行中任务时隐藏面板，不占空间");
        public static readonly string HidePet = T("Hide pet", "隐藏宠物形象");
        public static readonly string HidePetHint = T(
            "Keep the task details, hide the animated mascot",
            "只保留任务信息，不显示动画宠物形象");
        public static readonly string HideStatusItem = T("Hide tray icon", "隐藏托盘图标");
        public static readonly string HideStatusItemHint = T(
            "Keep the pawprint out of the system tray. Start a task to reach the panel again.",
            "面板隐藏时不在系统托盘留图标；启动任意任务即可唤回面板");
        public static readonly string CustomColor = T("Custom color", "自定义颜色");
        public static readonly string CustomHint = T("Pick any color", "点击色块任选颜色");
        public static readonly string Reset = T("Reset", "恢复默认");
        public static readonly string Close = T("Close", "关闭");
        public static readonly string TrayTooltip = T("DSH Notch Pet — live agent progress", "DSH 刘海宠物 — 实时任务进度");
    }
}
