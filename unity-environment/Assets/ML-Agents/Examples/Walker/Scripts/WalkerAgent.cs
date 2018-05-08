﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WalkerAgent : Agent
{
    [Header("Specific to Walker")] 
    public Vector3 goalDirection;
    public Transform hips;
    public Transform chest;
    public Transform spine;
    public Transform head;
    public Transform thighL;
    public Transform shinL;
    public Transform footL;
    public Transform thighR;
    public Transform shinR;
    public Transform footR;
    public Transform armL;
    public Transform forearmL;
    public Transform handL;
    public Transform armR;
    public Transform forearmR;
    public Transform handR;
    public Dictionary<Transform, BodyPart> bodyParts = new Dictionary<Transform, BodyPart>();

    /// <summary>
    /// Used to store relevant information for acting and learning for each body part in agent.
    /// </summary>
    [System.Serializable]
    public class BodyPart
    {
        public ConfigurableJoint joint;
        public Rigidbody rb;
        public Vector3 startingPos;
        public Quaternion startingRot;
        public GroundContact groundContact;
    }

    /// <summary>
    /// Create BodyPart object and add it to dictionary.
    /// </summary>
    public void SetupBodyPart(Transform t)
    {
        BodyPart bp = new BodyPart
        {
            rb = t.GetComponent<Rigidbody>(),
            joint = t.GetComponent<ConfigurableJoint>(),
            startingPos = t.position,
            startingRot = t.rotation
        };
        bodyParts.Add(t, bp);
        bp.groundContact = t.GetComponent<GroundContact>();
    }

    public override void InitializeAgent()
    {
        SetupBodyPart(hips);
        SetupBodyPart(chest);
        SetupBodyPart(spine);
        SetupBodyPart(head);
        SetupBodyPart(thighL);
        SetupBodyPart(shinL);
        SetupBodyPart(footL);
        SetupBodyPart(thighR);
        SetupBodyPart(shinR);
        SetupBodyPart(footR);
        SetupBodyPart(armL);
        SetupBodyPart(forearmL);
        SetupBodyPart(handL);
        SetupBodyPart(armR);
        SetupBodyPart(forearmR);
        SetupBodyPart(handR);
    }

    /// <summary>
    /// Obtains joint rotation (in Quaternion) from joint. 
    /// </summary>
    public static Quaternion GetJointRotation(ConfigurableJoint joint)
    {
        return (Quaternion.FromToRotation(joint.axis, joint.connectedBody.transform.rotation.eulerAngles));
    }

    /// <summary>
    /// Add relevant information on each body part to observations.
    /// </summary>
    public void CollectObservationBodyPart(BodyPart bp)
    {
        var rb = bp.rb;
        AddVectorObs(bp.groundContact.touchingGround ? 1 : 0); // Is this bp touching the ground
        bp.groundContact.touchingGround = false;

        AddVectorObs(rb.velocity);
        AddVectorObs(rb.angularVelocity);
        Vector3 localPosRelToHips = hips.InverseTransformPoint(rb.position);
        AddVectorObs(localPosRelToHips);

        if (bp.joint && (bp.rb.transform != handL && bp.rb.transform != handR))
        {
            var jointRotation = GetJointRotation(bp.joint);
            AddVectorObs(jointRotation); // Get the joint rotation
        }
    }

    /// <summary>
    /// Loop over body parts to add them to observation.
    /// </summary>
    public override void CollectObservations()
    {
        AddVectorObs(goalDirection);
        foreach (var bodyPart in bodyParts.Values)
        {
            CollectObservationBodyPart(bodyPart);
        }
    }

    /// <summary>
    /// Apply torque to bodyPart `bp` according to defined goal `x, y, z` angle and force `strength`.
    /// </summary>
    public void SetNormalizedTargetRotation(BodyPart bp, float x, float y, float z, float strength)
    {
        // Transform values from [-1, 1] to [0, 1]
        x = (x + 1f) * 0.5f;
        y = (y + 1f) * 0.5f;
        z = (z + 1f) * 0.5f;

        var xRot = Mathf.Lerp(bp.joint.lowAngularXLimit.limit, bp.joint.highAngularXLimit.limit, x);
        var yRot = Mathf.Lerp(-bp.joint.angularYLimit.limit, bp.joint.angularYLimit.limit, y);
        var zRot = Mathf.Lerp(-bp.joint.angularZLimit.limit, bp.joint.angularZLimit.limit, z);

        bp.joint.targetRotation = Quaternion.Euler(xRot, yRot, zRot);
        var jd = new JointDrive
        {
            positionSpring = ((strength + 1f) * 0.5f) * 10000f,
            maximumForce = 250000f
        };
        bp.joint.slerpDrive = jd;
    }

    public override void AgentAction(float[] vectorAction, string textAction)
    {
        // Apply action to all relevant body parts. 
        SetNormalizedTargetRotation(bodyParts[chest], vectorAction[0], vectorAction[1], vectorAction[2],
            vectorAction[26]);
        SetNormalizedTargetRotation(bodyParts[spine], vectorAction[3], vectorAction[4], vectorAction[5],
            vectorAction[27]);

        SetNormalizedTargetRotation(bodyParts[thighL], vectorAction[6], vectorAction[7], 0, vectorAction[28]);
        SetNormalizedTargetRotation(bodyParts[shinL], vectorAction[8], 0, 0, vectorAction[29]);
        SetNormalizedTargetRotation(bodyParts[footL], vectorAction[9], vectorAction[10], vectorAction[11],
            vectorAction[30]);
        SetNormalizedTargetRotation(bodyParts[thighR], vectorAction[12], vectorAction[13], 0, vectorAction[31]);
        SetNormalizedTargetRotation(bodyParts[shinR], vectorAction[14], 0, 0, vectorAction[32]);
        SetNormalizedTargetRotation(bodyParts[footR], vectorAction[15], vectorAction[16], vectorAction[17],
            vectorAction[33]);

        SetNormalizedTargetRotation(bodyParts[armL], vectorAction[18], vectorAction[19], 0, vectorAction[34]);
        SetNormalizedTargetRotation(bodyParts[forearmL], vectorAction[20], 0, 0, vectorAction[34]);
        SetNormalizedTargetRotation(bodyParts[armR], vectorAction[21], vectorAction[22], 0, vectorAction[36]);
        SetNormalizedTargetRotation(bodyParts[forearmR], vectorAction[23], 0, 0, vectorAction[37]);
        SetNormalizedTargetRotation(bodyParts[head], vectorAction[24], vectorAction[25], 0, vectorAction[38]);

        // Set reward for this step according to mixture of the following elements.
        // a. Velocity alignment with goal direction.
        // b. Rotation alignment with goal direction.
        // c. Encourage head height.
        // d. Discourage head movement.
        AddReward(
            + 0.03f * Vector3.Dot(goalDirection, bodyParts[hips].rb.velocity)
            + 0.01f * Vector3.Dot(goalDirection, hips.forward)
            + 0.01f * (head.position.y - hips.position.y)
            - 0.01f * Vector3.Distance(bodyParts[head].rb.velocity, bodyParts[hips].rb.velocity)
        );
    }

    /// <summary>
    /// Loop over body parts and reset them to initial conditions.
    /// </summary>
    public override void AgentReset()
    {
        transform.rotation = Quaternion.LookRotation(goalDirection);
        
        foreach (var item in bodyParts)
        {
            item.Key.position = item.Value.startingPos;
            item.Key.rotation = item.Value.startingRot;
            item.Value.rb.velocity = Vector3.zero;
            item.Value.rb.angularVelocity = Vector3.zero;
        }
    }
}