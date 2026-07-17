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
        Dripstone
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

    [Header("Manual Level Design")]
    public List<LevelDesignPreset> customLevelDesign = new List<LevelDesignPreset>();

    [Header("Visual Colors for Types")]
    public Color gradientStartColor = Color.blue;
    public Color gradientEndColor = Color.cyan;
    public Color spikeColor = new Color(0.4f, 0.4f, 0.45f); 
    public Color lavaColor = new Color(1f, 0.25f, 0f);      
    public Color movingPlatformColor = Color.green;         
    public Color dripstoneColor = new Color(0.5f, 0.35f, 0.25f); 

    [Header("Enemy Configuration")]
    public int enemyCount = 5;
    public float enemySpeed = 3f;
    public Vector3 enemyScale = new Vector3(0.5f, 0.5f, 0.5f);
    public Color[] enemyColors = new Color[] { Color.red, Color.magenta, new Color(1f, 0.5f, 0f) };

    private List<PlatformData> platforms = new List<PlatformData>();
    private List<EnemyData> enemies = new List<EnemyData>();
    private Dictionary<int, PlatformType> platformColliderMap = new Dictionary<int, PlatformType>();
    private HashSet<int> enemyColliderIds = new HashSet<int>();

    private Mesh cubeMesh;
    private MaterialPropertyBlock platformPropertyBlock;
    private MaterialPropertyBlock enemyPropertyBlock;
    private int cachedPlayerId = -1;

    void Start()
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
    }

    void Update()
    {
        UpdatePlatforms(); 
        UpdateEnemies();
        RenderAll();
    }

    void UpdatePlatforms()
    {
        for (int i = 0; i < platforms.Count; i++)
        {
            PlatformData plat = platforms[i];

            if (plat.type == PlatformType.MovingPlatform)
            {
                float pingPong = Mathf.PingPong(Time.time * plat.travelSpeed, 1f);
                plat.currentPosition = Vector3.Lerp(plat.startPosition, plat.startPosition + plat.travelOffset, pingPong);
                plat.matrix = Matrix4x4.TRS(plat.currentPosition, Quaternion.identity, plat.scale);
            }
            else if (plat.type == PlatformType.Dripstone)
            {
                if (plat.isRespawning)
                {
                    plat.timer += Time.deltaTime;

                    plat.matrix = Matrix4x4.TRS(plat.startPosition, Quaternion.identity, Vector3.zero);
                    CollisionManager.Instance.UpdateCollider(plat.colliderId, plat.startPosition, Vector3.zero);

                    if (plat.timer >= plat.dropDelay)
                    {
                        plat.isRespawning = false;
                        plat.isFalling = false;
                        plat.timer = 0f; 
                        plat.currentPosition = plat.startPosition;
                    }
                }
                else if (!plat.isFalling)
                {
                    plat.timer += Time.deltaTime;
                    if (plat.timer >= plat.dropDelay)
                    {
                        plat.isFalling = true;
                        plat.currentFallSpeed = 0f;
                    }
                    plat.currentPosition = plat.startPosition;
                    plat.matrix = Matrix4x4.TRS(plat.currentPosition, Quaternion.identity, plat.scale);
                    CollisionManager.Instance.UpdateCollider(plat.colliderId, plat.currentPosition, plat.scale);
                }
                else
                {
                    plat.currentFallSpeed += plat.gravityScale * Time.deltaTime;
                    Vector3 proposedPos = plat.currentPosition - new Vector3(0f, plat.currentFallSpeed * Time.deltaTime, 0f);

                    bool collided = false;

                    if (CollisionManager.Instance.CheckCollision(plat.colliderId, proposedPos, out List<int> collidingIds))
                    {
                        foreach (int hitId in collidingIds)
                        {
                            if (hitId == cachedPlayerId)
                            {
                                Debug.LogWarning("[CRUSHED] Player hit by Dripstone");
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
            }

            CollisionManager.Instance.UpdateMatrix(plat.colliderId, plat.matrix);
            platforms[i] = plat;
        }
    }

    void GenerateGradientPlatforms()
    {
        if (customLevelDesign == null || customLevelDesign.Count == 0) return;

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        foreach (var preset in customLevelDesign)
        {
            if (preset.localPosition.x < minX) minX = preset.localPosition.x;
            if (preset.localPosition.x > maxX) maxX = preset.localPosition.x;
        }

        float rangeX = Mathf.Max(0.1f, maxX - minX);

        for (int i = 0; i < customLevelDesign.Count; i++)
        {
            Vector3 pos = customLevelDesign[i].localPosition;
            pos.z = constantZPosition; 

            Vector3 scale = customLevelDesign[i].scale;
            PlatformType pType = customLevelDesign[i].type;

            Color platformColor;
            switch (pType)
            {
                case PlatformType.Spikes: platformColor = spikeColor; break;
                case PlatformType.Lava: platformColor = lavaColor; break;
                case PlatformType.MovingPlatform: platformColor = movingPlatformColor; break;
                case PlatformType.Dripstone: platformColor = dripstoneColor; break;
                default:
                    float t = (pos.x - minX) / rangeX;
                    platformColor = Color.Lerp(gradientStartColor, gradientEndColor, t);
                    break;
            }

            int id = CollisionManager.Instance.RegisterCollider(pos, scale, isPlayer: false);

            PlatformData newPlatform = new PlatformData
            {
                startPosition = pos,
                currentPosition = pos,
                scale = scale,
                color = platformColor,
                colliderId = id,
                matrix = Matrix4x4.TRS(pos, Quaternion.identity, scale),
                type = pType,
                travelOffset = customLevelDesign[i].travelOffset,
                travelSpeed = customLevelDesign[i].travelSpeed,
                dropDelay = customLevelDesign[i].dropDelay,
                gravityScale = customLevelDesign[i].gravityScale,
                maxFallDistance = customLevelDesign[i].maxFallDistance,
                timer = 0f,
                isFalling = false
            };

            CollisionManager.Instance.UpdateMatrix(id, newPlatform.matrix);
            platforms.Add(newPlatform);
            platformColliderMap[id] = pType;
        }
    }

    void GenerateEnemies()
    {
        List<PlatformData> normalPlatforms = platforms.FindAll(p => p.type == PlatformType.Normal);

        if (normalPlatforms.Count == 0)
        {
            Debug.LogError("[Enemy Generator] Cannot spawn enemies because there are no Normal platform");
            return;
        }

        List<Vector3> spawnedPositions = new List<Vector3>();

        float minSpawnSeparation = enemyScale.x * 2.5f; 

        for (int i = 0; i < enemyCount; i++)
        {
            Vector3 enemyPos = Vector3.zero;
            PlatformData spawnPlat;
            bool foundValidSpot = false;
            int attempts = 0;

            float halfEnemyWidth = enemyScale.x * 0.5f;
            float minX = 0f;
            float maxX = 0f;

            while (!foundValidSpot && attempts < 15)
            {
                attempts++;
                spawnPlat = normalPlatforms[Random.Range(0, normalPlatforms.Count)];
                float halfPlatWidth = spawnPlat.scale.x * 0.5f;

                minX = spawnPlat.currentPosition.x - halfPlatWidth + halfEnemyWidth;
                maxX = spawnPlat.currentPosition.x + halfPlatWidth - halfEnemyWidth;

                float randomOffsetX = Random.Range(-halfPlatWidth + halfEnemyWidth, halfPlatWidth - halfEnemyWidth);
                enemyPos = spawnPlat.currentPosition + new Vector3(randomOffsetX, (spawnPlat.scale.y * 0.5f) + (enemyScale.y * 0.5f) + 0.05f, 0);
                enemyPos.z = constantZPosition;

                bool tooClose = false;
                foreach (Vector3 existingPos in spawnedPositions)
                {
                    if (Vector3.Distance(enemyPos, existingPos) < minSpawnSeparation)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    foundValidSpot = true;
                }
            }

            int id = CollisionManager.Instance.RegisterCollider(enemyPos, enemyScale, isPlayer: false);

            float randomizedSpeed = enemySpeed * Random.Range(0.8f, 1.2f);
            float dir = Random.value > 0.5f ? 1f : -1f;
            Color enemyColor = enemyColors[Random.Range(0, enemyColors.Length)];

            EnemyData newEnemy = new EnemyData
            {
                position = enemyPos,
                scale = enemyScale,
                speed = randomizedSpeed, 
                direction = dir,
                color = enemyColor,
                colliderId = id,
                matrix = Matrix4x4.TRS(enemyPos, Quaternion.identity, enemyScale),
                minPatrolX = minX,
                maxPatrolX = maxX
            };

            CollisionManager.Instance.UpdateMatrix(id, newEnemy.matrix);
            enemies.Add(newEnemy);
            enemyColliderIds.Add(id);
            spawnedPositions.Add(enemyPos);
        }
    }

    void UpdateEnemies()
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyData enemy = enemies[i];

            float step = enemy.speed * enemy.direction * Time.deltaTime;
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
                        Debug.LogWarning($"[ENEMY] Hit player ID {cachedPlayerId}");
                    }
                    else
                    {
                        EnemyAndLevelManager.PlatformType type;
                        if (!enemyColliderIds.Contains(hitId)) 
                        {
                            if (platformColliderMap.TryGetValue(hitId, out type))
                            {
                                PlatformData plat = platforms.Find(p => p.colliderId == hitId);

                                if (Mathf.Abs(plat.currentPosition.y - enemy.position.y) < (plat.scale.y * 0.5f + enemy.scale.y * 0.5f - 0.05f))
                                {
                                    needsToTurnAround = true;
                                }
                            }
                        }
                    }
                }
            }

            if (needsToTurnAround)
            {
                enemy.direction *= -1f; 
                targetPosition = enemy.position + new Vector3(enemy.speed * enemy.direction * Time.deltaTime, 0, 0);
            }

            enemy.position = targetPosition;
            enemy.matrix = Matrix4x4.TRS(enemy.position, Quaternion.identity, enemy.scale);

            CollisionManager.Instance.UpdateCollider(enemy.colliderId, enemy.position, enemy.scale);
            CollisionManager.Instance.UpdateMatrix(enemy.colliderId, enemy.matrix);

            enemies[i] = enemy;
        }
    }

    void RenderAll()
    {
        if (cubeMesh == null || instancedMaterial == null) return;

        if (platforms.Count > 0)
        {
            int count = platforms.Count;
            Matrix4x4[] batchMatrices = new Matrix4x4[count];
            Vector4[] topColors = new Vector4[count]; 
            Vector4[] bottomColors = new Vector4[count]; 

            for (int i = 0; i < count; i++)
            {
                batchMatrices[i] = platforms[i].matrix;
                
                if (platforms[i].type == PlatformType.Normal)
                {
                    topColors[i] = gradientStartColor;
                    bottomColors[i] = gradientEndColor;
                }
                else 
                {
                    topColors[i] = platforms[i].color;
                    bottomColors[i] = platforms[i].color;
                }
            }

            platformPropertyBlock.SetVectorArray(name: "_Color", topColors); 
            platformPropertyBlock.SetVectorArray("_ColorBottom", bottomColors); 
            Graphics.DrawMeshInstanced(cubeMesh, 0, instancedMaterial, batchMatrices, count, platformPropertyBlock); 
        }

        if (enemies.Count > 0)
        {
            int count = enemies.Count;
            Matrix4x4[] batchMatrices = new Matrix4x4[count];
            Vector4[] enemyCols = new Vector4[count];

            for (int i = 0; i < count; i++)
            {
                batchMatrices[i] = enemies[i].matrix;
                enemyCols[i] = enemies[i].color;
            }

            enemyPropertyBlock.SetVectorArray("_Color", enemyCols);
            enemyPropertyBlock.SetVectorArray("_ColorBottom", enemyCols); 
            Graphics.DrawMeshInstanced(cubeMesh, 0, instancedMaterial, batchMatrices, count, enemyPropertyBlock); 
        }
    }

    void CreateCubeMesh()
    {
        cubeMesh = new Mesh();
        cubeMesh.name = "ManagerCube";

        Vector3[] vertices = new Vector3[8]
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

        int[] triangles = new int[36]
        {
            0, 4, 1, 1, 4, 5,
            2, 6, 3, 3, 6, 7,
            0, 3, 4, 4, 3, 7,
            1, 5, 2, 2, 5, 6,
            0, 1, 3, 3, 1, 2,
            4, 7, 5, 5, 7, 6
        };

        cubeMesh.vertices = vertices;
        cubeMesh.triangles = triangles;
        cubeMesh.RecalculateNormals();
        cubeMesh.RecalculateBounds();
    }

    public PlatformType GetPlatformType(int colliderId)
    {
        if (platformColliderMap.TryGetValue(colliderId, out PlatformType type))
        {
            return type;
        }
        return PlatformType.Normal;
    }

    public void SetPlayerID(int playerID)
    {
        cachedPlayerId = playerID;
    }
}