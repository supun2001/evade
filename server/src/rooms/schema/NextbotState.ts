import { Schema, type } from "@colyseus/schema";

export class NextbotState extends Schema {
  @type("number") x: number = 0;
  @type("number") y: number = 0;
  @type("number") z: number = 0;
  @type("number") rotationY: number = 0;
  @type("string") targetSessionId: string = "";
  @type("boolean") isActive: boolean = false;
  @type("number") velocityX: number = 0;
  @type("number") velocityY: number = 0;
  @type("number") velocityZ: number = 0;
  @type("number") sampleTimeMs: number = 0;
  @type("number") currentHealth: number = 100;
  @type("number") maxHealth: number = 100;
}
