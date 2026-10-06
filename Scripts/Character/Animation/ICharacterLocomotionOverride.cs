namespace PhysicsCharacterController
{
    // Implementations supply replacements per state; null means retain the authored base data.
    public interface ICharacterLocomotionOverride
    {
        LocomotionAnimationDataSO GetLocomotionOverride(StateSO stateSO);
        AirborneAnimationDataSO GetAirborneOverride(StateSO stateSO);
    }
}
