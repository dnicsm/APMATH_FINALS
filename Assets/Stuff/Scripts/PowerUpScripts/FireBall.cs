using System.Collections.Generic;
using UnityEngine;

public class FireBall : MonoBehaviour
{
    public Material mat;

    [Header("Sphere Properties")]
    public float radius = 0.25f;
    public int latitudes = 12;   // Slightly lower resolution for fast-moving projectiles
    public int longitudes = 16;
    private Mesh sphereMesh;

    [Header("Movement Settings")]
    public float speed = 12f;
    public float lifetime = 3f;
    
    private Vector3 currentPosition;
    private float age = 0f;
    private int fireballColliderID;
    private Vector3 colliderSize;

    // References to other systems
    private EnemyAndLevelManager levelManager;
    private PowerUps powerUpSystem;

    void Start()
    {
        // 1. Generate the standalone procedural sphere mesh geometry
        sphereMesh = CreateSphereMesh();

        // 2. Initialize the fireball at the position of the GameObject this script is attached to
        currentPosition = transform.position;

        // 3. Register the projectile with your custom CollisionManager
        colliderSize = Vector3.one * (radius * 2f);
        fireballColliderID = CollisionManager.Instance.RegisterCollider(currentPosition, colliderSize, isPlayer: false);

        // 4. Cache manager instances
        levelManager = Object.FindFirstObjectByType<EnemyAndLevelManager>();
        powerUpSystem = Object.FindFirstObjectByType<PowerUps>();
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // 1. Check lifetime countdown for automatic deletion
        age += dt;
        if (age >= lifetime)
        {
            DestroyProjectile();
            return;
        }

        // 2. Move continuously in a straight line to the right
        currentPosition += new Vector3(speed, 0f, 0f) * dt;

        // 3. Collision handling: Check if it hits anything along its path
        if (CollisionManager.Instance.CheckCollision(fireballColliderID, currentPosition, out List<int> hitIds))
        {
            bool hitSomethingSolid = false;

            foreach (int id in hitIds)
            {
                // Safety 1: Ignore player collisions (prevent friendly fire)
                Player playerComponent = Object.FindFirstObjectByType<Player>();
                // Assuming your CollisionManager or Player stores the active player ID
                // Alternatively, if you can access playerComponent, you can compare IDs if you expose it,
                // but checking the levelManager's cached player ID or component is safest.
                
                // Safety 2: Pass straight through power-ups without exploding
                if (powerUpSystem != null && powerUpSystem.TryGetPowerUpIndex(id, out _))
                {
                    continue; 
                }

                // Action: Check if the hit ID belongs to an enemy
                if (levelManager != null && levelManager.IsEnemyCollider(id))
                {
                    levelManager.DestroyEnemy(id); // One-shot kill the enemy!
                    hitSomethingSolid = true;
                    break; // Break the loop early since the fireball will explode
                }

                // If it wasn't a power-up or player, it's a solid environmental wall/platform
                hitSomethingSolid = true;
            }

            if (hitSomethingSolid)
            {
                DestroyProjectile();
                return;
            }
        }

        // 4. Synchronize transformation calculations with the central tracking repository
        CollisionManager.Instance.UpdateCollider(fireballColliderID, currentPosition, colliderSize);

        // 5. Build rendering transformation matrix and draw via low-level pipeline
        Matrix4x4 m = Matrix4x4.TRS(currentPosition, Quaternion.identity, Vector3.one);
        CollisionManager.Instance.UpdateMatrix(fireballColliderID, m);

        Graphics.DrawMesh(sphereMesh, m, mat, 0);
    }

    private void DestroyProjectile()
    {
        if (CollisionManager.Instance != null)
        {
            CollisionManager.Instance.RemoveCollider(fireballColliderID);
        }
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (CollisionManager.Instance != null)
        {
            CollisionManager.Instance.RemoveCollider(fireballColliderID);
        }
    }

    Mesh CreateSphereMesh()
    {
        var mesh = new Mesh();
        mesh.name = "FireballSphere";

        int vertexCount = (latitudes + 1) * (longitudes + 1);
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        
        int vIdx = 0;
        for (int lat = 0; lat <= latitudes; lat++)
        {
            float a1 = Mathf.PI * lat / latitudes;
            float sinA1 = Mathf.Sin(a1);
            float cosA1 = Mathf.Cos(a1);

            for (int lon = 0; lon <= longitudes; lon++)
            {
                float a2 = 2f * Mathf.PI * lon / longitudes;
                float sinA2 = Mathf.Sin(a2);
                float cosA2 = Mathf.Cos(a2);

                Vector3 normal = new Vector3(sinA1 * cosA2, cosA1, sinA1 * sinA2);
                vertices[vIdx] = normal * radius;
                uvs[vIdx] = new Vector2((float)lon / longitudes, (float)lat / latitudes);
                vIdx++;
            }
        }

        int nbFaces = longitudes * latitudes;
        int nbTriangles = nbFaces * 2;
        int nbIndexes = nbTriangles * 3;
        int[] triangles = new int[nbIndexes];

        int tIdx = 0;
        for (int lat = 0; lat < latitudes; lat++)
        {
            for (int lon = 0; lon < longitudes; lon++)
            {
                int current = lat * (longitudes + 1) + lon;
                int next = current + 1;
                int bottom = current + (longitudes + 1);
                int bottomNext = bottom + 1;

                triangles[tIdx++] = current;
                triangles[tIdx++] = bottom;
                triangles[tIdx++] = next;

                triangles[tIdx++] = next;
                triangles[tIdx++] = bottom;
                triangles[tIdx++] = bottomNext;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}