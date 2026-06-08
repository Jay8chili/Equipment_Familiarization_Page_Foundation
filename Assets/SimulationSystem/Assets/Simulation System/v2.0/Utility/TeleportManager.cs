using SimulationSystem.V0._1.Utility.Miscellanous;
using SimulationSystem.V02.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace SimulationSystem.V02.Utility
{
    public class TeleportManager : MonoBehaviour
    {
        public static TeleportManager Instance { get; private set; }

        [SerializeField] private ScreenFade OVR;
        [SerializeField] private AudioSource promptAudioSrc;

        [Header("Object Teleport")]
        [Tooltip("Offset applied in front of the player when teleporting an object. " +
                 "X = right, Y = up, Z = forward.")]
        public Vector3 objectTeleportOffset = new Vector3(0f, 0f, 1f);

        [Header("Teleport Message")]
        [SerializeField] private string teleportMessage = "Adjusting your position...";
        [Tooltip("How long the message stays on screen before the reveal")]
        [SerializeField] private float messageHoldTime = 1f;

        // Fired when the player teleport coroutine fully completes (fade in done).
        // SimulationState subscribes to this to delay starting interactions.
        public static Action TeleportStarted;
        public static Action TeleportCompleted;

        private CharacterController Cc;
        private bool isFadeAudio = true;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                return;
            }
            Instance = this;
        }

        void Start()
        {
            Cc = GetComponent<CharacterController>();
        }

        public void UpdatePlayerPos(Transform newPos)
        {
            StartCoroutine(SyncPlayerPosition(newPos));
        }

        IEnumerator SyncPlayerPosition(Transform pos)
        {
            TeleportStarted?.Invoke();

            // ── Fade out (sphere fills bottom → top) ────────────────────
            FadeAudio(true);

            bool fadeOutDone = false;
            OVR.OnFadeOutComplete += FlagDone;
            OVR.FadeOut();

            // Wait until the sphere is fully filled
            yield return new WaitUntil(() => fadeOutDone);

            Cc.enabled = false;
            // ── Move the player while the screen is fully black ─────────
            transform.position = pos.position;
            transform.rotation = pos.rotation;

            // Small extra frame to let physics / tracking settle
            yield return null;

            // ── Show transition message ─────────────────────────────────
            OVR.ShowMessage(teleportMessage);
            yield return new WaitForSeconds(messageHoldTime);
            OVR.HideMessage();

            // Brief pause so the text finishes fading out before the sphere drains
            yield return new WaitForSeconds(0.3f);

            // ── Fade in (sphere drains top → bottom) ────────────────────
            bool fadeInDone = false;
            OVR.OnFadeInComplete += FlagRevealDone;
            OVR.FadeIn();

            yield return new WaitUntil(() => fadeInDone);

            Cc.enabled = true;

            FadeAudio();

            TeleportCompleted?.Invoke();

            // ── Local helpers to avoid allocations from lambdas ─────────
            void FlagDone()
            {
                fadeOutDone = true;
                OVR.OnFadeOutComplete -= FlagDone;
            }
            void FlagRevealDone()
            {
                fadeInDone = true;
                OVR.OnFadeInComplete -= FlagRevealDone;
            }
        }

        /// <summary>
        /// Teleports the given object to a position in front of the player,
        /// applying objectTeleportOffset in the player's local space.
        /// </summary>
        public void TeleportObject(GameObject obj)
        {
            if (obj == null)
            {
                Debug.LogWarning("[TeleportManager] TeleportObject called with null object.");
                return;
            }

            Vector3 targetPosition = transform.position
                                   + transform.right * objectTeleportOffset.x
                                   + transform.up * objectTeleportOffset.y
                                   + transform.forward * objectTeleportOffset.z;

            obj.transform.position = targetPosition;
            Debug.Log($"[TeleportManager] Teleported '{obj.name}' to {targetPosition}");
        }

        void FadeAudio(bool fadeout = false)
        {
            if (isFadeAudio)
            {
                if (fadeout)
                {
                    promptAudioSrc.mute = true;
                }
                else
                {
                    promptAudioSrc.time = 0;
                    promptAudioSrc.mute = false;
                    if (SimulationManager.Instance.simulationMode == SimulationMode.Guided)
                    {
                        promptAudioSrc.Play();
                    }
                }
            }
        }
    }
}