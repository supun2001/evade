import config from "@colyseus/tools";
import { monitor } from "@colyseus/monitor";
import { playground } from "@colyseus/playground";
import { WebSocketTransport } from "@colyseus/ws-transport";
import express from "express";

/**
 * Import your Room files
 */
import { MyRoom } from "./rooms/MyRoom";
import {
    ensureAccountIndexes,
    equipSkin,
    getAccountByToken,
    loginAccount,
    purchaseSkin,
    registerAccount,
} from "./accountStore";

export default config({
    initializeTransport: ({ server }) => {
        return new WebSocketTransport({
            server,
            // The default transport cap is only 4 KB, which is too small for our map sync payloads.
            maxPayload: 8 * 1024 * 1024,
        });
    },

    initializeGameServer: (gameServer) => {
        /**
         * Define your room handlers:
         */
        gameServer.define('my_room', MyRoom);

    },

    initializeExpress: (app) => {
        // Enable CORS
        app.use((req, res, next) => {
            res.setHeader("Access-Control-Allow-Origin", "*");
            res.setHeader("Access-Control-Allow-Headers", "Origin, X-Requested-With, Content-Type, Accept, Authorization");
            res.setHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            if (req.method === "OPTIONS") {
                res.sendStatus(204);
                return;
            }
            next();
        });
        app.use(express.json());

        /**
         * Bind your custom express routes here:
         * Read more: https://expressjs.com/en/starter/basic-routing.html
         */
        app.get("/hello_world", (req, res) => {
            res.send("It's time to kick ass and chew bubblegum!");
        });

        app.post("/auth/register", async (req, res) => {
            try {
                const username = typeof req.body?.username === "string" ? req.body.username : "";
                const password = typeof req.body?.password === "string" ? req.body.password : "";
                const result = await registerAccount(username, password);
                res.json({
                    ok: true,
                    token: result.token,
                    account: result.account,
                });
            } catch (error) {
                const message = error instanceof Error ? error.message : "Failed to register.";
                res.status(400).json({ ok: false, error: message });
            }
        });

        app.post("/auth/login", async (req, res) => {
            try {
                const username = typeof req.body?.username === "string" ? req.body.username : "";
                const password = typeof req.body?.password === "string" ? req.body.password : "";
                const result = await loginAccount(username, password);
                res.json({
                    ok: true,
                    token: result.token,
                    account: result.account,
                });
            } catch (error) {
                const message = error instanceof Error ? error.message : "Failed to login.";
                res.status(400).json({ ok: false, error: message });
            }
        });

        app.get("/auth/me", async (req, res) => {
            try {
                const headerValue = typeof req.headers.authorization === "string" ? req.headers.authorization : "";
                const token = headerValue.startsWith("Bearer ") ? headerValue.slice(7) : "";
                const account = await getAccountByToken(token);
                if (!account) {
                    res.status(401).json({ ok: false, error: "Invalid login token." });
                    return;
                }

                res.json({
                    ok: true,
                    account,
                });
            } catch (error) {
                const message = error instanceof Error ? error.message : "Failed to load account.";
                res.status(500).json({ ok: false, error: message });
            }
        });

        app.post("/shop/purchase-skin", async (req, res) => {
            try {
                const headerValue = typeof req.headers.authorization === "string" ? req.headers.authorization : "";
                const token = headerValue.startsWith("Bearer ") ? headerValue.slice(7) : "";
                const skinIndex = Number(req.body?.skinIndex);
                const account = await purchaseSkin(token, skinIndex);
                res.json({
                    ok: true,
                    account,
                });
            } catch (error) {
                const message = error instanceof Error ? error.message : "Failed to buy skin.";
                res.status(400).json({ ok: false, error: message });
            }
        });

        app.post("/shop/equip-skin", async (req, res) => {
            try {
                const headerValue = typeof req.headers.authorization === "string" ? req.headers.authorization : "";
                const token = headerValue.startsWith("Bearer ") ? headerValue.slice(7) : "";
                const skinIndex = Number(req.body?.skinIndex);
                const account = await equipSkin(token, skinIndex);
                res.json({
                    ok: true,
                    account,
                });
            } catch (error) {
                const message = error instanceof Error ? error.message : "Failed to equip skin.";
                res.status(400).json({ ok: false, error: message });
            }
        });

        /**
         * Use @colyseus/playground
         * (It is not recommended to expose this route in a production environment)
         */
        if (process.env.NODE_ENV !== "production") {
            app.use("/", playground());
        }

        /**
         * Use @colyseus/monitor
         * It is recommended to protect this route with a password
         * Read more: https://docs.colyseus.io/tools/monitor/#restrict-access-to-the-panel-using-a-password
         */
        app.use("/monitor", monitor());
    },


    beforeListen: () => {
        /**
         * Before before gameServer.listen() is called.
         */
        void ensureAccountIndexes()
            .then(() => {
                console.log("Mongo account indexes ready.");
            })
            .catch((error) => {
                console.error("Failed to prepare Mongo account indexes:", error);
            });
    }
});
