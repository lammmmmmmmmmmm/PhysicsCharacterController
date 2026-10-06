using System.Collections.Generic;
using Animancer;
using UnityEngine;

namespace PhysicsCharacterController
{
    public class CharacterAnimator : MonoBehaviour
    {
        private const int BASE_LAYER_INDEX = 0;

        [SerializeField] private AnimancerComponent _animancer;
        [SerializeField] private TransitionLibrary _transitions;
        [SerializeField] private AnimationChannelSO[] _animationChannelSOs;

        private readonly Dictionary<AnimationChannelSO, AnimationLayerChannel> _animationChannels = new();
        private ICharacterLocomotionOverride _locomotionOverride;
        private LocomotionAnimationDataSO _requestedLocomotionDataSO;
        private AirborneAnimationDataSO _requestedAirborneDataSO;
        private float _baseSourceSpeedMetersPerSecond;
        private bool _isPlayingJumpAnimation;
        private AnimancerState _activeTransitionState;
        private AnimancerState _currentBaseState;

        private LinearMixerTransition _queuedBaseMixer;
        private ClipTransition _queuedBaseClip;
        private float _queuedBaseSourceSpeedMetersPerSecond;

        private bool _isTransitionPlayingOnBaseLayer;
        private AnimancerComponent.DisableAction _disableActionBeforeTemporaryVisualDeactivation;
        private bool _isPreservingPlaybackDuringTemporaryVisualDeactivation;

        private AnimancerLayer BaseLayer => _animancer.Layers[BASE_LAYER_INDEX];

        public StateSO CurrentTag { get; private set; }
        public bool HasLocomotionOverride { get; private set; }

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeAnimationChannels();
            DisableAnimationRootMotion();
        }

        private void OnEnable()
        {
            ResetRuntimeStateCache();
        }

        private void Update()
        {
            SynchronizeBaseStateWithTransition();
            foreach (AnimationLayerChannel channel in _animationChannels.Values)
            {
                channel.UpdateCallbacks();
            }
        }

        private void OnDisable()
        {
            CancelPendingTransitionPlayback();
            ResetAnimationChannels();
            ResetRuntimeStateCache();
        }

        private void OnDestroy()
        {
            CancelPendingTransitionPlayback();
        }

        #endregion

        #region Public Methods

        public void PreservePlaybackDuringTemporaryVisualDeactivation()
        {
            if (_isPreservingPlaybackDuringTemporaryVisualDeactivation)
            {
                Debug.LogError(
                    $"Cannot preserve animation playback for '{name}' because temporary visual deactivation is already active.",
                    this);
                return;
            }

            _disableActionBeforeTemporaryVisualDeactivation = _animancer.ActionOnDisable;
            _animancer.ActionOnDisable = AnimancerComponent.DisableAction.Pause;
            _isPreservingPlaybackDuringTemporaryVisualDeactivation = true;
        }

        public void RestoreDisableBehaviorAfterTemporaryVisualReactivation()
        {
            if (!_isPreservingPlaybackDuringTemporaryVisualDeactivation)
            {
                Debug.LogError(
                    $"Cannot restore animation disable behavior for '{name}' because no temporary visual deactivation is active.",
                    this);
                return;
            }

            _animancer.ActionOnDisable = _disableActionBeforeTemporaryVisualDeactivation;
            _isPreservingPlaybackDuringTemporaryVisualDeactivation = false;
        }

        public bool IsChannelOwnedBy(AnimationChannelSO channelSO, object owner)
        {
            return TryGetAnimationChannel(channelSO, out var channel) && channel.IsOwnedBy(owner);
        }

        public bool PlayWithCallbacks(AnimationChannelSO channelSO, object owner, int priority, AnimationClip clip,
            float fadeSeconds, float playbackSpeed, float contactTime01, System.Action contact, System.Action completed)
        {
            if (!TryGetAnimationChannel(channelSO, out var channel))
            {
                return false;
            }

            return channel.PlayWithCallbacks(owner, priority, clip, fadeSeconds, playbackSpeed, contactTime01, contact, completed);
        }

        public void SetChannelSpeed(AnimationChannelSO channelSO, object owner, float playbackSpeed)
        {
            if (TryGetAnimationChannel(channelSO, out AnimationLayerChannel channel))
            {
                channel.SetSpeed(owner, playbackSpeed);
            }
        }

