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

	[Type(21, "string")]
	public string displayName = default(string);

	[Type(22, "number")]
	public float timestamp = default(float);

		[Type(23, "boolean")]
		public bool isReady = default(bool);

		[Type(24, "number")]
		public float skinIndex = default(float);

	[Type(25, "boolean")]
	public bool isHitReacting = default(bool);

	[Type(26, "number")]
	public float hitReactionTimeRemaining = default(float);

	[Type(27, "number")]
	public float hitReactionPitch = default(float);

	[Type(28, "number")]
	public float hitReactionRoll = default(float);

	[Type(29, "number")]
	public float hitReactionSeed = default(float);

	[Type(30, "number")]
	public float hitTriggerId = default(float);

	[Type(31, "number")]
	public float hitSourceX = default(float);

	[Type(32, "number")]
	public float hitSourceY = default(float);

	[Type(33, "number")]
	public float hitSourceZ = default(float);

	[Type(34, "boolean")]
	public bool isCarrying = default(bool);

	[Type(35, "boolean")]
	public bool isBeingCarried = default(bool);

	[Type(36, "string")]
	public string carriedPlayerSessionId = default(string);

	[Type(37, "string")]
	public string carrierSessionId = default(string);

	[Type(38, "boolean")]
	public bool isEliminated = default(bool);

	[Type(39, "number")]
	public float speedBoostMultiplier = 1f;

	[Type(40, "number")]
	public float speedBoostTimeRemaining = default(float);

	[Type(41, "number")]
	public float jumpBoostMultiplier = 1f;

	[Type(42, "number")]
	public float jumpBoostTimeRemaining = default(float);

	[Type(43, "boolean")]
	public bool isSpectator = default(bool);

	[Type(44, "boolean")]
	public bool isShootingMode = default(bool);

	[Type(45, "number")]
	public float shotTriggerId = default(float);

	[Type(46, "number")]
	public float combatHealth = 100f;

	[Type(47, "number")]
	public float maxCombatHealth = 100f;

	[Type(48, "number")]
	public float kills = default(float);

	[Type(49, "number")]
	public float deaths = default(float);

	[Type(50, "number")]
	public float assists = default(float);
	}
