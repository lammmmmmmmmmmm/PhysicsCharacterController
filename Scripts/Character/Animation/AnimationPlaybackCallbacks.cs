using System;
using System.Collections.Generic;

namespace PhysicsCharacterController
{
    public sealed class AnimationPlaybackCallbacks
    {
        private readonly List<double> _contactTimes01 = new();
        private int _revision;
        private int _nextContactIndex;
        private Action _contact;
        private Action<int> _indexedContact;
        private Action _completed;
        public bool IsRunning { get; private set; }

        #region Public Methods

        public void Begin(double contactTime01, Action contact, Action completed)
        {
            BeginPlayback(completed);
            _contactTimes01.Add(Clamp01(contactTime01));
            _contact = contact;
        }

        /// <summary>
        /// For clips that land several hits per playback: <paramref name="contact"/> fires once per contact time, in time
        /// order, with that contact's index in the sorted order.
        /// </summary>
        public void Begin(IReadOnlyList<float> contactTimes01, Action<int> contact, Action completed)
        {
            BeginPlayback(completed);
            for (int contactIndex = 0; contactIndex < contactTimes01.Count; contactIndex++)
            {
                _contactTimes01.Add(Clamp01(contactTimes01[contactIndex]));
            }

            _contactTimes01.Sort();
            _indexedContact = contact;
        }

        public void Advance(double normalizedTime01)
        {
            if (!IsRunning) return;
            int revision = _revision;
            while (_nextContactIndex < _contactTimes01.Count && normalizedTime01 >= _contactTimes01[_nextContactIndex])
            {
                int contactIndex = _nextContactIndex++;
                InvokeContact(contactIndex);
                // A contact may cancel or restart playback; the rest of this tick belongs to the old one.
                if (!IsRunning || revision != _revision) return;
            }

            if (normalizedTime01 < 1) return;
            Action completed = _completed;
            Cancel();
            completed?.Invoke();
        }

        public void Cancel()
        {
            _revision++;
            IsRunning = false;
            _contact = null;
            _indexedContact = null;
            _completed = null;
        }

        #endregion

        #region Private Methods

        private void BeginPlayback(Action completed)
        {
            Cancel();
            _contactTimes01.Clear();
            _nextContactIndex = 0;
            _completed = completed;
            IsRunning = true;
        }

        private void InvokeContact(int contactIndex)
        {
            _contact?.Invoke();
            _indexedContact?.Invoke(contactIndex);
        }

        private static double Clamp01(double time01)
        {
            return Math.Max(0, Math.Min(1, time01));
        }

        #endregion
    }
}
