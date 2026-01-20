/*
 * Script Name: Portal.cs
 * Author:      Cyans
 * Affiliation: Chang'an University
 * Date:        November 15, 2025
 * 
 * Description: Level transition portal that checks for remaining enemies before
 *              allowing teleportation. Includes fade-out effect and scene loading
 *              with error handling.
 */

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement; 

public class Portal : MonoBehaviour
{
    #region --- Inspector Settings ---

    [Header("Level Settings")]
    [Tooltip("Next level name (must match Build Settings)")]
    public string nextSceneName = "Level2";

    [Tooltip("Wait time before teleport (for fade effect)")]
    public float waitTime = 0.2f;

    [Header("Combat Detection")]
    [Tooltip("Drag the Enemys parent object from scene here")]
    public Transform enemiesParent;

    #endregion

    #region --- Internal State ---

    // Prevent repeated triggering
    private bool isTriggered = false; 

    #endregion

    #region --- Unity Lifecycle ---

    private void OnTriggerEnter2D(Collider2D other) {
        if (isTriggered) return;
        
        if (other.CompareTag("Player") || other.CompareTag("player")) {
            
            // Check if there are still living monsters before entering
            if (CheckEnemiesAlive()) {
                Debug.Log("Portal inactive - enemies still alive");
                return;
            }

            StartCoroutine(TeleportProcess(other.gameObject));
        } 
    }

    #endregion

    #region --- Portal Logic ---

    // Check if there are living monsters
    private bool CheckEnemiesAlive() {
        // If parent not bound, allow by default
        if (enemiesParent == null) return false;

        // Get all rigidbody components in children
        // Because AttackPoint is empty object without rigidbody, it won't be counted
        // Only living monsters will be counted
        Rigidbody2D[] remainingEnemies = enemiesParent.GetComponentsInChildren<Rigidbody2D>();

        // If count > 0, means there are still monsters not fully dead
        if (remainingEnemies.Length > 0) {
            Debug.Log("Remaining enemy count: " + remainingEnemies.Length);
            return true;
        } else {
            return false;
        }
    }

    // Teleport process coroutine
    private IEnumerator TeleportProcess(GameObject player) {
        isTriggered = true;
        Debug.Log("Player detected, preparing to teleport to " + nextSceneName);

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        Animator ani = player.GetComponent<Animator>();
        SpriteRenderer sr = player.GetComponent<SpriteRenderer>();

        // Freeze player physics state
        if (rb != null) {
            rb.velocity = Vector2.zero; 
            rb.bodyType = RigidbodyType2D.Kinematic; 
        }

        // Play idle animation
        if (ani != null) {
            ani.SetBool("isRun", false); 
            ani.Play("Idle"); 
        }

        // Player transparency fade
        float timer = 0f;
        while (timer < waitTime) {
            timer += Time.deltaTime;
            if (sr != null) {
                float alpha = Mathf.Lerp(1f, 0f, timer / waitTime);
                sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, alpha);
            } 
            yield return null; 
        }

        // Load scene
        if (Application.CanStreamedLevelBeLoaded(nextSceneName)) {
            SceneManager.LoadScene(nextSceneName);
        } else {
            Debug.LogError("Scene not found: " + nextSceneName + " - Please check Build Settings");
            isTriggered = false;
            
            // Restore player state
            if (rb != null) rb.bodyType = RigidbodyType2D.Dynamic;
            if (sr != null) sr.color = Color.white; 
        }
    }

    #endregion
}
