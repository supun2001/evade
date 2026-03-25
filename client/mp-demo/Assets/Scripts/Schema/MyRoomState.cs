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

public partial class MyRoomState : Schema {
#if UNITY_5_3_OR_NEWER
[Preserve]
#endif
public MyRoomState() { }
	[Type(0, "map", typeof(MapSchema<Player>))]
	public MapSchema<Player> players = null;

	[Type(1, "boolean")]
	public bool isGameStarted = false;
}

