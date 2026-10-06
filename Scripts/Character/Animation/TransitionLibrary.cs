using System.Collections.Generic;
using UnityEngine;

namespace PhysicsCharacterController
{
    [CreateAssetMenu(menuName = "Character Animation/Transition Library")]
    public class TransitionLibrary : ScriptableObject
    {
        public List<Entry> Entries;

        public readonly struct TransitionSelection
        {
            public TransitionMode Mode { get; }
            public Animancer.ClipTransition Clip { get; }
            public Animancer.LinearMixerTransition Mixer { get; }

            public TransitionSelection(Animancer.ClipTransition clip)
            {
                Mode = TransitionMode.Clip;
                Clip = clip;
                Mixer = null;
            }

            public TransitionSelection(Animancer.LinearMixerTransition mixer)
            {
                Mode = TransitionMode.Mixer;
                Clip = null;
                Mixer = mixer;
            }
        }

        public enum TransitionMode
        {
            Clip = 0,
            Mixer = 1,
        }

        [System.Serializable]
        public class Entry
        {
            public StateSO From;
            public StateSO To;
            public float MinSourceSpeed = 0f;
            public float MaxSourceSpeed = float.MaxValue;
            public TransitionMode Mode = TransitionMode.Clip;
            public Animancer.ClipTransition Clip;
            public Animancer.LinearMixerTransition Mixer;

            public bool IsMatching(StateSO from, StateSO to, float sourceSpeed)
            {
                return From == from && To == to && sourceSpeed >= MinSourceSpeed && sourceSpeed <= MaxSourceSpeed;
            }

            public bool TryGetSelection(out TransitionSelection selection)
            {
                if (Mode == TransitionMode.Mixer)
                {
                    if (Mixer == null)
                    {
                        selection = default;
                        return false;
                    }

                    selection = new TransitionSelection(Mixer);
                    return true;
                }

                if (Clip == null)
                {
                    selection = default;
                    return false;
                }

                selection = new TransitionSelection(Clip);
                return true;
            }
        }

        public bool TryGet(StateSO from, StateSO to, float sourceSpeed, out TransitionSelection selection)
        {
            if (!from || !to || Entries == null)
            {
                selection = default;
                return false;
            }

            foreach (var e in Entries)
            {
                if (e.IsMatching(from, to, sourceSpeed) && e.TryGetSelection(out selection))
                {
                    return true;
                }
            }

            selection = default;
            return false;
        }
    }
}