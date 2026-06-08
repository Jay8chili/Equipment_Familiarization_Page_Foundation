using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Threading;
using static UnityEngine.InputSystem.InputAction;
namespace Platform
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;
        public int userID;
        public string role;
        public bool loggedIn;
        public enum SceneType
        {
            MainScene,
            Simulation
        };
        public SceneType sceneType;
        public int simulationID;
        public string simulationCode;
        
        private HintVRControls _controls;

        public InputActionReference inputActionReference;

        private void Awake()
        {
            if(Instance == null)
                Instance = this;
            else
            {
                Destroy(this.gameObject);
            }
            DontDestroyOnLoad(this.gameObject);
            DontDestroyOnLoad(transform.parent.gameObject);


            inputActionReference.action.performed += BackToMainScene;
            
            

        }
        private void OnDisable()
        {
            inputActionReference.action.performed -= BackToMainScene;

        }

        void BackToMainScene(CallbackContext a)
        {
            // if (sceneType == SceneType.Simulation)
            //     SwitchToMainScene();
            //
        }

        private void Start()
        {
            _controls = new HintVRControls();
            _controls.Enable();
            _controls.Player.MenuButton.performed += ctx =>
            {
                Debug.Log("Menu button pressed");
                if (sceneType == SceneType.Simulation)
                    SwitchToMainScene();
            };
        }

        private void Update()
        {
            if(Input.GetKeyDown(KeyCode.B))
            {
                if (sceneType == SceneType.Simulation)
                    SwitchToMainScene();
            }
            
        }

        public void SwitchToMainScene()
        {
            SceneHandler.Instance.ChangeScene("MainScene");
            sceneType = SceneType.MainScene;
        }

        public void SwitchToSimulation(string scene)
        {
            sceneType = SceneType.Simulation;
         /*   if(SimulationSystem.V0._1.Manager.GameManager.Instance != null)
            {
                SimulationSystem.V0._1.Manager.GameManager.Instance.onBackToMainMenu.AddListener(SwitchToMainScene);
            }*/
        }
        

    }
}