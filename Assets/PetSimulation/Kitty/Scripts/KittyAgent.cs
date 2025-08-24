using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(DecisionRequester))]
public class KittyAgent : Agent
{
	[Header("Scene references")]
	public Transform ball;
	public Transform goal;
	public Transform ballStart;
	public Transform agentStart;
	public Rigidbody ballRb;

	[Header("Motion tuning")]
	public float walkSpeed = 2f;
	public float runSpeed = 4f;
	public float idleSpeed = 0f; // for completeness

	private Rigidbody rb;
	private Animator anim;
	private float prevDist;
	// Reference to the GestureReceiver script in the scene
	private GestureReceiver gestureReceiver;

	public override void Initialize()
	{
		rb = GetComponent<Rigidbody>();
		anim = GetComponent<Animator>();
		gestureReceiver = Object.FindFirstObjectByType<GestureReceiver>();
	}

	public override void OnEpisodeBegin()
	{
		if (ball == null)
			ball = GameObject.FindWithTag("Ball")?.transform;
		if (goal == null)
			goal = GameObject.Find("Soccer Goal")?.transform;
		if (agentStart == null)
			agentStart = GameObject.Find("AgentStart")?.transform;
		if (ballStart == null)
			ballStart = GameObject.Find("BallStart")?.transform;

		if (ball != null)
			ballRb = ball.GetComponent<Rigidbody>();

		if (agentStart != null)
			transform.position = agentStart.position;

		if (rb != null)
		{
			rb.linearVelocity = Vector3.zero;
			rb.angularVelocity = Vector3.zero;
		}

		if (ballStart != null)
			ball.position = ballStart.position;

		if (ballRb != null)
		{
			ballRb.linearVelocity = Vector3.zero;
			ballRb.angularVelocity = Vector3.zero;
		}

		if (ball != null && goal != null)
			prevDist = Vector3.Distance(ball.position, goal.position);
	}

	public override void CollectObservations(VectorSensor sensor)
	{
		Vector3 relBall = ball != null ? transform.InverseTransformPoint(ball.position) : Vector3.zero;
		Vector3 relGoal = goal != null ? transform.InverseTransformPoint(goal.position) : Vector3.zero;
		Vector3 velSelf = rb != null ? transform.InverseTransformDirection(rb.linearVelocity) : Vector3.zero;
		Vector3 velBall = ballRb != null ? transform.InverseTransformDirection(ballRb.linearVelocity) : Vector3.zero;

		sensor.AddObservation(relBall.x);
		sensor.AddObservation(relBall.z);
		sensor.AddObservation(relGoal.x);
		sensor.AddObservation(relGoal.z);
		sensor.AddObservation(velSelf.x);
		sensor.AddObservation(velSelf.z);
		sensor.AddObservation(velBall.x);
		sensor.AddObservation(velBall.z);
		// pad to 12 floats
		sensor.AddObservation(0f);
		sensor.AddObservation(0f);
		sensor.AddObservation(0f);
		sensor.AddObservation(1f);
	}

	public override void OnActionReceived(ActionBuffers a)
	{
		if (rb == null || anim == null) return;

		float moveX = Mathf.Clamp(a.ContinuousActions[0], -1f, 1f);
		float moveZ = Mathf.Clamp(a.ContinuousActions[1], -1f, 1f);
		bool kick = a.ContinuousActions[2] > 0.5f;
		Vector3 dir = new Vector3(moveX, 0f, moveZ);

		// --- Hybrid gesture override logic ---
		int gesture = gestureReceiver ? gestureReceiver.gesture : -1;
		Debug.Log($"Gesture: {gesture} | moveX: {moveX:F2}, moveZ: {moveZ:F2}, kick: {kick} | dir.sqrMagnitude: {dir.sqrMagnitude:F2}");

		float speed = walkSpeed;
		int animState = 1; // Default to Walk

		if (gesture == 0) // Fist = Idle
		{
			speed = idleSpeed;
			animState = 0;
		}
		else if (gesture == 1) // Open palm = Walk
		{
			speed = walkSpeed;
			animState = 1;
		}
		else if (gesture == 2) // Two fingers = Run
		{
			speed = runSpeed;
			animState = 2;
		}
		// If gesture == -1 (no hand detected), you can let RL decide
		else
		{
			// Optional: let RL model's output determine speed/state if you encode this in your model
			// Here, fallback to walk for safety
			speed = walkSpeed;
			animState = 1;
		}

		Debug.Log($"Selected speed: {speed} | Animator State: {animState}");

		if (dir.sqrMagnitude > 1e-3f && speed > 0)
		{
			rb.MovePosition(rb.position + dir.normalized * speed * Time.fixedDeltaTime);
			transform.rotation = Quaternion.LookRotation(dir);
			anim.SetInteger("Action", animState);
			Debug.Log($"Moving! Direction: {dir.normalized}, Speed: {speed}");
		}
		else
		{
			anim.SetInteger("Action", 0); // Idle animation
			Debug.Log("Not moving (idle or no direction).");
		}

		if (kick)
		{
			Debug.Log("Kick triggered!");
			KickBall();
		}

		// --- Reward Shaping ---
		if (ball != null && goal != null)
		{
			float dist = Vector3.Distance(ball.position, goal.position);
			float distToBall = Vector3.Distance(transform.position, ball.position);
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
		c[2] = Input.GetKey(KeyCode.K) ? 1f : 0f;
	}

	void KickBall()
	{
		if (ball == null || ballRb == null || goal == null) return;
		Vector3 flatGoal = new Vector3(goal.position.x, ball.position.y, goal.position.z);
		Vector3 dir = (flatGoal - ball.position).normalized;
		ballRb.AddForce(dir * 10f, ForceMode.Impulse);
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