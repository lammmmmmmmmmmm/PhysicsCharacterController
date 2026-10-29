using System;
using HSM;
using NUnit.Framework;

namespace PhysicsCharacterController.Tests
{
    public sealed class StateMachineTickAllocationTests
    {
        #region Public Methods

        [Test]
        public void RepeatedTicks_AdvanceStateWithoutAllocatingDelegates()
        {
            var state = new CountingState();
            var machine = new StateMachine(state);
            machine.Tick(0.02f);
            machine.FixedTick(0.02f);

            long initialAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
            for (int tickIndex = 0; tickIndex < 1000; tickIndex++)
            {
                machine.Tick(0.02f);
                machine.FixedTick(0.02f);
            }
            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - initialAllocatedBytes;

            Assert.That(state.UpdateCount, Is.EqualTo(1001));
            Assert.That(state.FixedUpdateCount, Is.EqualTo(1001));
            Assert.That(allocatedBytes, Is.Zero);
        }

        #endregion

        private sealed class CountingState : State
        {
            public int UpdateCount { get; private set; }
            public int FixedUpdateCount { get; private set; }

            public CountingState() : base(null) { }

            protected override void OnUpdate(float deltaTime) => UpdateCount++;
            protected override void OnFixedUpdate(float fixedDeltaTime) => FixedUpdateCount++;
        }
    }
}
