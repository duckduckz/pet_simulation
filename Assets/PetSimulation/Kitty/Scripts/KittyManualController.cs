using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class KittyManualController : MonoBehaviour
{
	[Header("Scene References")]
	[Tooltip("Animator on the kitty")]
	public Animator anim;
	[Tooltip("Rigidbody on the football")]
	public Rigidbody ballRb;

	[Header("Tunable Speeds / Forces")]
	public float walkSpeed = 2f;
	public float runSpeed = 5f;
	public float kickForce = 6f;

	private Rigidbody rb;

	void Awake() // cache rigidbody early
	{
		rb = GetComponent<Rigidbody>();
		// Auto-fill references in case you forgot in the Inspector
		if (anim == null)
			anim = GetComponent<Animator>();
		if (ballRb == null)
			Debug.LogWarning($"{name}: Ball Rigidbody not assigned — kicks will do nothing.");
	}

	void Update()
	{
		// ---------- 1. kick input (works even while idle) ----------
		if (Input.GetKeyDown(KeyCode.K))
			KickBall();

		// ---------- 2. locomotion ----------
		float h = Input.GetAxisRaw("Horizontal");
		float v = Input.GetAxisRaw("Vertical");
		Vector3 dir = new Vector3(h, 0, v).normalized;

		if (dir.sqrMagnitude < 0.01f) // no movement keys
		{
			anim.SetInteger("Action", 0); // Idle
			return; // early-out
		}

		bool running = Input.GetKey(KeyCode.LeftShift);
		anim.SetInteger("Action", running ? 2 : 1); // Run or Walk
		float speed = running ? runSpeed : walkSpeed;
		rb.MovePosition(rb.position + dir * speed * Time.deltaTime);
		transform.rotation = Quaternion.LookRotation(dir);
	}

	// ---------- 3. helpers ----------
	void KickBall()
	{
		if (ballRb == null) return; // guard clause
		Vector3 kickDir = (ballRb.position - transform.position).normalized;
		ballRb.AddForce(kickDir * kickForce, ForceMode.Impulse);
	}
}