using UnityEngine;

namespace SimulationSystem.V02.Assistant
{
    /// <summary>
    /// Attach this to the root GameObject of any panel that contains <see cref="CustomButton"/> components.
    ///
    /// RESPONSIBILITIES
    /// ────────────────
    /// • Finds every CustomButton collider in its own hierarchy once in Awake — no runtime searches.
    /// • Exposes <see cref="SetCollidersEnabled"/> so AssistantManager can enable/disable all button
    ///   colliders on this panel in one call without knowing anything about the button layout.
    /// • All colliders start disabled. AssistantManager enables them when the panel becomes the
    ///   active panel and disables them the moment the panel starts popping out or is snap-hidden.
    ///
    /// SETUP
    /// ─────
    /// • Place this component on the same root GameObject that AssistantManager holds a reference to.
    /// • Each CustomButton and its Collider must be on the same GameObject (as per CustomButton spec).
    /// • No configuration needed — collider discovery is automatic.
    /// </summary>
    public class PanelButtonController : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        // PRIVATE STATE
        // ─────────────────────────────────────────────

        /// <summary>
        /// All Colliders belonging to CustomButton components found in this panel's hierarchy.
        /// Initialised to an empty array so SetCollidersEnabled is always safe to call,
        /// even if called before Awake runs (e.g. from another component's Awake).
        /// Populated once in Awake. Never reallocated at runtime.
        /// </summary>
        private Collider[] _buttonColliders = new Collider[0];

        // ─────────────────────────────────────────────
        // LIFECYCLE
        // ─────────────────────────────────────────────

        private void Awake()
        {
            // Find every CustomButton in the hierarchy (including inactive children)
            // and grab the Collider on the same GameObject.
            // Nulls (CustomButtons missing a Collider) are filtered out so SetCollidersEnabled
            // never needs a null check and the array is always safe to iterate.
            // This runs once — all subsequent calls to SetCollidersEnabled are O(n) array iterations.
            CustomButton[] buttons = GetComponentsInChildren<CustomButton>(includeInactive: true);

            var validColliders = new System.Collections.Generic.List<Collider>(buttons.Length);

            for (int i = 0; i < buttons.Length; i++)
            {
                Collider col = buttons[i].GetComponent<Collider>();

                if (col != null)
                {
                    validColliders.Add(col);
                }
                else
                {
                    Debug.LogWarning($"[PanelButtonController] CustomButton on '{buttons[i].gameObject.name}' " +
                                     "has no Collider on the same GameObject — skipped.");
                }
            }

            _buttonColliders = validColliders.ToArray();

            // All button colliders start disabled.
            // AssistantManager enables them only when this panel becomes the active panel.
            SetCollidersEnabled(false);
        }

        // ─────────────────────────────────────────────
        // PUBLIC API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Enables or disables every CustomButton collider on this panel.
        /// Called by AssistantManager — enable on pop-in start, disable on pop-out start or snap-hidden.
        /// Safe to call at any time including before Awake — _buttonColliders is never null.
        /// </summary>
        /// <param name="enabled">True to allow button interaction, false to block it.</param>
        public void SetCollidersEnabled(bool enabled)
        {
            // Nulls are filtered at Awake — every entry here is a valid Collider.
            foreach (Collider col in _buttonColliders)
                col.enabled = enabled;
        }
    }
}