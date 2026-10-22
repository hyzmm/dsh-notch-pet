import Foundation

enum L10n {
    static var isChinese: Bool {
        Locale.preferredLanguages.first?.lowercased().hasPrefix("zh") ?? false
    }

    static func t(_ english: String, _ chinese: String) -> String {
        isChinese ? chinese : english
    }

    static func phase(_ phase: PetPhase) -> String {
        switch phase {
        case .running, .thinking, .tool:
            return t("RUNNING", "运行中")
        case .waiting:
            return t("WAITING", "等待中")
        case .done:
            return t("DONE", "完成")
        case .error:
            return t("ERROR", "出错")
        case .idle:
            return t("IDLE", "待命")
        }
    }

    static func activity(_ focus: SessionFocus?) -> String {
        guard let focus else { return t("Resting in the notch", "在刘海待命") }
        let phase = PetPhase(raw: focus.phase)
        switch phase {
        case .tool:
            return t("Executing", "正在执行")
        case .waiting:
            return t("Waiting for you", "等你回复")
        case .thinking:
            return t("Thinking", "思考中")
        case .done:
            return t("Turn complete", "本轮完成")
        case .error:
            return t("Something went wrong", "遇到错误")
        case .running:
            return t("Working", "执行中")
        case .idle:
            return t("Resting in the notch", "在刘海待命")
        }
    }

    static func taskTitle(_ focus: SessionFocus?) -> String {
        let title = focus?.title?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return title.isEmpty ? t("DSH session", "DSH 会话") : title
    }

    /// The narrow line under the title. It mirrors the assistant text that is
    /// currently streaming in the DSH chat, falling back to the active step.
    static func liveProgress(_ focus: SessionFocus?, compact: Bool = false) -> String {
        guard let focus else { return t("Ready", "就绪") }
        let phase = PetPhase(raw: focus.phase)
        if phase == .error {
            let message = focus.lastError?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
            return message.isEmpty ? t("Something went wrong", "遇到错误") : message
        }
        if phase == .waiting {
            if let step = focus.activity, step.kind == "questions",
               let line = activityLine(step, compact: compact) {
                return line
            }
            return t("Waiting for your action", "等待你的操作")
        }
        if let step = focus.activity, let line = activityLine(step, compact: compact) {
            return line
        }
        if phase.isBusy,
           let reasoning = focus.reasoning?.trimmingCharacters(in: .whitespacesAndNewlines),
           !reasoning.isEmpty {
            return "\(t("Thinking", "正在分析请求")) · \(reasoning)"
        }
        switch phase {
        case .thinking, .running:
            return t("Thinking", "正在分析请求")
        case .done:
            return t("Turn complete", "本轮完成")
        case .idle:
            return t("Ready", "就绪")
        default:
            return activity(focus)
        }
    }

    private static func activityLine(_ step: ActivityState, compact: Bool) -> String? {
        let verb: String
        switch step.kind ?? "tools" {
        case "edit":
            verb = t("Editing file", "正在编辑文件")
        case "write":
            verb = t("Writing file", "正在写入文件")
        case "read":
            verb = t("Reading file", "正在读取文件")
        case "readImage":
            verb = t("Reading image", "正在读取图片")
        case "search":
            verb = t("Searching code", "正在搜索代码")
        case "commands":
            verb = t("Running command", "正在运行命令")
        case "code":
            verb = t("Running code", "正在运行代码")
        case "webSearch":
            verb = t("Searching the web", "正在搜索网页")
        case "webFetch":
            verb = t("Visiting webpage", "正在访问网页")
        case "subagents":
            verb = t("Coordinating subagents", "正在协调子智能体")
        case "plan":
            verb = t("Updating plan", "正在更新计划")
        case "questions":
            verb = t("Waiting for you", "等待你的操作")
        default:
            verb = t("Calling tool", "正在调用工具")
        }
        let raw = compact
            ? (step.detailShort ?? step.detail)
            : (step.detail ?? step.detailShort)
        let detail = raw?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        guard !detail.isEmpty else { return verb }
        return "\(verb) · \(detail)"
    }

