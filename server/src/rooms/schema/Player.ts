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
    @type("string") displayName: string = "";
    @type("number") timestamp: number = 0;
    @type("boolean") isReady: boolean = false;

    // Skin
    @type("number") skinIndex: number = 0;

    // Temporary hit reaction sync
    @type("boolean") isHitReacting: boolean = false;
    @type("number") hitReactionTimeRemaining: number = 0;
    @type("number") hitReactionPitch: number = 0;
    @type("number") hitReactionRoll: number = 0;
    @type("number") hitReactionSeed: number = 0;
    @type("number") hitTriggerId: number = 0;
    @type("number") hitSourceX: number = 0;
    @type("number") hitSourceY: number = 0;
    @type("number") hitSourceZ: number = 0;
    @type("boolean") isCarrying: boolean = false;
    @type("boolean") isBeingCarried: boolean = false;
    @type("string") carriedPlayerSessionId: string = "";
    @type("string") carrierSessionId: string = "";
    @type("boolean") isEliminated: boolean = false;
    @type("number") speedBoostMultiplier: number = 1;
    @type("number") speedBoostTimeRemaining: number = 0;
    @type("number") jumpBoostMultiplier: number = 1;
    @type("number") jumpBoostTimeRemaining: number = 0;
    @type("boolean") isSpectator: boolean = false;
    @type("boolean") isShootingMode: boolean = false;
    @type("number") shotTriggerId: number = 0;
    @type("number") combatHealth: number = 100;
    @type("number") maxCombatHealth: number = 100;
}