        public void SetLocomotionOverride(ICharacterLocomotionOverride locomotionOverride)
        {
            _locomotionOverride = locomotionOverride;
            RefreshLocomotionOverride();
        }

        public void SetBase(LocomotionAnimationDataSO animationDataSO, StateSO tag, float sourceSpeedMetersPerSecond)
        {
            LocomotionAnimationDataSO overrideSO = _locomotionOverride?.GetLocomotionOverride(tag);
            PlayBase((overrideSO != null ? overrideSO : animationDataSO).LocomotionMixer, tag, sourceSpeedMetersPerSecond,
                shouldPlayTransition: overrideSO == null);
            _requestedLocomotionDataSO = animationDataSO;
            HasLocomotionOverride = overrideSO != null;
        }

        public void SetBase(AirborneAnimationDataSO animationDataSO, bool isJumping, StateSO tag, float sourceSpeedMetersPerSecond)
        {
            AirborneAnimationDataSO overrideSO = _locomotionOverride?.GetAirborneOverride(tag);
            AirborneAnimationDataSO effectiveDataSO = overrideSO != null ? overrideSO : animationDataSO;
            PlayBase(isJumping ? effectiveDataSO.JumpClip : effectiveDataSO.FallClip, tag, sourceSpeedMetersPerSecond,
                shouldPlayTransition: overrideSO == null);
            _requestedAirborneDataSO = animationDataSO;
            _isPlayingJumpAnimation = isJumping;
            HasLocomotionOverride = overrideSO != null;
        }

        public void SetBase(LinearMixerTransition mixer, StateSO tag, float sourceSpeedMetersPerSecond)
        {
            PlayBase(mixer, tag, sourceSpeedMetersPerSecond, shouldPlayTransition: true);
        }

        public void SetBase(ClipTransition clip, StateSO tag, float sourceSpeedMetersPerSecond)
        {
            PlayBase(clip, tag, sourceSpeedMetersPerSecond, shouldPlayTransition: true);
        }

        public void UpdateTransitionMixerParameter(float sourceSpeedMetersPerSecond)
        {
            TrySetMixerParameter(_activeTransitionState, sourceSpeedMetersPerSecond);
        }

        public void UpdateLocomotionAnimationParameter(float sourceSpeedMetersPerSecond)
        {
            _baseSourceSpeedMetersPerSecond = sourceSpeedMetersPerSecond;
            if (_queuedBaseMixer != null)
            {
                _queuedBaseSourceSpeedMetersPerSecond = sourceSpeedMetersPerSecond;
                return;
            }

            if (!TrySetMixerParameter(_currentBaseState, sourceSpeedMetersPerSecond))
            {
                Debug.LogError($"Cannot update locomotion for '{name}' because its active base animation is not a valid mixer state.", this);
            }
        }

        public bool Play(
            AnimationChannelSO animationChannelSO,
            object animationOwner,
            int animationPriority,
            AnimationClip animationClip,
            float fadeDurationSeconds,
            bool shouldRestartAnimation = false)
        {
            if (animationOwner == null || animationClip == null || fadeDurationSeconds < 0f)
            {
                Debug.LogError(
                    $"Cannot play animation on '{name}'. Owner and clip are required, and fade duration cannot be negative.",
                    this);
                return false;
            }

            if (!TryGetAnimationChannel(animationChannelSO, out AnimationLayerChannel animationChannel))
            {
                return false;
            }

            return animationChannel.Play(animationOwner, animationPriority, animationClip, fadeDurationSeconds, shouldRestartAnimation);
        }

        public bool Stop(AnimationChannelSO animationChannelSO, object animationOwner, float fadeDurationSeconds)
        {
            if (animationOwner == null || fadeDurationSeconds < 0f)
            {
                Debug.LogError(
                    $"Cannot stop an animation channel on '{name}'. Owner is required, and fade duration cannot be negative.",
                    this);
                return false;
            }

            if (!TryGetAnimationChannel(animationChannelSO, out AnimationLayerChannel animationChannel))
            {
                return false;
            }

            return animationChannel.Stop(animationOwner, fadeDurationSeconds);
        }

        #endregion

        #region Private Methods