    static func progressLine(_ focus: SessionFocus?) -> String {
        guard let progress = focus?.progress, let total = progress.total, total > 0 else {
            return t("Live progress", "实时进度")
        }
        let completed = progress.completed ?? 0
        if progress.kind == "goal" {
            return t("Goal \(completed)/\(total) rounds", "目标 \(completed)/\(total) 轮")
        }
        return t("Tasks \(completed)/\(total)", "任务 \(completed)/\(total)")
    }

    static func footer(_ focus: SessionFocus?, activeCount: Int) -> String {
        guard let focus else {
            return t("Start a DSH task and I will wake up", "开始 DSH 任务，我就会醒来")
        }
        let phase = PetPhase(raw: focus.phase)
        if phase == .error, let error = focus.lastError, !error.isEmpty {
            return error
        }
        if let objective = focus.goal?.objective, !objective.isEmpty {
            return objective
        }
        if activeCount > 1 {
            return t("\(activeCount) agents are working", "\(activeCount) 个 agent 正在工作")
        }
        switch phase {
        case .waiting:
            return t("Take your time — I will wait", "慢慢来，我等你")
        case .done:
            return t("Nice. Ready for the next turn.", "干得漂亮，随时开始下一轮。")
        case .idle:
            return t("Start a DSH task and I will wake up", "开始 DSH 任务，我就会醒来")
        default:
            return t("DSH is working", "DSH 正在工作")
        }
    }

    static func elapsed(_ seconds: Double) -> String {
        guard seconds.isFinite, seconds >= 0 else { return "" }
        let total = Int(seconds.rounded(.down))
        if total < 60 { return "\(total)s" }
        if total < 3600 { return "\(total / 60)m \(total % 60)s" }
        return "\(total / 3600)h \(total % 3600 / 60)m"
    }

    static func accentPresetName(_ id: String) -> String {
        switch id {
        case "aurora": return t("Aurora", "极光蓝")
        case "glacier": return t("Glacier", "冰川青")
        case "iris": return t("Iris", "鸢尾紫")
        case "sakura": return t("Sakura", "樱花粉")
        case "tangerine": return t("Tangerine", "落日橙")
        case "mint": return t("Mint", "薄荷绿")
        default: return id.capitalized
        }
    }

    static let appName = t("DSH Notch Pet", "DSH 刘海宠物")
    static let openDSH = t("Open DSH", "打开 DSH")
    static let open = t("Open", "打开")
    static let quitPet = t("Quit Notch Pet", "退出刘海宠物")
    static let pin = t("Pin panel", "固定面板")
    static let unpin = t("Unpin panel", "取消固定")
    static let disconnected = t("Disconnected", "已断开")
    static let settings = t("Settings…", "设置…")
    static let settingsTitle = t("Notch Pet", "刘海宠物")
    static let settingsHint = t("Accent color and behavior", "主色调与显示方式")
    static let accentColor = t("Accent color", "主色调")
    static let onlyWhileWorking = t("Only while working", "仅在有任务时显示")
    static let onlyWhileWorkingHint = t(
        "Hide the notch when no task is running",
        "无进行中任务时隐藏刘海，不占空间"
    )
    static let hideStatusItem = t("Hide menu bar icon", "隐藏状态栏图标")
    static let hideStatusItemHint = t(
        "Keep the pawprint out of the menu bar. Start a task to reach the panel again.",
        "面板隐藏时不在状态栏留图标；启动任意任务即可唤回面板"
    )
    static let customColor = t("Custom color", "自定义颜色")
    static let customHint = t("Pick any color", "点击色块任选颜色")
    static let reset = t("Reset", "恢复默认")
    static let close = t("Close", "关闭")
}
