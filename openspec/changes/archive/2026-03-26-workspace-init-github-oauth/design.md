## Context

Workspace init already knows how to create a local git repository and AOS scaffold, but the app has no path for connecting a GitHub account or provisioning a remote repo on behalf of the user. The first-init screen currently stops at local bootstrap, which means users still have to create a GitHub repo and set `origin` manually if they want a hosted remote.

## Goals / Non-Goals

**Goals:**
- Let users connect GitHub during first workspace creation.
- Create or reuse a GitHub repository automatically during init.
- Set the local repository `origin` without manual git steps.
- Keep local bootstrap idempotent and preserve existing history.
- Surface OAuth, GitHub API, and remote configuration failures clearly.

**Non-Goals:**
- GitLab or other provider support.
- Persisting long-lived GitHub tokens as a user-facing account management feature.
- Complex org/team permission management beyond the default authenticated GitHub account.
- Reworking the existing local bootstrap flow for non-GitHub workspaces.

## Decisions

1. **Use a backend OAuth redirect + callback flow**
   - The frontend should start the flow, but the backend should own the OAuth code exchange and repo creation.
   - This keeps the GitHub client secret off the browser and keeps all provider logic in one place.
   - **Alternatives considered:**
     - Frontend-only OAuth: rejected because it would expose secrets and complicate token handling.
     - Manual PAT entry: rejected because the user explicitly wants a GitHub connection flow, not a token form.

2. **Treat GitHub provisioning as part of the workspace bootstrap pipeline**
   - After OAuth succeeds, the backend should provision the repo, run local bootstrap, and configure `origin` in one path.
   - This keeps first-init atomic from the user’s point of view and avoids splitting repo creation across separate screens.
   - **Alternatives considered:**
     - Create the repo first, then ask the frontend to run a separate bootstrap call. Rejected because it increases coordination and failure recovery complexity.

3. **Reuse the existing local bootstrap service for filesystem/git initialization**
   - Keep `WorkspaceBootstrapService` authoritative for `git init` and AOS scaffolding.
   - Extend it only as needed to accept an optional remote URL so the same bootstrap path can also configure `origin`.
   - **Alternatives considered:**
     - Reimplement git init in the GitHub service. Rejected because it duplicates process execution and filesystem logic.

4. **Do not persist GitHub access tokens beyond the bootstrap request**
   - The OAuth token is only needed to create the repo and confirm the account identity.
   - The system should discard the token after the callback completes unless a later product decision requires durable account linkage.
   - **Alternatives considered:**
     - Store the token for later operations. Rejected for this change because it adds security and lifecycle complexity that is not needed to satisfy first-init repo creation.

5. **Prefer idempotent repo creation behavior**
   - If the GitHub repository already exists for the authenticated account and requested name, treat it as reusable and continue bootstrap.
   - This makes retrying a partially completed init less fragile.
   - **Alternatives considered:**
     - Fail immediately on existing repo. Rejected because it makes retries painful and does not help the user recover from partial state.

## Risks / Trade-offs

- [OAuth app credentials required] → Mitigation: document the GitHub client ID/secret and callback URL environment variables clearly.
- [Partial remote creation] → Mitigation: keep repo creation and local bootstrap idempotent so retries can recover without destroying history.
- [GitHub API failures] → Mitigation: surface specific error messages for auth rejection, rate limits, and repo-name collisions.
- [Callback tampering] → Mitigation: protect the OAuth state payload so the workspace path and repo name cannot be altered in transit.

## Migration Plan

1. Add GitHub OAuth configuration and backend endpoints.
2. Extend workspace bootstrap to accept an optional `origin` URL.
3. Wire the first-init frontend flow to launch OAuth and handle success/error return states.
4. Add settings affordances for later reconnect/retry if needed.
5. Validate the flow with backend and frontend tests before enabling it in the default init path.

## Open Questions

- Should the default repository visibility be private or public?
- Should the first release support only the authenticated user’s personal account, or also allow choosing an organization owner?
- Should the app expose a separate reconnect/manage-connection UI after the initial repo is created?
