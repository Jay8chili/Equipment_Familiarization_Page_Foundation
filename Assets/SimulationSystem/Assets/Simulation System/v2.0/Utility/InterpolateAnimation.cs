using UnityEngine;

namespace SimulationSystem.V0._1.Utility.Animation
{
    public class InterpolateAnimation : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        private float i = 0;

        private void Start()
        {
            if (_animator == null)
            {
                if (TryGetComponent<Animator>(out Animator animator))
                {
                    _animator = animator;
                }
            }
        }

        public void SetAnimationProgress(float ratio)
        {
            _animator.SetFloat("Normal", ratio);
        }
    }
}
