using NUnit.Framework;

namespace PhysicsCharacterController.Tests
{
    public sealed class AnimationPlaybackCallbacksTests
    {
        private AnimationPlaybackCallbacks _callbacks;
        private int _contacts;
        private int _completions;

        [SetUp]
        public void SetUp() { _callbacks = new AnimationPlaybackCallbacks(); _contacts = 0; _completions = 0; }

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

        private void StartNextPlayback() => _callbacks.Begin(0.5, CountContact, CountCompletion);
        private void CountContact() => _contacts++;
        private void CountCompletion() => _completions++;
    }
}
