import { Room, Client } from "@colyseus/core";
import { MyRoomState } from "./schema/MyRoomState";
import { Player } from "./schema/Player";
import { NextbotState } from "./schema/NextbotState";

const NEXTBOTS_ENABLED = true;
const MAX_ACTIVE_NEXTBOTS = 5;
const DEFAULT_NEXTBOT_SPAWN_POINTS = [
  { x: 6.45, y: 0, z: -2.38 },
  { x: -6.45, y: 0, z: 2.38 },
  { x: 0, y: 0, z: 7.5 },
];
const DEFAULT_NEXTBOT_IDS = ["nextbot_0", "nextbot_1", "nextbot_2", "nextbot_3", "nextbot_4"];
const DEFAULT_PLAYER_SPAWN_POINTS = [
  { x: 0, y: 0, z: -6 },
  { x: 2, y: 0, z: -6 },
  { x: -2, y: 0, z: -6 },
];
const NEXTBOT_MOVE_SPEED = 9;
const NEXTBOT_STOPPING_DISTANCE = 0.7;
const NEXTBOT_INJURY_DISTANCE = 0.95;
const NEXTBOT_INJURY_COOLDOWN_MS = 1200;
const NEXTBOT_START_GRACE_MS = 3500;
const NEXTBOT_SCAN_INTERVAL_MS = 250;
const NEXTBOT_TARGET_LOCK_MS = 1400;
const NEXTBOT_SWITCH_SCORE_THRESHOLD = 20;
const NEXTBOT_SWITCH_CONFIRM_MS = 300;
const NEXTBOT_UNREACHABLE_TIMEOUT_MS = 1800;
const NEXTBOT_PREDICTION_TIME = 0.28;
const NEXTBOT_MAX_CHASE_RANGE = 70;
const NEXTBOT_ACQUIRE_RANGE = 80;
const NEXTBOT_MAX_VERTICAL_DELTA = 12;
const NEXTBOT_STALE_TARGET_TIMEOUT_MS = 1500;
const NEXTBOT_DISTANCE_SCORE_BASE = 120;
const NEXTBOT_VISIBLE_PROXY_RANGE = 18;
const NEXTBOT_VISIBLE_PROXY_BONUS = 15;
const NEXTBOT_FRONT_BONUS = 10;
const NEXTBOT_CURRENT_TARGET_BONUS = 30;
const NEXTBOT_RECENT_REACHABLE_BONUS = 15;
const NEXTBOT_RECENT_REACHABLE_MS = 1500;
const NEXTBOT_INTERCEPT_BONUS_MAX = 20;
const NEXTBOT_FRONT_ANGLE_THRESHOLD = 85;
const NEXTBOT_PATROL_REACHED_DISTANCE = 1.1;
const NEXTBOT_PATROL_MIN_TRAVEL_DISTANCE = 6;
const NEXTBOT_PATROL_BOUNDS_PADDING = 2;
const NEXTBOT_PATROL_SEPARATION_RADIUS = 6;
const NEXTBOT_PATROL_CANDIDATE_SAMPLES = 40;
const NEXTBOT_PATROL_WAIT_MIN_MS = 1000;
const NEXTBOT_PATROL_WAIT_MAX_MS = 2000;
const NEXTBOT_OBSTACLE_PADDING = 0.7;
const NEXTBOT_OBSTACLE_HEIGHT_PADDING = 1.5;
const NEXTBOT_MAX_ALLOWED_ASCENT = 3;
const NEXTBOT_FLOOR_BLEND_SAMPLE_COUNT = 4;
const NEXTBOT_FLOOR_BLEND_RADIUS = 3.5;
const NEXTBOT_DROP_START_HEIGHT = 0.9;
const NEXTBOT_DROP_GRAVITY = 22;
const NEXTBOT_DROP_LAND_BLEND_HEIGHT = 0.75;
const NEXTBOT_DROP_LAND_SNAP_DISTANCE = 0.015;
const NEXTBOT_DROP_LAND_BLEND_SPEED = 14;
const NEXTBOT_HOP_UPWARD_SPEED = 3.2;
const NEXTBOT_GROUNDED_VERTICAL_SMOOTH_SPEED = 10;
const NEXTBOT_DIAGNOSTIC_LOG_INTERVAL_MS = 5000;
const NEXTBOT_MAX_SIMULATION_DELTA_SECONDS = 0.05;
const NEXTBOT_MAX_MOVE_SUBSTEP_SECONDS = 1 / 60;
const NEXTBOT_LARGE_MOVE_DISTANCE = 0.9;
const NEXTBOT_LARGE_VERTICAL_MOVE_DISTANCE = 0.6;
const PLAYER_REVIVE_DISTANCE = 6;
const PLAYER_REVIVE_SYNC_GRACE_MS = 1000;
const PLAYER_INJURY_SYNC_GRACE_MS = 600;
const PLAYER_MAX_DOWNS_BEFORE_ELIMINATION = 3;
const DEFAULT_INTERMISSION_DURATION_MS = 30_000;
const DEFAULT_ROUND_DURATION_MS = 180_000;
const PLAYER_SPAWN_ROTATION_Y = 180;
const PLAYER_UPDATE_X = 0;
const PLAYER_UPDATE_Y = 1;
const PLAYER_UPDATE_Z = 2;
const PLAYER_UPDATE_ROTATION_Y = 3;
const PLAYER_UPDATE_VELOCITY_X = 4;
const PLAYER_UPDATE_VELOCITY_Y = 5;
const PLAYER_UPDATE_VELOCITY_Z = 6;
const PLAYER_UPDATE_ANIM_INPUT_X = 7;
const PLAYER_UPDATE_ANIM_INPUT_Y = 8;
const PLAYER_UPDATE_IS_GROUNDED = 9;
const PLAYER_UPDATE_IS_JUMPING = 10;
const PLAYER_UPDATE_IS_INJURED = 11;
const PLAYER_UPDATE_IS_CROUCHING = 12;
const PLAYER_UPDATE_IS_WALL_RUNNING = 13;
const PLAYER_UPDATE_WALL_RUN_SIDE = 14;
const PLAYER_UPDATE_MOVE_INPUT_X = 15;
const PLAYER_UPDATE_MOVE_INPUT_Y = 16;
const PLAYER_UPDATE_VISUAL_YAW = 17;
const PLAYER_UPDATE_CAMERA_ROTATION_X = 18;
const PLAYER_UPDATE_CAMERA_ROTATION_Y = 19;
const PLAYER_UPDATE_IS_HIT_REACTING = 20;
const PLAYER_UPDATE_HIT_REACTION_TIME_REMAINING = 21;
const PLAYER_UPDATE_HIT_REACTION_PITCH = 22;
const PLAYER_UPDATE_HIT_REACTION_ROLL = 23;
const PLAYER_UPDATE_HIT_REACTION_SEED = 24;
const PLAYER_UPDATE_SPEED_BOOST_MULTIPLIER = 25;
const PLAYER_UPDATE_SPEED_BOOST_TIME_REMAINING = 26;
const PLAYER_UPDATE_JUMP_BOOST_MULTIPLIER = 27;
const PLAYER_UPDATE_JUMP_BOOST_TIME_REMAINING = 28;

type SpawnPoint = { x: number; y: number; z: number };
type PredictedTargetPosition = { x: number; z: number; distance: number };
type ObstacleRect = { minX: number; maxX: number; minY: number; maxY: number; minZ: number; maxZ: number };
type FloorSample = { x: number; y: number; z: number };
type RoundPhase = "waiting" | "intermission" | "round";
type ScoredTarget = {
  player: Player;
  score: number;
  predicted: PredictedTargetPosition;
  eligible: boolean;
};
type NextbotControllerState = {
  id: string;
  moveSpeed: number;
  spawnIndex: number;
  groundedY: number;
  verticalVelocity: number;
  isAirborne: boolean;
  patrolTargetX: number;
  patrolTargetY: number;
  patrolTargetZ: number;
  patrolWaitUntil: number;
  nextInjuryAt: number;
  currentTargetSessionId: string;
};
type PlayerUpdateMessage = Record<string, unknown> | number[];
type PlayerRoundStats = {
  bestTimeMs: number;
  currentLifeStartMs: number | null;
  downedCount: number;
  revivesDone: number;
  joinOrder: number;
  displayName: string;
};
type RoundPhaseMessage = {
  phase: RoundPhase;
  roundIndex: number;
  timeRemainingMs: number;
  roundDurationMs: number;
  intermissionDurationMs: number;
};
type RoundAnnouncementMessage = {
  title: string;
  subtitle: string;
  durationSeconds: number;
};
type RoundResultEntry = {
  sessionId: string;
  displayName: string;
  bestTimeMs: number;
  downedCount: number;
  revivesDone: number;
  joinOrder: number;
  rank: number;
};
type RoundResultsMessage = {
  roundIndex: number;
  roundDurationMs: number;
  entries: RoundResultEntry[];
};

function readPlayerUpdateNumber(message: PlayerUpdateMessage, index: number, key: string): number {
  if (Array.isArray(message)) {
    const value = message[index];
    return typeof value === "number" ? value : 0;
  }

  const value = message[key];
  return typeof value === "number" ? value : 0;
}

function readPlayerUpdateBoolean(message: PlayerUpdateMessage, index: number, key: string): boolean {
  if (Array.isArray(message)) {
    return readPlayerUpdateNumber(message, index, key) !== 0;
  }

  const value = message[key];
  return value === true || value === 1;
}

function sanitizeDisplayName(value: unknown, fallback: string): string {
  if (typeof value !== "string") {
    return fallback;
  }

  const trimmed = value.trim();
  if (!trimmed) {
    return fallback;
  }

  return trimmed.slice(0, 24);
}

export class MyRoom extends Room<MyRoomState> {
  maxClients = 15;
  state = new MyRoomState();
  private roomCreatedAt = Date.now();
  private playerSafeUntil = new Map<string, number>();
  private nextbotSpawnPoints = DEFAULT_NEXTBOT_SPAWN_POINTS;
  private nextbotPatrolPoints: SpawnPoint[] = [];
  private nextbotObstacles: ObstacleRect[] = [];
  private nextbotFloorSamples: FloorSample[] = [];
  private nextbotIds = DEFAULT_NEXTBOT_IDS;
  private nextbotMoveSpeeds = new Map<string, number>();
  private nextbotControllers: NextbotControllerState[] = [];
  private playerSpawnPoints = DEFAULT_PLAYER_SPAWN_POINTS;
  private nextTargetScanAt = 0;
  private playerRevivedUntil = new Map<string, number>();
  private playerForcedInjuredUntil = new Map<string, number>();
  private currentPhase: RoundPhase = "waiting";
  private phaseEndsAt = 0;
  private roundIndex = 0;
  private roundStats = new Map<string, PlayerRoundStats>();
  private nextJoinOrder = 1;
  private hasStartedMatchFlow = false;
  private latestRoundResultsJson = "";
  private intermissionDurationMs = DEFAULT_INTERMISSION_DURATION_MS;
  private roundDurationMs = DEFAULT_ROUND_DURATION_MS;
  private nextbotDiagnosticWindowStartedAt = 0;
  private nextbotDiagnosticTickCount = 0;
  private nextbotDiagnosticDeltaSum = 0;
  private nextbotDiagnosticMinDelta = Number.POSITIVE_INFINITY;
  private nextbotDiagnosticMaxDelta = 0;
  private nextbotDiagnosticClampedTickCount = 0;
  private nextbotDiagnosticPlanarMoveSum = 0;
  private nextbotDiagnosticPlanarMoveMax = 0;
  private nextbotDiagnosticVerticalMoveSum = 0;
  private nextbotDiagnosticVerticalMoveMax = 0;
  private nextbotDiagnosticLargeMoveCount = 0;

