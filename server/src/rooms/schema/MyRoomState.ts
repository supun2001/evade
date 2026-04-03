import { MapSchema, Schema, type } from "@colyseus/schema";
import { Player } from "./Player";
import { NextbotState } from "./NextbotState";

export class MyRoomState extends Schema {
  @type({ map: Player }) players = new MapSchema<Player>();
  @type("boolean") isGameStarted = false;
  @type({ map: NextbotState }) nextbots = new MapSchema<NextbotState>();
}
