import { Schema, type } from "@colyseus/schema";

export class NextbotState extends Schema {
  @type("number") x: number = 0;
  @type("number") y: number = 0;
  @type("number") z: number = 0;
  @type("number") rotationY: number = 0;
  @type("string") targetSessionId: string = "";
  @type("boolean") isActive: boolean = false;
}
