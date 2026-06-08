
using SimulationSystem.V02.Extensions;
using System;
using UnityEngine;
using System.Threading;
namespace SimulationSystem.V0._1.UI
{
    public class UIAnimationHandler : MonoBehaviour
    {

        public bool IsForPrompts;
        public bool IsActive { get; private set; } = false;

        [SerializeField] private float animationTime = 3;
        [SerializeField] private Vector3 hoverScale = Vector3.one;
        [SerializeField] private Vector3 unHoverScale = Vector3.zero;


        [SerializeField] private Transform uiTransform;

        private DetectStates _states = DetectStates.Normal;
        private LabelStatus _status = LabelStatus.None;

        public void OnDetectOnce() => OnDetect();
        public void OnDetecting() { }
        public void OnDetecting(float value) { }
        public void OnDetectingFinished() { }
        public void OnUnDetected() => OnRemove();

        private void OnDetect()
        {
            _states = DetectStates.Detect;
        }
        private void OnRemove()
        {
            _states = DetectStates.UnDetect;
        }
        private async void Update()
        {

            if (_states == DetectStates.Detect && _status == LabelStatus.None)
            {
                if (!uiTransform)
                {
                    await this.transform.DoPop(hoverScale.x, animationTime, onComplete: () => { });
                }
                else
                {
                    await this.transform.DoPop(hoverScale.x, animationTime, onComplete: () => { });
                }

                _status = LabelStatus.Show;
            }
            else if (_states == DetectStates.UnDetect && _status == LabelStatus.Show)
            {
                if (!uiTransform)
                {
                    await this.transform.DoPop(unHoverScale.x, animationTime, onComplete:
                   () =>
                   {
                       _states = DetectStates.Normal;
                       _status = LabelStatus.None;
                   });
                }
                else
                {
                    await this.transform.DoPop(unHoverScale.x, animationTime, onComplete:
                       () =>

                       {
                           _states = DetectStates.Normal;
                           _status = LabelStatus.None;
                       });
                }

                _status = LabelStatus.Hide;
            }
            else if (_states == DetectStates.Normal && _status == LabelStatus.Hide)
            {
                _status = LabelStatus.None;
            }


        }


        public void DisableDetection()
        {
            //isFirstTrigger = false;
            IsActive = true;
        }

        #region PromptScreenFunctions
        public async void ScaleUp()
        {
            transform.localScale = this.hoverScale;
        }
        public async void ScaleDown()
        {

            transform.localScale = this.unHoverScale;
        }

        #endregion

    }
}
public enum DominantHand
{
    None,
    LeftHand,
    RightHand,
}

[Serializable]
public enum DetectStates
{
    Normal,
    Detect,
    UnDetect,
}

[Serializable]
public enum LabelStatus
{
    None,
    Show,
    Hide,
}