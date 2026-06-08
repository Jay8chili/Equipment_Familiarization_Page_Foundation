using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace SimulationSystem.V0._1.Utility.Miscellanous
{
    public class AnimatorBooleanController : MonoBehaviour
    {
        private Animator thisAnimator;

        private void Awake()
        {
             thisAnimator = GetComponent<Animator>();   
        }
        public void setBooleanFalse(string name)
        {
            thisAnimator.SetBool(name,false);
        }

        public void setBooleanTrue(string name)
        {
            thisAnimator.SetBool(name, true);
        }


    }
}
