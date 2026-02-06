# CryWolfApiServer - Production Backend (API Server + Matchmaking + Landing page) for the Live Game "Cry Wolf"
> This README is intentionally focused on **code navigation and production-grade backend concerns.**
For a full product write-up,
- see the resume: https://www.notion.so/WooYoung-Jeong-2f42f5b151de80d39cd2ea9900bfe6f3?source=copy_link
- see the portfolio: https://www.notion.so/Cry-Wolf-Portfolio-2f52f5b151de80529d24c00c87a685fa?source=copy_link

## Product Proof (Live)
iOS (App Store): https://apps.apple.com/kr/app/id6745862935
< br/>
Android (Google Play): https://play.google.com/store/apps/details?id=com.hamonstudio.crywolf&hl=ko

## Repository Structure
- `APIServer/` - HTTP API (account, economy, inventory, purchase, etc.)
- `MatchMakingServer/` - match making orchestration
- `Proxy/` - reverse proxy / edge configuration
- `Website/` - https://hamonstudio.net landing page / admin console page

## Review Guide (Start Here)
- Economy mutation safety (transaction + idempotency): `APIServer/Controllers/PaymentController` - store receipt validation + retry-safe grant.
- Matchmaking flow: `APIServer/ControllersMatchController`/`MatchMakingServer/`
- WebSocket(SignalR): `APIServer/SignalRHub/`

## Reliability Primitives
- Transaction-protected mutations for economy-critical operations
- Idempotency to prevent double-grants under retries
- Server-side validation, API server is the only trusted gateway to the private RDS. (never trust the client)

## Security & Configuration
This repository does not publish runnable production configuration or secret keys.
Secrets are injected via environment-specific configuration in deployment.

## Contact
- Email: hamonstd@gmail.com
