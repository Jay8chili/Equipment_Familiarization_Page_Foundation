using UnityEngine;
using System.Collections;
namespace SimulationSystem.V02.StateInteractions
{


    [RequireComponent(typeof(Collider))]
    public class GazeInteraction : Interactions, IDetect
    {
        [Tooltip("If true, the timer resets to zero when interaction is suspended. If false, the timer pauses and holds its progress.")]
        public bool resetTimer;

        [Tooltip("The transform used as the origin for raycasting. If left empty, defaults to the main camera at runtime.")]
        public Transform objectToCastRay;

        [Tooltip("Optional: the specific collider to detect. Not required when this script is attached to the target object itself.")]
        public Collider objectToDetect;

        [Header("Gaze Settings")]

        [Tooltip("Radius of the SphereCast used for gaze detection at distance. Smaller values require more precise gaze, larger values are more forgiving.")]
        private readonly float sphereCastRadius = 0.2f;

        [Tooltip("Maximum distance the SphereCast will travel forward from the camera. Objects beyond this distance will not be detected.")]
        private readonly float sphereCastDistance = 10f;

        [Tooltip("The radius around the object within which interaction is permitted. Moving outside this radius suspends the interaction and locks it until the user re-enters.")]
        private readonly float proximityRadius = 4f;

        [Header("Radial Progress UI")]
        public RadialInteractionUI radialUI;
        protected override RadialInteractionUI RadialUI => radialUI;

        // Cached reference to the main camera transform, assigned at runtime in StartInteraction.
        private Transform mainCamera;

        // Tracks whether the user is currently within the valid interaction range.
        // This is the sole gatekeeper — interaction cannot start or resume unless this is true.
        private bool isInProximity = false;

        /// <summary>
        /// Entry point for the interaction. Caches the main camera and starts the gaze detection coroutine.
        /// </summary>
        public override void StartInteraction()
        {
            base.StartInteraction();
            mainCamera = Camera.main.transform;
            StartCoroutine(StartRaycast());
        }

        /// <summary>
        /// Called when the interaction is fully completed.
        /// Stops the gaze coroutine and deactivates the object to prevent re-triggering.
        /// </summary>
        public override void OnInteractionComplete()
        {
            base.OnInteractionComplete();
            StopCoroutine(StartRaycast());
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Called every frame while the interaction is active.
        /// </summary>
        public override void OnInteractionUpdate()
        {
            base.OnInteractionUpdate();
        }

        /// <summary>
        /// Called when the user begins or resumes gazing at the object while in proximity.
        /// Starts or resumes the interaction timer.
        /// </summary>
        public override void OnInteractionStart()
        {
            base.OnInteractionStart();
            InteractionTimer.StartTimer();
        }

        /// <summary>
        /// Called when the user looks away or moves out of proximity.
        /// Pauses or resets the timer depending on the resetTimer flag.
        /// </summary>
        public override void OnInteractionSuspend()
        {
            if (canInteract)
            {
                base.OnInteractionSuspend();
                InteractionTimer.StopTimer(resetTimer);
            }
        }

        /// <summary>
        /// Updates isInProximity every frame based on the distance between
        /// the camera and this object. This is the sole authority on whether
        /// interaction is permitted — SphereCast and gaze checks are ignored
        /// if the user is outside this radius.
        /// </summary>
        private void UpdateProximity()
        {
            float distance = Vector3.Distance(objectToCastRay.position, transform.position);
            isInProximity = distance <= proximityRadius;
        }

        /// <summary>
        /// Determines whether the user is currently looking at this object.
        /// Uses two detection methods in sequence:
        /// 1. SphereCast — accurate gaze detection at normal distances.
        /// 2. Dot product fallback — handles close range and cases where
        ///    the camera is inside the object's collider (where SphereCast fails).
        /// </summary>
        private bool IsLookingAtObject()
        {
            // Primary: SphereCast from camera forward
            // Detects the object accurately when the camera is at a normal distance
            RaycastHit hit;
            if (Physics.SphereCast(objectToCastRay.position, sphereCastRadius, objectToCastRay.forward, out hit, sphereCastDistance))
            {
                if (hit.collider.gameObject == gameObject)
                    return true;
            }

            // Fallback: Dot product gaze check
            // Used when SphereCast fails at very close range or when the camera
            // is inside the object's collider bounds. Checks if the object is
            // within a ~60 degree forward cone from the camera (dot > 0.5f).
            Vector3 directionToObject = (transform.position - objectToCastRay.position).normalized;
            float dot = Vector3.Dot(objectToCastRay.forward, directionToObject);
            return dot > 0.5f;
        }

        /// <summary>
        /// Core gaze detection loop. Runs every frame until the interaction is completed.
        /// Two-gate system:
        /// Gate 1 — Proximity: is the user within range?
        /// Gate 2 — Gaze: is the user looking at the object?
        /// Both must be true for the interaction to run.
        /// </summary>
        IEnumerator StartRaycast()
        {
            // Default to main camera if no custom ray origin is assigned
            if (objectToCastRay == null)
                objectToCastRay = mainCamera;

            while (!IsCompleted)
            {
                if (canInteract)
                {
                    // Update proximity state first — this gates everything below
                    UpdateProximity();

                    if (isInProximity && IsLookingAtObject())
                    {
                        // User is in range and looking at the object — start or resume
                        OnInteractionStart();
                    }
                    else
                    {
                        // Either out of range or looking away — suspend and hold timer
                        OnInteractionSuspend();
                    }
                }

                yield return null;
            }
        }

        protected override IEnumerator ExecuteInteraction()
        {
            yield return null;
        }

        /// <summary>
        /// Editor visualisation for tuning gaze and proximity settings.
        /// Green sphere — within proximity range, red sphere — outside range.
        /// Yellow spheres — SphereCast tunnel start and end.
        /// </summary>
        private void OnDrawGizmos()
        {
            if (objectToCastRay == null) return;

            // Proximity boundary drawn around the object
            // Green = user is inside range, Red = user is outside range
            Gizmos.color = isInProximity ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position, proximityRadius);

            // SphereCast tunnel — shows the gaze detection cone from camera forward
            // Left sphere = cast origin, Right sphere = cast end at max distance
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(objectToCastRay.position, sphereCastRadius);
            Gizmos.DrawWireSphere(objectToCastRay.position + objectToCastRay.forward * sphereCastDistance, sphereCastRadius);
        }
    }
}