  onCreate(options: any) {
    this.roomCreatedAt = Date.now();
    this.nextbotDiagnosticWindowStartedAt = this.roomCreatedAt;
    this.nextbotSpawnPoints = this.resolveNextbotSpawnPoints(options);
    this.nextbotPatrolPoints = this.resolveNextbotPatrolPoints(options);
    this.nextbotObstacles = this.resolveNextbotObstacles(options);
    this.nextbotFloorSamples = this.resolveNextbotFloorSamples(options);
    this.nextbotIds = this.resolveNextbotIds(options);
    this.nextbotMoveSpeeds = this.resolveNextbotMoveSpeeds(options, this.nextbotIds);
    this.playerSpawnPoints = this.resolvePlayerSpawnPoints(options);
    this.intermissionDurationMs = this.resolvePositiveDurationMs(options?.intermissionDurationMs, DEFAULT_INTERMISSION_DURATION_MS);
    this.roundDurationMs = this.resolvePositiveDurationMs(options?.roundDurationMs, DEFAULT_ROUND_DURATION_MS);
    this.initializeNextbots();
    this.setPatchRate(1000 / 60);

    //Room ID
    this.roomId = Math.floor(1000 + Math.random() * 9000).toString();
    console.log("Room created!", options, "ID:", this.roomId);

    //Handle player movement
    this.onMessage("playerUpdate", (client, message) => {
      const player = this.state.players.get(client.sessionId);
      if (!player) return;
      const now = Date.now();
      const revivedUntil = this.playerRevivedUntil.get(client.sessionId) ?? 0;
      const forcedInjuredUntil = this.playerForcedInjuredUntil.get(client.sessionId) ?? 0;
      const ignoreStaleInjuredState = revivedUntil > now;
      const keepAuthoritativeInjuredState = forcedInjuredUntil > now;
      const playerUpdate = message as PlayerUpdateMessage;
      const wasInjured = player.isInjured;

      // Camera is informational, so keep it in sync every tick.
      player.cameraRotationX = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_CAMERA_ROTATION_X, "cameraRotationX");
      player.cameraRotationY = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_CAMERA_ROTATION_Y, "cameraRotationY");
      player.timestamp = now;

      if (player.isEliminated) {
        player.velocityX = 0;
        player.velocityY = 0;
        player.velocityZ = 0;
        player.animInputX = 0;
        player.animInputY = 0;
        player.moveInputX = 0;
        player.moveInputY = 0;
        player.isJumping = false;
        player.isInjured = true;
        player.isHitReacting = false;
        player.hitReactionTimeRemaining = 0;
        player.hitReactionPitch = 0;
        player.hitReactionRoll = 0;
        player.hitReactionSeed = 0;
        player.speedBoostMultiplier = 1;
        player.speedBoostTimeRemaining = 0;
        player.jumpBoostMultiplier = 1;
        player.jumpBoostTimeRemaining = 0;
        player.isCrouching = false;
        player.isWallRunning = false;
        player.wallRunSide = 0;
        return;
      }

      if (this.currentPhase !== "round") {
        player.isInjured = false;
        player.isHitReacting = false;
        player.hitReactionTimeRemaining = 0;
        player.hitReactionPitch = 0;
        player.hitReactionRoll = 0;
        player.hitReactionSeed = 0;
        player.speedBoostMultiplier = 1;
        player.speedBoostTimeRemaining = 0;
        player.jumpBoostMultiplier = 1;
        player.jumpBoostTimeRemaining = 0;
      }

      // Position & Rotation
      player.x = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_X, "x");
      player.y = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_Y, "y");
      player.z = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_Z, "z");
      player.rotationY = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_ROTATION_Y, "rotationY");

      // Velocity
      player.velocityX = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_VELOCITY_X, "velocityX");
      player.velocityY = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_VELOCITY_Y, "velocityY");
      player.velocityZ = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_VELOCITY_Z, "velocityZ");

      // Animation States
      player.animInputX = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_ANIM_INPUT_X, "animInputX");
      player.animInputY = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_ANIM_INPUT_Y, "animInputY");
      player.isGrounded = readPlayerUpdateBoolean(playerUpdate, PLAYER_UPDATE_IS_GROUNDED, "isGrounded");
      player.isJumping = readPlayerUpdateBoolean(playerUpdate, PLAYER_UPDATE_IS_JUMPING, "isJumping");
      player.isInjured = keepAuthoritativeInjuredState
        ? true
        : (ignoreStaleInjuredState ? false : readPlayerUpdateBoolean(playerUpdate, PLAYER_UPDATE_IS_INJURED, "isInjured"));
      player.isCrouching = readPlayerUpdateBoolean(playerUpdate, PLAYER_UPDATE_IS_CROUCHING, "isCrouching");
      player.isWallRunning = readPlayerUpdateBoolean(playerUpdate, PLAYER_UPDATE_IS_WALL_RUNNING, "isWallRunning");
      player.wallRunSide = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_WALL_RUN_SIDE, "wallRunSide");
      player.moveInputX = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_MOVE_INPUT_X, "moveInputX");
      player.moveInputY = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_MOVE_INPUT_Y, "moveInputY");
      player.visualYaw = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_VISUAL_YAW, "visualYaw");
      player.isHitReacting = ignoreStaleInjuredState ? false : readPlayerUpdateBoolean(playerUpdate, PLAYER_UPDATE_IS_HIT_REACTING, "isHitReacting");
      player.hitReactionTimeRemaining = ignoreStaleInjuredState ? 0 : readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_HIT_REACTION_TIME_REMAINING, "hitReactionTimeRemaining");
      player.hitReactionPitch = ignoreStaleInjuredState ? 0 : readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_HIT_REACTION_PITCH, "hitReactionPitch");
      player.hitReactionRoll = ignoreStaleInjuredState ? 0 : readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_HIT_REACTION_ROLL, "hitReactionRoll");
      player.hitReactionSeed = ignoreStaleInjuredState ? 0 : readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_HIT_REACTION_SEED, "hitReactionSeed");
      player.speedBoostMultiplier = Math.max(1, readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_SPEED_BOOST_MULTIPLIER, "speedBoostMultiplier"));
      player.speedBoostTimeRemaining = Math.max(0, readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_SPEED_BOOST_TIME_REMAINING, "speedBoostTimeRemaining"));
      player.jumpBoostMultiplier = Math.max(1, readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_JUMP_BOOST_MULTIPLIER, "jumpBoostMultiplier"));
      player.jumpBoostTimeRemaining = Math.max(0, readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_JUMP_BOOST_TIME_REMAINING, "jumpBoostTimeRemaining"));

      if (keepAuthoritativeInjuredState) {
        player.isHitReacting = false;
        player.hitReactionTimeRemaining = 0;
        player.hitReactionPitch = 0;
        player.hitReactionRoll = 0;
        player.hitReactionSeed = 0;
        player.speedBoostMultiplier = 1;
        player.speedBoostTimeRemaining = 0;
        player.jumpBoostMultiplier = 1;
        player.jumpBoostTimeRemaining = 0;
      }

      if (player.isBeingCarried) {
        player.velocityX = 0;
        player.velocityY = 0;
        player.velocityZ = 0;
        player.animInputX = 0;
        player.animInputY = 0;
        player.moveInputX = 0;
        player.moveInputY = 0;
        player.isJumping = false;
        player.isCrouching = false;
        player.isWallRunning = false;
        player.wallRunSide = 0;
      }

      if (!wasInjured && player.isInjured) {
        this.recordPlayerDowned(client.sessionId, now);
      }
    });

    this.onMessage("playerReady", (client, isReady) => {
      const player = this.state.players.get(client.sessionId);
      if (player) {
        if (isReady) {
          player.isSpectator = false;
        }
        player.isReady = isReady;

        // Check if all players are ready
        let allReady = true;
        this.state.players.forEach((p) => {
          if (!p.isReady) allReady = false;
        });

        this.tryStartRoundLoop();
      }
    });

    this.onMessage("playerSpectating", (client, isSpectating) => {
      const player = this.state.players.get(client.sessionId);
      if (!player) {
        return;
      }

      player.isSpectator = !!isSpectating;
      if (player.isSpectator) {
        player.isReady = false;
        player.isInjured = false;
        player.isEliminated = false;
        player.isHitReacting = false;
        player.hitReactionTimeRemaining = 0;
        player.hitReactionPitch = 0;
        player.hitReactionRoll = 0;
        player.hitReactionSeed = 0;
        player.isCarrying = false;
        player.isBeingCarried = false;
        player.carriedPlayerSessionId = "";
        player.carrierSessionId = "";
        player.velocityX = 0;
        player.velocityY = 0;
        player.velocityZ = 0;
      }
    });

    this.onMessage("revivePlayer", (client, message) => {
      const reviver = this.state.players.get(client.sessionId);
      const targetSessionId = typeof message?.targetSessionId === "string" ? message.targetSessionId : "";
      const target = this.state.players.get(targetSessionId);

      if (!reviver || !target || target.sessionId === client.sessionId) {
        return;
      }

      if (reviver.isInjured || reviver.isHitReacting || reviver.isEliminated || !target.isInjured || target.isEliminated) {
        return;
      }

      const distance = Math.hypot(target.x - reviver.x, target.z - reviver.z);
      if (distance > PLAYER_REVIVE_DISTANCE) {
        return;
      }

      this.clearCarryStateForPlayer(target.sessionId);
      target.isInjured = false;
      target.isEliminated = false;
      target.isHitReacting = false;
      target.hitReactionTimeRemaining = 0;
      target.hitReactionPitch = 0;
      target.hitReactionRoll = 0;
      target.hitReactionSeed = 0;
      this.playerRevivedUntil.set(targetSessionId, Date.now() + PLAYER_REVIVE_SYNC_GRACE_MS);
      this.playerForcedInjuredUntil.delete(targetSessionId);
      this.recordPlayerRevived(targetSessionId, Date.now());
      const reviverStats = this.roundStats.get(client.sessionId);
      if (reviverStats != null && this.currentPhase === "round") {
        reviverStats.revivesDone += 1;
      }

      const targetClient = this.clients.find((roomClient) => roomClient.sessionId === targetSessionId);
      targetClient?.send("playerRevived", "revived");
    });

    this.onMessage("carryPlayer", (client, message) => {
      const carrier = this.state.players.get(client.sessionId);
      const targetSessionId = typeof message?.targetSessionId === "string" ? message.targetSessionId : "";
      const target = this.state.players.get(targetSessionId);

      if (!carrier || !target || carrier.sessionId === target.sessionId) {
        return;
      }

      if (carrier.isInjured || carrier.isHitReacting || carrier.isEliminated || target.isHitReacting || !target.isInjured || target.isEliminated) {
        return;
      }

      const distance = Math.hypot(target.x - carrier.x, target.z - carrier.z);
      if (distance > PLAYER_REVIVE_DISTANCE) {
        return;
      }

      if (carrier.isCarrying && carrier.carriedPlayerSessionId === targetSessionId) {
        this.clearCarryStateForPlayer(carrier.sessionId);
        return;
      }

      if (carrier.isCarrying || carrier.isBeingCarried || target.isCarrying || target.isBeingCarried) {
        return;
      }

      carrier.isCarrying = true;
      carrier.carriedPlayerSessionId = target.sessionId;
      target.isBeingCarried = true;
      target.carrierSessionId = carrier.sessionId;
      target.isHitReacting = false;
      target.hitReactionTimeRemaining = 0;
      target.hitReactionPitch = 0;
      target.hitReactionRoll = 0;
      target.hitReactionSeed = 0;
    });

    //Set update rate (60 times per second)
    this.setSimulationInterval((deltaTime) =>
      this.update(deltaTime), 1000 / 60
    );

    this.onMessage("setSkin", (client, skinIndex) => {
      const player = this.state.players.get(client.sessionId);
      if (player) {
        player.skinIndex = skinIndex;
      }
    });

    this.onMessage("syncNextbotConfigs", (_client, payload) => {
      this.applyNextbotConfigOverrides(payload);
    });
  }

  onJoin(client: Client, options: any) {
    console.log(client.sessionId, "joined!");

    this.applyNextbotConfigOverrides(options);

    //Create a new player
    const player = new Player();
    player.sessionId = client.sessionId;
    player.displayName = sanitizeDisplayName(options?.username, `Player ${this.nextJoinOrder}`);

    player.isReady = false;
    player.isSpectator = false;

    const joinOrder = this.nextJoinOrder;
    const spawnPosition = this.getPlayerSpawnPosition(client.sessionId);
    player.x = spawnPosition.x;
    player.y = spawnPosition.y;
    player.z = spawnPosition.z;
    player.rotationY = PLAYER_SPAWN_ROTATION_Y;
    player.isGrounded = true;
    player.isJumping = false;
    player.isInjured = false;
    player.isCrouching = false;
    player.isWallRunning = false;
    player.wallRunSide = 0;
    player.moveInputX = 0;
    player.moveInputY = 0;
    player.visualYaw = PLAYER_SPAWN_ROTATION_Y;
    player.isHitReacting = false;
    player.hitReactionTimeRemaining = 0;
    player.hitReactionPitch = 0;
    player.hitReactionRoll = 0;
    player.hitReactionSeed = 0;
    player.hitTriggerId = 0;
    player.hitSourceX = 0;
    player.hitSourceY = 0;
    player.hitSourceZ = 0;
    player.isCarrying = false;
    player.isBeingCarried = false;
    player.carriedPlayerSessionId = "";
    player.carrierSessionId = "";
    player.speedBoostMultiplier = 1;
    player.speedBoostTimeRemaining = 0;
    player.jumpBoostMultiplier = 1;
    player.jumpBoostTimeRemaining = 0;

    //Add player to state
    this.state.players.set(client.sessionId, player);
    this.playerSafeUntil.set(client.sessionId, Date.now() + NEXTBOT_START_GRACE_MS);
    this.roundStats.set(client.sessionId, {
      bestTimeMs: 0,
      currentLifeStartMs: this.currentPhase === "round" ? Date.now() : null,
      downedCount: 0,
      revivesDone: 0,
      joinOrder,
      displayName: player.displayName,
    });
    this.nextJoinOrder += 1;

    this.sendRoundPhaseToClient(client);
    if (this.latestRoundResultsJson.length > 0 && this.currentPhase === "intermission") {
      client.send("roundResults", this.latestRoundResultsJson);
    }

    this.tryStartRoundLoop();
  }

  onLeave(client: Client, consented: boolean) {
    console.log(client.sessionId, "left!");

    //Remove player from state
    this.clearCarryStateForPlayer(client.sessionId);
    this.state.players.delete(client.sessionId);
    this.playerSafeUntil.delete(client.sessionId);
    this.playerRevivedUntil.delete(client.sessionId);
    this.playerForcedInjuredUntil.delete(client.sessionId);
    this.roundStats.delete(client.sessionId);

    // If no players left, reset game state
    if (this.state.players.size === 0) {
      this.state.isGameStarted = false;
      this.currentPhase = "waiting";
      this.phaseEndsAt = 0;
      this.roundIndex = 0;
      this.hasStartedMatchFlow = false;
      this.latestRoundResultsJson = "";
      this.clearAllNextbotTargetingState();
    }

    // Unlock the room for late commers 
    this.unlock();
  }

  onDispose() {
    console.log("room", this.roomId, "disposing...");
  }

  private clearCarryStateForPlayer(sessionId: string) {
    const player = this.state.players.get(sessionId);
    if (!player) {
      return;
    }

    if (player.isCarrying && player.carriedPlayerSessionId) {
      const carriedPlayer = this.state.players.get(player.carriedPlayerSessionId);
      if (carriedPlayer) {
        carriedPlayer.isBeingCarried = false;
        carriedPlayer.carrierSessionId = "";
      }
    }

    if (player.isBeingCarried && player.carrierSessionId) {
      const carrier = this.state.players.get(player.carrierSessionId);
      if (carrier) {
        carrier.isCarrying = false;
        carrier.carriedPlayerSessionId = "";
      }
    }

    player.isCarrying = false;
    player.isBeingCarried = false;
    player.carriedPlayerSessionId = "";
    player.carrierSessionId = "";
    player.isEliminated = false;
    player.speedBoostMultiplier = 1;
    player.speedBoostTimeRemaining = 0;
    player.jumpBoostMultiplier = 1;
    player.jumpBoostTimeRemaining = 0;
  }

  update(deltaTime: number) {
    const now = Date.now();
    this.recordNextbotTickDiagnostic(now, deltaTime);
    this.updateRoundFlow(now);
    if (!NEXTBOTS_ENABLED) {
      this.setAllNextbotsActive(false);
      this.clearAllNextbotTargetingState();
      return;
    }

    const shouldActivateNextbots = this.state.isGameStarted && this.getReadyPlayerCount() > 0;
    this.setAllNextbotsActive(shouldActivateNextbots);

    if (!shouldActivateNextbots) {
      this.clearAllNextbotTargetingState();
      return;
    }

    if (now >= this.nextTargetScanAt) {
      this.nextTargetScanAt = now + NEXTBOT_SCAN_INTERVAL_MS;
      this.assignNextbotTargets(now);
    }

    const deltaSeconds = deltaTime / 1000;
    for (let index = 0; index < this.nextbotControllers.length; index++) {
      const controller = this.nextbotControllers[index];
      const nextbot = this.getNextbotState(index);
      if (nextbot == null) {
        continue;
      }

      const target = controller.currentTargetSessionId
        ? this.state.players.get(controller.currentTargetSessionId)
        : undefined;

      if (target != null && this.isScoreEligibleTarget(target, now, nextbot)) {
        nextbot.targetSessionId = target.sessionId;
        const predictedTarget = this.getPredictedTargetPosition(target, nextbot);
        const groundY = this.getGroundYForPosition(predictedTarget.x, predictedTarget.z, controller.groundedY);
        this.moveNextbotTowardsPosition(controller, nextbot, predictedTarget, deltaSeconds, controller.moveSpeed, groundY);
        this.tryInjurePlayer(controller, nextbot, target, now);
        continue;
      }

      if (controller.currentTargetSessionId) {
        this.setNextPatrolTargetFromCurrentPosition(controller, nextbot);
      }

      controller.currentTargetSessionId = "";
      nextbot.targetSessionId = "";
      this.moveNextbotOnPatrol(controller, nextbot, deltaSeconds);
    }
  }

  private initializeNextbots() {
    this.state.nextbots.clear();
    this.nextbotControllers = [];

    for (let index = 0; index < this.nextbotIds.length; index++) {
      const botId = this.getNextbotId(index);
      const spawnPoint = this.getNextbotSpawnPoint(index);
      const nextbotState = new NextbotState();
      nextbotState.x = spawnPoint.x;
      nextbotState.y = spawnPoint.y;
      nextbotState.z = spawnPoint.z;
      nextbotState.rotationY = 0;
      nextbotState.targetSessionId = "";
      nextbotState.isActive = false;
      nextbotState.velocityX = 0;
      nextbotState.velocityY = 0;
      nextbotState.velocityZ = 0;
      nextbotState.sampleTimeMs = 0;
      this.state.nextbots.set(botId, nextbotState);
      this.nextbotControllers.push({
        id: botId,
        moveSpeed: this.getConfiguredNextbotMoveSpeed(botId),
        spawnIndex: index,
        groundedY: spawnPoint.y,
        verticalVelocity: 0,
        isAirborne: false,
        patrolTargetX: spawnPoint.x,
        patrolTargetY: spawnPoint.y,
        patrolTargetZ: spawnPoint.z,
        patrolWaitUntil: 0,
        nextInjuryAt: 0,
        currentTargetSessionId: "",
      });
    }

    this.assignRandomPatrolTargets();
  }

  private resetNextbotsToSpawnPoints() {
    for (let index = 0; index < this.nextbotControllers.length; index++) {
      const controller = this.nextbotControllers[index];
      const nextbot = this.getNextbotState(index);
      if (nextbot == null) {
        continue;
      }

      const spawnPoint = this.getNextbotSpawnPoint(controller.spawnIndex);
      nextbot.x = spawnPoint.x;
      nextbot.y = spawnPoint.y;
      nextbot.z = spawnPoint.z;
      nextbot.rotationY = 0;
      nextbot.targetSessionId = "";
      nextbot.isActive = false;
      nextbot.velocityX = 0;
      nextbot.velocityY = 0;
      nextbot.velocityZ = 0;
      nextbot.sampleTimeMs = 0;
      controller.currentTargetSessionId = "";
      controller.nextInjuryAt = 0;
      controller.groundedY = spawnPoint.y;
      controller.verticalVelocity = 0;
      controller.isAirborne = false;
      controller.patrolTargetX = spawnPoint.x;
      controller.patrolTargetY = spawnPoint.y;
      controller.patrolTargetZ = spawnPoint.z;
      controller.patrolWaitUntil = 0;
    }

    this.assignRandomPatrolTargets();
  }

  private setAllNextbotsActive(isActive: boolean) {
    for (let index = 0; index < this.nextbotControllers.length; index++) {
      const nextbot = this.getNextbotState(index);
      if (nextbot != null) {
        nextbot.isActive = isActive;
        if (!isActive) {
          nextbot.targetSessionId = "";
          nextbot.velocityX = 0;
          nextbot.velocityY = 0;
          nextbot.velocityZ = 0;
          nextbot.sampleTimeMs = this.getRoomElapsedTimeMs();
        }
      }
    }
  }

  private assignNextbotTargets(now: number) {
    const claimedTargets = new Set<string>();

    for (let index = 0; index < this.nextbotControllers.length; index++) {
      const controller = this.nextbotControllers[index];
      const nextbot = this.getNextbotState(index);
      const currentTarget = controller.currentTargetSessionId
        ? this.state.players.get(controller.currentTargetSessionId)
        : undefined;

      if (nextbot != null
        && currentTarget != null
        && !claimedTargets.has(currentTarget.sessionId)
        && this.isScoreEligibleTarget(currentTarget, now, nextbot)) {
        claimedTargets.add(currentTarget.sessionId);
        nextbot.targetSessionId = currentTarget.sessionId;
        continue;
      }

      controller.currentTargetSessionId = "";
      if (nextbot != null) {
        nextbot.targetSessionId = "";
      }
    }

    for (let index = 0; index < this.nextbotControllers.length; index++) {
      const controller = this.nextbotControllers[index];
      const nextbot = this.getNextbotState(index);
      if (nextbot == null || controller.currentTargetSessionId) {
        continue;
      }

      const bestCandidate = this.findBestTargetForNextbot(nextbot, now, claimedTargets);
      if (bestCandidate == null) {
        continue;
      }

      controller.currentTargetSessionId = bestCandidate.player.sessionId;
      nextbot.targetSessionId = bestCandidate.player.sessionId;
      claimedTargets.add(bestCandidate.player.sessionId);
    }
  }

  private findBestTargetForNextbot(nextbot: NextbotState, now: number, claimedTargets: Set<string>) {
    let bestTarget: ScoredTarget | undefined;

    this.state.players.forEach((player) => {
      if (claimedTargets.has(player.sessionId)) {
        return;
      }

      const scoredTarget = this.buildScoredTarget(player, now, nextbot, false);
      if (scoredTarget == null
        || !scoredTarget.eligible
        || scoredTarget.predicted.distance > NEXTBOT_ACQUIRE_RANGE) {
        return;
      }

      if (bestTarget == null || scoredTarget.score > bestTarget.score) {
        bestTarget = scoredTarget;
      }
    });

    return bestTarget;
  }

  private moveNextbotOnPatrol(controller: NextbotControllerState, nextbot: NextbotState, deltaSeconds: number) {
    const patrolTarget = {
      x: controller.patrolTargetX,
      y: controller.patrolTargetY,
      z: controller.patrolTargetZ,
    };
    const distance = Math.hypot(patrolTarget.x - nextbot.x, patrolTarget.z - nextbot.z);
    if (distance <= NEXTBOT_PATROL_REACHED_DISTANCE) {
      if (controller.patrolWaitUntil <= 0) {
        controller.patrolWaitUntil = Date.now() + this.getRandomPatrolWaitMs();
        return;
      }

      if (Date.now() < controller.patrolWaitUntil) {
        return;
      }

      this.setNextPatrolTargetFromCurrentPosition(controller, nextbot);
    }

    const nextPatrolTarget = {
      x: controller.patrolTargetX,
      y: controller.patrolTargetY,
      z: controller.patrolTargetZ,
    };
    this.moveNextbotTowardsPosition(controller, nextbot, {
      x: nextPatrolTarget.x,
      z: nextPatrolTarget.z,
      distance: Math.hypot(nextPatrolTarget.x - nextbot.x, nextPatrolTarget.z - nextbot.z),
    }, deltaSeconds, controller.moveSpeed, this.getGroundYForPosition(nextPatrolTarget.x, nextPatrolTarget.z, controller.groundedY));
  }

  private moveNextbotTowardsPosition(
    controller: NextbotControllerState,
    nextbot: NextbotState,
    target: PredictedTargetPosition,
    deltaSeconds: number,
    moveSpeed: number = NEXTBOT_MOVE_SPEED,
    targetY?: number,
  ) {
    const previousX = nextbot.x;
    const previousY = nextbot.y;
    const previousZ = nextbot.z;
    const simulatedDeltaSeconds = Math.min(Math.max(deltaSeconds, 0), NEXTBOT_MAX_SIMULATION_DELTA_SECONDS);
    const effectiveMoveSpeed = Number.isFinite(moveSpeed) && moveSpeed > 0 ? moveSpeed : NEXTBOT_MOVE_SPEED;
    if (deltaSeconds - simulatedDeltaSeconds > 0.0001) {
      this.nextbotDiagnosticClampedTickCount += 1;
    }

    const substepCount = Math.max(1, Math.ceil(simulatedDeltaSeconds / NEXTBOT_MAX_MOVE_SUBSTEP_SECONDS));
    const substepDeltaSeconds = simulatedDeltaSeconds / substepCount;
    for (let stepIndex = 0; stepIndex < substepCount; stepIndex++) {
      this.moveNextbotTowardsPositionStep(
        controller,
        nextbot,
        target,
        substepDeltaSeconds,
        effectiveMoveSpeed,
        targetY,
      );
    }

    this.recordNextbotMovementDiagnostic(previousX, previousY, previousZ, nextbot.x, nextbot.y, nextbot.z);
    const safeDeltaSeconds = Math.max(0.0001, simulatedDeltaSeconds);
    nextbot.velocityX = (nextbot.x - previousX) / safeDeltaSeconds;
    nextbot.velocityY = (nextbot.y - previousY) / safeDeltaSeconds;
    nextbot.velocityZ = (nextbot.z - previousZ) / safeDeltaSeconds;
    nextbot.sampleTimeMs = this.getRoomElapsedTimeMs();
  }

  private moveNextbotTowardsPositionStep(
    controller: NextbotControllerState,
    nextbot: NextbotState,
    target: PredictedTargetPosition,
    deltaSeconds: number,
    moveSpeed: number,
    targetY?: number,
  ) {
    const dx = target.x - nextbot.x;
    const dz = target.z - nextbot.z;
    const distance = Math.hypot(dx, dz);

    if (distance <= 0.0001) {
      this.moveNextbotVerticallyTowardsTarget(controller, nextbot, targetY, deltaSeconds, moveSpeed);
      return;
    }

    nextbot.rotationY = Math.atan2(dx, dz) * (180 / Math.PI);

    if (distance <= NEXTBOT_STOPPING_DISTANCE) {
      this.moveNextbotVerticallyTowardsTarget(controller, nextbot, targetY, deltaSeconds, moveSpeed);
      return;
    }

    const moveDistance = Math.min(distance - NEXTBOT_STOPPING_DISTANCE, moveSpeed * deltaSeconds);
    const desiredMoveX = (dx / distance) * moveDistance;
    const desiredMoveZ = (dz / distance) * moveDistance;
    const resolvedMove = this.resolveNextbotObstacleAwareMove(nextbot, target, desiredMoveX, desiredMoveZ, targetY);
    nextbot.x += resolvedMove.x;
    nextbot.z += resolvedMove.z;
    if (Math.hypot(resolvedMove.x, resolvedMove.z) > 0.0001) {
      nextbot.rotationY = Math.atan2(resolvedMove.x, resolvedMove.z) * (180 / Math.PI);
    }

    this.moveNextbotVerticallyTowardsTarget(
      controller,
      nextbot,
      this.getGroundYForPosition(nextbot.x, nextbot.z, targetY ?? nextbot.y),
      deltaSeconds,
      moveSpeed);
  }

  private getRoomElapsedTimeMs() {
    return Date.now() - this.roomCreatedAt;
  }

  private recordNextbotTickDiagnostic(now: number, deltaTime: number) {
    if (this.nextbotDiagnosticWindowStartedAt <= 0) {
      this.nextbotDiagnosticWindowStartedAt = now;
    }

    this.nextbotDiagnosticTickCount += 1;
    this.nextbotDiagnosticDeltaSum += deltaTime;
    this.nextbotDiagnosticMinDelta = Math.min(this.nextbotDiagnosticMinDelta, deltaTime);
    this.nextbotDiagnosticMaxDelta = Math.max(this.nextbotDiagnosticMaxDelta, deltaTime);

    const elapsed = now - this.nextbotDiagnosticWindowStartedAt;
    if (elapsed < NEXTBOT_DIAGNOSTIC_LOG_INTERVAL_MS) {
      return;
    }

    const averageDelta = this.nextbotDiagnosticTickCount > 0
      ? this.nextbotDiagnosticDeltaSum / this.nextbotDiagnosticTickCount
      : 0;
    const averagePlanarMove = this.nextbotDiagnosticTickCount > 0
      ? this.nextbotDiagnosticPlanarMoveSum / this.nextbotDiagnosticTickCount
      : 0;
    const averageVerticalMove = this.nextbotDiagnosticTickCount > 0
      ? this.nextbotDiagnosticVerticalMoveSum / this.nextbotDiagnosticTickCount
      : 0;
    console.log(
      `[NextbotDiag][Server] room=${this.roomId} ticks=${this.nextbotDiagnosticTickCount} avgDelta=${averageDelta.toFixed(2)}ms minDelta=${this.nextbotDiagnosticMinDelta.toFixed(2)}ms maxDelta=${this.nextbotDiagnosticMaxDelta.toFixed(2)}ms clampedTicks=${this.nextbotDiagnosticClampedTickCount} planarAvg=${averagePlanarMove.toFixed(3)} planarMax=${this.nextbotDiagnosticPlanarMoveMax.toFixed(3)} verticalAvg=${averageVerticalMove.toFixed(3)} verticalMax=${this.nextbotDiagnosticVerticalMoveMax.toFixed(3)} largeMoves=${this.nextbotDiagnosticLargeMoveCount} players=${this.clients.length}`
    );

    this.nextbotDiagnosticWindowStartedAt = now;
    this.nextbotDiagnosticTickCount = 0;
    this.nextbotDiagnosticDeltaSum = 0;
    this.nextbotDiagnosticMinDelta = Number.POSITIVE_INFINITY;
    this.nextbotDiagnosticMaxDelta = 0;
    this.nextbotDiagnosticClampedTickCount = 0;
    this.nextbotDiagnosticPlanarMoveSum = 0;
    this.nextbotDiagnosticPlanarMoveMax = 0;
    this.nextbotDiagnosticVerticalMoveSum = 0;
    this.nextbotDiagnosticVerticalMoveMax = 0;
    this.nextbotDiagnosticLargeMoveCount = 0;
  }

  private recordNextbotMovementDiagnostic(
    previousX: number,
    previousY: number,
    previousZ: number,
    nextX: number,
    nextY: number,
    nextZ: number,
  ) {
    const planarMoveDistance = Math.hypot(nextX - previousX, nextZ - previousZ);
    const verticalMoveDistance = Math.abs(nextY - previousY);
    this.nextbotDiagnosticPlanarMoveSum += planarMoveDistance;
    this.nextbotDiagnosticPlanarMoveMax = Math.max(this.nextbotDiagnosticPlanarMoveMax, planarMoveDistance);
    this.nextbotDiagnosticVerticalMoveSum += verticalMoveDistance;
    this.nextbotDiagnosticVerticalMoveMax = Math.max(this.nextbotDiagnosticVerticalMoveMax, verticalMoveDistance);
    if (planarMoveDistance >= NEXTBOT_LARGE_MOVE_DISTANCE || verticalMoveDistance >= NEXTBOT_LARGE_VERTICAL_MOVE_DISTANCE) {
      this.nextbotDiagnosticLargeMoveCount += 1;
    }
  }

  private resolveNextbotObstacleAwareMove(
    nextbot: NextbotState,
    target: PredictedTargetPosition,
    desiredMoveX: number,
    desiredMoveZ: number,
    targetY?: number,
  ) {
    if (this.nextbotObstacles.length === 0) {
      return { x: desiredMoveX, z: desiredMoveZ };
    }

    const currentX = nextbot.x;
    const currentZ = nextbot.z;
    const desiredDistance = Math.hypot(desiredMoveX, desiredMoveZ);
    if (desiredDistance <= 0.0001) {
      return { x: 0, z: 0 };
    }

    const candidates = [
      { x: desiredMoveX, z: desiredMoveZ },
      { x: desiredMoveX, z: 0 },
      { x: 0, z: desiredMoveZ },
    ];

    const tangentX = -desiredMoveZ / desiredDistance * Math.max(Math.abs(desiredMoveX), Math.abs(desiredMoveZ));
    const tangentZ = desiredMoveX / desiredDistance * Math.max(Math.abs(desiredMoveX), Math.abs(desiredMoveZ));
    candidates.push({ x: tangentX, z: tangentZ });
    candidates.push({ x: -tangentX, z: -tangentZ });

    let bestMove = { x: 0, z: 0 };
    let bestScore = Number.POSITIVE_INFINITY;

    for (const candidate of candidates) {
      const nextX = currentX + candidate.x;
      const nextZ = currentZ + candidate.z;
      if (this.wouldNextbotMoveHitObstacle(currentX, currentZ, nextX, nextZ, nextbot.y, targetY)) {
        continue;
      }

      const remainingDistance = Math.hypot(target.x - nextX, target.z - nextZ);
      const movementPenalty = Math.hypot(candidate.x, candidate.z) * -0.05;
      const score = remainingDistance + movementPenalty;
      if (score < bestScore) {
        bestScore = score;
        bestMove = candidate;
      }
    }

    return bestMove;
  }

  private moveNextbotVerticallyTowardsTarget(
    controller: NextbotControllerState,
    nextbot: NextbotState,
    targetY: number | undefined,
    deltaSeconds: number,
    moveSpeed: number,
  ) {
    if (targetY != null && Number.isFinite(targetY)) {
      controller.groundedY = targetY;
      const verticalDelta = targetY - nextbot.y;
      if (verticalDelta > NEXTBOT_MAX_ALLOWED_ASCENT) {
        return;
      }

      if (verticalDelta <= -NEXTBOT_DROP_START_HEIGHT && !controller.isAirborne) {
        controller.isAirborne = true;
        controller.verticalVelocity = NEXTBOT_HOP_UPWARD_SPEED;
      }

      if (controller.isAirborne) {
        controller.verticalVelocity -= NEXTBOT_DROP_GRAVITY * deltaSeconds;
        nextbot.y += controller.verticalVelocity * deltaSeconds;

        const landingDelta = nextbot.y - targetY;
        if (controller.verticalVelocity <= 0 && landingDelta <= NEXTBOT_DROP_LAND_BLEND_HEIGHT) {
          const landingBlend = 1 - Math.exp(-NEXTBOT_DROP_LAND_BLEND_SPEED * deltaSeconds);
          nextbot.y += (targetY - nextbot.y) * landingBlend;
        }

        if (nextbot.y <= targetY + NEXTBOT_DROP_LAND_SNAP_DISTANCE && controller.verticalVelocity <= 0) {
          controller.verticalVelocity = 0;
          controller.isAirborne = false;
          if (Math.abs(nextbot.y - targetY) <= NEXTBOT_DROP_LAND_SNAP_DISTANCE) {
            nextbot.y = targetY;
          }
        }

        return;
      }

      const maxVerticalStep = moveSpeed * deltaSeconds;
      if (verticalDelta < 0 && Math.abs(verticalDelta) <= NEXTBOT_DROP_LAND_BLEND_HEIGHT) {
        const groundedBlend = 1 - Math.exp(-NEXTBOT_GROUNDED_VERTICAL_SMOOTH_SPEED * deltaSeconds);
        nextbot.y += (targetY - nextbot.y) * groundedBlend;
      } else if (Math.abs(verticalDelta) <= maxVerticalStep) {
        nextbot.y = targetY;
      } else {
        nextbot.y += Math.sign(verticalDelta) * maxVerticalStep;
      }
    }
  }

  private tryInjurePlayer(controller: NextbotControllerState, nextbot: NextbotState, target: Player, now: number) {
    const dx = target.x - nextbot.x;
    const dz = target.z - nextbot.z;
    const distance = Math.hypot(dx, dz);

    if (distance > NEXTBOT_INJURY_DISTANCE || now < controller.nextInjuryAt || target.isInjured || target.isHitReacting || target.isEliminated) {
      return;
    }

    controller.nextInjuryAt = now + NEXTBOT_INJURY_COOLDOWN_MS;
    this.recordPlayerDowned(target.sessionId, now);
    target.isInjured = true;
    target.isHitReacting = false;
    target.hitReactionTimeRemaining = 0;
    target.hitReactionPitch = 0;
    target.hitReactionRoll = 0;
    target.hitReactionSeed = 0;
    this.playerForcedInjuredUntil.set(target.sessionId, now + PLAYER_INJURY_SYNC_GRACE_MS);
    target.hitTriggerId += 1;
    target.hitSourceX = nextbot.x;
    target.hitSourceY = nextbot.y;
    target.hitSourceZ = nextbot.z;
  }

  private buildScoredTarget(player: Player, now: number, nextbot: NextbotState, isCurrentTarget: boolean): ScoredTarget | undefined {
    if (!this.isScoreEligibleTarget(player, now, nextbot)) {
      return undefined;
    }

    const predicted = this.getPredictedTargetPosition(player, nextbot);
    const distanceScore = Math.max(0, NEXTBOT_DISTANCE_SCORE_BASE - predicted.distance);
    const visibilityProxyBonus = predicted.distance <= NEXTBOT_VISIBLE_PROXY_RANGE ? NEXTBOT_VISIBLE_PROXY_BONUS : 0;
    const facingBonus = this.isTargetInFront(predicted, nextbot) ? NEXTBOT_FRONT_BONUS : 0;
    const currentTargetBonus = isCurrentTarget ? NEXTBOT_CURRENT_TARGET_BONUS : 0;
    const interceptBonus = this.getInterceptBonus(player, predicted, nextbot);

    return {
      player,
      predicted,
      eligible: true,
      score: distanceScore
        + visibilityProxyBonus
        + facingBonus
        + currentTargetBonus
        + interceptBonus,
    };
  }

  private getPredictedTargetPosition(player: Player, nextbot: NextbotState): PredictedTargetPosition {
    const predictedX = player.x + player.velocityX * NEXTBOT_PREDICTION_TIME;
    const predictedZ = player.z + player.velocityZ * NEXTBOT_PREDICTION_TIME;
    const dx = predictedX - nextbot.x;
    const dz = predictedZ - nextbot.z;

    return {
      x: predictedX,
      z: predictedZ,
      distance: Math.hypot(dx, dz),
    };
  }

  private isScoreEligibleTarget(player: Player, now: number, nextbot: NextbotState) {
    if (player.isSpectator) {
      return false;
    }

    if (!player.isReady) {
      return false;
    }

    if (player.isEliminated) {
      return false;
    }

    const safeUntil = this.playerSafeUntil.get(player.sessionId) ?? 0;
    if (safeUntil > now) {
      return false;
    }

    if (player.isInjured || player.isHitReacting) {
      return false;
    }

    if (Math.abs(player.y - nextbot.y) > NEXTBOT_MAX_VERTICAL_DELTA) {
      return false;
    }

    if (now - player.timestamp > NEXTBOT_STALE_TARGET_TIMEOUT_MS) {
      return false;
    }

    const predicted = this.getPredictedTargetPosition(player, nextbot);
    if (predicted.distance > NEXTBOT_MAX_CHASE_RANGE) {
      return false;
    }

    return true;
  }

  private isTargetInFront(predicted: PredictedTargetPosition, nextbot: NextbotState) {
    const dx = predicted.x - nextbot.x;
    const dz = predicted.z - nextbot.z;
    const targetYaw = Math.atan2(dx, dz) * (180 / Math.PI);
    const yawDelta = Math.abs(this.deltaAngle(nextbot.rotationY, targetYaw));
    return yawDelta <= NEXTBOT_FRONT_ANGLE_THRESHOLD;
  }

  private getInterceptBonus(player: Player, predicted: PredictedTargetPosition, nextbot: NextbotState) {
    const speed = Math.hypot(player.velocityX, player.velocityZ);
    if (speed <= 0.1) {
      return 0;
    }

    const toPredictedX = predicted.x - nextbot.x;
    const toPredictedZ = predicted.z - nextbot.z;
    const toPredictedDistance = Math.hypot(toPredictedX, toPredictedZ);
    if (toPredictedDistance <= 0.0001) {
      return 0;
    }

    const playerDirectionX = player.velocityX / speed;
    const playerDirectionZ = player.velocityZ / speed;
    const towardBotDot = (playerDirectionX * (-toPredictedX / toPredictedDistance))
      + (playerDirectionZ * (-toPredictedZ / toPredictedDistance));

    if (towardBotDot <= 0) {
      return 0;
    }

    return towardBotDot * NEXTBOT_INTERCEPT_BONUS_MAX;
  }

  private deltaAngle(current: number, target: number) {
    let delta = (target - current) % 360;
    if (delta > 180) {
      delta -= 360;
    } else if (delta < -180) {
      delta += 360;
    }

    return delta;
  }

  private clearAllNextbotTargetingState() {
    for (let index = 0; index < this.nextbotControllers.length; index++) {
      const controller = this.nextbotControllers[index];
      controller.currentTargetSessionId = "";
      const nextbot = this.getNextbotState(index);
      if (nextbot != null) {
        nextbot.targetSessionId = "";
      }
    }
  }

  private getPlayerSpawnPosition(sessionId: string) {
    const spawnPoints = this.playerSpawnPoints.length > 0 ? this.playerSpawnPoints : DEFAULT_PLAYER_SPAWN_POINTS;
    const normalizedIndex = this.getStableSpawnIndex(sessionId, spawnPoints.length);
    const spawnPoint = spawnPoints[normalizedIndex];
    return {
      x: spawnPoint.x,
      y: spawnPoint.y,
      z: spawnPoint.z,
    };
  }

  private getNextbotId(index: number) {
    if (index < 0 || index >= this.nextbotIds.length) {
      return DEFAULT_NEXTBOT_IDS[Math.max(0, Math.min(DEFAULT_NEXTBOT_IDS.length - 1, index))];
    }

    return this.nextbotIds[index];
  }

  private getNextbotState(index: number) {
    return this.state.nextbots.get(this.getNextbotId(index));
  }

  private getNextbotSpawnPoint(spawnIndex: number): SpawnPoint {
    const spawnPoints = this.nextbotSpawnPoints.length > 0 ? this.nextbotSpawnPoints : DEFAULT_NEXTBOT_SPAWN_POINTS;
    const normalizedIndex = ((spawnIndex % spawnPoints.length) + spawnPoints.length) % spawnPoints.length;
    return spawnPoints[normalizedIndex];
  }

  private assignRandomPatrolTargets() {
    for (let index = 0; index < this.nextbotControllers.length; index++) {
      const controller = this.nextbotControllers[index];
      const spawnPoint = this.getNextbotSpawnPoint(controller.spawnIndex);
      const patrolTarget = this.getRandomPatrolTarget(spawnPoint.x, spawnPoint.y, spawnPoint.z, controller);
      controller.patrolTargetX = patrolTarget.x;
      controller.patrolTargetY = patrolTarget.y;
      controller.patrolTargetZ = patrolTarget.z;
      controller.patrolWaitUntil = 0;
    }
  }

  private setNextPatrolTargetFromCurrentPosition(controller: NextbotControllerState, nextbot: NextbotState) {
    const patrolTarget = this.getRandomPatrolTarget(nextbot.x, nextbot.y, nextbot.z, controller);
    controller.patrolTargetX = patrolTarget.x;
    controller.patrolTargetY = patrolTarget.y;
    controller.patrolTargetZ = patrolTarget.z;
    controller.patrolWaitUntil = 0;
  }

  private getRandomPatrolWaitMs() {
    return NEXTBOT_PATROL_WAIT_MIN_MS
      + Math.floor(Math.random() * (NEXTBOT_PATROL_WAIT_MAX_MS - NEXTBOT_PATROL_WAIT_MIN_MS + 1));
  }

  private getRandomPatrolTarget(originX: number, originY: number, originZ: number, requestingController?: NextbotControllerState): SpawnPoint {
    const patrolAreaPoints = this.nextbotPatrolPoints.length > 0
      ? this.nextbotPatrolPoints
      : this.getAutomaticPatrolAreaPoints();

    let minX = Number.POSITIVE_INFINITY;
    let maxX = Number.NEGATIVE_INFINITY;
    let minZ = Number.POSITIVE_INFINITY;
    let maxZ = Number.NEGATIVE_INFINITY;
    for (const point of patrolAreaPoints) {
      minX = Math.min(minX, point.x);
      maxX = Math.max(maxX, point.x);
      minZ = Math.min(minZ, point.z);
      maxZ = Math.max(maxZ, point.z);
    }
    minX -= NEXTBOT_PATROL_BOUNDS_PADDING;
    maxX += NEXTBOT_PATROL_BOUNDS_PADDING;
    minZ -= NEXTBOT_PATROL_BOUNDS_PADDING;
    maxZ += NEXTBOT_PATROL_BOUNDS_PADDING;

    let bestTargetX = originX;
    let bestTargetZ = originZ;
    let bestScore = Number.NEGATIVE_INFINITY;

    for (let attempts = 0; attempts < NEXTBOT_PATROL_CANDIDATE_SAMPLES; attempts++) {
      const candidateX = minX + Math.random() * (maxX - minX);
      const candidateZ = minZ + Math.random() * (maxZ - minZ);
      const travelDistance = Math.hypot(candidateX - originX, candidateZ - originZ);
      if (travelDistance < NEXTBOT_PATROL_MIN_TRAVEL_DISTANCE) {
        continue;
      }

      const separationDistance = this.getPatrolTargetSeparationDistance(candidateX, candidateZ, requestingController);
      const separationPenalty = separationDistance < NEXTBOT_PATROL_SEPARATION_RADIUS
        ? (NEXTBOT_PATROL_SEPARATION_RADIUS - separationDistance) * 1000
        : 0;
      const score = separationDistance * 10 + travelDistance - separationPenalty;

      if (score > bestScore) {
        bestScore = score;
        bestTargetX = candidateX;
        bestTargetZ = candidateZ;
      }
    }

    if (!Number.isFinite(bestScore)) {
      for (let attempts = 0; attempts < 12; attempts++) {
        const candidateX = minX + Math.random() * (maxX - minX);
        const candidateZ = minZ + Math.random() * (maxZ - minZ);
        const travelDistance = Math.hypot(candidateX - originX, candidateZ - originZ);
        if (travelDistance >= NEXTBOT_PATROL_MIN_TRAVEL_DISTANCE) {
          bestTargetX = candidateX;
          bestTargetZ = candidateZ;
          break;
        }
      }
    }

    return {
      x: bestTargetX,
      y: originY,
      z: bestTargetZ,
    };
  }

  private isPatrolTargetSeparated(targetX: number, targetZ: number, requestingController?: NextbotControllerState) {
    return this.getPatrolTargetSeparationDistance(targetX, targetZ, requestingController) >= NEXTBOT_PATROL_SEPARATION_RADIUS;
  }

  private getAutomaticPatrolAreaPoints(): SpawnPoint[] {
    const automaticPoints: SpawnPoint[] = [];

    for (let index = 0; index < this.nextbotSpawnPoints.length; index++) {
      automaticPoints.push(this.nextbotSpawnPoints[index]);
    }

    for (let index = 0; index < this.playerSpawnPoints.length; index++) {
      automaticPoints.push(this.playerSpawnPoints[index]);
    }

    if (automaticPoints.length > 0) {
      return automaticPoints;
    }

    return DEFAULT_NEXTBOT_SPAWN_POINTS;
  }

  private getPatrolTargetSeparationDistance(targetX: number, targetZ: number, requestingController?: NextbotControllerState) {
    let closestDistanceSq = Number.POSITIVE_INFINITY;

    for (let index = 0; index < this.nextbotControllers.length; index++) {
      const controller = this.nextbotControllers[index];
      if (controller === requestingController) {
        continue;
      }

      const patrolDx = controller.patrolTargetX - targetX;
      const patrolDz = controller.patrolTargetZ - targetZ;
      closestDistanceSq = Math.min(closestDistanceSq, (patrolDx * patrolDx) + (patrolDz * patrolDz));

      const nextbot = this.getNextbotState(index);
      if (nextbot != null) {
        const currentDx = nextbot.x - targetX;
        const currentDz = nextbot.z - targetZ;
        closestDistanceSq = Math.min(closestDistanceSq, (currentDx * currentDx) + (currentDz * currentDz));
      }
    }

    if (!Number.isFinite(closestDistanceSq)) {
      return Number.POSITIVE_INFINITY;
    }

    return Math.sqrt(closestDistanceSq);
  }

  private resolveNextbotSpawnPoints(options: any): SpawnPoint[] {
    const candidatePoints = options?.nextbotSpawnPoints;
    if (!Array.isArray(candidatePoints) || candidatePoints.length === 0) {
      return DEFAULT_NEXTBOT_SPAWN_POINTS;
    }

    const parsedPoints = candidatePoints
      .map((point) => {
        const x = Number(point?.x);
        const y = Number(point?.y);
        const z = Number(point?.z);
        if (!Number.isFinite(x) || !Number.isFinite(y) || !Number.isFinite(z)) {
          return undefined;
        }

        return { x, y, z };
      })
      .filter((point): point is { x: number; y: number; z: number } => point !== undefined);

    if (parsedPoints.length === 0) {
      return DEFAULT_NEXTBOT_SPAWN_POINTS;
    }

    return parsedPoints;
  }

  private resolveNextbotPatrolPoints(options: any): SpawnPoint[] {
    const candidatePoints = options?.nextbotPatrolPoints;
    if (!Array.isArray(candidatePoints) || candidatePoints.length === 0) {
      return [];
    }

    const parsedPoints = candidatePoints
      .map((point) => {
        const x = Number(point?.x);
        const y = Number(point?.y);
        const z = Number(point?.z);
        if (!Number.isFinite(x) || !Number.isFinite(y) || !Number.isFinite(z)) {
          return undefined;
        }

        return { x, y, z };
      })
      .filter((point): point is { x: number; y: number; z: number } => point !== undefined);

    if (parsedPoints.length === 0) {
      return [];
    }

    return parsedPoints;
  }

  private resolveNextbotObstacles(options: any): ObstacleRect[] {
    const candidateObstacles = options?.nextbotObstacles;
    if (!Array.isArray(candidateObstacles) || candidateObstacles.length === 0) {
      return [];
    }

    return candidateObstacles
      .map((obstacle) => {
        const minX = Number(obstacle?.minX);
        const maxX = Number(obstacle?.maxX);
        const minY = Number(obstacle?.minY);
        const maxY = Number(obstacle?.maxY);
        const minZ = Number(obstacle?.minZ);
        const maxZ = Number(obstacle?.maxZ);
        if (!Number.isFinite(minX)
          || !Number.isFinite(maxX)
          || !Number.isFinite(minY)
          || !Number.isFinite(maxY)
          || !Number.isFinite(minZ)
          || !Number.isFinite(maxZ)
          || minX >= maxX
          || minY >= maxY
          || minZ >= maxZ) {
          return undefined;
        }

        return { minX, maxX, minY, maxY, minZ, maxZ };
      })
      .filter((obstacle): obstacle is ObstacleRect => obstacle !== undefined);
  }

  private resolveNextbotFloorSamples(options: any): FloorSample[] {
    const candidateSamples = options?.nextbotFloorSamples;
    if (!Array.isArray(candidateSamples) || candidateSamples.length === 0) {
      return [];
    }

    return candidateSamples
      .map((sample) => {
        const x = Number(sample?.x);
        const y = Number(sample?.y);
        const z = Number(sample?.z);
        if (!Number.isFinite(x) || !Number.isFinite(y) || !Number.isFinite(z)) {
          return undefined;
        }

        return { x, y, z };
      })
      .filter((sample): sample is FloorSample => sample !== undefined);
  }

  private getGroundYForPosition(x: number, z: number, fallbackY: number) {
    if (this.nextbotFloorSamples.length === 0) {
      return fallbackY;
    }

    const nearestSamples: Array<{ sample: FloorSample; distanceSq: number }> = [];

    for (const sample of this.nextbotFloorSamples) {
      const dx = sample.x - x;
      const dz = sample.z - z;
      const distanceSq = (dx * dx) + (dz * dz);

      if (!Number.isFinite(distanceSq)) {
        continue;
      }

      nearestSamples.push({ sample, distanceSq });
    }

    if (nearestSamples.length === 0) {
      return fallbackY;
    }

    nearestSamples.sort((left, right) => left.distanceSq - right.distanceSq);

    const closest = nearestSamples[0];
    if (closest.distanceSq <= 0.0001) {
      return closest.sample.y;
    }

    const maxBlendDistanceSq = NEXTBOT_FLOOR_BLEND_RADIUS * NEXTBOT_FLOOR_BLEND_RADIUS;
    let blendedY = 0;
    let totalWeight = 0;
    let usedSampleCount = 0;

    for (let index = 0; index < nearestSamples.length && usedSampleCount < NEXTBOT_FLOOR_BLEND_SAMPLE_COUNT; index++) {
      const candidate = nearestSamples[index];
      if (candidate.distanceSq > maxBlendDistanceSq) {
        continue;
      }

      const weight = 1 / Math.max(0.0001, candidate.distanceSq);
      blendedY += candidate.sample.y * weight;
      totalWeight += weight;
      usedSampleCount += 1;
    }

    if (totalWeight > 0) {
      return blendedY / totalWeight;
    }

    return closest.sample.y;
  }

  private wouldNextbotMoveHitObstacle(
    startX: number,
    startZ: number,
    endX: number,
    endZ: number,
    currentY: number,
    targetY?: number,
  ) {
    for (const obstacle of this.nextbotObstacles) {
      if (!this.isObstacleRelevantForNextbotHeight(obstacle, currentY, targetY)) {
        continue;
      }

      const inflatedObstacle = {
        minX: obstacle.minX - NEXTBOT_OBSTACLE_PADDING,
        maxX: obstacle.maxX + NEXTBOT_OBSTACLE_PADDING,
        minZ: obstacle.minZ - NEXTBOT_OBSTACLE_PADDING,
        maxZ: obstacle.maxZ + NEXTBOT_OBSTACLE_PADDING,
      };

      const startInside = this.isPointInsideObstacle2D(startX, startZ, inflatedObstacle);
      if (!startInside && this.doesSegmentIntersectObstacle2D(startX, startZ, endX, endZ, inflatedObstacle)) {
        return true;
      }
    }

    return false;
  }

  private isObstacleRelevantForNextbotHeight(obstacle: ObstacleRect, currentY: number, targetY?: number) {
    const minY = Math.min(currentY, targetY ?? currentY) - 0.5;
    const maxY = Math.max(currentY, targetY ?? currentY) + NEXTBOT_OBSTACLE_HEIGHT_PADDING;
    return obstacle.maxY >= minY && obstacle.minY <= maxY;
  }

  private isPointInsideObstacle2D(x: number, z: number, obstacle: { minX: number; maxX: number; minZ: number; maxZ: number }) {
    return x >= obstacle.minX && x <= obstacle.maxX && z >= obstacle.minZ && z <= obstacle.maxZ;
  }

  private doesSegmentIntersectObstacle2D(
    startX: number,
    startZ: number,
    endX: number,
    endZ: number,
    obstacle: { minX: number; maxX: number; minZ: number; maxZ: number },
  ) {
    if (this.isPointInsideObstacle2D(endX, endZ, obstacle)) {
      return true;
    }

    const edges = [
      [obstacle.minX, obstacle.minZ, obstacle.maxX, obstacle.minZ],
      [obstacle.maxX, obstacle.minZ, obstacle.maxX, obstacle.maxZ],
      [obstacle.maxX, obstacle.maxZ, obstacle.minX, obstacle.maxZ],
      [obstacle.minX, obstacle.maxZ, obstacle.minX, obstacle.minZ],
    ];

    for (const [edgeStartX, edgeStartZ, edgeEndX, edgeEndZ] of edges) {
      if (this.doSegmentsIntersect2D(startX, startZ, endX, endZ, edgeStartX, edgeStartZ, edgeEndX, edgeEndZ)) {
        return true;
      }
    }

    return false;
  }

  private doSegmentsIntersect2D(
    aStartX: number,
    aStartZ: number,
    aEndX: number,
    aEndZ: number,
    bStartX: number,
    bStartZ: number,
    bEndX: number,
    bEndZ: number,
  ) {
    const orientation = (px: number, pz: number, qx: number, qz: number, rx: number, rz: number) =>
      (qz - pz) * (rx - qx) - (qx - px) * (rz - qz);
    const onSegment = (px: number, pz: number, qx: number, qz: number, rx: number, rz: number) =>
      qx >= Math.min(px, rx)
      && qx <= Math.max(px, rx)
      && qz >= Math.min(pz, rz)
      && qz <= Math.max(pz, rz);

    const o1 = orientation(aStartX, aStartZ, aEndX, aEndZ, bStartX, bStartZ);
    const o2 = orientation(aStartX, aStartZ, aEndX, aEndZ, bEndX, bEndZ);
    const o3 = orientation(bStartX, bStartZ, bEndX, bEndZ, aStartX, aStartZ);
    const o4 = orientation(bStartX, bStartZ, bEndX, bEndZ, aEndX, aEndZ);

    if ((o1 > 0) !== (o2 > 0) && (o3 > 0) !== (o4 > 0)) {
      return true;
    }

    if (o1 === 0 && onSegment(aStartX, aStartZ, bStartX, bStartZ, aEndX, aEndZ)) return true;
    if (o2 === 0 && onSegment(aStartX, aStartZ, bEndX, bEndZ, aEndX, aEndZ)) return true;
    if (o3 === 0 && onSegment(bStartX, bStartZ, aStartX, aStartZ, bEndX, bEndZ)) return true;
    if (o4 === 0 && onSegment(bStartX, bStartZ, aEndX, aEndZ, bEndX, bEndZ)) return true;

    return false;
  }

  private resolveNextbotIds(options: any): string[] {
    const candidateIds = options?.nextbotIds;
    if (!Array.isArray(candidateIds) || candidateIds.length === 0) {
      return DEFAULT_NEXTBOT_IDS.slice(0, MAX_ACTIVE_NEXTBOTS);
    }

    const parsedIds = candidateIds
      .map((id) => typeof id === "string" ? id.trim() : "")
      .filter((id) => id.length > 0);

    if (parsedIds.length === 0) {
      return DEFAULT_NEXTBOT_IDS.slice(0, MAX_ACTIVE_NEXTBOTS);
    }

    const uniqueIds: string[] = [];
    const seenIds = new Set<string>();
    for (let index = 0; index < parsedIds.length; index++) {
      const id = parsedIds[index];
      if (seenIds.has(id)) {
        continue;
      }

      seenIds.add(id);
      uniqueIds.push(id);
    }

    if (uniqueIds.length <= MAX_ACTIVE_NEXTBOTS) {
      return uniqueIds;
    }

    for (let index = uniqueIds.length - 1; index > 0; index--) {
      const swapIndex = Math.floor(Math.random() * (index + 1));
      const currentId = uniqueIds[index];
      uniqueIds[index] = uniqueIds[swapIndex];
      uniqueIds[swapIndex] = currentId;
    }

    return uniqueIds.slice(0, MAX_ACTIVE_NEXTBOTS);
  }

  private resolveNextbotMoveSpeeds(options: any, nextbotIds: string[]): Map<string, number> {
    const moveSpeeds = new Map<string, number>();
    const candidateConfigs = options?.nextbotConfigs;

    if (Array.isArray(candidateConfigs)) {
      for (const config of candidateConfigs) {
        const id = typeof config?.id === "string" ? config.id.trim() : "";
        const speed = Number(config?.speed);
        if (!id || !Number.isFinite(speed) || speed <= 0) {
          continue;
        }

        moveSpeeds.set(id, speed);
      }
    }

    for (const nextbotId of nextbotIds) {
      if (!moveSpeeds.has(nextbotId)) {
        moveSpeeds.set(nextbotId, NEXTBOT_MOVE_SPEED);
      }
    }

    return moveSpeeds;
  }

  private applyNextbotConfigOverrides(options: any) {
    const refreshedMoveSpeeds = this.resolveNextbotMoveSpeeds(options, this.nextbotIds);
    this.nextbotMoveSpeeds = refreshedMoveSpeeds;

    for (let index = 0; index < this.nextbotControllers.length; index++) {
      const controller = this.nextbotControllers[index];
      if (controller == null) {
        continue;
      }

      controller.moveSpeed = this.getConfiguredNextbotMoveSpeed(controller.id);
    }
  }

  private getConfiguredNextbotMoveSpeed(nextbotId: string): number {
    const configuredSpeed = this.nextbotMoveSpeeds.get(nextbotId);
    if (!Number.isFinite(configuredSpeed) || configuredSpeed == null || configuredSpeed <= 0) {
      return NEXTBOT_MOVE_SPEED;
    }

    return configuredSpeed;
  }

  private resolvePlayerSpawnPoints(options: any): SpawnPoint[] {
    const candidatePoints = options?.playerSpawnPoints;
    if (!Array.isArray(candidatePoints) || candidatePoints.length === 0) {
      return DEFAULT_PLAYER_SPAWN_POINTS;
    }

    const parsedPoints = candidatePoints
      .map((point) => {
        const x = Number(point?.x);
        const y = Number(point?.y);
        const z = Number(point?.z);
        if (!Number.isFinite(x) || !Number.isFinite(y) || !Number.isFinite(z)) {
          return undefined;
        }

        return { x, y, z };
      })
      .filter((point): point is { x: number; y: number; z: number } => point !== undefined);

    if (parsedPoints.length === 0) {
      return DEFAULT_PLAYER_SPAWN_POINTS;
    }

    return parsedPoints;
  }

  private tryStartRoundLoop() {
    if (this.currentPhase !== "waiting" || this.state.players.size === 0) {
      return;
    }

    let allReady = true;
    this.state.players.forEach((player) => {
      if (player.isSpectator) {
        return;
      }

      if (!player.isReady) {
        allReady = false;
      }
    });

    if (!allReady) {
      return;
    }

    if (!this.hasStartedMatchFlow) {
      this.hasStartedMatchFlow = true;
      this.broadcast("startGame");
    }

    this.beginIntermission(Date.now());
  }

  private getReadyPlayerCount() {
    let readyPlayerCount = 0;
    this.state.players.forEach((player) => {
      if (player.isReady && !player.isSpectator) {
        readyPlayerCount += 1;
      }
    });

    return readyPlayerCount;
  }

  private updateRoundFlow(now: number) {
    if (this.state.players.size === 0) {
      return;
    }

    if (this.currentPhase === "intermission" && now >= this.phaseEndsAt) {
      this.beginRound(now);
      return;
    }

    if (this.currentPhase === "round" && now >= this.phaseEndsAt) {
      this.endRound(now);
    }
  }

  private beginIntermission(now: number) {
    this.currentPhase = "intermission";
    this.phaseEndsAt = now + this.intermissionDurationMs;
    this.state.isGameStarted = false;
    this.resetNextbotsToSpawnPoints();
    this.resetPlayersForIntermission(now);
    this.broadcastRoundPhase();
  }

  private beginRound(now: number) {
    this.currentPhase = "round";
    this.roundIndex += 1;
    this.phaseEndsAt = now + this.roundDurationMs;
    this.state.isGameStarted = true;
    this.latestRoundResultsJson = "";
    this.resetNextbotsToSpawnPoints();
    this.resetPlayersForRoundStart(now);
    this.broadcastRoundPhase();
    this.broadcastRoundAnnouncement({
      title: "ROUND STARTED",
      subtitle: "SURVIVE FOR 3 MINUTES",
      durationSeconds: 3,
    });
  }

  private endRound(now: number) {
    const results = this.buildRoundResults(now);
    this.beginIntermission(now);
    this.latestRoundResultsJson = JSON.stringify(results);
    this.broadcast("roundResults", this.latestRoundResultsJson);
  }

  private resetPlayersForIntermission(now: number) {
    this.state.players.forEach((player) => {
      this.clearCarryStateForPlayer(player.sessionId);
      const spawnPosition = this.getPlayerSpawnPosition(player.sessionId);
      player.x = spawnPosition.x;
      player.y = spawnPosition.y;
      player.z = spawnPosition.z;
      player.rotationY = PLAYER_SPAWN_ROTATION_Y;
      player.visualYaw = PLAYER_SPAWN_ROTATION_Y;
      player.velocityX = 0;
      player.velocityY = 0;
      player.velocityZ = 0;
      player.isInjured = false;
      player.isEliminated = false;
      player.isHitReacting = false;
      player.hitReactionTimeRemaining = 0;
      player.hitReactionPitch = 0;
      player.hitReactionRoll = 0;
      player.hitReactionSeed = 0;
      player.hitTriggerId = 0;
      player.hitSourceX = 0;
      player.hitSourceY = 0;
      player.hitSourceZ = 0;
      player.isGrounded = true;
      player.isJumping = false;
      player.isCrouching = false;
      player.isWallRunning = false;
      player.wallRunSide = 0;
      player.moveInputX = 0;
      player.moveInputY = 0;
      this.playerSafeUntil.set(player.sessionId, now + this.intermissionDurationMs + NEXTBOT_START_GRACE_MS);
      this.playerRevivedUntil.set(player.sessionId, now + PLAYER_REVIVE_SYNC_GRACE_MS);
      this.playerForcedInjuredUntil.delete(player.sessionId);
    });

    this.sendRoundPlayerResetMessages();
  }

  private resetPlayersForRoundStart(now: number) {
    const safeUntil = now + NEXTBOT_START_GRACE_MS;
    this.state.players.forEach((player) => {
      const stats = this.roundStats.get(player.sessionId);
      if (stats != null) {
        stats.bestTimeMs = 0;
        stats.currentLifeStartMs = now;
        stats.downedCount = 0;
        stats.revivesDone = 0;
      }

      this.clearCarryStateForPlayer(player.sessionId);
      const spawnPosition = this.getPlayerSpawnPosition(player.sessionId);
      player.x = spawnPosition.x;
      player.y = spawnPosition.y;
      player.z = spawnPosition.z;
      player.rotationY = PLAYER_SPAWN_ROTATION_Y;
      player.visualYaw = PLAYER_SPAWN_ROTATION_Y;
      player.velocityX = 0;
      player.velocityY = 0;
      player.velocityZ = 0;
      player.isInjured = false;
      player.isEliminated = false;
      player.isHitReacting = false;
      player.hitReactionTimeRemaining = 0;
      player.hitReactionPitch = 0;
      player.hitReactionRoll = 0;
      player.hitReactionSeed = 0;
      player.hitTriggerId = 0;
      player.hitSourceX = 0;
      player.hitSourceY = 0;
      player.hitSourceZ = 0;
      player.isGrounded = true;
      player.timestamp = now;
      player.isJumping = false;
      player.isCrouching = false;
      player.isWallRunning = false;
      player.wallRunSide = 0;
      player.moveInputX = 0;
      player.moveInputY = 0;
      this.playerSafeUntil.set(player.sessionId, safeUntil);
      this.playerRevivedUntil.set(player.sessionId, now + PLAYER_REVIVE_SYNC_GRACE_MS);
      this.playerForcedInjuredUntil.delete(player.sessionId);
    });

    this.sendRoundPlayerResetMessages();
  }

  private sendRoundPlayerResetMessages() {
    for (let index = 0; index < this.clients.length; index++) {
      const client = this.clients[index];
      const player = this.state.players.get(client.sessionId);
      if (!player) {
        continue;
      }

      client.send("roundPlayerReset", JSON.stringify({
        x: player.x,
        y: player.y,
        z: player.z,
        rotationY: player.rotationY,
      }));
    }
  }

  private recordPlayerDowned(sessionId: string, now: number) {
    if (this.currentPhase !== "round") {
      return;
    }

    const stats = this.roundStats.get(sessionId);
    if (stats == null || stats.currentLifeStartMs == null) {
      return;
    }

    const runTime = Math.max(0, now - stats.currentLifeStartMs);
    stats.bestTimeMs = Math.max(stats.bestTimeMs, runTime);
    stats.downedCount += 1;
    stats.currentLifeStartMs = null;

    const player = this.state.players.get(sessionId);
    if (player != null && stats.downedCount >= PLAYER_MAX_DOWNS_BEFORE_ELIMINATION) {
      this.clearCarryStateForPlayer(sessionId);
      player.isEliminated = true;
      player.isInjured = true;
      player.isHitReacting = false;
      player.hitReactionTimeRemaining = 0;
      player.hitReactionPitch = 0;
      player.hitReactionRoll = 0;
      player.hitReactionSeed = 0;
      player.velocityX = 0;
      player.velocityY = 0;
      player.velocityZ = 0;
      player.animInputX = 0;
      player.animInputY = 0;
      player.moveInputX = 0;
      player.moveInputY = 0;
      player.isJumping = false;
      player.isCrouching = false;
      player.isWallRunning = false;
      player.wallRunSide = 0;
    }
  }

  private recordPlayerRevived(sessionId: string, now: number) {
    if (this.currentPhase !== "round") {
      return;
    }

    const stats = this.roundStats.get(sessionId);
    if (stats == null) {
      return;
    }

    stats.currentLifeStartMs = now;
  }

  private buildRoundResults(now: number): RoundResultsMessage {
    const entries: RoundResultEntry[] = [];

    this.state.players.forEach((player) => {
      const stats = this.roundStats.get(player.sessionId);
      if (stats == null) {
        return;
      }

      if (stats.currentLifeStartMs != null) {
        const runTime = Math.max(0, now - stats.currentLifeStartMs);
        stats.bestTimeMs = Math.max(stats.bestTimeMs, runTime);
      }

      entries.push({
        sessionId: player.sessionId,
        displayName: stats.displayName,
        bestTimeMs: stats.bestTimeMs,
        downedCount: stats.downedCount,
        revivesDone: stats.revivesDone,
        joinOrder: stats.joinOrder,
        rank: 0,
      });
    });

    entries.sort((a, b) => {
      if (a.bestTimeMs !== b.bestTimeMs) {
        return b.bestTimeMs - a.bestTimeMs;
      }

      if (a.downedCount !== b.downedCount) {
        return a.downedCount - b.downedCount;
      }

      if (a.revivesDone !== b.revivesDone) {
        return b.revivesDone - a.revivesDone;
      }

      return a.joinOrder - b.joinOrder;
    });

    for (let i = 0; i < entries.length; i++) {
      const previous = i > 0 ? entries[i - 1] : undefined;
      const current = entries[i];
      if (previous != null
        && previous.bestTimeMs === current.bestTimeMs
        && previous.downedCount === current.downedCount
        && previous.revivesDone === current.revivesDone) {
        current.rank = previous.rank;
      } else {
        current.rank = i + 1;
      }
    }

    return {
      roundIndex: this.roundIndex,
      roundDurationMs: this.roundDurationMs,
      entries,
    };
  }

  private createRoundPhaseMessage(now: number): RoundPhaseMessage {
    return {
      phase: this.currentPhase,
      roundIndex: this.roundIndex,
      timeRemainingMs: this.phaseEndsAt > 0 ? Math.max(0, this.phaseEndsAt - now) : 0,
      roundDurationMs: this.roundDurationMs,
      intermissionDurationMs: this.intermissionDurationMs,
    };
  }

  private broadcastRoundPhase() {
    this.broadcast("roundPhase", JSON.stringify(this.createRoundPhaseMessage(Date.now())));
  }

  private sendRoundPhaseToClient(client: Client) {
    client.send("roundPhase", JSON.stringify(this.createRoundPhaseMessage(Date.now())));
  }

  private broadcastRoundAnnouncement(message: RoundAnnouncementMessage) {
    this.broadcast("roundAnnouncement", JSON.stringify(message));
  }

  private resolvePositiveDurationMs(candidate: unknown, fallback: number) {
    const value = Number(candidate);
    if (!Number.isFinite(value)) {
      return fallback;
    }

    return Math.max(1000, Math.round(value));
  }

  private getStableSpawnIndex(sessionId: string, spawnPointCount: number) {
    if (spawnPointCount <= 0) {
      return 0;
    }

    let hash = 0;
    for (let i = 0; i < sessionId.length; i++) {
      hash = ((hash * 31) + sessionId.charCodeAt(i)) | 0;
    }

    const normalized = hash % spawnPointCount;
    return normalized < 0 ? normalized + spawnPointCount : normalized;
  }
}
