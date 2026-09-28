using UnityEngine;

namespace PhysicsCharacterController
{
    /// <summary>
    /// Visually decouples the character mesh from physics jitter, gated by surface roughness.
    /// On smooth ground (flat or gradual slopes) the mesh locks to the body after landing recovery.
    /// Rough terrain is detected by how fast the ground contact normal varies;
    /// there, vertical smoothing fades in to absorb the bumps. Horizontal rattle is always
    /// filtered tightly so stopping never reads as sliding. Attach to the mesh child of the
    /// physics root.
    /// </summary>
    public class CharacterMeshSmoother : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GroundChecker _groundChecker;

        [Header("Vertical Smoothing")]
        [SerializeField] private AirborneSmoothingSwitch _verticalSmoothing = new(groundedSmoothTimeSeconds: 0.15f, airborneSmoothTimeSeconds: 0.05f);
        [Tooltip("Max vertical distance the mesh may stray from its authored position on the collider")]
        [SerializeField] private float _maxVerticalOffsetMeters = 0.25f;

        [Header("Roughness Detection")]
        [SerializeField] private GroundSurfaceRoughnessTracker _surfaceRoughnessTracker = new();

        [Header("Horizontal Smoothing")]
        [Tooltip("Keep tight: only filters collision rattle. Higher values make the mesh slide when the character stops")]
        [SerializeField] private float _horizontalSmoothTimeSeconds = 0.06f;
        [Tooltip("Max horizontal distance the mesh may stray from its authored position on the collider")]
        [SerializeField] private float _maxHorizontalOffsetMeters = 0.1f;

        private Vector3 _authoredLocalPosition;
        private Vector3 _smoothedWorldPosition;
        private Vector3 _smoothVelocity;

        #region Unity Lifecycle

        private void Awake()
        {
            _authoredLocalPosition = transform.localPosition;
        }

        private void OnEnable()
        {
            _smoothedWorldPosition = GetTargetWorldPosition();
            _smoothVelocity = Vector3.zero;
            _verticalSmoothing.Reset();
            _surfaceRoughnessTracker.Reset();
        }

        private void LateUpdate()
        {
            FollowPhysicsBody();
        }

        #endregion

        private void FollowPhysicsBody()
        {
            if (Time.deltaTime <= 0f)
            {
                return;
            }

            bool isGrounded = _groundChecker.IsGrounded;
            float roughness01 = _surfaceRoughnessTracker.Update(isGrounded, _groundChecker.GroundHit.normal, Time.fixedDeltaTime, Time.deltaTime);

            Vector3 targetWorldPosition = GetTargetWorldPosition();

            float verticalOffsetMeters = _smoothedWorldPosition.y - targetWorldPosition.y;
            float verticalSmoothTimeSeconds = _verticalSmoothing.UpdateSmoothTimeSeconds(
                isGrounded,
                Time.deltaTime,
                verticalOffsetMeters,
                roughness01);

            float smoothedY = Mathf.SmoothDamp(_smoothedWorldPosition.y, targetWorldPosition.y, ref _smoothVelocity.y, verticalSmoothTimeSeconds);
            smoothedY = Mathf.Clamp(
                smoothedY,
                targetWorldPosition.y - _maxVerticalOffsetMeters,
                targetWorldPosition.y + _maxVerticalOffsetMeters);

            float smoothedX = Mathf.SmoothDamp(_smoothedWorldPosition.x, targetWorldPosition.x, ref _smoothVelocity.x, _horizontalSmoothTimeSeconds);
            float smoothedZ = Mathf.SmoothDamp(_smoothedWorldPosition.z, targetWorldPosition.z, ref _smoothVelocity.z, _horizontalSmoothTimeSeconds);
            Vector2 horizontalOffset = new(smoothedX - targetWorldPosition.x, smoothedZ - targetWorldPosition.z);
            if (horizontalOffset.sqrMagnitude > _maxHorizontalOffsetMeters * _maxHorizontalOffsetMeters)
            {
                horizontalOffset = horizontalOffset.normalized * _maxHorizontalOffsetMeters;
            }

            _smoothedWorldPosition = new Vector3(
                targetWorldPosition.x + horizontalOffset.x,
                smoothedY,
                targetWorldPosition.z + horizontalOffset.y);

            transform.position = _smoothedWorldPosition;
        }

        private Vector3 GetTargetWorldPosition()
        {
            return transform.parent.TransformPoint(_authoredLocalPosition);
        }
    }
}
