import Foundation

struct PetPayload: Decodable {
    var v: Int?
    var now: Double?
    var origin: String?
    var overall: String?
    var activeCount: Int?
    var totalCount: Int?
    var focus: SessionFocus?
    var sessions: [SessionSummary]?

    static let empty = PetPayload(
        v: 1,
        now: Date().timeIntervalSince1970 * 1000,
        origin: nil,
        overall: "idle",
        activeCount: 0,
        totalCount: 0,
        focus: nil,
        sessions: []
    )
}

struct SessionFocus: Decodable {
    var id: String?
    var title: String?
    var cwd: String?
    var root: Bool?
    var agentPreset: String?
    var model: String?
    var running: Bool?
    var phase: String?
    var tool: String?
    var toolCount: Int?
    var turn: Int?
    var step: Int?
    var startedAt: Double?
    var completedAt: Double?
    var updatedAt: Double?
    var lastTool: String?
    var lastError: String?
    var reasoning: String?
    var usage: TokenUsage?
    var activity: ActivityState?
    var progress: ProgressState?
    var goal: GoalState?
    var todos: [TodoItem]?
}

struct ActivityState: Decodable {
    var kind: String?
    var detail: String?
    var detailShort: String?
    var tool: String?
}

/// Whole-session provider-reported usage. `input` is the uncached prompt side;
/// cached reads and writes are reported separately, exactly as DSH's own usage
/// pill presents them.
struct TokenUsage: Decodable {
    var input: Int?
    var output: Int?
    var cacheRead: Int?
    var cacheWrite: Int?

    var uncachedInputTokens: Int { max(0, input ?? 0) }
    var outputTokens: Int { max(0, output ?? 0) }
    var cacheReadTokens: Int { max(0, cacheRead ?? 0) }
    var cacheWriteTokens: Int { max(0, cacheWrite ?? 0) }

    var billedInputTokens: Int {
        uncachedInputTokens + cacheReadTokens + cacheWriteTokens
    }

    var totalTokens: Int { billedInputTokens + outputTokens }

    var hasUsage: Bool { totalTokens > 0 }

    /// A partial cache hit never rounds up to a misleading 100%.
    var cacheHitText: String? {
        let denominator = billedInputTokens
        guard denominator > 0, cacheReadTokens > 0 else { return nil }
        if cacheReadTokens >= denominator { return "100%" }
        let percent = Double(cacheReadTokens) / Double(denominator) * 100
        if percent >= 99.95 { return "99.9%" }
        if percent >= 99.5 { return String(format: "%.1f%%", percent) }
        return "\(Int(percent.rounded()))%"
    }
}

struct ProgressState: Decodable {
    var kind: String?
    var completed: Int?
    var total: Int?
    var label: String?

    var fraction: Double? {
        guard let total, total > 0 else { return nil }
        return min(1, max(0, Double(completed ?? 0) / Double(total)))
    }
}

struct GoalState: Decodable {
    var phase: String?
    var objective: String?
    var roundsStarted: Int?
    var maxGoalRounds: Int?
}

struct TodoItem: Decodable, Identifiable {
    var content: String?
    var status: String?

    var id: String { "\(status ?? "")|\(content ?? "")" }
}

struct SessionSummary: Decodable, Identifiable {
    var id: String?
    var title: String?
    var running: Bool?
    var phase: String?
    var tool: String?
    var updatedAt: Double?
    var root: Bool?
}

enum PetPhase: String {
    case running
    case thinking
    case tool
    case waiting
    case done
    case error
    case idle

    init(raw: String?) {
        switch raw {
        case "running": self = .running
        case "thinking": self = .thinking
        case "tool": self = .tool
        case "waiting": self = .waiting
        case "done": self = .done
        case "error": self = .error
        default: self = .idle
        }
    }

    var isBusy: Bool {
        switch self {
        case .running, .thinking, .tool: return true
        default: return false
        }
    }
}
