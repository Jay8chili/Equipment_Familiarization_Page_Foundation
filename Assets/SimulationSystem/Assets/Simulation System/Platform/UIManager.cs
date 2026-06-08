using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Platform
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager instance;

        private void Awake()
        {
            instance = this;
        }

        [SerializeField] private GameObject loginPanel;
        [SerializeField] private GameObject contentPanel;
        [SerializeField] private GameObject modulePanel;
        [SerializeField] private GameObject simulationPanel;

        private void Start()
        {
            if (GameManager.Instance.loggedIn)
            {
                OpenContentPanel();
                CloseLoginPanel();
            }
        }

        public void OpenContentPanel()
        {
            contentPanel.SetActive(true);
            modulePanel.SetActive(true);
        }

        public void OpenModulePanel()
        {
            modulePanel.SetActive(true);
        }

        public void CloseModulePanel()
        {
            modulePanel.SetActive(false);
        }
        
        public void OpenSimulationPanel(int id)
        {
            simulationPanel.SetActive(true);
            simulationPanel.GetComponent<PopulateSimulations>().FetchSimulations(id);
        }

        public void CloseSimulationPanel()
        {
            simulationPanel.SetActive(false);
        }

        public void CloseLoginPanel()
        {
            loginPanel.SetActive(false);
        }
    }
}
