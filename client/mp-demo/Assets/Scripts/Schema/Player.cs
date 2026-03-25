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

	[Type(11, "number")]
	public float cameraRotationX = default(float);

	[Type(12, "number")]
	public float cameraRotationY = default(float);

	[Type(13, "string")]
	public string sessionId = default(string);

	[Type(14, "number")]
	public float timestamp = default(float);

	[Type(15, "boolean")]
	public bool isReady = default(bool);

	[Type(16, "number")]
	public float skinIndex = default(float);
}

