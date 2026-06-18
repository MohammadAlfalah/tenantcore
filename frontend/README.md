# TenantCore — Frontend

React + TypeScript + Tailwind CSS single-page app for TenantCore. See the
[root README](../README.md) for full setup, architecture, and the API reference.

## Scripts

```bash
npm install     # install dependencies
npm run dev     # dev server at http://localhost:5173 (proxies /api → http://localhost:5048)
npm run build   # type-check + production build to dist/
npm run lint    # ESLint
npm run preview # preview the production build
```

## Configuration

The app calls relative `/api/*` URLs. In dev, Vite proxies them to the backend (configurable via the
`VITE_API_PROXY_TARGET` env var in [`vite.config.ts`](vite.config.ts)). In Docker, nginx proxies
`/api/*` to the API container. Set `VITE_API_BASE_URL` only if you need to point at an absolute API
origin.
