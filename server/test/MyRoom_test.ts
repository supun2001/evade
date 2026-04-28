import assert from "assert";
import { ColyseusTestServer, boot } from "@colyseus/testing";

// import your "app.config.ts" file here.
import appConfig from "../src/app.config";
import { MyRoomState } from "../src/rooms/schema/MyRoomState";

describe("testing your Colyseus app", () => {
  let colyseus: ColyseusTestServer;

  before(async () => colyseus = await boot(appConfig));
  after(async () => colyseus.shutdown());

  beforeEach(async () => await colyseus.cleanup());

  it("connecting into a room", async () => {
    // `room` is the server-side Room instance reference.
    const room = await colyseus.createRoom<MyRoomState>("my_room", {});

    // `client1` is the client-side `Room` instance reference (same as JavaScript SDK)
    const client1 = await colyseus.connectTo(room);

    // make your assertions
    assert.strictEqual(client1.sessionId, room.clients[0].sessionId);

    // wait for state sync
    await room.waitForNextPatch();

    const player = room.state.players.get(client1.sessionId);
    assert.ok(player);
    assert.strictEqual(player.sessionId, client1.sessionId);
    assert.strictEqual(room.state.nextbots.size, 5);
  });

  it("keeps hit-reacting players revivable while the injured pose latches", async () => {
    const room = await colyseus.createRoom<MyRoomState>("my_room", {});
    const reviver = await colyseus.connectTo(room);
    const target = await colyseus.connectTo(room);

    await room.waitForNextPatch();

    reviver.send("playerUpdate", {
      x: 0,
      y: 0,
      z: 0,
      isGrounded: true,
      isInjured: false,
      isHitReacting: false,
    });
    target.send("playerUpdate", {
      x: 1,
      y: 0,
      z: 0,
      isGrounded: true,
      isInjured: false,
      isHitReacting: true,
      hitReactionTimeRemaining: 2.4,
    });

    await room.waitForNextPatch();

    const targetState = room.state.players.get(target.sessionId);
    assert.ok(targetState);
    assert.strictEqual(targetState.isHitReacting, true);
    assert.strictEqual(targetState.isInjured, true);

    reviver.send("revivePlayer", {
      targetSessionId: target.sessionId,
    });

    await room.waitForNextPatch();

    assert.strictEqual(targetState.isHitReacting, false);
    assert.strictEqual(targetState.isInjured, false);
  });

  it("keeps nextbots out of configured obstacles", async () => {
    const room = await colyseus.createRoom<MyRoomState>("my_room", {
      nextbotObstacles: [
        { minX: 1, maxX: 3, minY: -1, maxY: 3, minZ: -1, maxZ: 1 },
      ],
    });

    const roomAny = room as any;
    const nextbot = room.state.nextbots.get("nextbot_0");
    assert.ok(nextbot);
    const controller = roomAny.nextbotControllers[0];
    assert.ok(controller);

    nextbot!.x = 0;
    nextbot!.y = 0;
    nextbot!.z = 0;

    roomAny.moveNextbotTowardsPosition(controller, nextbot, { x: 5, z: 0, distance: 5 }, 1, 2, 0);

    const insideObstacle = nextbot!.x >= 1 && nextbot!.x <= 3 && nextbot!.z >= -1 && nextbot!.z <= 1;
    assert.strictEqual(insideObstacle, false);
  });

  it("keeps nextbots from crossing thin wall obstacles", async () => {
    const room = await colyseus.createRoom<MyRoomState>("my_room", {
      nextbotObstacles: [
        { minX: -10, maxX: 10, minY: -1, maxY: 3, minZ: -0.125, maxZ: 0.125 },
      ],
    });

    const roomAny = room as any;
    const nextbot = room.state.nextbots.get("nextbot_0");
    assert.ok(nextbot);
    const controller = roomAny.nextbotControllers[0];
    assert.ok(controller);

    nextbot!.x = 0;
    nextbot!.y = 0;
    nextbot!.z = -0.6;

    roomAny.moveNextbotTowardsPosition(controller, nextbot, { x: 0, z: 2, distance: 2.6 }, 1, 1, 0);

    assert.ok(nextbot!.z < -0.125);
  });

  it("steers nextbots away from nearby walls instead of sliding along them", async () => {
    const room = await colyseus.createRoom<MyRoomState>("my_room", {
      nextbotObstacles: [
        { minX: 1, maxX: 1.2, minY: -1, maxY: 3, minZ: -10, maxZ: 10 },
      ],
    });

    const roomAny = room as any;
    const nextbot = room.state.nextbots.get("nextbot_0");
    assert.ok(nextbot);
    const controller = roomAny.nextbotControllers[0];
    assert.ok(controller);

    nextbot!.x = 0.35;
    nextbot!.y = 0;
    nextbot!.z = 0;

    roomAny.moveNextbotTowardsPosition(controller, nextbot, { x: 0.35, z: 5, distance: 5 }, 1, 1, 0);

    assert.ok(nextbot!.x < 0.2);
  });

  it("plans around walls when a route exists", async () => {
    const room = await colyseus.createRoom<MyRoomState>("my_room", {
      nextbotObstacles: [
        { minX: 1, maxX: 1.2, minY: -1, maxY: 3, minZ: -2, maxZ: 2 },
      ],
      nextbotSpawnPoints: [
        { x: 0, y: 0, z: 0 },
      ],
      playerSpawnPoints: [
        { x: 3, y: 0, z: 0 },
      ],
    });

    const roomAny = room as any;
    const nextbot = room.state.nextbots.get("nextbot_0");
    assert.ok(nextbot);
    const controller = roomAny.nextbotControllers[0];
    assert.ok(controller);

    nextbot!.x = 0;
    nextbot!.y = 0;
    nextbot!.z = 0;

    const waypoints = roomAny.findNextbotPathWaypoints(0, 0, 3, 0, 0, 0);
    assert.ok(waypoints.length > 0);
    assert.ok(waypoints.some((waypoint: { x: number; z: number }) => Math.abs(waypoint.z) > 2));

    for (let tick = 0; tick < 160; tick++) {
      roomAny.moveNextbotTowardsPosition(controller, nextbot, { x: 3, z: 0, distance: Math.hypot(3 - nextbot!.x, nextbot!.z) }, 0.05, 2, 0);
    }

    assert.ok(nextbot!.x > 2);
    const insideObstacle = nextbot!.x >= 1 && nextbot!.x <= 1.2 && nextbot!.z >= -2 && nextbot!.z <= 2;
    assert.strictEqual(insideObstacle, false);
  });

  it("expands sparse spawn points using patrol points", async () => {
    const room = await colyseus.createRoom<MyRoomState>("my_room", {
      nextbotSpawnPoints: [
        { x: 0, y: 0, z: 0 },
      ],
      nextbotPatrolPoints: [
        { x: 10, y: 0, z: 0 },
        { x: -10, y: 0, z: 0 },
        { x: 0, y: 0, z: 10 },
        { x: 0, y: 0, z: -10 },
        { x: 14, y: 0, z: 14 },
      ],
    });

    const roomAny = room as any;
    assert.ok(roomAny.nextbotSpawnPoints.length >= room.state.nextbots.size);
  });

  it("prefers configured patrol points over random interior positions", async () => {
    const room = await colyseus.createRoom<MyRoomState>("my_room", {
      nextbotSpawnPoints: [
        { x: 0, y: 0, z: 0 },
      ],
      nextbotPatrolPoints: [
        { x: 10, y: 0, z: 0 },
        { x: 0, y: 0, z: 12 },
        { x: -12, y: 0, z: 0 },
      ],
    });

    const roomAny = room as any;
    const patrolTarget = roomAny.getRandomPatrolTarget(0, 0, 0, roomAny.nextbotControllers[0]);
    const isConfiguredPoint = roomAny.nextbotPatrolPoints.some((point: { x: number; y: number; z: number }) =>
      Math.abs(point.x - patrolTarget.x) < 0.001
      && Math.abs(point.y - patrolTarget.y) < 0.001
      && Math.abs(point.z - patrolTarget.z) < 0.001);

    assert.strictEqual(isConfiguredPoint, true);
  });

  it("keeps nextbots on their grounded spawn height while chasing", async () => {
    const room = await colyseus.createRoom<MyRoomState>("my_room", {});
    const roomAny = room as any;
    const nextbot = room.state.nextbots.get("nextbot_0");
    assert.ok(nextbot);
    const controller = roomAny.nextbotControllers[0];
    assert.ok(controller);

    nextbot!.x = 0;
    nextbot!.y = 0;
    nextbot!.z = 0;

    controller.groundedY = 0;
    roomAny.moveNextbotTowardsPosition(controller, nextbot, { x: 5, z: 0, distance: 5 }, 1, 2, 10);

    assert.strictEqual(nextbot!.y, 0);
  });

  it("uses floor samples to follow uneven ground", async () => {
    const room = await colyseus.createRoom<MyRoomState>("my_room", {
      nextbotFloorSamples: [
        { x: 0, y: 0, z: 0 },
        { x: 4, y: 2, z: 0 },
      ],
    });

    const roomAny = room as any;
    const nextbot = room.state.nextbots.get("nextbot_0");
    assert.ok(nextbot);
    const controller = roomAny.nextbotControllers[0];
    assert.ok(controller);

    nextbot!.x = 0;
    nextbot!.y = 0;
    nextbot!.z = 0;

    roomAny.moveNextbotTowardsPosition(controller, nextbot, { x: 4, z: 0, distance: 4 }, 1, 4, 0);

    assert.ok(Math.abs(nextbot!.y - 2) < 0.1);
  });

  it("does not injure players through walls", async () => {
    const room = await colyseus.createRoom<MyRoomState>("my_room", {
      nextbotObstacles: [
        { minX: 0.4, maxX: 0.6, minY: -1, maxY: 3, minZ: -1, maxZ: 1 },
      ],
    });
    const client = await colyseus.connectTo(room);
    await room.waitForNextPatch();

    const roomAny = room as any;
    const nextbot = room.state.nextbots.get("nextbot_0");
    assert.ok(nextbot);
    const controller = roomAny.nextbotControllers[0];
    assert.ok(controller);

    const target = roomAny.state.players.get(client.sessionId);
    assert.ok(target);

    nextbot!.x = 0;
    nextbot!.y = 0;
    nextbot!.z = 0;
    target.x = 0.9;
    target.y = 0;
    target.z = 0;
    target.isInjured = false;
    target.isHitReacting = false;
    target.isEliminated = false;
    controller.nextInjuryAt = 0;

    roomAny.tryInjurePlayer(controller, nextbot, target, Date.now());

    assert.strictEqual(target.isInjured, false);
  });
});
