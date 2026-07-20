using System.Collections.Generic;
using UnityEngine;

public class EnemyAndLevelManager : MonoBehaviour
{
    public enum PlatformType
    {
        Normal,
        Spikes,
        Lava,
        MovingPlatform,
        Dripstone,
        Void,
        Goal
    }

    [System.Serializable]
    public struct LevelDesignPreset
    {
        public Vector3 localPosition;
        public Vector3 scale;
        public PlatformType type;

        [Header("Moving Platform Settings")]
        public Vector3 travelOffset;
        public float travelSpeed;

        [Header("Dripstone Settings")]
        public float dropDelay;
        public float gravityScale;
        public float maxFallDistance;
    }

    [System.Serializable]
    public struct PlatformData
    {
        public Vector3 startPosition;
        public Vector3 currentPosition;
        public Vector3 scale;
        public Color color;
        public int colliderId;
        public Matrix4x4 matrix;
        public PlatformType type;

        public Vector3 travelOffset;
        public float travelSpeed;

        public float dropDelay;
        public float gravityScale;
        public float maxFallDistance;
        public float timer;
        public bool isFalling;
        public bool isRespawning;
        public float currentFallSpeed;
    }

    [System.Serializable]
    public struct EnemyData
    {
        public Vector3 position;
        public Vector3 scale;
        public float speed;
        public float direction;
        public Color color;
        public int colliderId;
        public Matrix4x4 matrix;
        public float minPatrolX;
        public float maxPatrolX;
    }

    [Header("Rendering Setup")]
    public Material instancedMaterial;
    public float constantZPosition = 1f;

    [Header("Void & Fall Settings")]
    public bool autoGenerateVoidFloor = true;
    public float voidYOffset = 4f;
    public float voidExtraWidth = 30f;
    public float killYThreshold = -15f;

    [Header("Manual Level Design")]
    public List<LevelDesignPreset> customLevelDesign = new List<LevelDesignPreset>();

    [Header("Visual Colors for Types")]
    public Color gradientStartColor = Color.blue;
    public Color gradientEndColor = Color.cyan;
    public Color spikeColor = new Color(0.4f, 0.4f, 0.45f);
    public Color lavaColor = new Color(1f, 0.25f, 0f);
    public Color movingPlatformColor = Color.green;
    public Color dripstoneColor = new Color(0.5f, 0.35f, 0.25f);
    public Color voidColor = new Color(0.1f, 0.05f, 0.15f, 0.5f);
    public Color goalColor = Color.yellow;

    [Header("Enemy Configuration")]
    public float enemySpeed = 3f;
    public Vector3 enemyScale = new Vector3(0.5f, 0.5f, 0.5f);
    public Color[] enemyColors = new Color[] { Color.red, Color.magenta, new Color(1f, 0.5f, 0f) };

    [Header("Enemy Spawning Rules")]
    public float minPlatformWidthForEnemies = 8.0f;
    public int maxEnemiesPerPlatform = 2;

    private readonly List<PlatformData> platforms = new List<PlatformData>();
    private readonly List<EnemyData> enemies = new List<EnemyData>();
    private readonly Dictionary<int, PlatformType> platformColliderMap = new Dictionary<int, PlatformType>();
    private readonly Dictionary<int, int> platformColliderToIndex = new Dictionary<int, int>();
    private readonly HashSet<int> enemyColliderIds = new HashSet<int>();

    private Mesh cubeMesh;
    private MaterialPropertyBlock platformPropertyBlock;
    private MaterialPropertyBlock enemyPropertyBlock;

    private Matrix4x4[] platformMatrices;
    private Vector4[] platformTopColors;
    private Vector4[] platformBottomColors;

    private Matrix4x4[] enemyMatrices;
    private Vector4[] enemyColorsBuffer;

    private int cachedPlayerId = -1;
    private Player playerInstance;

    private void Start()
    {
        if (instancedMaterial != null && !instancedMaterial.enableInstancing)
        {
            instancedMaterial.enableInstancing = true;
        }

        CreateCubeMesh();
        platformPropertyBlock = new MaterialPropertyBlock();
        enemyPropertyBlock = new MaterialPropertyBlock();

        GenerateGradientPlatforms();
        GenerateEnemies();

        playerInstance = Object.FindAnyObjectByType<Player>();
    }

    private void Update()
    {
        UpdatePlatforms();
        UpdateEnemies();
        CheckPlayerVoidFall();
        RenderAll();
    }

