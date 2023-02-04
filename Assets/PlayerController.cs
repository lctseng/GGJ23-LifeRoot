using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // Object linking
    public LineRenderer rootRenderer;
    public GameObject possibleTarget;
    public Rigidbody2D playerRigidbody;
    public SpringJoint2D rootJoint;
    public AttachDetectorController attachDetectorController;
    public float rootLength;

    // Config
    public float addForceDotValueThreshold = 0.7f;
    public float forceFactor = 0.5f;
    public float speedLimit = 10f;
    public float leaveForceFactor = 10f;
    public float rootNatureLegnthForce = 10f;
    public float rootNatureLength = 7f;
    public float maxRootLength = 10f;

    // Reader
    public float speed;

    // Internal
    Vector3 lastPosition;
    Vector3 direction;
    GameObject currentTarget;

    // Start is called before the first frame update
    void Start()
    {
        lastPosition = transform.position;
    }


    void FixedUpdate()
    {
        UpdateVectors();
        if (currentTarget != null)
        {
            UpdateRoot();
            ApplySwingForce();
            if (rootJoint.distance > maxRootLength)
            {
                rootJoint.autoConfigureDistance = false;
                rootJoint.distance = maxRootLength;

            }
            else
            {
                rootJoint.autoConfigureDistance = true;
            }
        }
        else
        {
            possibleTarget = attachDetectorController.currentTarget;
        }

    }
    
    void UpdateVectors()
    {
        direction = transform.position - lastPosition;
        speed = direction.magnitude / Time.fixedDeltaTime;
        lastPosition = transform.position;
    }

    void UpdateRoot()
    {
        Vector3 direction = currentTarget.transform.position - transform.position;
        transform.up = direction.normalized;
        rootLength = direction.magnitude;
        if (rootLength > rootNatureLength)
        {
            Vector3 force = transform.up * (rootLength - rootNatureLength) / rootNatureLength * rootNatureLegnthForce;
            playerRigidbody.AddForceAtPosition(force, transform.position, ForceMode2D.Force);
        }
    }

    void Update()
    {
        DrawRoot();
        UpdateControl();
    }

    void DrawRoot()
    {
        if (currentTarget != null)
        {
            rootRenderer.enabled = true;
            rootRenderer.SetPosition(0, transform.position);
            rootRenderer.SetPosition(1, currentTarget.transform.position);
        }
        else
        {
            rootRenderer.enabled = false;
        }
            
    }
    
    void ApplySwingForce()
    {
        float dotValue = Vector3.Dot(direction.normalized, transform.right);
        if (Math.Abs(dotValue) > addForceDotValueThreshold && speed < speedLimit)
        {
            Vector3 force = transform.right * forceFactor * dotValue;
            playerRigidbody.AddForceAtPosition(force, transform.position);
        }
    }

    void UpdateControl()
    {
        if (Input.GetKeyDown("space")){
            if(currentTarget == null)
            {
                TryAttach();
            }
        }
        else if (Input.GetKeyUp("space")){
            if (currentTarget != null) {
                OnRootDetach();
            }
        }
    }

    void TryAttach()
    {
        currentTarget = possibleTarget;
        if(currentTarget != null)
        {
            OnRootAttach();
        }
    }

    void OnRootAttach()
    {
        lastPosition = transform.position;
        rootJoint.enabled = true;
        rootJoint.connectedBody = currentTarget.GetComponent<Rigidbody2D>();
        UpdateRoot();
    }

    void OnRootDetach()
    {
        rootJoint.enabled = false;
        rootJoint.connectedBody = null;
        currentTarget = null;
        Vector3 force = direction * leaveForceFactor;
        playerRigidbody.AddForceAtPosition(force, transform.position, ForceMode2D.Force);
    }
}