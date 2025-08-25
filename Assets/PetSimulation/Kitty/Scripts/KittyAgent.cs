using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(DecisionRequester))]
public class KittyAgent : Agent
{
    private const int ACT_IDLE = 0;
    private const int ACT_WALK = 1;
    private const int ACT_RUN  = 2;
    private const int ACT_LOSE = 3;   

    private const int GES_IDLE = 0;
    private const int GES_WALK = 1;
    private const int GES_RUN  = 2;
    private const int GES_KICK = 3;   
    private const int GES_LOSE = 4;   

    [Header("Scene references")]
    public Transform ball;
    public Transform goal;
    public Transform ballStart;
    public Transform agentStart;
    public Rigidbody ballRb;

    [Header("Motion tuning")]
    public float walkSpeed = 2f;
    public float runSpeed = 4f;
    public float idleSpeed = 0f;

    [Header("Quality-of-life")]
    public bool autoFaceWhenNear = true;
    public float faceAssistDistance = 1.2f;

    [Header("Kick settings")]
    public float kickForce = 10f;
    public float kickDistance = 2.0f;
    [Range(-1f, 1f)] public float kickFacingDot = 0.7f;
    public float kickCooldown = 0.35f;

    [Header("Assist toward ball (only when Walk/Run)")]
    [Tooltip("Start blending toward the ball when farther than this.")]
    public float assistStartDist = 0.8f;
    [Tooltip("Use full assist at/above this distance.")]
    public float assistFullDist = 3.0f;
    [Tooltip("Max blend weight toward the ball (0=no assist, 1=ignore RL dir).")]
    [Range(0f, 1f)] public float assistMaxWeight = 0.65f;

    private Rigidbody rb;
    private Animator anim;
    private float prevDist;
    private GestureReceiver gestureReceiver;