    #region Platform Logic
    public Vector3 GetPlatformDelta(int platformColliderId)
    {
        if (platformColliderToIndex.TryGetValue(platformColliderId, out int index))
        {
            PlatformData plat = platforms[index];
            if (plat.type == PlatformType.MovingPlatform)
            {
                Vector3 prevPos = plat.currentPosition;
                float pingPong = Mathf.PingPong(Time.time * plat.travelSpeed, 1f);
                Vector3 nextPos = Vector3.Lerp(plat.startPosition, plat.startPosition + plat.travelOffset, pingPong);
                return nextPos - prevPos;
            }
        }
        return Vector3.zero;
    }

    private void UpdatePlatforms()
    {
        float deltaTime = Time.deltaTime;
        float currentTime = Time.time;

        for (int i = 0; i < platforms.Count; i++)
        {
            PlatformData plat = platforms[i];

            if (i < customLevelDesign.Count && plat.type != PlatformType.MovingPlatform && plat.type != PlatformType.Dripstone)
            {
                SyncInspectorPlatform(ref plat, customLevelDesign[i]);
            }
            else if (plat.type == PlatformType.MovingPlatform)
            {
                UpdateMovingPlatform(ref plat, currentTime);
            }
            else if (plat.type == PlatformType.Dripstone)
            {
                UpdateDripstonePlatform(ref plat, deltaTime);
            }

            CollisionManager.Instance.UpdateMatrix(plat.colliderId, plat.matrix);
            platforms[i] = plat;
        }
    }

    private void SyncInspectorPlatform(ref PlatformData plat, LevelDesignPreset preset)
    {
        Vector3 desiredPos = preset.localPosition;
        desiredPos.z = constantZPosition;
        Vector3 desiredScale = preset.scale;

        if (plat.currentPosition != desiredPos || plat.scale != desiredScale)
        {
            plat.startPosition = desiredPos;
            plat.currentPosition = desiredPos;
            plat.scale = desiredScale;
            plat.matrix = Matrix4x4.TRS(desiredPos, Quaternion.identity, plat.scale);

            CollisionManager.Instance.UpdateCollider(plat.colliderId, desiredPos, plat.scale);
        }
    }

    private void UpdateMovingPlatform(ref PlatformData plat, float currentTime)
    {
        float pingPong = Mathf.PingPong(currentTime * plat.travelSpeed, 1f);
        plat.currentPosition = Vector3.Lerp(plat.startPosition, plat.startPosition + plat.travelOffset, pingPong);
        plat.matrix = Matrix4x4.TRS(plat.currentPosition, Quaternion.identity, plat.scale);

        CollisionManager.Instance.UpdateCollider(plat.colliderId, plat.currentPosition, plat.scale);
    }

    private void UpdateDripstonePlatform(ref PlatformData plat, float deltaTime)
    {
        if (plat.isRespawning)
        {
            plat.timer += deltaTime;
            plat.matrix = Matrix4x4.TRS(plat.startPosition, Quaternion.identity, Vector3.zero);
            CollisionManager.Instance.UpdateCollider(plat.colliderId, plat.startPosition, Vector3.zero);

            if (plat.timer >= plat.dropDelay)
            {
                plat.isRespawning = false;
                plat.isFalling = false;
                plat.timer = 0f;
                plat.currentPosition = plat.startPosition;
            }
            return;
        }

        if (!plat.isFalling)
        {
            plat.timer += deltaTime;
            if (plat.timer >= plat.dropDelay)
            {
                plat.isFalling = true;
                plat.currentFallSpeed = 0f;
            }
            plat.currentPosition = plat.startPosition;
            plat.matrix = Matrix4x4.TRS(plat.currentPosition, Quaternion.identity, plat.scale);
            CollisionManager.Instance.UpdateCollider(plat.colliderId, plat.currentPosition, plat.scale);
            return;
        }

        plat.currentFallSpeed += plat.gravityScale * deltaTime;
        Vector3 proposedPos = plat.currentPosition - new Vector3(0f, plat.currentFallSpeed * deltaTime, 0f);

        bool collided = false;

        if (CollisionManager.Instance.CheckCollision(plat.colliderId, proposedPos, out List<int> collidingIds))
        {
            foreach (int hitId in collidingIds)
            {
                if (hitId == cachedPlayerId)
                {
                    playerInstance?.TakeDamage(playerInstance.damage, "Dripstone");
                }
            }
            collided = true;
        }
        else if (Vector3.Distance(plat.startPosition, proposedPos) >= plat.maxFallDistance)
        {
            collided = true;
        }

        if (collided)
        {
            plat.isRespawning = true;
            plat.timer = 0f;
            plat.matrix = Matrix4x4.TRS(plat.startPosition, Quaternion.identity, Vector3.zero);
            CollisionManager.Instance.UpdateCollider(plat.colliderId, plat.startPosition, Vector3.zero);
        }
        else
        {
            plat.currentPosition = proposedPos;
            plat.matrix = Matrix4x4.TRS(plat.currentPosition, Quaternion.identity, plat.scale);
            CollisionManager.Instance.UpdateCollider(plat.colliderId, plat.currentPosition, plat.scale);
        }
    }
    #endregion

