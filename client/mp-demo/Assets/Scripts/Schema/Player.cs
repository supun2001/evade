// 
// THIS FILE HAS BEEN GENERATED AUTOMATICALLY
// DO NOT CHANGE IT MANUALLY UNLESS YOU KNOW WHAT YOU'RE DOING
// 
// GENERATED USING @colyseus/schema 3.0.76
// 

using Colyseus.Schema;
#if UNITY_5_3_OR_NEWER
using UnityEngine.Scripting;
#endif

public partial class Player : Schema {
#if UNITY_5_3_OR_NEWER
[Preserve]
#endif
public Player() { }
	[Type(0, "number")]
	public float x = default(float);

	[Type(1, "number")]
	public float y = default(float);

	[Type(2, "number")]
	public float z = default(float);

	[Type(3, "number")]
	public float rotationY = default(float);

	[Type(4, "number")]
	public float velocityX = default(float);

	[Type(5, "number")]
	public float velocityY = default(float);

	[Type(6, "number")]
	public float velocityZ = default(float);

	[Type(7, "number")]
	public float animInputX = default(float);

	[Type(8, "number")]
	public float animInputY = default(float);

	[Type(9, "boolean")]
	public bool isGrounded = default(bool);

	[Type(10, "boolean")]
	public bool isJumping = default(bool);

	[Type(11, "boolean")]
	public bool isInjured = default(bool);

	[Type(12, "boolean")]
	public bool isCrouching = default(bool);

	[Type(13, "boolean")]
	public bool isWallRunning = default(bool);

	[Type(14, "number")]
	public float wallRunSide = default(float);

	[Type(15, "number")]
	public float moveInputX = default(float);

	[Type(16, "number")]
	public float moveInputY = default(float);

	[Type(17, "number")]
	public float visualYaw = 180f;

	[Type(18, "number")]
	public float cameraRotationX = default(float);

	[Type(19, "number")]
	public float cameraRotationY = default(float);

	[Type(20, "string")]
	public string sessionId = default(string);

	[Type(21, "number")]
	public float timestamp = default(float);

		[Type(22, "boolean")]
		public bool isReady = default(bool);

		[Type(23, "number")]
		public float skinIndex = default(float);
	}