    private bool _kickPressedLast;
    private bool _pendingKick;
    private float _lastKickTime;
    private bool _ballFrozen;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
        gestureReceiver = Object.FindFirstObjectByType<GestureReceiver>();
    }

    public override void OnEpisodeBegin()
    {
        if (ball == null)       ball = GameObject.FindWithTag("Ball")?.transform;
        if (goal == null)       goal = GameObject.Find("Soccer Goal")?.transform;
        if (agentStart == null) agentStart = GameObject.Find("AgentStart")?.transform;
        if (ballStart == null)  ballStart = GameObject.Find("BallStart")?.transform;

        if (ball != null)       ballRb = ball.GetComponent<Rigidbody>();

        if (agentStart != null) transform.position = agentStart.position;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (ballStart != null && ball != null)
            ball.position = ballStart.position;

        if (ballRb != null)
        {
            ballRb.constraints = RigidbodyConstraints.None;
            ballRb.linearVelocity = Vector3.zero;
            ballRb.angularVelocity = Vector3.zero;
        }

        if (ball != null && goal != null)
            prevDist = Vector3.Distance(ball.position, goal.position);

        _kickPressedLast = false;
        _lastKickTime = -999f;
        _ballFrozen = false;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 relBall = ball ? transform.InverseTransformPoint(ball.position) : Vector3.zero;
        Vector3 relGoal = goal ? transform.InverseTransformPoint(goal.position) : Vector3.zero;
        Vector3 velSelf = rb ? transform.InverseTransformDirection(rb.linearVelocity) : Vector3.zero;
        Vector3 velBall = ballRb ? transform.InverseTransformDirection(ballRb.linearVelocity) : Vector3.zero;

        sensor.AddObservation(relBall.x);
        sensor.AddObservation(relBall.z);
        sensor.AddObservation(relGoal.x);
        sensor.AddObservation(relGoal.z);
        sensor.AddObservation(velSelf.x);
        sensor.AddObservation(velSelf.z);
        sensor.AddObservation(velBall.x);
        sensor.AddObservation(velBall.z);
       
        sensor.AddObservation(0f);
        sensor.AddObservation(0f);
        sensor.AddObservation(0f);
        sensor.AddObservation(1f);
    }

    public override void OnActionReceived(ActionBuffers a)
    {
        if (rb == null || anim == null) return;

        // direction from model 
        float moveX = Mathf.Clamp(a.ContinuousActions[0], -1f, 1f);
        float moveZ = Mathf.Clamp(a.ContinuousActions[1], -1f, 1f);
        Vector3 rlDir = new Vector3(moveX, 0f, moveZ);
        if (rlDir.sqrMagnitude > 1f) rlDir.Normalize();

        // gestures control
        int gesture = gestureReceiver ? gestureReceiver.gesture : -1;
        float speed; int animState;

		// map gestures -> animation state
		if (gesture == GES_IDLE)
		{
			speed = idleSpeed; animState = ACT_IDLE;
		}
		else if (gesture == GES_WALK)
		{
			speed = walkSpeed; animState = ACT_WALK;
		}
		else if (gesture == GES_RUN)
		{
			speed = runSpeed; animState = ACT_RUN;
		}
		else if (gesture == GES_LOSE)
		{
			speed = 0f; animState = ACT_LOSE;
		}
		else
		{
			speed = walkSpeed; animState = ACT_WALK;
		}

        // freeze&unfreeze ball while Idle or Lose sit
        if (ballRb)
        {
            bool shouldFreeze = (animState == ACT_IDLE) || (animState == ACT_LOSE);
            if (shouldFreeze && !_ballFrozen)
            {
                ballRb.linearVelocity = Vector3.zero;
                ballRb.angularVelocity = Vector3.zero;
                ballRb.constraints = RigidbodyConstraints.FreezePositionX
                                   | RigidbodyConstraints.FreezePositionZ
                                   | RigidbodyConstraints.FreezeRotation;
                _ballFrozen = true;
            }
            else if (!shouldFreeze && _ballFrozen)
            {
                ballRb.constraints = RigidbodyConstraints.None;
                _ballFrozen = false;
            }
        }

        // when distance or facing to the ball 
        float distToBall = Mathf.Infinity;
        Vector3 towardBall = Vector3.zero;
        float facingDot = -1f;
        if (ball)
        {
            Vector3 toBall = ball.position - transform.position;
            toBall.y = 0f;
            distToBall = toBall.magnitude;
            towardBall = distToBall > 1e-3f ? toBall / distToBall : Vector3.zero;
            if (towardBall != Vector3.zero)
                facingDot = Vector3.Dot(transform.forward, towardBall);
        }

        // final move direction
        Vector3 dir = rlDir;

        // normal assist while walking/running
        bool isWalkOrRun = (animState == ACT_WALK) || (animState == ACT_RUN);
        if (isWalkOrRun && towardBall != Vector3.zero)
        {
            float t = Mathf.InverseLerp(assistStartDist, assistFullDist, distToBall);
            float w = Mathf.Clamp01(t) * assistMaxWeight;         
            dir = (dir.sqrMagnitude < 1e-6f) ? towardBall : Vector3.Slerp(dir.normalized, towardBall, w);
        }

        // kick-pending assist: when showing 3 fingers but not yet in range, strongly steer to the ball
        bool gestureKickHeld = (gesture == GES_KICK);
        if (gestureKickHeld && (distToBall > kickDistance || facingDot < kickFacingDot))
        {
            if (towardBall != Vector3.zero)
            {
                dir = towardBall;                    // full override while 3-fingers is up & not eligible
                speed = Mathf.Max(speed, runSpeed);  // ensure we don't crawl toward it
                transform.rotation = Quaternion.LookRotation(towardBall);
                Debug.Log($"[KickAssist] Driving toward ball. dist={distToBall:F2}, dot={facingDot:F2}");
            }
        }

        // move/animate
        if (dir.sqrMagnitude > 1e-6f && speed > 0f)
        {
            Vector3 step = dir.normalized * speed * Time.fixedDeltaTime;
            rb.MovePosition(rb.position + step);
            if (!(gestureKickHeld && towardBall != Vector3.zero)) // rotation already handled above for kick-assist
                transform.rotation = Quaternion.LookRotation(dir);
            anim.SetInteger("Action", animState);
        }
        else
        {
            // idle or Lose: no movement
            anim.SetInteger("Action", animState);
        }

        // Kick
        if (gestureKickHeld && !_kickPressedLast) _pendingKick = true;
        else if (!gestureKickHeld)                _pendingKick = false;

        bool canKickNow = (distToBall <= kickDistance) && (facingDot >= kickFacingDot);
        bool cooldownOk = (Time.time - _lastKickTime) >= kickCooldown;

        if (_pendingKick && canKickNow && cooldownOk)
        {
            Debug.Log($"KICK! dist={distToBall:F2}, dot={facingDot:F2}");
            KickBall();
            _lastKickTime = Time.time;
            _pendingKick = false;
        }
        else if (gestureKickHeld)
        {
            Debug.Log($"Kick pending... dist={distToBall:F2} (need ≤{kickDistance}), dot={facingDot:F2} (need ≥{kickFacingDot}), cooldownOK={cooldownOk}");
        }

        _kickPressedLast = gestureKickHeld;

        // rewards
        if (ball && goal)
        {
            float dist = Vector3.Distance(ball.position, goal.position);
            AddReward(0.5f * (prevDist - dist));
            AddReward(-0.01f * distToBall);
            AddReward(-0.001f);
            prevDist = dist;
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var c = actionsOut.ContinuousActions;
        c[0] = Input.GetAxisRaw("Horizontal");
        c[1] = Input.GetAxisRaw("Vertical");
        c[2] = 0f; // we ignore RL kick; kicks are from gesture==3
    }

    void KickBall()
    {
        if (!ball || !ballRb || !goal) return;

        if (_ballFrozen)
        {
            ballRb.constraints = RigidbodyConstraints.None;
            _ballFrozen = false;
        }

        Vector3 flatGoal = new Vector3(goal.position.x, ball.position.y, goal.position.z);
        Vector3 dir = (flatGoal - ball.position).normalized;
        ballRb.AddForce(dir * kickForce, ForceMode.Impulse);

        Debug.Log("KickBall(): impulse applied");
    }

    public void ScoredGoal()
    {
        AddReward(+1f);
        EndEpisode();
    }

    public void OutOfBounds()
    {
        AddReward(-1f);
        EndEpisode();
    }
}