    #region Generation
    private void GenerateGradientPlatforms()
    {
        if (customLevelDesign == null || customLevelDesign.Count == 0) return;

        CalculateLevelBounds(out float minX, out float maxX, out float minY);
        float rangeX = Mathf.Max(0.1f, maxX - minX);

        for (int i = 0; i < customLevelDesign.Count; i++)
        {
            LevelDesignPreset preset = customLevelDesign[i];
            Vector3 pos = preset.localPosition;
            pos.z = constantZPosition;

            Color platformColor = GetPlatformColor(preset.type, pos.x, minX, rangeX);
            int id = CollisionManager.Instance.RegisterCollider(pos, preset.scale, isPlayer: false);

            PlatformData newPlatform = new PlatformData
            {
                startPosition = pos,
                currentPosition = pos,
                scale = preset.scale,
                color = platformColor,
                colliderId = id,
                matrix = Matrix4x4.TRS(pos, Quaternion.identity, preset.scale),
                type = preset.type,
                travelOffset = preset.travelOffset,
                travelSpeed = preset.travelSpeed,
                dropDelay = preset.dropDelay,
                gravityScale = preset.gravityScale,
                maxFallDistance = preset.maxFallDistance,
                timer = 0f,
                isFalling = false
            };

            CollisionManager.Instance.UpdateMatrix(id, newPlatform.matrix);
            platforms.Add(newPlatform);
            
            int index = platforms.Count - 1;
            platformColliderMap[id] = preset.type;
            platformColliderToIndex[id] = index;
        }

        if (autoGenerateVoidFloor)
        {
            GenerateVoidFloor(minX, maxX, minY);
        }
    }

    private void GenerateVoidFloor(float minX, float maxX, float minY)
    {
        float voidThickness = 50f;
        float voidY = minY - voidYOffset - (voidThickness * 0.5f);
        float totalWidth = (maxX - minX) + voidExtraWidth;

        Vector3 voidPos = new Vector3((minX + maxX) * 0.5f, voidY, constantZPosition);
        Vector3 voidScale = new Vector3(totalWidth, voidThickness, 1f);

        int voidId = CollisionManager.Instance.RegisterCollider(voidPos, voidScale, isPlayer: false);

        PlatformData voidFloor = new PlatformData
        {
            startPosition = voidPos,
            currentPosition = voidPos,
            scale = voidScale,
            color = voidColor,
            colliderId = voidId,
            matrix = Matrix4x4.TRS(voidPos, Quaternion.identity, voidScale),
            type = PlatformType.Void
        };

        CollisionManager.Instance.UpdateMatrix(voidId, voidFloor.matrix);
        platforms.Add(voidFloor);

        int index = platforms.Count - 1;
        platformColliderMap[voidId] = PlatformType.Void;
        platformColliderToIndex[voidId] = index;

        killYThreshold = minY - voidYOffset;
    }

    private void CalculateLevelBounds(out float minX, out float maxX, out float minY)
    {
        minX = float.MaxValue;
        maxX = float.MinValue;
        minY = float.MaxValue;

        foreach (var preset in customLevelDesign)
        {
            if (preset.localPosition.x < minX) minX = preset.localPosition.x;
            if (preset.localPosition.x > maxX) maxX = preset.localPosition.x;
            if (preset.localPosition.y < minY) minY = preset.localPosition.y;
        }
    }

    private Color GetPlatformColor(PlatformType type, float xPos, float minX, float rangeX)
    {
        return type switch
        {
            PlatformType.Spikes => spikeColor,
            PlatformType.Lava => lavaColor,
            PlatformType.MovingPlatform => movingPlatformColor,
            PlatformType.Dripstone => dripstoneColor,
            PlatformType.Void => voidColor,
            PlatformType.Goal => goalColor,
            _ => Color.Lerp(gradientStartColor, gradientEndColor, (xPos - minX) / rangeX)
        };
    }
    #endregion

