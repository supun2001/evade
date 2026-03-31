import { Schema, type } from "@colyseus/schema";

export class Player extends Schema {
    // Position & Rotation
    @type("number") x: number = 0;
    @type("number") y: number = 0;
    @type("number") z: number = 0;
    @type("number") rotationY: number = 0;

    // Velocity 
    @type("number") velocityX: number = 0;
    @type("number") velocityY: number = 0;
    @type("number") velocityZ: number = 0;

    // Animation States 
    @type("number") animInputX: number = 0;
    @type("number") animInputY: number = 0;
    @type("boolean") isGrounded: boolean = true;
    @type("boolean") isJumping: boolean = false;
    @type("boolean") isInjured: boolean = false;
    @type("boolean") isCrouching: boolean = false;
    @type("boolean") isWallRunning: boolean = false;
    @type("number") wallRunSide: number = 0;
    @type("number") moveInputX: number = 0;
    @type("number") moveInputY: number = 0;
    @type("number") visualYaw: number = 180;

    // Camera Rotation (for looking up/down)
    @type("number") cameraRotationX: number = 0;
    @type("number") cameraRotationY: number = 0;

    // Player Info
    @type("string") sessionId: string = "";
    @type("number") timestamp: number = 0;
    @type("boolean") isReady: boolean = false;

    // Skin
    @type("number") skinIndex: number = 0;
}
