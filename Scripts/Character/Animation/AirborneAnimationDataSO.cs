using Animancer;
using UnityEngine;

namespace PhysicsCharacterController
{
    [CreateAssetMenu(menuName = "Physics Character Controller/Character Animation/Airborne Animation Data")]
    public class AirborneAnimationDataSO : ScriptableObject
    {
        [SerializeField] private ClipTransition _jumpClip;
        [SerializeField] private ClipTransition _fallClip;

        public ClipTransition JumpClip => _jumpClip;
        public ClipTransition FallClip => _fallClip;
    }
}
