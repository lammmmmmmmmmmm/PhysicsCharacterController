using System.Collections.Generic;
using NUnit.Framework;

namespace PhysicsCharacterController.Tests
{
    public sealed class AnimationPlaybackCallbacksTests
    {
        private AnimationPlaybackCallbacks _callbacks;
        private int _contacts;
        private int _completions;
        private readonly List<int> _contactIndices = new();

        [SetUp]
        public void SetUp() { _callbacks = new AnimationPlaybackCallbacks(); _contacts = 0; _completions = 0; _contactIndices.Clear(); }

        [Test]
        public void Advance_CrossesContactAndEnd_InvokesEachOnce()
        {
            _callbacks.Begin(0.5, CountContact, CountCompletion);
            _callbacks.Advance(0.49);
            Assert.That(_contacts, Is.Zero);
            _callbacks.Advance(1.2);
            _callbacks.Advance(2);
            Assert.That(_contacts, Is.EqualTo(1));
            Assert.That(_completions, Is.EqualTo(1));
        }

        [Test]
        public void Cancel_BeforeContact_SuppressesBothCallbacks()
        {
            _callbacks.Begin(0.5, CountContact, CountCompletion);
            _callbacks.Cancel();
            _callbacks.Advance(2);
            Assert.That(_contacts, Is.Zero);
            Assert.That(_completions, Is.Zero);
        }

        [Test]
        public void Begin_SupersedesPreviousPlayback_DiscardsPreviousCallbacks()
        {
            _callbacks.Begin(0.5, CountContact, CountCompletion);
            _callbacks.Begin(0.5, null, null);
            _callbacks.Advance(2);
            Assert.That(_contacts, Is.Zero);
            Assert.That(_completions, Is.Zero);
        }

        [Test]
        public void Contact_CancelsPlayback_SuppressesSameTickCompletion()
        {
            _callbacks.Begin(0.5, _callbacks.Cancel, CountCompletion);
            _callbacks.Advance(2);
            Assert.That(_completions, Is.Zero);
        }

        [Test]
        public void Contact_StartsNextPlayback_DoesNotAdvanceItWithPreviousTime()
        {
            _callbacks.Begin(0.5, StartNextPlayback, CountCompletion);
            _callbacks.Advance(2);
            Assert.That(_contacts, Is.Zero);
            Assert.That(_completions, Is.Zero);
            _callbacks.Advance(0.5);
            Assert.That(_contacts, Is.EqualTo(1));
        }

        [Test]
        public void Advance_SeveralContacts_InvokesEachOnceInTimeOrder()
        {
            _callbacks.Begin(new[] { 0.75f, 0.25f, 0.5f }, RecordContactIndex, CountCompletion);
            _callbacks.Advance(0.3);
            Assert.That(_contactIndices, Is.EqualTo(new[] { 0 }));
            _callbacks.Advance(0.8);
            _callbacks.Advance(0.9);
            Assert.That(_contactIndices, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(_completions, Is.Zero);
        }

        [Test]
        public void Advance_JumpsPastSeveralContactsAndEnd_InvokesAllContactsBeforeCompletion()
        {
            _callbacks.Begin(new[] { 0.25f, 0.75f }, RecordContactIndex, RecordCompletionAfterContacts);
            _callbacks.Advance(1.5);
            Assert.That(_contactIndices, Is.EqualTo(new[] { 0, 1, -1 }));
        }

        [Test]
        public void Contact_CancelsPlaybackMidway_SuppressesLaterContacts()
        {
            _callbacks.Begin(new[] { 0.25f, 0.75f }, CancelOnFirstContact, CountCompletion);
            _callbacks.Advance(2);
            Assert.That(_contactIndices, Is.EqualTo(new[] { 0 }));
            Assert.That(_completions, Is.Zero);
        }

        [Test]
        public void Begin_NoContactTimes_StillCompletes()
        {
            _callbacks.Begin(new float[0], RecordContactIndex, CountCompletion);
            _callbacks.Advance(1);
            Assert.That(_contactIndices, Is.Empty);
            Assert.That(_completions, Is.EqualTo(1));
        }

        private void RecordContactIndex(int contactIndex) => _contactIndices.Add(contactIndex);
        private void RecordCompletionAfterContacts() => _contactIndices.Add(-1);
        private void CancelOnFirstContact(int contactIndex) { _contactIndices.Add(contactIndex); _callbacks.Cancel(); }
        private void StartNextPlayback() => _callbacks.Begin(0.5, CountContact, CountCompletion);
        private void CountContact() => _contacts++;
        private void CountCompletion() => _completions++;
    }
}