    #region Enemy Logic
    private void GenerateEnemies()
    {
        enemies.Clear();
        enemyColliderIds.Clear();

        foreach (PlatformData plat in platforms)
        {
            if (plat.type != PlatformType.Normal || plat.scale.x < minPlatformWidthForEnemies) 
                continue;

            float platformHalfWidth = plat.scale.x * 0.5f;
            float platformTopY = plat.currentPosition.y + (plat.scale.y * 0.5f) + (enemyScale.y * 0.5f) + 0.05f;
            float usableHalfWidth = Mathf.Max(0.5f, platformHalfWidth - 0.8f);

            for (int i = 0; i < maxEnemiesPerPlatform; i++)
            {
                float preferredX = (i == 0) ? -usableHalfWidth * 0.5f : usableHalfWidth * 0.5f;
                float[] xOffsetsToTry = { preferredX, preferredX - 0.5f, preferredX + 0.5f, preferredX - 1.0f, preferredX + 1.0f, 0f };

                foreach (float xOffset in xOffsetsToTry)
                {
                    if (Mathf.Abs(xOffset) > usableHalfWidth) continue;

                    Vector3 candidatePos = new Vector3(plat.currentPosition.x + xOffset, platformTopY, constantZPosition);

                    if (IsSpawnPositionSafe(candidatePos, plat.colliderId))
                    {
                        CreateEnemy(candidatePos, plat);
                        break;
                    }
                }
            }
        }
    }

    private bool IsSpawnPositionSafe(Vector3 candidatePos, int parentPlatformId)
    {
        int tempId = CollisionManager.Instance.RegisterCollider(candidatePos, enemyScale, isPlayer: false);
        bool safe = true;

        if (CollisionManager.Instance.CheckCollision(tempId, candidatePos, out List<int> hitIds))
        {
            foreach (int hitId in hitIds)
            {
                if (hitId == parentPlatformId) continue;

                if (platformColliderMap.TryGetValue(hitId, out PlatformType hitType))
                {
                    if (hitType == PlatformType.Lava || hitType == PlatformType.Spikes ||
                        hitType == PlatformType.Dripstone || hitType == PlatformType.Void)
                    {
                        safe = false;
                        break;
                    }
                }
            }
        }

        CollisionManager.Instance.RemoveCollider(tempId);
        return safe;
    }

    private void CreateEnemy(Vector3 spawnPos, PlatformData plat)
    {
        int id = CollisionManager.Instance.RegisterCollider(spawnPos, enemyScale, isPlayer: false);
        float halfPlatWidth = plat.scale.x * 0.5f;
        float halfEnemyWidth = enemyScale.x * 0.5f;

        EnemyData newEnemy = new EnemyData
        {
            position = spawnPos,
            scale = enemyScale,
            speed = enemySpeed * Random.Range(0.8f, 1.2f),
            direction = Random.value > 0.5f ? 1f : -1f,
            color = enemyColors[Random.Range(0, enemyColors.Length)],
            colliderId = id,
            matrix = Matrix4x4.TRS(spawnPos, Quaternion.identity, enemyScale),
            minPatrolX = plat.currentPosition.x - halfPlatWidth + halfEnemyWidth,
            maxPatrolX = plat.currentPosition.x + halfPlatWidth - halfEnemyWidth
        };

        CollisionManager.Instance.UpdateMatrix(id, newEnemy.matrix);
        enemies.Add(newEnemy);
        enemyColliderIds.Add(id);
    }

