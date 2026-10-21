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

    static func shortActivity(_ focus: SessionFocus?) -> String {
        guard let focus else { return "DSH" }
        let phase = PetPhase(raw: focus.phase)
        if phase == .waiting { return t("WAIT", "等待") }
        if phase == .error { return t("ERR", "出错") }
        if phase == .done { return t("DONE", "完成") }
        let tool = focus.tool?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if phase == .tool, !tool.isEmpty { return String(tool.prefix(12)) }
        if phase.isBusy { return t("THINK", "思考") }
        return "DSH"
    }

    static func count(_ active: Int, total: Int) -> String {
        if active > 1 {
            return t("\(active) running", "\(active) 个运行中")
        }
        if active == 1 {
            return t("1 running", "1 个运行中")
        }
        if total > 1 {
            return t("\(total) sessions", "\(total) 个会话")
        }
        return t("Ready", "就绪")
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
        if PetPhase(raw: focus.phase) == .error, let error = focus.lastError, !error.isEmpty {
            return error
        }
        if let label = focus.progress?.label, !label.isEmpty {
            return label
        }
        if let objective = focus.goal?.objective, !objective.isEmpty {
            return objective
        }
        if activeCount > 1 {
            return t("\(activeCount) agents are working", "\(activeCount) 个 agent 正在工作")
        }
        switch PetPhase(raw: focus.phase) {
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

    static let openDSH = t("Open DSH", "打开 DSH")
    static let open = t("Open", "打开")
    static let quitPet = t("Quit Notch Pet", "退出刘海宠物")
    static let pin = t("Pin panel", "固定面板")
    static let unpin = t("Unpin panel", "取消固定")
    static let disconnected = t("Disconnected", "已断开")
}
