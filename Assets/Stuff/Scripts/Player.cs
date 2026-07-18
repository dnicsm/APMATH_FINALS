using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class Player : MonoBehaviour
{
    public Mesh mesh;
    public Material mat;
    public Image HealthBar;
    public Image LoseUI;
    public Image WinUI;

    [Header("Power-Up Durations")]
    public float powerUpDuration = 5f;

    [Header("Fireball Settings")]
    public Material fireballMaterial; 
    private bool hasFireballPowerUp = false;
    private float fireballShootTimer = 0f;
    public float fireballCooldown = 0.4f; 
    private float fireballActiveTimer = 0f;

    [HideInInspector]
    public Vector3 meshPosition = new Vector3(0, 5f, 0);
    private Quaternion meshRotation = Quaternion.identity;

    public float MaxHealth = 5f;
    public float CurrentHealth;

    private float baseSpeed = 5f;
    private float jumpForce = 12f;
    private float upwardGravity = 35f;
    private float downwardGravity = 15f;
    
    private Vector3 velocity = Vector3.zero;
    private bool isGrounded = false;
    
    private Vector3 playerSize = new Vector3(1f, 1.5f, 1f);
    private int playerColliderID;

    public float damage;
    public float iframeDuration = 1.5f;
    private float iframeTimer = 0f;
    private EnemyAndLevelManager levelManager;

    private bool canTakeDamage = true;
    private bool canKillEnemies = false;
    private float invincibilityActiveTimer = 0f;

    void Start()
    {
        damage = 0.20f * MaxHealth;
        CurrentHealth = MaxHealth;
        playerColliderID = CollisionManager.Instance.RegisterCollider(meshPosition, playerSize, isPlayer: true);

        levelManager = Object.FindFirstObjectByType<EnemyAndLevelManager>();
        if (levelManager != null)
        {
            levelManager.SetPlayerID(playerColliderID);
        }
        
        HealthManager();
    }

    void Update()
    {
        if(Input.GetKeyDown(KeyCode.R))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        float dt = Time.deltaTime;

        if (iframeTimer > 0f)
        {
            iframeTimer -= dt;
        }

        if (hasFireballPowerUp)
        {
            fireballActiveTimer -= dt;
            if (fireballActiveTimer <= 0f)
            {
                hasFireballPowerUp = false;
                Debug.Log("[POWERUP] Fireball has expired!");
            }
            else
            {
                fireballShootTimer += dt;
                if (fireballShootTimer >= fireballCooldown)
                {
                    fireballShootTimer = 0f;
                    SpawnFireballProjectile();
                }
            }
        }

        if (!canTakeDamage || canKillEnemies)
        {
            invincibilityActiveTimer -= dt;
            if (invincibilityActiveTimer <= 0f)
            {
                canTakeDamage = true;
                canKillEnemies = false;
                Debug.Log("[POWERUP] Invincibility has expired!");
            }
        }

        float xInput = Input.GetAxis("Horizontal");

        if (isGrounded)
        {
            Vector3 groundCheckPos = meshPosition + new Vector3(0, -0.05f, 0);
            if (!CollisionManager.Instance.CheckCollision(playerColliderID, groundCheckPos, out List<int> groundHits))
            {
                isGrounded = false;
            }
            else
            {
                bool hitSolidSurface = false;
                PowerUps powerUpSystem = Object.FindFirstObjectByType<PowerUps>();
                
                foreach (int id in groundHits)
                {
                    if (powerUpSystem == null || !powerUpSystem.spawnPositions.Contains(CollisionManager.Instance.GetMatrix(id).GetPosition()))
                    {
                        hitSolidSurface = true;
                        break;
                    }
                }

                if (!hitSolidSurface)
                {
                    isGrounded = false; 
                }
            }
        }

        float currentSpeed = isGrounded ? baseSpeed : (baseSpeed * 0.5f);
        velocity.x = xInput * currentSpeed;

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            velocity.y = jumpForce;
            isGrounded = false;
        }

        if (!isGrounded)
        {
            if (velocity.y > 0)
            {
                velocity.y -= upwardGravity * dt;
            }
            else
            {
                velocity.y -= downwardGravity * dt;
            }
        }

        Vector3 displacement = velocity * dt;
        
        Vector3 targetPosX = meshPosition + new Vector3(displacement.x, 0, 0);
        if (CollisionManager.Instance.CheckCollision(playerColliderID, targetPosX, out List<int> hitHorizontalIds))
        {
            ProcessItemPickups(hitHorizontalIds);
            CheckHazardCollisions(hitHorizontalIds);
            
            if (ContainsSolidObstacle(hitHorizontalIds)) 
            {
                velocity.x = 0;
            }
            else
            {
                meshPosition.x = targetPosX.x;
            }
        }
        else
        {
            meshPosition.x = targetPosX.x;
        }

        Vector3 targetPosY = meshPosition + new Vector3(0, displacement.y, 0);
        if (CollisionManager.Instance.CheckCollision(playerColliderID, targetPosY, out List<int> hitVerticalIds))
        {
            ProcessItemPickups(hitVerticalIds);
            CheckHazardCollisions(hitVerticalIds);
            
            if (ContainsSolidObstacle(hitVerticalIds))
            {
                if (velocity.y <= 0)
                {
                    isGrounded = true;
                }
                velocity.y = 0;
            }
            else
            {
                meshPosition.y = targetPosY.y;
            }
        }
        else
        {
            meshPosition.y = targetPosY.y;
        }

        CollisionManager.Instance.UpdateCollider(playerColliderID, meshPosition, playerSize);

        Matrix4x4 m = Matrix4x4.TRS(meshPosition, meshRotation, Vector3.one);
        CollisionManager.Instance.UpdateMatrix(playerColliderID, m);

        Graphics.DrawMesh(mesh, m, mat, 0);
    }

    private void CheckHazardCollisions(List<int> hitIDs)
{
    if (levelManager == null) return;

    foreach (int id in hitIDs)
    {
        if (levelManager.IsEnemyCollider(id))
        {
            if (canKillEnemies)
            {
                levelManager.DestroyEnemy(id);
            }
            else
            {
                TakeDamage(damage, "Enemy");
            }
            continue;
        }

        EnemyAndLevelManager.PlatformType pType = levelManager.GetPlatformType(id);
        
        if (pType == EnemyAndLevelManager.PlatformType.Spikes)
        {
            TakeDamage(damage, "Spikes");
        }
        else if (pType == EnemyAndLevelManager.PlatformType.Lava)
        {
            TakeDamage(MaxHealth, "Lava"); 
        }
        else if (pType == EnemyAndLevelManager.PlatformType.Void)
        {
            TakeDamage(MaxHealth, "Void Pit"); 
        }
        else if (pType == EnemyAndLevelManager.PlatformType.Goal)
        {
            WinUI.gameObject.SetActive(true);
            Debug.LogWarning("!!!!! VICTORY COMPLETED !!!!! You touched the Goal mesh and won!");
            Time.timeScale = 0f;
        }
    }
}

    public void TakeDamage(float amount, string source)
    {
        if(!canTakeDamage) return;
        if (iframeTimer > 0f) return;

        CurrentHealth -= amount;
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, MaxHealth);
        iframeTimer = iframeDuration;

        Debug.Log($"[DAMAGE] Player hurt by {source}! Remaining Health: {CurrentHealth}");
        HealthManager();
    }

    private bool ContainsSolidObstacle(List<int> hitIDs)
{
    PowerUps powerUpSystem = Object.FindFirstObjectByType<PowerUps>();
    
    foreach (int id in hitIDs)
    {
        if (powerUpSystem != null && powerUpSystem.TryGetPowerUpIndex(id, out _))
        {
            continue; 
        }
        
        if (levelManager != null && levelManager.IsEnemyCollider(id) && canKillEnemies)
        {
            continue;
        }

        if (levelManager != null)
        {
            EnemyAndLevelManager.PlatformType pType = levelManager.GetPlatformType(id);
            if (pType == EnemyAndLevelManager.PlatformType.Void || pType == EnemyAndLevelManager.PlatformType.Goal)
            {
                continue;
            }
        }

        return true; 
    }
    return false;
}

    private void ProcessItemPickups(List<int> hitIDs)
    {
        PowerUps powerUpSystem = Object.FindFirstObjectByType<PowerUps>();
        if (powerUpSystem == null) return;

        for (int i = hitIDs.Count - 1; i >= 0; i--)
        {
            int id = hitIDs[i];

            if (powerUpSystem.TryGetPowerUpIndex(id, out int index))
            {
                PowerUps.PowerUpType pickedType = powerUpSystem.powerUpTypes[index];
                OnPowerUpCollected(pickedType);
                powerUpSystem.CollectPowerUp(id);
            }
        }
    }

    private void OnPowerUpCollected(PowerUps.PowerUpType type)
    {
        switch (type)
        {
            case PowerUps.PowerUpType.FireBall:
                hasFireballPowerUp = true;
                fireballShootTimer = 0f; 
                fireballActiveTimer = powerUpDuration; 
                Debug.Log("Fireball Power-Up Activated!");
                break;

            case PowerUps.PowerUpType.ExtraLife:
                if (CurrentHealth < MaxHealth)
                {
                    CurrentHealth = Mathf.Clamp(CurrentHealth + damage, 0f, MaxHealth);
                    HealthManager();
                    Debug.Log("Player gained an Extra Life!");
                }
                break;

            case PowerUps.PowerUpType.Invincibility:
                canTakeDamage = false;
                canKillEnemies = true;
                invincibilityActiveTimer = powerUpDuration;
                Debug.Log("Player is now Invincible!");
                break;
        }
    }

    private void SpawnFireballProjectile()
    {
        GameObject fbGO = new GameObject("ActiveFireball");
        fbGO.transform.position = meshPosition + new Vector3(0.8f, 0f, 0f);
        FireBall fbScript = fbGO.AddComponent<FireBall>();
        fbScript.mat = fireballMaterial;
    }

    private void OnDestroy()
    {
        if (CollisionManager.Instance != null)
        {
            CollisionManager.Instance.RemoveCollider(playerColliderID);
        }
    }

    public void HealthManager()
    {
        if (HealthBar != null)
        {
            HealthBar.fillAmount = CurrentHealth / MaxHealth;
        }

        if (CurrentHealth <= 0f)
        {
            Debug.LogError("Player has died.");
            LoseUI.gameObject.SetActive(true);
            Time.timeScale = 0f;
        }
    }
}