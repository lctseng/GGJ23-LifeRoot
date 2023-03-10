using System;
using System.Collections;
using System.Collections.Generic;
using Game.GameEvent;
using UniRx;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance;

    // Object linking
    public LineRenderer rootRenderer;
    public GameObject possibleTarget;
    public Rigidbody2D playerRigidbody;
    public SpringJoint2D rootJoint;
    public AttachDetectorController attachDetectorController;
    public float rootLength;
    public Collider2D playerCollider;
    public Girl girlController;
    public bool canControl = false;
    public VoidEvent finishGameEvent;
    public VoidEvent startGameEvent;
    public AudioClip dieAudioClip;
    public AudioClip victoryAudioClip;
    public bool judgeWinLoss = false;



    // Config
    public float addForceDotValueThreshold = 0.7f;
    public float forceFactor = 0.5f;
    public float speedLimit = 10f;
    public float leaveForceFactor = 10f;
    public float rootNatureLengthForce = 10f;
    public float rootNatureLength = 7f;
    public float maxRootLength = 10f;
    public float jumpForceFactor = 10f;
    public Vector3 floorJumpDirection = new Vector3(0, 1, 0);
    public float jumpFloorCooldown = 1f;
    public float minDrag = 0.5f;
    public float dieSpeed = 25f;
    public float deadShowTime = 1f;
    public float rootExtendFactor = 8f;

    // Reader
    public float speed;
    public Transform playerTransform;
    public float velocity;
    public float playerY;

    // Internal
    Vector3 lastPosition;
    Vector3 direction;
    Vector3 fixedDirection;
    Collider2D[] floorCollidedResults = new Collider2D[2];
    ContactFilter2D floorFilter;
    public bool touchFloor = false;
    bool prevTouchFloor = false;
    DateTime lastJump;
    bool dead = false;
    DateTime dieAt;
    bool deadEventFired = false;
    public AudioSource audioSource;
    public AudioSource winAudioSource;




    public GameObject _currentTarget { private set; get; }
    private GameObject currentTarget
    {
        set
        {
            _currentTarget = value;
            EventAggregator.Publish(new HookRock(_currentTarget));
        }
        get => _currentTarget;
    }

    void Awake()
    {
        Instance = this;

    }
    
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        playerTransform = playerRigidbody.gameObject.transform;
        finishGameEvent.Register(OnVictory);
        startGameEvent.Register(OnStart);
        lastPosition = playerTransform.position;
        fixedDirection = playerTransform.right;
        currentTarget = null;
        EventAggregator.OnEvent<BreakRoot>().Subscribe(e => { if (e.O == currentTarget) OnRootDetach(); }).AddTo(this);
        floorFilter = new ContactFilter2D();
        floorFilter.useLayerMask = true;
        floorFilter.layerMask = LayerMask.GetMask("Floor");
        lastJump = DateTime.Now;
    }



    void FixedUpdate()
    {
        UpdateVectors();
        UpdateFloor();

        if (currentTarget != null)
        {
            UpdateRoot();
            ApplySwingForce();
            UpdateRootHooking();
        }
        else
        {
            //playerTransform.right = fixedDirection;
            possibleTarget = attachDetectorController.currentTarget;
        }
        UpdateSpeedLimit();

    }

    void UpdateRootHooking()
    {

        if (ObjBaseCtrl.HookingObj != null)
        {
            if (rootJoint.distance > maxRootLength)
            {
                rootJoint.autoConfigureDistance = false;
                rootJoint.distance = maxRootLength;

            }
            else if (rootJoint.distance < rootNatureLength)
            {
                rootJoint.autoConfigureDistance = false;
                rootJoint.distance += rootExtendFactor * Time.fixedDeltaTime;

            }
            else
            {
                rootJoint.autoConfigureDistance = true;
            }
        }
    }


    void UpdateVectors()
    {
        Vector3 diff = playerTransform.position - lastPosition;
        direction = diff.normalized;
        speed = playerRigidbody.velocity.magnitude;
        lastPosition = playerTransform.position;
    }

    void UpdateRoot()
    {
        Vector3 direction = currentTarget.transform.position - playerTransform.position;
        playerTransform.up = direction.normalized;
        rootLength = direction.magnitude;
        if (rootLength > rootNatureLength)
        {
            Vector3 force = playerTransform.up * (rootLength - rootNatureLength) / rootNatureLength * rootNatureLengthForce;
            playerRigidbody.AddForceAtPosition(force, playerTransform.position, ForceMode2D.Force);
        }
    }

    void Update()
    {
        DrawRoot();
        if (dead)
        {
            UpdateDead();
        }
        else if(canControl)
        {
            UpdateControl();
        }
        
    }

    void DrawRoot()
    {
        if (currentTarget != null)
        {
            rootRenderer.enabled = true;
            rootRenderer.SetPosition(0, playerRigidbody.gameObject.transform.position);
            rootRenderer.SetPosition(1, currentTarget.transform.position);
        }
        else
        {
            rootRenderer.enabled = false;
        }

    }

    void ApplySwingForce()
    {
        float dotValue = Vector3.Dot(direction.normalized, playerTransform.right);
        if (Math.Abs(dotValue) > addForceDotValueThreshold && speed < speedLimit)
        {
            Vector3 force = playerTransform.right * forceFactor * dotValue;
            playerRigidbody.AddForce(force);
        }
    }

    void UpdateControl()
    {
        if (Input.GetKeyDown("space"))
        {
            if (currentTarget == null)
            {
                if (!TryAttach() && touchFloor)
                {
                    JumpFromFloor();
                }
            }
        }
        else if (Input.GetKeyUp("space"))
        {
            if (currentTarget != null)
            {
                OnRootDetach();
            }
        }
    }

    bool TryAttach()
    {
        currentTarget = possibleTarget;
        if (currentTarget != null)
        {
            OnRootAttach();
            return true;
        }
        return false;
    }

    void OnRootAttach()
    {
        lastPosition = playerTransform.position;
        rootJoint.enabled = true;
        rootJoint.connectedBody = currentTarget.GetComponent<Rigidbody2D>();
        UpdateRoot();
    }

    public void OnRootDetach()
    {
        rootJoint.enabled = false;
        rootJoint.connectedBody = null;
        currentTarget = null;
        Vector3 force = direction * leaveForceFactor;
        playerRigidbody.AddForce(force, ForceMode2D.Force);
        fixedDirection = playerTransform.right;
    }

    void UpdateFloor()
    {
        if (!judgeWinLoss) return;
        touchFloor = playerCollider.OverlapCollider(floorFilter, floorCollidedResults) > 0;
        if(!prevTouchFloor && touchFloor)
        {
            OnTouchFloor();
        }
        prevTouchFloor = touchFloor;
    }

    void OnTouchFloor()
    {
        
        if(speed > dieSpeed)
        {
            OnDie();
        }
    }

    void OnDie()
    {
        OnRootDetach();
        judgeWinLoss = false;
        dead = true;
        canControl = false;
        Debug.Log("Die!");
        girlController.BreakBody(Girl.BodyPart.All);
        playerCollider.enabled = false;
        Destroy(attachDetectorController.gameObject.transform.parent.gameObject);
        audioSource.PlayOneShot(dieAudioClip, 1);
        dieAt = DateTime.Now;
    }
    void UpdateDead()
    {
        if(!deadEventFired && ((DateTime.Now - dieAt).TotalSeconds > deadShowTime))
        {
            EventAggregator.Publish(new OnPlayerDead());
            deadEventFired = true;
            
        }
        
    }

    void JumpFromFloor()
    {
        float timeDiff = (float)(DateTime.Now - lastJump).TotalSeconds;
        if(timeDiff < jumpFloorCooldown)
        {
            return;
        }
        lastJump = DateTime.Now;
        Vector3 force = floorJumpDirection * jumpForceFactor;
        playerRigidbody.AddForce(force, ForceMode2D.Impulse);

    }

    void UpdateSpeedLimit()
    {
        if (speed > speedLimit + minDrag)
        {
            playerRigidbody.drag=  speed - speedLimit;
        }
        else
        {
            playerRigidbody.drag = minDrag;
        }
        playerRigidbody.angularDrag = playerRigidbody.drag * 2f;
    }

    public void OnStart()
    {
        Debug.Log("Start!");
        canControl = true;
        judgeWinLoss = true;


    }

    void OnVictory()
    {
        Debug.Log("Win!");
        judgeWinLoss = false;

    }
}

public class HookRock {
    public GameObject O;
    public HookRock(GameObject o) {
        O = o;
    }
}

public class BreakRoot {
    public GameObject O;
    public BreakRoot(GameObject o) {
        O = o;
    }
}


