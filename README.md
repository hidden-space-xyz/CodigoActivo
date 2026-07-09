<p align="center">
  <img
    width="100%"
    alt="Código Activo"
    src="https://capsule-render.vercel.app/api?type=waving&height=220&color=0:f9a320,50:ff6b5e,100:2dd4d9&text=C%C3%B3digo%20Activo&fontColor=ffffff&fontSize=52&fontAlign=50&fontAlignY=40&descAlignY=62&animation=fadeIn"
  />
</p>
<p align="center">
<img alt=".NET" src="https://img.shields.io/badge/.NET_10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" />
<img alt="Vue" src="https://img.shields.io/badge/Vue_3-4FC08D?style=for-the-badge&logo=vuedotjs&logoColor=white" />
<img alt="TypeScript" src="https://img.shields.io/badge/TypeScript-3178C6?style=for-the-badge&logo=typescript&logoColor=white" />
<img alt="PostgreSQL" src="https://img.shields.io/badge/PostgreSQL-4169E1?style=for-the-badge&logo=postgresql&logoColor=white" />
<img alt="Vite" src="https://img.shields.io/badge/Vite-646CFF?style=for-the-badge&logo=vite&logoColor=white" />
<img alt="License" src="https://img.shields.io/badge/GPL--3.0-red?style=for-the-badge" />
</p>

# 🌐 &lt;Codigoactivo/&gt;

**Official website for `<Codigoactivo/>`, a nonprofit association based in León.**

## 📖 Overview

`<Codigoactivo/>` is a León-based nonprofit that since 2018 has helped children and young people discover programming and computational thinking through free, hands-on learning — from their very first blocks in Scratch to Python and artificial intelligence.


This repository is its digital home: a **public site** for events, announcements, resources
and member sign-up, plus an **admin back-office** where the team runs it all.

## 🧰 Tech Stack

| Layer          | Technologies                                                                                   |
| -------------- | --------------------------------------------------------------------------------------------- |
| **Backend**    | ASP.NET Core (.NET 10) · EF Core · PostgreSQL · Argon2id · Serilog · Swashbuckle (OpenAPI)     |
| **Frontend**   | Vue 3 · Vite · TypeScript · PrimeVue · TanStack Query · TipTap · Orval (generated REST client) |
| **Quality**    | Meziantou.Analyzer · SonarAnalyzer · CSharpier · ESLint · Prettier · `vue-tsc` · Steiger       |
| **Deployment** | Docker · Docker Compose · nginx                                                                |

## 🏗️ Repository layout

```
CodigoActivo/
├── backend/    ASP.NET Core Web API (.NET 10, EF Core + PostgreSQL)
└── frontend/   Vue 3 + Vite SPA (TypeScript, Feature-Sliced Design)
```

Two independently developed apps that ship together as a **same-origin** stack. See
**[ARCHITECTURE.md](ARCHITECTURE.md)** for the full picture.

## 🚀 Quick start

Fastest path — run the whole stack in Docker:

```bash
cp .env.example .env         # set at least POSTGRES_PASSWORD
docker compose up --build    # SPA → http://localhost:8080 · API → http://localhost:5150 (Swagger at /swagger)
```

Or run the apps directly for development, with hot reload:

```bash
# Backend — needs a reachable PostgreSQL; the connection is built from POSTGRES_* env vars
cd backend && dotnet run --project src/CodigoActivo.API    # http://localhost:5150

# Frontend
cd frontend && npm install && npm run dev                  # http://localhost:5173
```

> [!TIP]
> Prerequisites, per-app commands, and the full development workflow are in
> **[CONTRIBUTING.md](CONTRIBUTING.md)**.

## 📚 Documentation

| Document                             | What's inside                                                                        |
| ------------------------------------ | ----------------------------------------------------------------------------------- |
| **[ARCHITECTURE.md](ARCHITECTURE.md)** | Clean-architecture backend, Feature-Sliced frontend, and the API contract linking them |
| **[CONTRIBUTING.md](CONTRIBUTING.md)** | Local setup, commands, coding conventions, testing, and the API-generation workflow    |
| **[DEPLOYMENT.md](DEPLOYMENT.md)**     | Docker Compose stack, environment-variable reference, and production run                |
| **[SECURITY.md](SECURITY.md)**         | Security model, container hardening, and how to report a vulnerability                  |

> [!NOTE]
> Per-directory guidance for AI coding agents lives in the `CLAUDE.md` files
> (root, `backend/`, `frontend/`).

## 📝 License

Released under the [GNU General Public License v3.0](LICENSE).

<p align="center">
  <img
    width="100%"
    src="https://capsule-render.vercel.app/api?type=waving&section=footer&height=140&color=0:2dd4d9,50:ff6b5e,100:f9a320&animation=twinkling"
  />
</p>