        private void PlayBase(LinearMixerTransition mixer, StateSO tag, float sourceSpeedMetersPerSecond, bool shouldPlayTransition)
        {
            _requestedLocomotionDataSO = null;
            _requestedAirborneDataSO = null;
            HasLocomotionOverride = false;
            _baseSourceSpeedMetersPerSecond = sourceSpeedMetersPerSecond;
            if (shouldPlayTransition && TryGetTransitionSelection(tag, sourceSpeedMetersPerSecond, out var selection))
            {
                CancelPendingTransitionPlayback();
                QueueBaseAnimation(mixer, sourceSpeedMetersPerSecond);
                PlayOneShotOnBaseLayer(selection, sourceSpeedMetersPerSecond);
            }
            else
            {
                CancelPendingTransitionPlayback();
                _currentBaseState = BaseLayer.Play(mixer);
                TrySetMixerParameter(_currentBaseState, sourceSpeedMetersPerSecond);
            }

            CurrentTag = tag;
        }

        private void PlayBase(ClipTransition clip, StateSO tag, float sourceSpeedMetersPerSecond, bool shouldPlayTransition)
        {
            _requestedLocomotionDataSO = null;
            _requestedAirborneDataSO = null;
            HasLocomotionOverride = false;
            _baseSourceSpeedMetersPerSecond = sourceSpeedMetersPerSecond;
            if (shouldPlayTransition && TryGetTransitionSelection(tag, sourceSpeedMetersPerSecond, out var selection))
            {
                CancelPendingTransitionPlayback();
                QueueBaseAnimation(clip);
                PlayOneShotOnBaseLayer(selection, sourceSpeedMetersPerSecond);
            }
            else
            {
                CancelPendingTransitionPlayback();
                _currentBaseState = BaseLayer.Play(clip);
            }

            CurrentTag = tag;
        }

        private void RefreshLocomotionOverride()
        {
            if (_requestedLocomotionDataSO != null)
            {
                SetBase(_requestedLocomotionDataSO, CurrentTag, _baseSourceSpeedMetersPerSecond);
            }
            else if (_requestedAirborneDataSO != null)
            {
                SetBase(_requestedAirborneDataSO, _isPlayingJumpAnimation, CurrentTag, _baseSourceSpeedMetersPerSecond);
            }
        }

        private void InitializeAnimationChannels()
        {
            _animationChannels.Clear();
            var configuredLayerIndices = new HashSet<int>();

            foreach (AnimationChannelSO animationChannelSO in _animationChannelSOs)
            {
                if (animationChannelSO.LayerIndex <= BASE_LAYER_INDEX)
                {
                    Debug.LogError(
                        $"Animation channel '{animationChannelSO.name}' targets reserved base layer {BASE_LAYER_INDEX}. " +
                        "Overlay channels must use a higher layer index.",
                        this);
                    continue;
                }

                if (_animationChannels.ContainsKey(animationChannelSO))
                {
                    Debug.LogError($"Animation channel '{animationChannelSO.name}' is configured more than once.", this);
                    continue;
                }

                if (!configuredLayerIndices.Add(animationChannelSO.LayerIndex))
                {
                    Debug.LogError(
                        $"Animation channel '{animationChannelSO.name}' reuses Animancer layer {animationChannelSO.LayerIndex}. " +
                        "Layer indices must be unique.",
                        this);
                    continue;
                }

                AnimancerLayer layer = _animancer.Layers[animationChannelSO.LayerIndex];
                _animationChannels.Add(animationChannelSO, new AnimationLayerChannel(animationChannelSO, layer));
            }
        }

        private void DisableAnimationRootMotion()
        {
            _animancer.Animator.applyRootMotion = false;
        }

        private bool TryGetAnimationChannel(AnimationChannelSO animationChannelSO, out AnimationLayerChannel animationChannel)
        {
            if (animationChannelSO == null)
            {
                Debug.LogError($"Cannot control an animation channel on '{name}' because no channel was provided.", this);
                animationChannel = null;
                return false;
            }

            if (_animationChannels.TryGetValue(animationChannelSO, out animationChannel))
            {
                return true;
            }

            Debug.LogError($"Animation channel '{animationChannelSO.name}' is not configured on '{name}'.", this);
            return false;
        }

        private bool TryGetTransitionSelection(StateSO newTag, float sourceSpeedMetersPerSecond,
            out TransitionLibrary.TransitionSelection selection)
        {
            if (!CurrentTag || CurrentTag == newTag)
            {
                CancelPendingTransitionPlayback();
                selection = default;
                return false;
            }

            if (_transitions.TryGet(CurrentTag, newTag, sourceSpeedMetersPerSecond, out var transition))
            {
                selection = transition;
                return true;
            }

            selection = default;
            return false;
        }

