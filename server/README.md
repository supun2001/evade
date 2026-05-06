# Evade Server

Colyseus multiplayer server for the Unity client in this repo.

## Local Run

```bash
cp .env.example .env
npm ci
npm run build
npm start
```

The server listens on `PORT` or `2567` by default.

Useful endpoints:

- `/` returns a small JSON status payload
- `/healthz` returns the same JSON status payload for host health checks
- `/hello_world` returns a plain-text smoke-test response

## Environment

Set these in your host:

```bash
NODE_ENV=production
PORT=2567
MONGODB_URI=your-mongodb-connection-string
MONGODB_DB_NAME=evade
```

If `MONGODB_URI` is not set, the server falls back to `mongodb://127.0.0.1:27017`.

## Docker Deploy

Build:

```bash
docker build -t evade-server .
```

Run:

```bash
docker run --rm -p 2567:2567 \
  -e NODE_ENV=production \
  -e PORT=2567 \
  -e MONGODB_URI=your-mongodb-connection-string \
  -e MONGODB_DB_NAME=evade \
  evade-server
```

Once deployed behind HTTPS, your public Colyseus URL should look like:

```text
wss://your-server-hostname.example.com
```

## Unity Client Override

The Unity client now supports a runtime server override without changing every scene.

For WebGL testing, append `?server=` to the game URL:

```text
https://your-webgl-host.example.com/?server=wss://your-server-hostname.example.com
```

The client also accepts `https://...` in the query string and converts it to `wss://...` automatically.

On desktop builds or in the editor, you can launch with:

```bash
-serverUrl wss://your-server-hostname.example.com
```
