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
    var progress: ProgressState?
    var goal: GoalState?
    var todos: [TodoItem]?
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
