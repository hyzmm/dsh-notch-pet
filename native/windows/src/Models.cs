using System;
using System.Collections.Generic;
using System.Globalization;

namespace DshNotchPet
{
    internal sealed class PetPayload
    {
        public int? v { get; set; }
        public double? now { get; set; }
        public string origin { get; set; }
        public string overall { get; set; }
        public int? activeCount { get; set; }
        public int? totalCount { get; set; }
        public SessionFocus focus { get; set; }
        public List<SessionSummary> sessions { get; set; }

        public static PetPayload Empty()
        {
            PetPayload payload = new PetPayload();
            payload.v = 1;
            payload.now = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            payload.overall = "idle";
            payload.activeCount = 0;
            payload.totalCount = 0;
            payload.sessions = new List<SessionSummary>();
            return payload;
        }
    }

    internal sealed class SessionFocus
    {
        public string id { get; set; }
        public string title { get; set; }
        public string cwd { get; set; }
        public bool? root { get; set; }
        public string agentPreset { get; set; }
        public string model { get; set; }
        public bool? running { get; set; }
        public string phase { get; set; }
        public string tool { get; set; }
        public int? toolCount { get; set; }
        public int? turn { get; set; }
        public int? step { get; set; }
        public double? startedAt { get; set; }
        public double? completedAt { get; set; }
        public double? updatedAt { get; set; }
        public string lastTool { get; set; }
        public string lastError { get; set; }
        public string reasoning { get; set; }
        public TokenUsage usage { get; set; }
        public ActivityState activity { get; set; }
        public ProgressState progress { get; set; }
        public GoalState goal { get; set; }
        public List<TodoItem> todos { get; set; }
    }

    internal sealed class ActivityState
    {
        public string kind { get; set; }
        public string detail { get; set; }
        public string detailShort { get; set; }
        public string tool { get; set; }
    }

    /// <summary>
    /// Whole-session provider-reported usage. `input` is the uncached prompt
    /// side; cache reads and writes are reported separately, exactly as DSH's
    /// own usage pill presents them.
    /// </summary>
    internal sealed class TokenUsage
    {
        public long? input { get; set; }
        public long? output { get; set; }
        public long? cacheRead { get; set; }
        public long? cacheWrite { get; set; }

        public long UncachedInputTokens { get { return Math.Max(0, input ?? 0); } }
        public long OutputTokens { get { return Math.Max(0, output ?? 0); } }
        public long CacheReadTokens { get { return Math.Max(0, cacheRead ?? 0); } }
        public long CacheWriteTokens { get { return Math.Max(0, cacheWrite ?? 0); } }

        public long BilledInputTokens
        {
            get { return UncachedInputTokens + CacheReadTokens + CacheWriteTokens; }
        }

        public long TotalTokens { get { return BilledInputTokens + OutputTokens; } }
        public bool HasUsage { get { return TotalTokens > 0; } }

        /// <summary>A partial cache hit never rounds up to a misleading 100%.</summary>
        public string CacheHitText()
        {
            long denominator = BilledInputTokens;
            if (denominator <= 0 || CacheReadTokens <= 0) return null;
            if (CacheReadTokens >= denominator) return "100%";
            double percent = (double)CacheReadTokens / denominator * 100.0;
            if (percent >= 99.95) return "99.9%";
            if (percent >= 99.5)
                return String.Format(CultureInfo.InvariantCulture, "{0:0.0}%", percent);
            return String.Format(CultureInfo.InvariantCulture, "{0}%",
                (long)Math.Round(percent, MidpointRounding.AwayFromZero));
        }
    }

    internal sealed class ProgressState
    {
        public string kind { get; set; }
        public int? completed { get; set; }
        public int? total { get; set; }
        public string label { get; set; }

        public double? Fraction
        {
            get
            {
                if (!total.HasValue || total.Value <= 0) return null;
                double value = (double)(completed ?? 0) / total.Value;
                return Math.Min(1, Math.Max(0, value));
            }
        }
    }

    internal sealed class GoalState
    {
        public string phase { get; set; }
        public string objective { get; set; }
        public int? roundsStarted { get; set; }
        public int? maxGoalRounds { get; set; }
    }

    internal sealed class TodoItem
    {
        public string content { get; set; }
        public string status { get; set; }
    }

    internal sealed class SessionSummary
    {
        public string id { get; set; }
        public string title { get; set; }
        public bool? running { get; set; }
        public string phase { get; set; }
        public string tool { get; set; }
        public double? updatedAt { get; set; }
        public bool? root { get; set; }
    }

    internal enum PetPhase
    {
        Running,
        Thinking,
        Tool,
        Waiting,
        Done,
        Error,
        Idle
    }

    internal static class PetPhaseExtensions
    {
        public static PetPhase Parse(string raw)
        {
            switch (raw)
            {
                case "running": return PetPhase.Running;
                case "thinking": return PetPhase.Thinking;
                case "tool": return PetPhase.Tool;
                case "waiting": return PetPhase.Waiting;
                case "done": return PetPhase.Done;
                case "error": return PetPhase.Error;
                default: return PetPhase.Idle;
            }
        }

        public static bool IsBusy(this PetPhase phase)
        {
            return phase == PetPhase.Running || phase == PetPhase.Thinking || phase == PetPhase.Tool;
        }

        public static string Key(this PetPhase phase)
        {
            switch (phase)
            {
                case PetPhase.Running: return "running";
                case PetPhase.Thinking: return "thinking";
                case PetPhase.Tool: return "tool";
                case PetPhase.Waiting: return "waiting";
                case PetPhase.Done: return "done";
                case PetPhase.Error: return "error";
                default: return "idle";
            }
        }
    }
}
