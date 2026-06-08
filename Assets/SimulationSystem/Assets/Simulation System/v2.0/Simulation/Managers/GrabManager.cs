using UnityEngine;

public class GrabManager : MonoBehaviour
{
	#region Variables

    public static GrabManager Instance { get; private set; }
    [Header("Refrences")]
    public GrabPinchDetector leftPinchDetector;
    public GrabPinchDetector rightPinchDetector;
    #endregion

    #region Unity Methods
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    #endregion
}


