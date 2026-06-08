using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using System;

public class ObjectMovementHelper : MonoBehaviour
{
    #region  PublicDeclarations
    public bool ResetThisObjectOnRelease;
    //move to detect
    public UnityEvent onDestinationReached;
    #endregion

    #region  MoveToHelperDeclarations
    private int _resetDelay = 2;
   
    #endregion

    #region  RessetableDeclarations
    private bool _setupDone;
    private MyTransform ResetTransform;
    private Task PrevTask;

    private TaskCompletionSource<bool> _moveToPositionTaskCompletionSource;
    private CancellationTokenSource _moveToPositionCancellationToken;
    #endregion



    private void Awake()
    {
      
        SetupObject();

      
    }

    private void OnDisable()
    {
      _moveToPositionCancellationToken?.Cancel();
    }
    private void OnApplicationQuit()
    {
      _moveToPositionCancellationToken?.Cancel();
    }
    private void OnDestroy()
    {
      _moveToPositionCancellationToken?.Cancel();
    }
    
    public async void MoveToPosition(TransformContainer moveToTransform, bool needsDelay)
    {
        // Cancel the previous task if it exists
        _moveToPositionCancellationToken?.Cancel();
        _moveToPositionCancellationToken = new CancellationTokenSource();

        // Create a new task completion source
        _moveToPositionTaskCompletionSource = new TaskCompletionSource<bool>();

        try
        {
            if (needsDelay)
            {
                await Task.Delay(_resetDelay * 1000);
            }

            await transform.MoveToPositionAsync(moveToTransform, _moveToPositionCancellationToken.Token);

            // Notify that the destination has been reached
            onDestinationReached?.Invoke();
        }
        catch (OperationCanceledException)
        {
            // Handle cancellation if needed
        }
        finally
        {
            // Complete the task completion source
            _moveToPositionTaskCompletionSource.TrySetResult(true);
        }
    }
    private void SetupObject()
    {
        if (_setupDone) return;
        //else
        ResetTransform = new MyTransform(this.transform);
        _setupDone = true;
    }
    private void BeginReset()
    {
        if (ResetThisObjectOnRelease)
        {
            MoveToPosition(ResetTransform.GetThisTransform(), true);
        }

    }

}
