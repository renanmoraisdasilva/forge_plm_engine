# Forge PLM — Frontend

The Slice 7 SPA: app shell, actor switch, Products screens, and the Engineering Change
screens. Every screen talks to the live API — there is no mock adapter and no environment
configuration to switch between modes.

## Prerequisites

- **Node.js 20.19+ or 22.12+** (Vite 8)
- **Docker** — the backend and database run as containers so you do not need the .NET SDK

## 1. Start the backend as containers

The compose stack runs PostgreSQL and the API on **port 5199**. There is no CORS to configure
because the dev server proxies to it.

From the **repository root** (one level above this folder):

```bash
docker compose up -d --wait     # builds + starts db and backend, returns when healthy
```

`--wait` blocks on the healthchecks, so the command returns once migrations and the seed run
have finished. The database is seeded with the prototype data (`SERVER-100`, revision **B**).

Check it is up:

```bash
curl http://localhost:5199/api/products?pageSize=1
```

Useful lifecycle commands:

```bash
docker compose logs -f backend   # watch the API
docker compose down              # stop (data survives in the named volume)
docker compose down -v           # stop AND delete the data
```

> The container currently serves **only `/api` and `/health`**. It does not serve the SPA:
> the Dockerfile's frontend stage is still a placeholder, so `http://localhost:5199/` is a
> 404. Use the Vite dev server below, which is also the intended development workflow.

## 2. Run the frontend in dev mode

```bash
cd frontend
npm install
npm run dev
```

Open <http://localhost:5173>. Vite proxies `/api` to `http://localhost:5199`
(see `vite.config.ts`), so the client always uses relative URLs and there is no CORS setup.

## 3. Environment

The frontend reads **no environment variables** and needs no `.env` file. The API base is the
relative path `/api`, resolved by the dev-server proxy locally.

## Scripts

| Command | What it does |
|---|---|
| `npm run dev` | Vite dev server on `:5173` with `/api` proxied to `:5199` |
| `npm run build` | `tsc --noEmit` then a production build into `dist/` |
| `npm run preview` | Serve the built `dist/` locally |
| `npm test` | Vitest unit tests (single run) |

## Notes

- Engineering changes write to PostgreSQL through the live API; there is no in-memory fallback.
- The dev container seeds `SERVER-100` at revision B. Creating and releasing an ECO advances
  it (the demo path changes `MB-020` to `MB-021` and produces revision C).
- The frontend gate is `npm run build` plus `npm test`. Do **not** run the .NET test suite from
  here — see `docs/HANDOFF.md` §4.
