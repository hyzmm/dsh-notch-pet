using System;

namespace DshNotchPet
{
    [Flags]
    internal enum PetChange
    {
        None = 0,
        Content = 1,
        Expanded = 2,
        Pinned = 4,
        Settings = 8,
        Connection = 16
    }

    internal sealed class PetViewModel
    {
        public const double CollapsedWidth = 360;
        public const double CollapsedHeight = 58;
        public const double ExpandedWidth = 390;
        public const double ExpandedHeight = 168;

        public PetPayload Payload { get; private set; }
        public bool Connected { get; private set; }
        public bool Pinned { get; private set; }
        public bool Expanded { get; private set; }
        public bool SettingsOpen { get; private set; }
        public bool Celebrating { get; set; }
        public DateTime CelebrationStarted { get; set; }
        public bool HasNotch { get { return false; } }

        public event Action<PetChange> Changed;

        public PetViewModel()
        {
            Payload = PetPayload.Empty();
        }

        public SessionFocus Focus { get { return Payload == null ? null : Payload.focus; } }
        public int ActiveCount { get { return Payload == null ? 0 : (Payload.activeCount ?? 0); } }

        public PetPhase Phase
        {
            get
            {
                PetPayload payload = Payload;
                if (payload == null) return PetPhase.Idle;
                string raw = payload.focus != null ? payload.focus.phase : payload.overall;
                return PetPhaseExtensions.Parse(raw);
            }
        }

        public bool HasActiveTask
        {
            get
            {
                PetPhase phase = Phase;
                return ActiveCount > 0 || phase.IsBusy() || phase == PetPhase.Waiting;
            }
        }

        public bool HasProgress
        {
            get { return Focus != null && Focus.progress != null && Focus.progress.Fraction.HasValue; }
        }

        public bool ShowsProgress
        {
            get { return Phase.IsBusy() || HasProgress; }
        }

        public string Origin
        {
            get { return Payload == null ? null : Payload.origin; }
        }

        public DesktopApp Desktop
        {
            get { return Payload == null ? null : Payload.desktop; }
        }

        public void ApplyPayload(PetPayload payload)
        {
            if (payload == null) return;
            PetPhase previous = Phase;
            Payload = payload;
            PetPhase next = Phase;
            if (next == PetPhase.Done && previous != PetPhase.Done)
            {
                Celebrating = true;
                CelebrationStarted = DateTime.UtcNow;
            }
            Raise(PetChange.Content);
        }

        public void SetConnected(bool connected)
        {
            if (Connected == connected) return;
            Connected = connected;
            Raise(PetChange.Connection);
        }

        public void SetExpanded(bool expanded)
        {
            if (Expanded == expanded) return;
            Expanded = expanded;
            Raise(PetChange.Expanded);
        }

        public void SetPinned(bool pinned)
        {
            if (Pinned == pinned) return;
            Pinned = pinned;
            Raise(PetChange.Pinned);
        }

        public void SetSettingsOpen(bool open)
        {
            if (SettingsOpen == open) return;
            SettingsOpen = open;
            Raise(PetChange.Settings);
        }

        public void RaiseContent()
        {
            Raise(PetChange.Content);
        }

        private void Raise(PetChange change)
        {
            Action<PetChange> handler = Changed;
            if (handler != null) handler(change);
        }
    }
}
