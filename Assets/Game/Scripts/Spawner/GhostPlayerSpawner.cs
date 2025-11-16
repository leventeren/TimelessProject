using Game.Player.Recorder.Scripts;
using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Player.Scripts
{
    public class GhostPlayerSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Required] private MoveRecorder recorder;
        [SerializeField, Required] private GameObject ghostPlayerPrefab;
        [SerializeField] private Transform spawnParent;
        
        [Header("Spawn Settings")]
        [SerializeField, Min(1)] private int numberOfGhosts = 5;
        [SerializeField, Min(0f)] private float delayBetweenGhosts = 0.1f;
        [SerializeField] private bool autoSpawn = true;
        [SerializeField] private float autoSpawnDelay = 1f;
        
        [Header("Timing Settings")]
        [Tooltip("Her ghost'un başlangıç gecikmesi (delay between ghosts ile aynı olmalı genelde)")]
        [SerializeField] private float playbackDelay = 0.1f;
        [SerializeField, Range(0f, 1f)] private float driftCorrectionFactor = 0.1f;
        
        [Header("Visual Settings")]
        [SerializeField] private bool randomizeColors = true;
        [SerializeField] private Color[] ghostColors = new Color[] 
        { 
            new Color(1f, 0.5f, 0.5f, 0.7f),  // Red
            new Color(0.5f, 1f, 0.5f, 0.7f),  // Green
            new Color(0.5f, 0.5f, 1f, 0.7f),  // Blue
            new Color(1f, 1f, 0.5f, 0.7f),    // Yellow
            new Color(1f, 0.5f, 1f, 0.7f)     // Magenta
        };
        
        [Header("Position Settings")]
        [SerializeField] private bool useCustomSpawnPositions = false;
        [SerializeField, ShowIf("useCustomSpawnPositions")] 
        private Vector3[] spawnPositions = new Vector3[0];
        
        [Header("Mirror Settings")]
        [SerializeField] private bool enableMirrorMode = false;
        [SerializeField, ShowIf("enableMirrorMode")]
        private bool[] mirrorPattern = new bool[0];
        
        [Header("Performance")]
        [SerializeField] private bool useJobsSystem = false;
        
        [Header("Debug")]
        [SerializeField, ReadOnly] private int spawnedCount = 0;
        [SerializeField, ReadOnly] private bool isSpawning = false;
        
        private List<GhostPlayer> spawnedGhosts = new List<GhostPlayer>();
        private Coroutine spawnCoroutine;

        private void Awake()
        {
            ValidateReferences();
            
            if (spawnParent == null)
            {
                var parentObj = new GameObject("GhostPlayers");
                spawnParent = parentObj.transform;
                spawnParent.SetParent(transform);
            }
        }

        private void Start()
        {
            if (autoSpawn)
            {
                StartCoroutine(AutoSpawnRoutine());
            }
        }

        private void ValidateReferences()
        {
            if (recorder == null)
            {
                Debug.LogError($"[GhostPlayerSpawner] MoveRecorder is not assigned on {gameObject.name}!");
            }
            
            if (ghostPlayerPrefab == null)
            {
                Debug.LogError($"[GhostPlayerSpawner] Ghost Player Prefab is not assigned on {gameObject.name}!");
            }
            
            if (ghostPlayerPrefab != null && ghostPlayerPrefab.GetComponent<GhostPlayer>() == null)
            {
                Debug.LogError($"[GhostPlayerSpawner] Ghost Player Prefab doesn't have GhostPlayer component!");
            }
        }

        private IEnumerator AutoSpawnRoutine()
        {
            yield return new WaitForSeconds(autoSpawnDelay);
            
            while (recorder == null || recorder.FrameCount < 2)
            {
                yield return null;
            }
            
            SpawnAllGhosts();
        }

        [Button("Spawn All Ghosts"), PropertySpace(10), DisableIf("isSpawning")]
        public void SpawnAllGhosts()
        {
            if (isSpawning)
            {
                Debug.LogWarning("[GhostPlayerSpawner] Already spawning!");
                return;
            }
            
            if (recorder == null || recorder.FrameCount == 0)
            {
                Debug.LogWarning("[GhostPlayerSpawner] Cannot spawn: no frames recorded");
                return;
            }
            
            if (spawnCoroutine != null)
            {
                StopCoroutine(spawnCoroutine);
            }
            
            spawnCoroutine = StartCoroutine(SpawnGhostsRoutine());
        }

        private IEnumerator SpawnGhostsRoutine()
        {
            isSpawning = true;
            spawnedCount = 0;
            
            for (var i = 0; i < numberOfGhosts; i++)
            {
                SpawnSingleGhost(i);
                spawnedCount++;
                
                if (i < numberOfGhosts - 1 && delayBetweenGhosts > 0f)
                {
                    yield return new WaitForSeconds(delayBetweenGhosts);
                }
            }
            
            isSpawning = false;
            Debug.Log($"[GhostPlayerSpawner] Spawned {spawnedCount} ghosts");
        }

        private void SpawnSingleGhost(int index)
        {
            var ghostObj = Instantiate(ghostPlayerPrefab, spawnParent);
            var ghost = ghostObj.GetComponent<GhostPlayer>();
            
            if (ghost == null)
            {
                Debug.LogError($"[GhostPlayerSpawner] Spawned object doesn't have GhostPlayer component!");
                Destroy(ghostObj);
                return;
            }
            
            ghostObj.name = $"Ghost_{index:D2}";
            
            if (useCustomSpawnPositions && index < spawnPositions.Length)
            {
                ghostObj.transform.position = spawnPositions[index];
            }
            
            var ghostRecorderField = ghost.GetType().GetField("recorder", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (ghostRecorderField != null)
            {
                ghostRecorderField.SetValue(ghost, recorder);
            }
            
            var delay = playbackDelay * (index + 1);
            ghost.SetPlaybackDelay(delay);
            ghost.SetDriftCorrection(driftCorrectionFactor);

            if (enableMirrorMode && mirrorPattern.Length > 0)
            {
                var shouldMirror = mirrorPattern[index % mirrorPattern.Length];
                var mirrorField = ghost.GetType().GetField("mirrorMode", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (mirrorField != null)
                {
                    mirrorField.SetValue(ghost, shouldMirror);
                }
            }
            
            if (randomizeColors && ghostColors.Length > 0)
            {
                var colorComponent = ghost.ColorComponent;
                if (colorComponent != null)
                {
                    var color = ghostColors[index % ghostColors.Length];
                    colorComponent.SetColor(color);
                }
            }
            
            if (useJobsSystem)
            {
                ghost.SetUseJobsSystem(true);
            }
            
            spawnedGhosts.Add(ghost);
        }

        [Button("Spawn Single Ghost"), PropertySpace(5)]
        public void SpawnSingleGhostManual()
        {
            if (recorder == null || recorder.FrameCount == 0)
            {
                Debug.LogWarning("[GhostPlayerSpawner] Cannot spawn: no frames recorded");
                return;
            }
            
            SpawnSingleGhost(spawnedCount);
            spawnedCount++;
        }

        [Button("Clear All Ghosts"), PropertySpace(10)]
        public void ClearAllGhosts()
        {
            if (spawnCoroutine != null)
            {
                StopCoroutine(spawnCoroutine);
                isSpawning = false;
            }
            
            foreach (var ghost in spawnedGhosts)
            {
                if (ghost != null)
                {
                    Destroy(ghost.gameObject);
                }
            }
            
            spawnedGhosts.Clear();
            spawnedCount = 0;
            
            Debug.Log("[GhostPlayerSpawner] Cleared all ghosts");
        }

        [Button("Play All Ghosts"), PropertySpace(10)]
        public void PlayAllGhosts()
        {
            foreach (var ghost in spawnedGhosts)
            {
                if (ghost != null)
                {
                    ghost.Play();
                }
            }
            
            Debug.Log($"[GhostPlayerSpawner] Playing {spawnedGhosts.Count} ghosts");
        }

        [Button("Pause All Ghosts")]
        public void PauseAllGhosts()
        {
            foreach (var ghost in spawnedGhosts)
            {
                if (ghost != null)
                {
                    ghost.Pause();
                }
            }
        }

        [Button("Resume All Ghosts")]
        public void ResumeAllGhosts()
        {
            foreach (var ghost in spawnedGhosts)
            {
                if (ghost != null)
                {
                    ghost.Resume();
                }
            }
        }

        [Button("Stop All Ghosts")]
        public void StopAllGhosts()
        {
            foreach (var ghost in spawnedGhosts)
            {
                if (ghost != null)
                {
                    ghost.Stop();
                }
            }
        }

        [Button("Restart All Ghosts")]
        public void RestartAllGhosts()
        {
            foreach (var ghost in spawnedGhosts)
            {
                if (ghost != null)
                {
                    ghost.Restart();
                }
            }
        }

        public void SetGhostTimeScale(float timeScale)
        {
            foreach (var ghost in spawnedGhosts)
            {
                if (ghost != null)
                {
                    ghost.SetTimeScale(timeScale);
                }
            }
        }

        public void SetGhostReverse(bool reverse)
        {
            foreach (var ghost in spawnedGhosts)
            {
                if (ghost != null)
                {
                    ghost.SetReverse(reverse);
                }
            }
        }

        #region Properties

        public int SpawnedCount => spawnedCount;
        public bool IsSpawning => isSpawning;
        public List<GhostPlayer> SpawnedGhosts => new List<GhostPlayer>(spawnedGhosts);
        public int ActiveGhostCount => spawnedGhosts.Count;

        #endregion

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!useCustomSpawnPositions || spawnPositions == null)
                return;
            
            Gizmos.color = Color.yellow;
            
            for (int i = 0; i < spawnPositions.Length; i++)
            {
                Vector3 worldPos = transform.TransformPoint(spawnPositions[i]);
                Gizmos.DrawWireSphere(worldPos, 0.5f);
                
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(worldPos + Vector3.up * 0.7f, $"Spawn {i}");
                #endif
            }
        }

        [Button("Auto-fill Spawn Positions"), PropertySpace(10), ShowIf("useCustomSpawnPositions")]
        private void AutoFillSpawnPositions()
        {
            spawnPositions = new Vector3[numberOfGhosts];
            
            for (int i = 0; i < numberOfGhosts; i++)
            {
                spawnPositions[i] = new Vector3(i * 2f, 0f, 0f);
            }
            
            Debug.Log($"[GhostPlayerSpawner] Auto-filled {numberOfGhosts} spawn positions");
        }

        [Button("Setup Mirror Pattern"), ShowIf("enableMirrorMode")]
        private void SetupMirrorPattern()
        {
            mirrorPattern = new bool[numberOfGhosts];
            
            for (int i = 0; i < numberOfGhosts; i++)
            {
                mirrorPattern[i] = i % 2 == 1; // Alternating pattern
            }
            
            Debug.Log($"[GhostPlayerSpawner] Setup alternating mirror pattern");
        }
#endif
    }
}