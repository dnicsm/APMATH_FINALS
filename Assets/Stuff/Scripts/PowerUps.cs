using System;
using System.Collections.Generic;
using UnityEngine;

public class PowerUps : MonoBehaviour
{
    public Material mat;
    
    [Header("Sphere Mesh Geometry")]
    public float radius = 0.2f;
    public int latitudes = 16;
    public int longitudes = 24;
    private Mesh sphereMesh;

    public enum PowerUpType
    {
        FireBall,
        ExtraLife,
        Invincibility
    }

    [Header("Instancing Setup")]
    // Converted to Lists so items can be destroyed at runtime!
    public List<Vector3> spawnPositions = new List<Vector3>()
    {
        new Vector3(-3, 2, 0),
        new Vector3(0, 2, 0),
        new Vector3(3, 2, 0)
    };

    [Tooltip("Ensure this list has the exact same size as Spawn Positions!")]
    public List<PowerUpType> powerUpTypes = new List<PowerUpType>()
    {
        PowerUpType.FireBall,
        PowerUpType.ExtraLife,
        PowerUpType.Invincibility
    };

    [Header("Custom Color Configuration")]
    public Color fireBallColor = Color.red;
    public Color extraLifeColor = Color.green;
    public Color invincibilityColor = Color.yellow;

    // Operational tracking values
    private List<float> yAngles = new();
    private List<float> xAngles = new();
    private List<int> colliderIDs = new(); // Dynamic reference linkage maps
    
    private MaterialPropertyBlock propertyBlock;
    private int colorShaderPropertyID;

    private void Start()
{
    sphereMesh = CreateSphereMesh();
    
    propertyBlock = new MaterialPropertyBlock();
    colorShaderPropertyID = Shader.PropertyToID("_Color");

    while (powerUpTypes.Count < spawnPositions.Count) powerUpTypes.Add(PowerUpType.FireBall);
    while (powerUpTypes.Count > spawnPositions.Count) powerUpTypes.RemoveAt(powerUpTypes.Count - 1);

    Vector3 colliderSize = Vector3.one * (radius * 2f);
    for (int i = 0; i < spawnPositions.Count; i++)
    {
        yAngles.Add(UnityEngine.Random.Range(0f, 360f));
        xAngles.Add(UnityEngine.Random.Range(0f, 360f));
        
        int registeredID = CollisionManager.Instance.RegisterCollider(spawnPositions[i], colliderSize, isPlayer: false);
        colliderIDs.Add(registeredID);

        // --- ADD THIS LINE SO THE COLLISION MANAGER KNOWS ITS MATRIX/POSITION ---
        Matrix4x4 initialMatrix = Matrix4x4.TRS(spawnPositions[i], Quaternion.identity, Vector3.one);
        CollisionManager.Instance.UpdateMatrix(registeredID, initialMatrix);
    }
}

    void Update()
    {
        if (sphereMesh == null || spawnPositions.Count == 0) return;

        int count = spawnPositions.Count;
        Matrix4x4[] matrices = new Matrix4x4[count];
        Vector4[] instancedColors = new Vector4[count];

        for (int i = 0; i < count; i++)
        {
            // Spin processing loop
            yAngles[i] += 90f * Time.deltaTime;
            xAngles[i] -= 45f * Time.deltaTime;

            matrices[i] = Matrix4x4.TRS(
                spawnPositions[i],
                Quaternion.Euler(xAngles[i], yAngles[i], 0),
                Vector3.one 
            );

            switch (powerUpTypes[i])
            {
                case PowerUpType.FireBall:
                    instancedColors[i] = fireBallColor;
                    break;
                case PowerUpType.ExtraLife:
                    instancedColors[i] = extraLifeColor;
                    break;
                case PowerUpType.Invincibility:
                    instancedColors[i] = invincibilityColor;
                    break;
                default:
                    instancedColors[i] = Color.white;
                    break;
            }
        }

        propertyBlock.SetVectorArray(colorShaderPropertyID, instancedColors);
        Graphics.DrawMeshInstanced(sphereMesh, 0, mat, matrices, count, propertyBlock);
    }

    // Call this custom execution helper function from Player when a collision is tracked!
    public void CollectPowerUp(int collisionID)
    {
        int index = colliderIDs.IndexOf(collisionID);
        if (index != -1)
        {
            Debug.Log($"Collected: {powerUpTypes[index]}!");

            // 1. Clear tracker off Collision System
            CollisionManager.Instance.RemoveCollider(collisionID);

            // 2. Clear out reference nodes instantly to drop it from rendering completely
            spawnPositions.RemoveAt(index);
            powerUpTypes.RemoveAt(index);
            yAngles.RemoveAt(index);
            xAngles.RemoveAt(index);
            colliderIDs.RemoveAt(index);
        }
    }
    
    Mesh CreateSphereMesh()
    {
        var mesh = new Mesh();
        mesh.name = "GeneratedSphere";

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

    public bool TryGetPowerUpIndex(int colliderID, out int index)
{
    index = colliderIDs.IndexOf(colliderID);
    return index != -1;
}
}