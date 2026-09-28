using System;

namespace PhysicsCharacterController
{
    public sealed class AnimationPlaybackCallbacks
    {
        private int _revision;
        private double _contactTime01;
        private Action _contact;
        private Action _completed;
        private bool _hasContacted;
        public bool IsRunning { get; private set; }

        #region Public Methods

        public void Begin(double contactTime01, Action contact, Action completed)
        {
            Cancel();
            _contactTime01 = Math.Max(0, Math.Min(1, contactTime01));
            _contact = contact;
            _completed = completed;
            _hasContacted = false;
            IsRunning = true;
        }

        public void Advance(double normalizedTime01)
        {
            if (!IsRunning) return;
            int revision = _revision;
            if (!_hasContacted && normalizedTime01 >= _contactTime01)
            {
                _hasContacted = true;
                _contact?.Invoke();
            }
            if (!IsRunning || revision != _revision || normalizedTime01 < 1) return;
            Action completed = _completed;
            Cancel();
            completed?.Invoke();
        }

        public void Cancel()
        {
            _revision++;
            IsRunning = false;
            _contact = null;
            _completed = null;
        }

        #endregion
    }
}
