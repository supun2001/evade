import { MapSchema, Schema, type } from "@colyseus/schema";
import { Player } from "./Player";

export class MyRoomState extends Schema {
  @type({ map: Player }) players = new MapSchema<Player>();
  @type("boolean") isGameStarted = false;
}
