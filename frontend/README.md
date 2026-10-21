# Forge PLM — Frontend

The Slice 7 SPA: app shell, actor switch, Products screens (live API), and the
Engineering Change screens (live or contract mock).

## Prerequisites

- **Node.js 20.19+ or 22.12+** (Vite 8)
- **Docker** — the backend and database run as containers so you do not need the .NET SDK

## 1. Start the backend as containers

There is no separate API server for development: the compose stack runs PostgreSQL and the
API, and the API serves `/api` on **port 5199**. There is no CORS to configure because the
dev server proxies to it.

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

> The API container also serves a **built** copy of this SPA at
> <http://localhost:5199>. That is the demo/run mode, not development — ignore it while
> developing and use the Vite dev server below for hot reload.

## 2. Run the frontend in dev mode

```bash
cd frontend
npm install
npm run dev
```

Open <http://localhost:5173>. Vite proxies `/api` to `http://localhost:5199`
(see `vite.config.ts`), so the client always uses relative URLs and there is no CORS setup.

## 3. Environment

The frontend needs **no environment file** to run. There is exactly one optional variable:

| Variable | Values | Default |
|---|---|---|
| `VITE_ECO_API_MODE` | `live` · `mock` | unset — auto-detected |

`VITE_ECO_API_MODE` forces which ECO implementation the app uses. This matters because the
frozen ECO endpoints do not exist yet:

- `live` — call the real `/api/ecos` endpoints.
- `mock` — use the in-memory contract adapter for the ECO screens. Products and BOM still
  read the live API, and the UI shows a **MOCK ECO API** note.
- **unset (default)** — the app probes `GET /api/ecos?pageSize=1` at start-up: a 2xx answer
  means live, anything else (including today's 404) means mock. No edit is needed when the
  backend lands the endpoints.

To set it, copy the example and edit the value:

```bash
cp .env.example .env     # then set VITE_ECO_API_MODE=live
```

`.env` is git-ignored (`*.example` is kept). Restart `npm run dev` after changing it — Vite
reads env files at start-up.

## Scripts

| Command | What it does |
|---|---|
| `npm run dev` | Vite dev server on `:5173` with `/api` proxied to `:5199` |
| `npm run build` | `tsc --noEmit` then a production build into `dist/` |
| `npm run preview` | Serve the built `dist/` locally |
| `npm test` | Vitest unit tests (single run) |

## Notes

- Data changes made through the **mock** ECO adapter live in memory only and reset on reload;
  they are not written to PostgreSQL.
- The frontend gate is `npm run build` plus `npm test`. Do **not** run the .NET test suite from
  here — see `docs/HANDOFF.md` §4.
