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

  it("keeps nextbots out of configured obstacles", async () => {
    const room = await colyseus.createRoom<MyRoomState>("my_room", {
      nextbotObstacles: [
        { minX: 1, maxX: 3, minY: -1, maxY: 3, minZ: -1, maxZ: 1 },
      ],
    });

    const roomAny = room as any;
    const nextbot = room.state.nextbots.get("nextbot_0");
    assert.ok(nextbot);

    nextbot!.x = 0;
    nextbot!.y = 0;
    nextbot!.z = 0;

    roomAny.moveNextbotTowardsPosition(nextbot, { x: 5, z: 0, distance: 5 }, 1, 2, 0);

    const insideObstacle = nextbot!.x >= 1 && nextbot!.x <= 3 && nextbot!.z >= -1 && nextbot!.z <= 1;
    assert.strictEqual(insideObstacle, false);
  });

  it("keeps nextbots on their grounded spawn height while chasing", async () => {
    const room = await colyseus.createRoom<MyRoomState>("my_room", {});
    const roomAny = room as any;
    const nextbot = room.state.nextbots.get("nextbot_0");
    assert.ok(nextbot);

    nextbot!.x = 0;
    nextbot!.y = 0;
    nextbot!.z = 0;

    roomAny.nextbotControllers[0].groundedY = 0;
    roomAny.moveNextbotTowardsPosition(nextbot, { x: 5, z: 0, distance: 5 }, 1, 2, 10);

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

    nextbot!.x = 0;
    nextbot!.y = 0;
    nextbot!.z = 0;

    roomAny.moveNextbotTowardsPosition(nextbot, { x: 4, z: 0, distance: 4 }, 1, 4, 0);

    assert.strictEqual(nextbot!.y, 2);
  });
});
