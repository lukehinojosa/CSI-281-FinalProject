using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(GOAPAgent))]
public class EnemyUnit : NetworkBehaviour
{
    [Header("Combat")]
    public float attackRange = 1.5f;
    public float attackRate = 2.0f;
    private float lastAttackTime;

    [Header("Possession")]
    public bool isPossessed = false;
    public float moveSpeed = 6f;

    private Transform playerTarget;
    private PlayerStats playerStats;
    private GOAPAgent agent;
    private CharacterController controller;
    
    public float turnSmoothTime = 0.1f;
    private float turnSmoothVelocity;
    
    // Sync possession state across network
    public NetworkVariable<bool> netIsPossessed = new NetworkVariable<bool>(false);

    void Start()
    {
        agent = GetComponent<GOAPAgent>();
        controller = GetComponent<CharacterController>();
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTarget = player.transform;
            playerStats = player.GetComponent<PlayerStats>();
        }
    }

    void Update()
    {
        // Host runs logic to update NetworkVariable
        if (IsServer)
        {
            netIsPossessed.Value = isPossessed;
        }
        
        // Possession Logic (Multiplayer Only)
        if (isPossessed)
        {
            // Only allow movement if I own this object (Client Authority)
            if (IsOwner) 
            {
                HandleManualControl();
            }
        }
        
        // Attack Logic (Server Only handles damage)
        if (IsServer)
        {
            // Automatic attack if close enough.
            TryAttack();
        }
    }
    
    // Server Authoritative Possession Swap
    [ServerRpc(RequireOwnership = false)]
    public void RequestPossessionServerRpc(ulong newOwnerId)
    {
        // Transfer ownership to the client who asked
        GetComponent<NetworkObject>().ChangeOwnership(newOwnerId);
        isPossessed = true;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ReleasePossessionServerRpc()
    {
        // Give ownership back to Server
        GetComponent<NetworkObject>().RemoveOwnership();
        isPossessed = false;
    }

    void HandleManualControl()
    {
        // Disable AI but keep Energy depletion running
        if (!agent.isManualControl) 
        {
            agent.isManualControl = true;
        }

        // Manual Movement
        float h = 0;
        float v = 0;
        if (Input.GetKey(KeyCode.LeftArrow)) h = -1;
        if (Input.GetKey(KeyCode.RightArrow)) h = 1;
        if (Input.GetKey(KeyCode.UpArrow)) v = 1;
        if (Input.GetKey(KeyCode.DownArrow)) v = -1;

        Vector3 inputDir = new Vector3(h, 0, v).normalized;
        
        if (inputDir.magnitude >= 0.1f)
        {
            // Smooth Rotation
            float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            // Move in the direction we are aiming
            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            controller.Move(moveDir.normalized * moveSpeed * Time.deltaTime);
        }
        
        controller.Move(Vector3.down * 9.8f * Time.deltaTime); // Gravity
    }

    void TryAttack()
    {
        if (playerTarget == null || !playerTarget.gameObject.activeSelf) return;

        float dist = Vector3.Distance(transform.position, playerTarget.position);
        if (dist <= attackRange)
        {
            if (Time.time > lastAttackTime + attackRate)
            {
                lastAttackTime = Time.time;
                Debug.Log($"{name} attacked Player!");
                if (playerStats != null) playerStats.TakeDamage();
            }
        }
    }

    // Called when possession ends
    public void ReleasePossession()
    {
        isPossessed = false;
        
        // Re-enable AI
        if (agent != null)
        {
            agent.isManualControl = false;
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        // For manual recharging. The AI handles its own recharging via the GOAP actions.
        if (isPossessed && other.CompareTag("EnergyStation"))
        {
            if (agent != null)
            {
                agent.ReplenishEnergy();
                Debug.Log($"{name} manually recharged energy!");
            }
        }
    }
}