        private void QueueBaseAnimation(LinearMixerTransition mixer, float sourceSpeedMetersPerSecond)
        {
            _queuedBaseClip = null;
            _queuedBaseMixer = mixer;
            _queuedBaseSourceSpeedMetersPerSecond = sourceSpeedMetersPerSecond;
        }

        private void QueueBaseAnimation(ClipTransition clip)
        {
            _queuedBaseMixer = null;
            _queuedBaseClip = clip;
            _queuedBaseSourceSpeedMetersPerSecond = 0f;
        }

        private void PlayOneShotOnBaseLayer(TransitionLibrary.TransitionSelection selection, float sourceSpeedMetersPerSecond)
        {
            if (selection.Mode == TransitionLibrary.TransitionMode.Mixer)
            {
                _activeTransitionState = BaseLayer.Play(selection.Mixer);
                TrySetMixerParameter(_activeTransitionState, sourceSpeedMetersPerSecond);
            }
            else
            {
                _activeTransitionState = BaseLayer.Play(selection.Clip);
            }

            _isTransitionPlayingOnBaseLayer = true;
            _activeTransitionState.Events(this).OnEnd = FadeToQueuedBaseAnimation;
        }

        private void FadeToQueuedBaseAnimation()
        {
            if (!_isTransitionPlayingOnBaseLayer)
            {
                return;
            }

            if (_activeTransitionState is LinearMixerState activeTransitionMixerState && IsRuntimeStateValid(activeTransitionMixerState))
            {
                _queuedBaseSourceSpeedMetersPerSecond = activeTransitionMixerState.Parameter;
            }

            _isTransitionPlayingOnBaseLayer = false;
            if (IsRuntimeStateValid(_activeTransitionState))
            {
                _activeTransitionState.Events(this).OnEnd = null;
            }
            _activeTransitionState = null;

            if (_queuedBaseMixer != null)
            {
                _currentBaseState = BaseLayer.Play(_queuedBaseMixer);
                TrySetMixerParameter(_currentBaseState, _queuedBaseSourceSpeedMetersPerSecond);
            }
            else if (_queuedBaseClip != null)
            {
                _currentBaseState = BaseLayer.Play(_queuedBaseClip);
            }

            ClearQueuedBaseAnimation();
        }

        private void CancelPendingTransitionPlayback()
        {
            _isTransitionPlayingOnBaseLayer = false;

            if (IsRuntimeStateValid(_activeTransitionState))
            {
                _activeTransitionState.Events(this).OnEnd = null;
            }

            _activeTransitionState = null;
            ClearQueuedBaseAnimation();
        }

        private void ClearQueuedBaseAnimation()
        {
            _queuedBaseMixer = null;
            _queuedBaseClip = null;
            _queuedBaseSourceSpeedMetersPerSecond = 0f;
        }

        private void SynchronizeBaseStateWithTransition()
        {
            if (!IsRuntimeStateValid(_activeTransitionState) || !IsRuntimeStateValid(_currentBaseState))
            {
                return;
            }

            // Adjust speed so the cycles match perfectly
            // 2 animations need to have the similar pose at the same time so that syncing them looks good
            _currentBaseState.Speed = _currentBaseState.Length / _activeTransitionState.Length * _activeTransitionState.Speed;
            _currentBaseState.NormalizedTime = _activeTransitionState.NormalizedTime;
        }

        private void ResetRuntimeStateCache()
        {
            _requestedLocomotionDataSO = null;
            _requestedAirborneDataSO = null;
            HasLocomotionOverride = false;
            _activeTransitionState = null;
            _currentBaseState = null;
            _isTransitionPlayingOnBaseLayer = false;
            CurrentTag = null;
            ClearQueuedBaseAnimation();
        }

        private void ResetAnimationChannels()
        {
            foreach (AnimationLayerChannel animationChannel in _animationChannels.Values)
            {
                animationChannel.Reset();
            }
        }

        private static bool TrySetMixerParameter(AnimancerState state, float sourceSpeedMetersPerSecond)
        {
            if (!IsRuntimeStateValid(state) || state is not LinearMixerState mixerState)
            {
                return false;
            }

            mixerState.Parameter = sourceSpeedMetersPerSecond;
            return true;
        }

        private static bool IsRuntimeStateValid(AnimancerState state)
        {
            return state != null && state.IsValid();
        }

        #endregion
    }
}