    private void UpdateEnemies()
    {
        float deltaTime = Time.deltaTime;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyData enemy = enemies[i];

            float step = enemy.speed * enemy.direction * deltaTime;
            Vector3 targetPosition = enemy.position + new Vector3(step, 0, 0);
            bool needsToTurnAround = false;

            if (targetPosition.x <= enemy.minPatrolX)
            {
                targetPosition.x = enemy.minPatrolX;
                needsToTurnAround = true;
            }
            else if (targetPosition.x >= enemy.maxPatrolX)
            {
                targetPosition.x = enemy.maxPatrolX;
                needsToTurnAround = true;
            }

            if (!needsToTurnAround && CollisionManager.Instance.CheckCollision(enemy.colliderId, targetPosition, out List<int> collidingIds))
            {
                foreach (int hitId in collidingIds)
                {
                    if (hitId == cachedPlayerId)
                    {
                        playerInstance?.TakeDamage(playerInstance.damage, "Enemy");
                    }
                    else if (!enemyColliderIds.Contains(hitId))
                    {

                        if (platformColliderToIndex.TryGetValue(hitId, out int platIndex))
                        {
                            PlatformData plat = platforms[platIndex];
                            if (Mathf.Abs(plat.currentPosition.y - enemy.position.y) < (plat.scale.y * 0.5f + enemy.scale.y * 0.5f - 0.05f))
                            {
                                needsToTurnAround = true;
                            }
                        }
                    }
                }
            }

            if (needsToTurnAround)
            {
                enemy.direction *= -1f;
                targetPosition = enemy.position + new Vector3(enemy.speed * enemy.direction * deltaTime, 0, 0);
            }

            enemy.position = targetPosition;
            enemy.matrix = Matrix4x4.TRS(enemy.position, Quaternion.identity, enemy.scale);

            CollisionManager.Instance.UpdateCollider(enemy.colliderId, enemy.position, enemy.scale);
            CollisionManager.Instance.UpdateMatrix(enemy.colliderId, enemy.matrix);

            enemies[i] = enemy;
        }
    }

    public void DestroyEnemy(int enemyColliderId)
    {
        int index = enemies.FindIndex(e => e.colliderId == enemyColliderId);
        if (index != -1)
        {
            CollisionManager.Instance.RemoveCollider(enemyColliderId);
            enemies.RemoveAt(index);
            enemyColliderIds.Remove(enemyColliderId);
        }
    }

    #endregion

    #region Rendering & Bounds
    private void RenderAll()
    {
        if (cubeMesh == null || instancedMaterial == null) return;

        RenderPlatforms();
        RenderEnemies();
    }

    private void RenderPlatforms()
    {
        int count = platforms.Count;
        if (count == 0) return;

        EnsurePlatformBuffers(count);

        for (int i = 0; i < count; i++)
        {
            platformMatrices[i] = platforms[i].matrix;

            if (platforms[i].type == PlatformType.Normal)
            {
                platformTopColors[i] = gradientStartColor;
                platformBottomColors[i] = gradientEndColor;
            }
            else
            {
                platformTopColors[i] = platforms[i].color;
                platformBottomColors[i] = platforms[i].color;
            }
        }

        platformPropertyBlock.SetVectorArray("_Color", platformTopColors);
        platformPropertyBlock.SetVectorArray("_ColorBottom", platformBottomColors);
        Graphics.DrawMeshInstanced(cubeMesh, 0, instancedMaterial, platformMatrices, count, platformPropertyBlock);
    }

    private void RenderEnemies()
    {
        int count = enemies.Count;
        if (count == 0) return;

        EnsureEnemyBuffers(count);

        for (int i = 0; i < count; i++)
        {
            enemyMatrices[i] = enemies[i].matrix;
            enemyColorsBuffer[i] = enemies[i].color;
        }

        enemyPropertyBlock.SetVectorArray("_Color", enemyColorsBuffer);
        enemyPropertyBlock.SetVectorArray("_ColorBottom", enemyColorsBuffer);
        Graphics.DrawMeshInstanced(cubeMesh, 0, instancedMaterial, enemyMatrices, count, enemyPropertyBlock);
    }

    private void EnsurePlatformBuffers(int capacity)
    {
        if (platformMatrices == null || platformMatrices.Length < capacity)
        {
            platformMatrices = new Matrix4x4[capacity];
            platformTopColors = new Vector4[capacity];
            platformBottomColors = new Vector4[capacity];
        }
    }

    private void EnsureEnemyBuffers(int capacity)
    {
        if (enemyMatrices == null || enemyMatrices.Length < capacity)
        {
            enemyMatrices = new Matrix4x4[capacity];
            enemyColorsBuffer = new Vector4[capacity];
        }
    }

    private void CheckPlayerVoidFall()
    {
        if (playerInstance != null && playerInstance.transform.position.y <= killYThreshold)
        {
            playerInstance.TakeDamage(9999f, "Void");
        }
    }

    private void CreateCubeMesh()
    {
        cubeMesh = new Mesh { name = "ManagerCube" };

        cubeMesh.vertices = new Vector3[8]
        {
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f)
        };

        cubeMesh.triangles = new int[36]
        {
            0, 4, 1, 1, 4, 5,
            2, 6, 3, 3, 6, 7,
            0, 3, 4, 4, 3, 7,
            1, 5, 2, 2, 5, 6,
            0, 1, 3, 3, 1, 2,
            4, 7, 5, 5, 7, 6
        };

        cubeMesh.RecalculateNormals();
        cubeMesh.RecalculateBounds();
    }
    #endregion

    #region Colliders
    public PlatformType GetPlatformType(int colliderId)
    {
        return platformColliderMap.TryGetValue(colliderId, out PlatformType type) ? type : PlatformType.Normal;
    }

    public void SetPlayerID(int playerID) => cachedPlayerId = playerID;

    public bool IsEnemyCollider(int colliderId) => enemyColliderIds.Contains(colliderId);
    #endregion
}