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

public partial class NextbotState : Schema {
#if UNITY_5_3_OR_NEWER
[Preserve]
#endif
public NextbotState() { }
	[Type(0, "number")]
	public float x = default(float);

	[Type(1, "number")]
	public float y = default(float);

	[Type(2, "number")]
	public float z = default(float);

	[Type(3, "number")]
	public float rotationY = default(float);

	[Type(4, "string")]
	public string targetSessionId = default(string);

	[Type(5, "boolean")]
	public bool isActive = default(bool);

	[Type(6, "number")]
	public float velocityX = default(float);

	[Type(7, "number")]
	public float velocityY = default(float);

	[Type(8, "number")]
	public float velocityZ = default(float);
}
