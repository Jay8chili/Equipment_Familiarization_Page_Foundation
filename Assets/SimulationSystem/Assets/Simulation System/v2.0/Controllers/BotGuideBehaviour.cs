using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BotGuideBehaviour
/// ------------------------------------------------------------
/// Coroutine-driven guide bot that leads the player to a target
/// using a grid-based path.
///
/// IMPORTANT DESIGN CHOICE:
/// - Arrival is determined ONLY by the bot entering a destination radius
/// - Player position NEVER ends the guidance
/// - Player only influences how far the bot is allowed to move ahead
/// </summary>
public class BotGuideBehaviour : MonoBehaviour
{
    // Events
    public static Action GuideFinished;

    #region References

    [Header("References")]
    public Transform player;
    public GridPathfinder pathfinder;
    public Transform target;

    #endregion

    #region Movement

    [Header("Movement")]
    public float moveSpeed = 1.5f;
    public float rotateSpeed = 6f;
    public float waypointTolerance = 0.15f;

    #endregion

    #region Player Constraints

    [Header("Player Constraints")]
    [Tooltip("Absolute max distance bot can be ahead of player")]
    public float maxLeadDistance = 2.0f;

    [Tooltip("Preferred lead distance before bot waits")]
    public float desiredLeadDistance = 1.4f;

    #endregion

    #region Arrival

    [Header("Arrival")]
    [Tooltip("If the bot enters this radius around the destination, guidance ends")]
    public float botArrivalRadius = 0.6f;

    #endregion

    #region VR Head Tracking

    [Header("VR Head Tracking")]
    [Tooltip("Assign your Main Camera / CenterEyeAnchor here")]
    public Transform vrHead;
    [Tooltip("Vertical offset from eye level. Negative = below eyes, positive = above.")]
    public float headHeightOffset = 0f;

    #endregion

    #region Debug

    [Header("Debug")]
    public GuideBotState currentState = GuideBotState.Idle;

    #endregion

    #region Internals

    private Coroutine guideRoutine;
    private List<Vector3> path;
    private int pathIndex;
    private Vector3 finalTarget;

    #endregion

    #region Public API

    /// <summary>
    /// Starts guiding the player toward a target position.
    /// </summary>
    public void GuideTo(Vector3 targetWorldPos)
    {
        StopGuidance();
        Debug.Log("[BotGuideBeaviour]" + "Guideto is called");
        Debug.Log("[BotGuideBeaviour]" + targetWorldPos);

        guideRoutine = StartCoroutine(GuideRoutine(targetWorldPos));
    }

    /// <summary>
    /// Stops guidance immediately.
    /// </summary>
    public void StopGuidance()
    {
        if (guideRoutine != null)
        {
            StopCoroutine(guideRoutine);
            guideRoutine = null;
        }

        currentState = GuideBotState.Idle;
    }

    #endregion

    #region Helpers

    private float CurrentHeadY =>
        vrHead != null ? vrHead.position.y + headHeightOffset : transform.position.y;

    #endregion

    #region Core Coroutine

    /// <summary>
    /// Main guidance coroutine acting as a finite-state machine.
    /// </summary>
    private IEnumerator GuideRoutine(Vector3 targetWorldPos)
    {
        currentState = GuideBotState.ComputingPath;

        if (!pathfinder.ComputePathWorld(
                transform.position,
                targetWorldPos,
                out path))
        {
            currentState = GuideBotState.Aborted;
            Debug.Log("[BotguideBehaviour]" + "1");
            yield break;
        }

        if (path == null || path.Count < 2)
        {
            currentState = GuideBotState.Aborted;
            Debug.Log("[BotguideBehaviour]" + "2");

            yield break;
        }

        pathIndex = 0;
        finalTarget = new Vector3(targetWorldPos.x, CurrentHeadY, targetWorldPos.z);

        currentState = GuideBotState.MovingAlongPath;

        while (true)
        {
            // ----------------------------------------------------
            // BOT ARRIVAL CHECK (ONLY TERMINATION CONDITION)
            // ----------------------------------------------------
            if (HorizontalDistance(transform.position, finalTarget) <= botArrivalRadius)
            {
                currentState = GuideBotState.Arrived;
                Debug.Log("GuideBot: Bot reached destination radius");
                GuideFinished?.Invoke();
                StopGuidance();
                yield break;
            }

            float playerDistance =
                HorizontalDistance(player.position, transform.position);

            // ----------------------------------------------------
            // HARD LEAD LIMIT
            // ----------------------------------------------------
            if (playerDistance >= maxLeadDistance)
            {
                currentState = GuideBotState.WaitingForPlayer;
                LookAtPlayer();
                yield return null;
                continue;
            }

            // ----------------------------------------------------
            // SOFT LEAD LIMIT
            // ----------------------------------------------------
            if (playerDistance >= desiredLeadDistance)
            {
                currentState = GuideBotState.WaitingForPlayer;
                LookAtPlayer();
                yield return null;
                continue;
            }

            // ----------------------------------------------------
            // MOVE ALONG PATH
            // ----------------------------------------------------
            currentState = GuideBotState.MovingAlongPath;

            Vector3 waypoint = path[pathIndex];
            waypoint.y = CurrentHeadY;

            MoveTowards(waypoint);

            if (Vector3.Distance(transform.position, waypoint) <= waypointTolerance)
            {
                pathIndex++;
                if (pathIndex >= path.Count)
                    pathIndex = path.Count - 1;
            }

            yield return null;
        }
    }

    #endregion

    #region Helpers

    private void MoveTowards(Vector3 target)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            return;

        Quaternion lookRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            lookRot,
            rotateSpeed * Time.deltaTime
        );

        transform.position = Vector3.MoveTowards(
            transform.position,
            target,
            moveSpeed * Time.deltaTime
        );
    }

    private void LookAtPlayer()
    {
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
            return;

        Quaternion lookRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            lookRot,
            rotateSpeed * Time.deltaTime
        );
    }

    private float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    #endregion
}

/// <summary>
/// Finite states for the guide bot lifecycle.
/// </summary>
public enum GuideBotState
{
    Idle,               // Not guiding
    ComputingPath,      // Requesting path from GridPathfinder
    MovingAlongPath,    // Actively leading the player
    WaitingForPlayer,   // Paused, facing player
    Arrived,            // Destination reached
    Aborted             // Failed or cancelled
}